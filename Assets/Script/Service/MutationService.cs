using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Haare.Client.Routine;
using R3;
using Script.Data;
using Script.UI;
using UnityEngine;
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
    // 기획 대조 문서(2026-07-16) 15장에서 우선순위 1위로 꼽힌 gap - 지금까지는 임계치를
    // 넘는 "순간" 곧바로 변이가 터졌을 뿐, 전체 기획 정리.md 7장이 요구하는 "말투 변화/카메라
    // 노이즈/비정상적인 신체 움직임/장기 반응 증가/갑작스러운 침묵/공격 전조"로 미리 암시하는
    // 단계가 전혀 없었다(개발 구현 지시서 8장 완료 기준 "위험의 전조를 인식하고"와 직접
    // 충돌). Calm→Tense→Critical→(Mutated) 3단계 전조를 자극도 비율 구간으로 나눠 추가.
    public enum AgitationLevel
    {
        Calm,
        Tense,
        Critical
    }

    public class MutationService : NativeRoutine
    {
        [Inject] private CaseSessionService _caseSessionService;

        private const float MutationThreshold = 100f;

        // 전조 구간 경계 - 자극도가 임계치의 40%를 넘으면 Tense(경미한 이상 신호),
        // 75%를 넘으면 Critical(명백한 위험 신호)로 승격. 100%에서 실제 변이(TriggerMutation).
        private const float TenseRatio = 0.4f;
        private const float CriticalRatio = 0.75f;

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

        // 사용자 요청(2026-07-24): "비상 대응 로직 시에는 카메라 UI에서 오른쪽 위에 10초짜리
        // 타이머를 주고, 10초 경과시, 대응 실패로 처리해줘". SubjectMonitorPanel이 이 값을
        // 구독해서 카운트다운 숫자를 표시한다.
        public const float EmergencyResponseTimeLimit = 10f;

        public ReactiveProperty<float> EmergencyResponseSecondsRemaining { get; } =
            new ReactiveProperty<float>(EmergencyResponseTimeLimit);

        // 10초 안에 4단계를 못 끝내 강제로 실패 처리됐는지 - IsResolved(성공적으로 끝냄)와는
        // 별개 상태다. EmergencyPanelController/ComputerViewController가 이 값이 true가 되면
        // 더 이상 클릭/깜빡임에 반응하지 않도록 게이트로 함께 쓴다.
        public ReactiveProperty<bool> ResponseFailed { get; } = new ReactiveProperty<bool>(false);

        // 변이 전 전조 단계 - SubjectMonitorPanel이 구독해서 화면 색/문구를 단계별로 미리
        // 바꾼다. 자극도 자체는 여전히 숫자로 노출 안 함(이 프로퍼티는 3단계 구간만 알려줌).
        public ReactiveProperty<AgitationLevel> Agitation { get; } = new ReactiveProperty<AgitationLevel>(AgitationLevel.Calm);

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

            var ratio = _stimulation / MutationThreshold;
            var agitation = ratio >= CriticalRatio ? AgitationLevel.Critical
                : ratio >= TenseRatio ? AgitationLevel.Tense
                : AgitationLevel.Calm;

            // 단계가 실제로 바뀔 때만 CLI에 암시 문구를 남긴다(매 검사마다 스팸 안 함) -
            // HealthService.ApplyExamRisk가 5단계 건강 상태 변화에만 로그를 남기는 것과 같은
            // 절제 원칙.
            if (agitation != Agitation.CurrentValue)
            {
                Agitation.Value = agitation;
                var hint = BuildAgitationHint(agitation);
                if (!string.IsNullOrEmpty(hint)) _caseSessionService?.Log(hint);
            }

            if (_stimulation >= MutationThreshold)
            {
                TriggerMutation();
            }
        }

        // 전체 기획 정리.md 7장이 예로 든 암시 신호(말투 변화/카메라 노이즈/비정상적인 신체
        // 움직임/장기 반응 증가/갑작스러운 침묵/공격 전조) 중 이 프로젝트에서 실제로 표현
        // 가능한 것들(카메라 노이즈=화면 연출, 나머지는 CLI 관찰 로그)을 골라 썼다 - 3D
        // 검사체 모델이 없어 "행동 변화"를 직접 연출할 순 없지만, CLI를 통한 관찰 보고
        // 형태로는 암시할 수 있다.
        private static string BuildAgitationHint(AgitationLevel level)
        {
            switch (level)
            {
                case AgitationLevel.Tense:
                    return "[관찰] 검사체의 호흡과 말투가 평소와 다르게 불규칙해졌습니다.";
                case AgitationLevel.Critical:
                    return "[경고] 모니터에 간헐적인 노이즈가 발생하고, 검사체가 갑자기 말을 멈췄습니다 - 위험 신호로 보입니다.";
                default:
                    return null;
            }
        }

        private void TriggerMutation()
        {
            IsMutated.Value = true;
            EmergencyResponseSecondsRemaining.Value = EmergencyResponseTimeLimit;
            _caseSessionService?.Log("[경고] 검사체가 기생체 자극으로 변이했습니다! 즉시 비상 대응이 필요합니다.");
        }

        // 변이 발생 후 4단계를 전부 끝내기 전까지만 카운트다운한다 - 성공(IsResolved)이나
        // 이미 실패 처리(ResponseFailed)됐으면 더 셀 이유가 없다. EmergencyResponseSignal
        // (Script.UI, 정적 브릿지)도 매 프레임 같이 갱신한다 - CoreCanvas의
        // EmergencyTimerHintController(전역 UI, ssh 씬 DI 그래프 밖)가 이 서비스를 직접
        // 주입받을 수 없어서(CliInputFocus와 같은 스코프 문제) 폴링으로 상태를 읽어간다.
        public override void UpdateProcess()
        {
            base.UpdateProcess();

            var active = IsMutated.CurrentValue && !IsResolved.CurrentValue && !ResponseFailed.CurrentValue;
            EmergencyResponseSignal.Active = active;

            if (!active) return;

            var remaining = EmergencyResponseSecondsRemaining.CurrentValue - Time.deltaTime;
            if (remaining <= 0f)
            {
                EmergencyResponseSecondsRemaining.Value = 0f;
                EmergencyResponseSignal.SecondsRemaining = 0f;
                FailEmergencyResponse().Forget();
                return;
            }

            EmergencyResponseSecondsRemaining.Value = remaining;
            EmergencyResponseSignal.SecondsRemaining = remaining;
        }

        // 10초 안에 4단계를 전부 못 끝내면 "대응 실패"로 처리한다. 사용자 지시: "대응 실패는
        // 검사 실패와 동일한 리스크" - FinalReportPanel이 오답 제출 시 보여주는 "판정 오류"와
        // 같은 실패 등급으로 사례를 강제 종료한다(4단계를 제때 완료했을 때의 중립적인 "강제
        // 종료" 문구와는 구분되는, 명백한 실패 결과).
        private async UniTask FailEmergencyResponse()
        {
            if (ResponseFailed.CurrentValue) return;
            ResponseFailed.Value = true;

            var d = _caseSessionService.CurrentCase?.Data;
            var report = $"사례 {d?.caseId} ({d?.subjectName}) - 비상 대응 제한 시간(10초) 초과로 대응 실패.\n" +
                         $"변이 직전 마지막 검사: {_lastOrgan} - {_lastMethod} (강도: {_lastIntensity})\n" +
                         $"완료된 절차: {EmergencyStepsCompleted.CurrentValue}/{EmergencyStepNames.Length}건 - " +
                         "시간 초과로 나머지 미완료.";

            await _caseSessionService.AddLibraryEntry(CaseFileEntryType.Incident, "사고 기록", report);
            await _caseSessionService.CompleteCase(
                "<color=#DD6644>판정 오류</color> - 비상 대응 시간 초과로 실패 처리됨(검사 실패와 동일하게 처리).");

            _caseSessionService.Log("[비상 대응 실패] 10초 안에 대응하지 못해 사례가 실패로 종료되었습니다.");
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
            if (!IsMutated.CurrentValue || IsResolved.CurrentValue || ResponseFailed.CurrentValue) return;
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
