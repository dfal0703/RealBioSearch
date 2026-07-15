using System;
using Cysharp.Threading.Tasks;
using Haare.Client.Routine;
using Haare.Client.UI;
using Script.Service;
using Script.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Script.UI.Panels
{
    // 화면 구성 "중앙" - 개발 구현 지시서 6장 "4단계 - 핵심 검사 루프".
    // 검사 부위/방식/강도는 각 필드를 클릭하면 그 아래 옵션 목록(드롭다운)이 뜨고, 항목을
    // 클릭하면 선택되며 목록이 닫힌다 - LibraryPanel의 행 생성 패턴(런타임 GameObject 조립 +
    // ClickRelay)을 그대로 재사용해서 별도 드롭다운 프리팹 없이 구현. 한 번에 하나의 목록만
    // 열려 있을 수 있다(새로 열면 기존 건 자동으로 닫힘). 검사 강도는 아직 아무 로직에도 안
    // 쓰인다(개발 구현 지시서 7장 "5단계 - 검사 위험과 상태 변화"에서 실제로 건강 상태에 영향을
    // 주게 될 때까지는 UI만 존재).
    [PanelAttribute("Prefabs/UI/Panels/ExamControlPanel")]
    public class ExamControlPanel : MonoRoutine, ICustomPanel
    {
        [SerializeField] private GameObject partField;
        [SerializeField] private TMP_Text partLabel;
        [SerializeField] private GameObject partDropdownRoot;
        [SerializeField] private RectTransform partDropdownContent;

        [SerializeField] private GameObject methodField;
        [SerializeField] private TMP_Text methodLabel;
        [SerializeField] private GameObject methodDropdownRoot;
        [SerializeField] private RectTransform methodDropdownContent;

        [SerializeField] private GameObject intensityField;
        [SerializeField] private TMP_Text intensityLabel;
        [SerializeField] private GameObject intensityDropdownRoot;
        [SerializeField] private RectTransform intensityDropdownContent;

        [SerializeField] private GameObject runButton;
        [SerializeField] private TMP_Text runButtonLabel;

        [SerializeField] private TMP_FontAsset rowFont;

        [Inject] private ExamService _examService;

        private static readonly string[] Intensities = { "약", "중", "강" };

        private int _partIndex;
        private int _methodIndex;
        private int _intensityIndex;
        private bool _isRunning;

        public SceneUIManager uiManager { get; set; }
        public GameObject panel { get; set; }

        public void OpenPanel()
        {
            gameObject.SetActive(true);
            panel = gameObject;
        }

        public void BindEvent()
        {
            AttachClick(partField, () => ToggleDropdown(partDropdownRoot));
            AttachClick(methodField, () => ToggleDropdown(methodDropdownRoot));
            AttachClick(intensityField, () => ToggleDropdown(intensityDropdownRoot));
            AttachClick(runButton, RunExam);

            BuildDropdownRows(partDropdownContent, ExamService.Organs, SelectPart);
            BuildDropdownRows(methodDropdownContent, ExamService.Methods, SelectMethod);
            BuildDropdownRows(intensityDropdownContent, Intensities, SelectIntensity);

            CloseAllDropdowns();
            UpdateLabels();
        }

        private static void AttachClick(GameObject target, Action onClick)
        {
            if (target == null) return;
            var relay = target.GetComponent<ClickRelay>();
            if (relay == null) relay = target.AddComponent<ClickRelay>();
            relay.onClick = onClick;
        }

        // 옵션 목록 하나를 통째로 런타임에 조립한다(LibraryPanel.CreateRow와 같은 이유 -
        // 개수가 고정이라도 프리팹에 미리 손으로 써두는 것보다 코드 한 군데서 관리하는 쪽이
        // 유지보수하기 쉽다).
        private void BuildDropdownRows(RectTransform content, string[] options, Action<int> onSelect)
        {
            if (content == null) return;

            var uiLayer = content.gameObject.layer;

            for (var i = 0; i < options.Length; i++)
            {
                var index = i;

                var rowGo = new GameObject(options[i], typeof(RectTransform)) { layer = uiLayer };
                var rowRect = (RectTransform)rowGo.transform;
                rowRect.SetParent(content, false);
                rowRect.sizeDelta = new Vector2(0, 26f);

                var image = rowGo.AddComponent<Image>();
                image.color = new Color(1f, 1f, 1f, 0.06f);
                image.raycastTarget = true;

                var textGo = new GameObject("Label", typeof(RectTransform)) { layer = uiLayer };
                var textRect = (RectTransform)textGo.transform;
                textRect.SetParent(rowRect, false);
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(8, 0);
                textRect.offsetMax = new Vector2(-8, 0);

                var text = textGo.AddComponent<TextMeshProUGUI>();
                text.text = options[i];
                text.fontSize = 14;
                text.color = new Color(0.85f, 0.85f, 0.85f, 1f);
                text.alignment = TextAlignmentOptions.MidlineLeft;
                text.raycastTarget = false;
                if (rowFont != null) text.font = rowFont;

                var relay = rowGo.AddComponent<ClickRelay>();
                relay.onClick = () => onSelect(index);
            }
        }

        private void ToggleDropdown(GameObject target)
        {
            if (target == null) return;

            var opening = !target.activeSelf;
            CloseAllDropdowns();
            if (opening) target.SetActive(true);
        }

        private void CloseAllDropdowns()
        {
            if (partDropdownRoot != null) partDropdownRoot.SetActive(false);
            if (methodDropdownRoot != null) methodDropdownRoot.SetActive(false);
            if (intensityDropdownRoot != null) intensityDropdownRoot.SetActive(false);
        }

        private void SelectPart(int index)
        {
            _partIndex = index;
            CloseAllDropdowns();
            UpdateLabels();
        }

        private void SelectMethod(int index)
        {
            _methodIndex = index;
            CloseAllDropdowns();
            UpdateLabels();
        }

        private void SelectIntensity(int index)
        {
            _intensityIndex = index;
            CloseAllDropdowns();
            UpdateLabels();
        }

        // 원래 있던 "▾" 화살표는 NEXONLv1GothicBold SDF 폰트 애셋에 그 글리프가 없어서 네모
        // 박스로 깨져 보이는 게 Stage 1 때부터 확인돼 있었다(구현현황 문서에 미룬 걸로 기록됨) -
        // 지금 이 패널을 실제로 쓰게 됐으니 폰트에 이미 있는 대괄호로 바꿔서 해결.
        private void UpdateLabels()
        {
            if (partLabel != null) partLabel.text = $"검사 부위 [{ExamService.Organs[_partIndex]}]";
            if (methodLabel != null) methodLabel.text = $"검사 방식 [{ExamService.Methods[_methodIndex]}]";
            if (intensityLabel != null) intensityLabel.text = $"검사 강도 [{Intensities[_intensityIndex]}]";
        }

        private void RunExam()
        {
            if (_isRunning || _examService == null) return;
            CloseAllDropdowns();
            RunExamAsync().Forget();
        }

        private async UniTaskVoid RunExamAsync()
        {
            _isRunning = true;
            if (runButtonLabel != null) runButtonLabel.text = "실행 중...";

            var organ = ExamService.Organs[_partIndex];
            var method = ExamService.Methods[_methodIndex];
            var intensity = Intensities[_intensityIndex];
            await _examService.RunExam(organ, method, intensity);

            if (runButtonLabel != null) runButtonLabel.text = "실행";
            _isRunning = false;
        }
    }
}
