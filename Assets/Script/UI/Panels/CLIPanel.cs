using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cysharp.Threading.Tasks;
using Haare.Client.Routine;
using Haare.Client.UI;
using R3;
using Script.Service;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using VContainer;

namespace Script.UI.Panels
{
    // 화면 구성 "우측 하단" - 검사 요청 수신/시스템 메시지/명령어 입력/대화 로그.
    // Stage 3: 클릭형 선택지 대신 자유 텍스트 입력(사용자 명시적 결정)으로 명령어를 받는다.
    // 입력 줄은 박스 없이 로그와 같은 폰트/색으로 ">" 프롬프트만 표시하는 터미널 스타일.
    [PanelAttribute("Prefabs/UI/Panels/CLIPanel")]
    public class CLIPanel : MonoRoutine, ICustomPanel, IPointerClickHandler
    {
        [SerializeField] private TMP_Text logText;
        [SerializeField] private ScrollRect logScrollRect;
        [SerializeField] private TMP_Text inputLineText;
        [SerializeField] private GameObject autocompleteRoot;
        [SerializeField] private TMP_Text autocompleteText;

        // Addressables로 로드되는 패널이라 Constructor()(Awake) 시점엔 아직 주입이 안 끝나있다 -
        // resolver.Inject()는 SceneUIManager.LoadPanel()이 BindEvent() 호출 직전에 하므로,
        // 주입된 값을 쓰는 초기화는 전부 BindEvent()에서 한다.
        [Inject] private CaseSessionService _caseSessionService;
        [Inject] private DialogueService _dialogueService;

        private readonly StringBuilder _inputBuilder = new StringBuilder();
        private string _imePreview = string.Empty;
        private bool _imeWasActive;

        // 명령어 이름(첫 단어)이든 "ask " 뒤 키워드든 구분 없이 "지금 후보 목록"으로 통일해서
        // 다룬다 - 화살표 키 탐색/Tab/Enter 확정 로직이 둘 다에 그대로 먹히게.
        private readonly List<string> _suggestions = new List<string>();
        private int _suggestionIndex = -1;

        public SceneUIManager uiManager { get; set; }
        public GameObject panel { get; set; }

        public void OpenPanel()
        {
            gameObject.SetActive(true);
            panel = gameObject;
        }

        public void BindEvent()
        {
            if (_caseSessionService != null)
            {
                _caseSessionService.OnLog
                    .Subscribe(AppendLog)
                    .AddTo(disposables);
            }

            // SetIMEEnabled는 포커스를 얻고/잃을 때 UpdateProcess에서 토글한다(부팅 시 한 번만
            // 켜두면 CLI에 포커스가 없을 때도 계속 켜진 상태로 남아, 게임 내내 OS IME 조합 창이
            // 걸려 있게 된다 - Keyboard.cs 문서: "Typically, this is not desirable while playing
            // a game"). 이벤트 구독 자체는 패널 생애주기 동안 계속 유지.
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                keyboard.onTextInput += OnTextInput;
                keyboard.onIMECompositionChange += OnImeCompositionChange;
            }

            UpdateInputDisplay();
        }

        // MonoRoutine의 OnDestroy는 private라 오버라이드가 안 되지만, Unity는 상속 계층의
        // 모든 클래스에 정의된 매직 메서드를 각각 호출해주므로 여기 새로 정의해도 안전하게 같이 호출된다.
        private void OnDestroy()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                keyboard.onTextInput -= OnTextInput;
                keyboard.onIMECompositionChange -= OnImeCompositionChange;
            }
        }

        // 화면 클릭 릴레이(ComputerViewController)가 ExecuteEvents.ExecuteHierarchy로 이 패널
        // 하위 어딘가를 클릭하면 계층을 타고 올라와 여기 도달한다 - 이걸로 "CLI 영역 클릭 시
        // 입력 포커스 획득"을 구현한다.
        public void OnPointerClick(PointerEventData eventData)
        {
            CliInputFocus.IsActive = true;
            UpdateInputDisplay();
        }

        protected override void UpdateProcess()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // CliInputFocus는 ComputerViewController(컴퓨터 시점 이탈)에서도 바깥에서 꺼질 수
            // 있으므로, 포커스 상태 변화 자체를 매 프레임 여기서 감지해 IME on/off를 맞춘다.
            if (CliInputFocus.IsActive != _imeWasActive)
            {
                _imeWasActive = CliInputFocus.IsActive;
                keyboard.SetIMEEnabled(_imeWasActive);

                // New Input System의 SetIMEEnabled만으로는 한/영 전환 키 자체가 OS로 안 넘어가는
                // 문제가 있었음(같은 PC의 다른 유니티 프로젝트와 비교해서 확인). 그 프로젝트는
                // TMP_InputField를 쓰는데, TMP_InputField.ActivateInputField()가 내부적으로
                // 레거시 UnityEngine.Input.imeCompositionMode도 같이 켠다(BaseInput.cs -
                // "Input.imeCompositionMode = value") - New/레거시 두 API가 서로 다른 네이티브
                // 경로를 타는 것으로 보여 여기서도 레거시 쪽을 같이 켜준다. Active Input
                // Handling이 "Both"여야 레거시 Input 클래스가 동작한다(ProjectSettings 확인 완료).
                Input.imeCompositionMode = _imeWasActive ? IMECompositionMode.On : IMECompositionMode.Auto;

                if (!_imeWasActive) ClearSuggestions();
            }

            if (!CliInputFocus.IsActive) return;

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                // 자동완성 목록이 떠 있으면 그것부터 닫고, 이미 닫혀 있으면 CLI 포커스 자체를 뺀다
                // (셸/IDE 자동완성의 흔한 관례 - Esc 한 번에 한 단계씩만 취소).
                if (_suggestions.Count > 0)
                {
                    ClearSuggestions();
                }
                else
                {
                    CliInputFocus.IsActive = false;
                    UpdateInputDisplay();
                }

                return;
            }

            if (keyboard.upArrowKey.wasPressedThisFrame && _suggestions.Count > 0)
            {
                _suggestionIndex = (_suggestionIndex - 1 + _suggestions.Count) % _suggestions.Count;
                RenderSuggestions();
            }
            else if (keyboard.downArrowKey.wasPressedThisFrame && _suggestions.Count > 0)
            {
                _suggestionIndex = (_suggestionIndex + 1) % _suggestions.Count;
                RenderSuggestions();
            }

            if (keyboard.backspaceKey.wasPressedThisFrame && _inputBuilder.Length > 0)
            {
                _inputBuilder.Remove(_inputBuilder.Length - 1, 1);
                UpdateInputDisplay();
            }

            if (keyboard.tabKey.wasPressedThisFrame)
            {
                AcceptSuggestion();
            }

            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            {
                // 자동완성 후보가 떠 있고 지금 입력이 그 후보와 완전히 같지 않다면(= 아직 고르는
                // 중이면) Enter는 "선택 확정"으로 쓰고, 이미 완전히 일치하는 상태(더 채울 게
                // 없음)라면 평범하게 명령을 실행한다 - 그래야 "help" 같은 완전한 명령어를 치고
                // Enter를 눌렀을 때 실행되지 않고 자동완성만 되는 걸 막는다.
                if (_suggestions.Count > 0 && _suggestionIndex >= 0 && !IsHighlightedSuggestionAlreadyTyped())
                {
                    AcceptSuggestion();
                }
                else
                {
                    SubmitInput();
                }
            }
        }

        private void OnTextInput(char c)
        {
            if (!CliInputFocus.IsActive) return;
            // 백스페이스/엔터 등 제어 문자는 UpdateProcess에서 직접 폴링하므로 여기서는
            // 인쇄 가능한 문자(완성된 한글 음절 포함)만 받는다.
            if (char.IsControl(c)) return;

            _inputBuilder.Append(c);
            UpdateInputDisplay();
        }

        private void OnImeCompositionChange(IMECompositionString composition)
        {
            if (!CliInputFocus.IsActive) return;
            _imePreview = composition.ToString();
            UpdateInputDisplay();
        }

        private void SubmitInput()
        {
            var input = _inputBuilder.ToString().Trim();
            _inputBuilder.Clear();
            _imePreview = string.Empty;
            ClearSuggestions();
            UpdateInputDisplay();

            if (input.Length == 0) return;

            AppendLog($"> {input}");
            ExecuteCommandAsync(input).Forget();
        }

        private async UniTaskVoid ExecuteCommandAsync(string input)
        {
            if (_dialogueService == null) return;
            var response = await _dialogueService.Execute(input);
            if (!string.IsNullOrEmpty(response)) AppendLog(response);
        }

        private void UpdateInputDisplay()
        {
            if (inputLineText == null) return;
            var caret = CliInputFocus.IsActive ? "_" : "";
            inputLineText.text = $"> {_inputBuilder}{_imePreview}{caret}";
            RefreshSuggestions();
        }

        // 첫 단어(공백 전)면 명령어 이름 자동완성, "ask " 뒤면 키워드 자동완성 - 둘 다 같은
        // _suggestions 목록/화살표 탐색 로직을 공유한다. 입력이 바뀔 때마다 다시 계산하고
        // 맨 위 후보를 기본 선택 상태로 되돌린다.
        private void RefreshSuggestions()
        {
            _suggestions.Clear();
            _suggestionIndex = -1;

            if (CliInputFocus.IsActive)
            {
                var currentInput = _inputBuilder.ToString();
                var spaceIndex = currentInput.IndexOf(' ');

                if (spaceIndex < 0)
                {
                    if (currentInput.Length > 0)
                    {
                        _suggestions.AddRange(DialogueService.Commands.Where(c => c.StartsWith(currentInput)));
                    }
                }
                else if (currentInput.Substring(0, spaceIndex) == "ask" && _dialogueService != null)
                {
                    var partial = currentInput.Substring(spaceIndex + 1);
                    if (!partial.Contains(' '))
                    {
                        var keywords = _dialogueService.GetAskKeywords();
                        _suggestions.AddRange(partial.Length == 0
                            ? keywords
                            : keywords.Where(k => k.StartsWith(partial)));
                    }
                }
            }

            if (_suggestions.Count > 0) _suggestionIndex = 0;
            RenderSuggestions();
        }

        private void RenderSuggestions()
        {
            if (autocompleteRoot == null || autocompleteText == null) return;

            if (_suggestions.Count == 0)
            {
                autocompleteRoot.SetActive(false);
                return;
            }

            autocompleteRoot.SetActive(true);
            // 화살표로 고른 항목을 대괄호로 감싸 표시(선택 상태를 텍스트만으로 구분).
            autocompleteText.text = string.Join("   ",
                _suggestions.Select((s, i) => i == _suggestionIndex ? $"[{s}]" : s));
        }

        private void ClearSuggestions()
        {
            _suggestions.Clear();
            _suggestionIndex = -1;
            RenderSuggestions();
        }

        // 지금 강조된 후보를 입력줄에 채운다. 첫 단어(명령어) 자리에서 채우면 뒤에 공백을 붙여
        // 바로 인자를 이어 타이핑할 수 있게 하고, "ask " 뒤 키워드 자리는 그걸로 끝(더 채울 인자
        // 없음)이라 공백을 안 붙인다.
        private void AcceptSuggestion()
        {
            if (_suggestions.Count == 0 || _suggestionIndex < 0) return;

            var currentInput = _inputBuilder.ToString();
            var spaceIndex = currentInput.IndexOf(' ');
            var accepted = _suggestions[_suggestionIndex];

            _inputBuilder.Clear();
            if (spaceIndex < 0)
            {
                _inputBuilder.Append(accepted).Append(' ');
            }
            else
            {
                _inputBuilder.Append(currentInput, 0, spaceIndex + 1).Append(accepted);
            }

            UpdateInputDisplay();
        }

        private bool IsHighlightedSuggestionAlreadyTyped()
        {
            var currentInput = _inputBuilder.ToString();
            var spaceIndex = currentInput.IndexOf(' ');
            var typed = spaceIndex < 0 ? currentInput : currentInput.Substring(spaceIndex + 1);
            return typed == _suggestions[_suggestionIndex];
        }

        private void AppendLog(string message)
        {
            if (logText == null) return;
            logText.text += $"\n{message}";

            if (logScrollRect == null) return;
            // ContentSizeFitter가 새 텍스트 높이만큼 LogText를 다시 늘리는 레이아웃 패스가
            // 이번 프레임에 아직 안 끝났을 수 있어서, 강제로 캔버스를 갱신한 다음에 맨 아래로
            // 스크롤해야 최신 줄이 실제로 보이는 위치로 맞아떨어진다.
            Canvas.ForceUpdateCanvases();
            logScrollRect.verticalNormalizedPosition = 0f;
        }
    }
}
