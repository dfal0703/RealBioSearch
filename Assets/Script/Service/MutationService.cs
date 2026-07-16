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

        // 방식별 기본 자극도 - HealthService의 위험도 서열(관찰 < 음향 < 촬영·투과)과 같은
        // 전제를 공유하되, 별도 자원이라 값 자체는 독립적으로 정의한다.
        private static readonly Dictionary<string, float> StimulationBase = new Dictionary<string, float>
        {
            { "관찰 검사", 3f },
            { "음향 검사", 10f },
            { "촬영·투과 검사", 16f }
        };

        private static readonly Dictionary<string, float> IntensityMultiplier = new Dictionary<string, float>
        {
            { "약", 0.5f },
            { "중", 1f },
            { "강", 2f }
        };

        public ReactiveProperty<bool> IsMutated { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> IsResolved { get; } = new ReactiveProperty<bool>(false);

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

        // SubjectMonitorPanel의 비상 정지 버튼이 부르는 유일한 진입점 - 검사 강제 종료 +
        // 사고 기록 생성("검사 중단 및 상황 종료" + "관련 이미지와 사고 기록 생성", 이미지
        // 생성 시스템은 아직 없어 텍스트 기록만 만든다).
        public async UniTask ResolveEmergency()
        {
            if (!IsMutated.CurrentValue || IsResolved.CurrentValue) return;
            IsResolved.Value = true;

            var d = _caseSessionService.CurrentCase?.Data;
            var report = $"사례 {d?.caseId} ({d?.subjectName}) - 검사 중 기생체 변이 발생.\n" +
                         $"변이 직전 마지막 검사: {_lastOrgan} - {_lastMethod} (강도: {_lastIntensity})\n" +
                         "대응: 비상 정지 버튼 작동, 검사 강제 종료.";

            await _caseSessionService.AddLibraryEntry(CaseFileEntryType.Incident, "사고 기록", report);
            await _caseSessionService.CompleteCase("사고 발생으로 인한 강제 종료 - 최종 판정 미제출");

            _caseSessionService.Log("[비상 대응 완료] 검사가 강제 종료되었습니다.");
        }
    }
}
