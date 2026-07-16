using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Haare.Client.Routine;
using R3;
using Script.Data;
using VContainer;

namespace Script.Service
{
    // 개발 구현 지시서 8장 "6단계 - 기생체 변이와 비상 상황". 기생체 자극도는 검사체의
    // 체력(HealthService)과는 별개 자원이다("검사체의 체력"과 "기생체 자극도"는 전체 기획
    // 정리.md 7장에서 구분된 두 축). 화면엔 수치를 직접 노출하지 않고("자극도 역시 직접적인
    // 숫자로 표시하지 않는다"), 임계치를 초과하는 순간 SubjectMonitorPanel의 연출 급변 + 비상
    // 버튼 등장으로만 드러난다. 변이는 "일반적인 전투 시스템이 아니라 평상시의 정적인 문서
    // 업무를 갑작스럽게 붕괴시키는 예외 상황"(같은 문서 8장)이라 한 번 터지면 되돌리지 않는
    // 단발성 이벤트로 구현한다(재변이/반복 트리거 없음).
    public class MutationService : NativeRoutine
    {
        [Inject] private CaseSessionService _caseSessionService;

        private const float MutationThreshold = 100f;

        // 방식별 기본 자극도 - HealthService의 위험도 서열(관찰 < 음향 < 압력·진동 < 촬영·
        // 투과 < 전기 < 채취 < 극단적 자극)과 같은 전제를 공유하되, 별도 자원이라 값 자체는
        // 독립적으로 정의한다.
        private static readonly Dictionary<string, float> StimulationBase = new Dictionary<string, float>
        {
            { "관찰 검사", 3f },
            { "음향 검사", 10f },
            { "압력·진동 검사", 13f },
            { "촬영·투과 검사", 16f },
            { "전기 검사", 20f },
            { "채취 검사", 24f },
            { "극단적 자극 검사", 32f }
        };

        private static readonly Dictionary<string, float> IntensityMultiplier = new Dictionary<string, float>
        {
            { "약", 0.5f },
            { "중", 1f },
            { "강", 2f }
        };

        // 전체 기획 정리.md 8장 "변이와 비상 상황"의 4개 대응 방식을 그대로 4개의 개별
        // 물리 버튼으로 구현한다(사용자 지시 - 처음엔 버튼 하나를 4번 누르는 순차 진행이었으나,
        // "4개의 각기 다른 버튼을 클릭해서 하는 방식"으로 다시 바꿈). 순서는 강제하지 않는다 -
        // 4개 버튼을 아무 순서로나 눌러서 전부 완료하면 된다(계기판 여러 버튼 중 어느 걸
        // 먼저 눌러야 하는지 기획서에 규정된 바 없음).
        public static readonly string[] EmergencyStepNames =
        {
            "비상 버튼 작동", "검사 장비 긴급 정지", "검사실 봉쇄", "제압 절차 실행"
        };

        public ReactiveProperty<bool> IsMutated { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> IsResolved { get; } = new ReactiveProperty<bool>(false);

        // 4개 중 몇 개가 완료됐는지(순서 무관 카운트) - SubjectMonitorPanel/
        // EmergencyPanelController가 진행률 표시에 구독해서 쓴다.
        public ReactiveProperty<int> EmergencyStepsCompleted { get; } = new ReactiveProperty<int>(0);

        private readonly bool[] _stepCompleted = new bool[EmergencyStepNames.Length];

        private float _stimulation;
        private string _lastOrgan;
        private string _lastMethod;
        private string _lastIntensity;

        public void ApplyExamStimulation(string organ, string method, string intensity)
        {
            if (IsMutated.CurrentValue) return; // 이미 터진 사건 - 더 쌓을 이유 없음

            _lastOrgan = organ;
            _lastMethod = method;
            _lastIntensity = intensity;

            var baseVal = StimulationBase.TryGetValue(method, out var b) ? b : 5f;
            var multiplier = IntensityMultiplier.TryGetValue(intensity, out var m) ? m : 1f;
            _stimulation += baseVal * multiplier;

            if (_stimulation >= MutationThreshold)
            {
                TriggerMutation();
            }
        }

        private void TriggerMutation()
        {
            IsMutated.Value = true;
            _caseSessionService?.Log("[경고] 검사체가 기생체 자극으로 변이했습니다! 즉시 비상 대응이 필요합니다.");
        }

        public bool IsStepCompleted(int stepIndex)
        {
            return stepIndex >= 0 && stepIndex < _stepCompleted.Length && _stepCompleted[stepIndex];
        }

        // 계기판의 4개 버튼 중 하나가 클릭될 때마다 EmergencyPanelController가 그 버튼의
        // 인덱스를 넘겨서 부르는 진입점 - 이미 완료된 버튼을 다시 눌러도 무시한다(멱등). 4개
        // 전부 완료해야 비로소 검사 강제 종료 + 사고 기록 생성("검사 중단 및 상황 종료" +
        // "관련 이미지와 사고 기록 생성", 이미지 생성 시스템은 아직 없어 텍스트 기록만
        // 만든다)까지 이어진다.
        public async UniTask CompleteEmergencyStep(int stepIndex)
        {
            if (!IsMutated.CurrentValue || IsResolved.CurrentValue) return;
            if (stepIndex < 0 || stepIndex >= EmergencyStepNames.Length) return;
            if (_stepCompleted[stepIndex]) return;

            _stepCompleted[stepIndex] = true;
            var doneCount = EmergencyStepsCompleted.CurrentValue + 1;
            _caseSessionService.Log(
                $"[비상 대응 {doneCount}/{EmergencyStepNames.Length}] {EmergencyStepNames[stepIndex]} 완료.");
            EmergencyStepsCompleted.Value = doneCount;

            if (doneCount < EmergencyStepNames.Length) return;

            IsResolved.Value = true;

            var d = _caseSessionService.CurrentCase?.Data;
            var report = $"사례 {d?.caseId} ({d?.subjectName}) - 검사 중 기생체 변이 발생.\n" +
                         $"변이 직전 마지막 검사: {_lastOrgan} - {_lastMethod} (강도: {_lastIntensity})\n" +
                         // "→"도 폰트 애셋에 글리프가 없어 깨지는 부류라 ASCII 화살표로 대체.
                         $"대응 절차: {string.Join(" -> ", EmergencyStepNames)} - 전 단계 완료.";

            await _caseSessionService.AddLibraryEntry(CaseFileEntryType.Incident, "사고 기록", report);
            await _caseSessionService.CompleteCase("사고 발생으로 인한 강제 종료 - 최종 판정 미제출");

            _caseSessionService.Log("[비상 대응 완료] 4단계 절차가 모두 완료되어 검사가 강제 종료되었습니다.");
        }
    }
}
