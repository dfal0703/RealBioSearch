using Haare.Client.Routine;
using Haare.Client.UI;
using R3;
using Script.Service;
using TMPro;
using UnityEngine;
using VContainer;

namespace Script.UI.Panels
{
    // 화면 구성 "우측 하단" - 검사 요청 수신/시스템 메시지/명령어 입력/대화 로그.
    // Stage 1에서는 텍스트 로그 영역 + 입력창 플레이스홀더만 표시했고, Stage 2에서
    // CaseSessionService의 부팅 로그(검사 요청 수신 등)를 받아 화면에 반영하기 시작한다.
    // 실제 명령어 파싱은 아직 없다.
    [PanelAttribute("Prefabs/UI/Panels/CLIPanel")]
    public class CLIPanel : MonoRoutine, ICustomPanel
    {
        [SerializeField] private TMP_Text logText;

        // Addressables로 로드되는 패널이라 Constructor()(Awake) 시점엔 아직 주입이 안 끝나있다 -
        // resolver.Inject()는 SceneUIManager.LoadPanel()이 BindEvent() 호출 직전에 하므로,
        // 주입된 값을 쓰는 초기화는 전부 BindEvent()에서 한다.
        [Inject] private CaseSessionService _caseSessionService;

        public SceneUIManager uiManager { get; set; }
        public GameObject panel { get; set; }

        public void OpenPanel()
        {
            gameObject.SetActive(true);
            panel = gameObject;
        }

        public void BindEvent()
        {
            if (_caseSessionService == null) return;

            _caseSessionService.OnLog
                .Subscribe(AppendLog)
                .AddTo(disposables);
        }

        private void AppendLog(string message)
        {
            if (logText == null) return;
            logText.text += $"\n{message}";
        }
    }
}
