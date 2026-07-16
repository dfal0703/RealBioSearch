using System;
using Cysharp.Threading.Tasks;
using Haare.Client.Routine;
using Haare.Client.UI;
using Script.Data;
using Script.Service;
using Script.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Script.UI.Panels
{
    // 화면 구성 "좌측 최하단" - 개발 구현 지시서 9장 "7단계 - 최종 판정 시스템". 감염 여부 +
    // 감염 부위를 선택해 제출하면 CaseFileDefinition의 정답과 대조해 정확도를 판정하고,
    // HealthService.CurrentState(검사체 상태)까지 함께 반영해 결과를 보여준다("정확성과
    // 검사체 상태에 따른 결과를 확인할 수 있다" - 9장 완료 기준). 보고서는 자동으로 채워지지
    // 않고 플레이어가 직접 선택해야 한다(9장 설계 의도). 필드/드롭다운 조립은
    // ExamControlPanel과 같은 런타임 ClickRelay 패턴을 재사용.
    [PanelAttribute("Prefabs/UI/Panels/FinalReportPanel")]
    public class FinalReportPanel : MonoRoutine, ICustomPanel
    {
        [SerializeField] private TMP_Text statusText;

        [SerializeField] private GameObject infectionField;
        [SerializeField] private TMP_Text infectionLabel;
        [SerializeField] private GameObject infectionDropdownRoot;
        [SerializeField] private RectTransform infectionDropdownContent;

        [SerializeField] private GameObject regionField;
        [SerializeField] private TMP_Text regionLabel;
        [SerializeField] private GameObject regionDropdownRoot;
        [SerializeField] private RectTransform regionDropdownContent;

        [SerializeField] private GameObject submitButton;
        [SerializeField] private TMP_Text submitButtonLabel;

        [SerializeField] private TMP_FontAsset rowFont;

        [Inject] private CaseSessionService _caseSessionService;
        [Inject] private HealthService _healthService;
        [Inject] private MutationService _mutationService;

        // "미감염" 판정에서는 부위 선택 자체가 의미 없어지지만, UI는 항상 하나를 골라둔
        // 상태를 유지해야 하므로 드롭다운은 계속 둔다 - 채점 시 감염 여부가 "비감염"이면
        // 부위 일치 여부를 아예 채점에서 제외한다.
        private static readonly string[] InfectionOptions = { "감염", "비감염" };

        private int _infectionIndex;
        private int _regionIndex;
        private bool _submitted;

        public SceneUIManager uiManager { get; set; }
        public GameObject panel { get; set; }

        public void OpenPanel()
        {
            gameObject.SetActive(true);
            panel = gameObject;
        }

        public void BindEvent()
        {
            AttachClick(infectionField, () => ToggleDropdown(infectionDropdownRoot));
            AttachClick(regionField, () => ToggleDropdown(regionDropdownRoot));
            AttachClick(submitButton, Submit);

            BuildDropdownRows(infectionDropdownContent, InfectionOptions, SelectInfection);
            BuildDropdownRows(regionDropdownContent, ExamService.Organs, SelectRegion);

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

        // ExamControlPanel.BuildDropdownRows와 동일한 런타임 조립 패턴 - 개수가 고정이라도
        // 프리팹에 손으로 채우기보다 코드 한 군데서 관리하는 쪽을 그대로 따른다.
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
                rowRect.sizeDelta = new Vector2(0, 24f);

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
                text.fontSize = 13;
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
            if (_submitted || target == null) return;

            var opening = !target.activeSelf;
            CloseAllDropdowns();
            if (opening) target.SetActive(true);
        }

        private void CloseAllDropdowns()
        {
            if (infectionDropdownRoot != null) infectionDropdownRoot.SetActive(false);
            if (regionDropdownRoot != null) regionDropdownRoot.SetActive(false);
        }

        private void SelectInfection(int index)
        {
            _infectionIndex = index;
            CloseAllDropdowns();
            UpdateLabels();
        }

        private void SelectRegion(int index)
        {
            _regionIndex = index;
            CloseAllDropdowns();
            UpdateLabels();
        }

        private void UpdateLabels()
        {
            if (infectionLabel != null) infectionLabel.text = $"감염 여부 [{InfectionOptions[_infectionIndex]}]";
            if (regionLabel != null) regionLabel.text = $"감염 부위 [{ExamService.Organs[_regionIndex]}]";
        }

        private void Submit()
        {
            if (_submitted || _caseSessionService?.CurrentCase == null) return;

            // 변이 사고로 이미 종료된 사례는 다시 제출할 수 없다(MutationService.
            // ResolveEmergency가 CompleteCase를 이미 호출해뒀음) - ExamService/DialogueService가
            // 사례 종료 뒤 검사/대화를 막는 것과 같은 전제.
            if (_caseSessionService.CurrentCase.Data.status == CaseStatus.Closed)
            {
                if (statusText != null) statusText.text = "이미 종료된 사례입니다.";
                return;
            }

            // 비상 상황이 아직 해결되지 않은 채 최종 보고서부터 제출해버리는 걸 막는다 -
            // ExamService가 변이 중 검사를 막는 것과 같은 전제("즉각적으로 비상 대응을 수행").
            if (_mutationService != null && _mutationService.IsMutated.CurrentValue && !_mutationService.IsResolved.CurrentValue)
            {
                if (statusText != null) statusText.text = "비상 상황부터 먼저 해결하십시오.";
                return;
            }

            _submitted = true;
            CloseAllDropdowns();

            var guessInfected = _infectionIndex == 0; // InfectionOptions[0] == "감염"
            var guessRegion = ExamService.Organs[_regionIndex];

            var definition = _caseSessionService.CurrentDefinition;
            var actuallyInfected = definition?.isInfected ?? false;
            var actualRegion = definition?.infectedRegion ?? "";

            var infectionCorrect = guessInfected == actuallyInfected;
            // 비감염 판정이면 부위 자체가 의미 없으니 부위 일치 여부는 채점에서 제외한다.
            var regionCorrect = !actuallyInfected || guessRegion == actualRegion;
            var fullyCorrect = infectionCorrect && regionCorrect;

            var healthState = _healthService?.CurrentState ?? HealthState.Stable;
            var verdictDraft = $"감염 여부: {(guessInfected ? "감염" : "비감염")} / 감염 부위: {guessRegion}" +
                                $" / 정답: {(actuallyInfected ? "감염" : "비감염")}({actualRegion})" +
                                $" / 검사체 상태: {HealthService.StateLabel(healthState)}";

            _caseSessionService.CompleteCase(verdictDraft).Forget();
            _caseSessionService.Log($"[최종 보고서 제출] {verdictDraft}");

            if (statusText != null) statusText.text = BuildResultText(fullyCorrect, healthState);
            if (submitButtonLabel != null) submitButtonLabel.text = "제출 완료";
        }

        private static string BuildResultText(bool fullyCorrect, HealthState healthState)
        {
            if (!fullyCorrect)
            {
                return "<color=#DD6644>판정 오류</color> - 감염 여부/부위가 정답과 다릅니다.";
            }

            switch (healthState)
            {
                case HealthState.Stable:
                case HealthState.MinorAbnormality:
                    return "<color=#88CC88>판정 성공</color> - 검사체도 안정적으로 종료됨.";
                default:
                    return "<color=#DDA33D>판정은 정확했으나</color> 검사체 상태가 위태로운 채로 종료됨.";
            }
        }
    }
}
