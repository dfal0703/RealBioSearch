using Haare.Client.Routine;
using Haare.Client.UI;
using R3;
using Script.Service;
using TMPro;
using UnityEngine;
using VContainer;

namespace Script.UI.Panels
{
    // 화면 구성 "좌측 하단 상부" - 건강 상태 UI. 정확한 체력 수치는 노출하지 않고 5단계
    // (안정/경미한 이상/불안정/위험/위급)로만 표시한다(전체 기획 정리.md 7장). 잔여 업무
    // 시간은 체력과 달리 "공개하지 않는다"는 제약이 없어(오히려 "남은 시간이 감소"하는 걸
    // 플레이어가 직접 체감해야 하는 자원) 분 단위 숫자를 그대로 보여준다. 화면 구성 6영역
    // 중 잔여 시간 전용 영역이 따로 정의돼 있지 않아 이 패널에 건강 상태와 함께 표시하기로
    // 판단했다(구현현황 문서 Stage 5 계획 참고 - 다른 배치가 필요하면 피드백으로 조정).
    [PanelAttribute("Prefabs/UI/Panels/HealthStatusPanel")]
    public class HealthStatusPanel : MonoRoutine, ICustomPanel
    {
        [SerializeField] private TMP_Text statusText;

        [Inject] private HealthService _healthService;
        [Inject] private CaseTimeService _caseTimeService;

        public SceneUIManager uiManager { get; set; }
        public GameObject panel { get; set; }

        public void OpenPanel()
        {
            gameObject.SetActive(true);
            panel = gameObject;
        }

        public void BindEvent()
        {
            if (_healthService == null || _caseTimeService == null) return;

            _healthService.Health.Subscribe(_ => Refresh()).AddTo(disposables);
            _caseTimeService.RemainingMinutes.Subscribe(_ => Refresh()).AddTo(disposables);
            Refresh();
        }

        private void Refresh()
        {
            if (statusText == null) return;

            var state = _healthService.CurrentState;
            var minutes = Mathf.CeilToInt(_caseTimeService.RemainingMinutes.CurrentValue);

            statusText.text =
                $"건강 상태<br><color={StateColor(state)}>{HealthService.StateLabel(state)}</color>" +
                $"<br>잔여 업무 시간: {minutes}분";
        }

        private static string StateColor(HealthState state)
        {
            switch (state)
            {
                case HealthState.Stable: return "#88CC88";
                case HealthState.MinorAbnormality: return "#CCCC66";
                case HealthState.Unstable: return "#DDA33D";
                case HealthState.Critical: return "#DD6644";
                default: return "#FF3333";
            }
        }
    }
}
