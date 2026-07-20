using Haare.Client.Routine;
using R3;
using UnityEngine;
using VContainer;

namespace Script.Service
{
    // 개발 구현 지시서 7장 "5단계 - 검사 위험과 상태 변화" - "검사체마다 업무상 할당된 시간이
    // 존재하며, 검사 시간이 누적되면서 남은 시간이 감소한다"(전체 기획 정리.md 7장). "행동
    // 포인트보다 현실 시간과 게임 시간의 비율로 표현"하라는 지시라 UpdateProcess()에서 실시간
    // 경과에 따라 수동으로 계속 줄어든다(passive decay) - 여기에 더해 ExamService가 검사를
    // 실행할 때마다 ConsumeMinutes()로 추가 소모분을 더한다(active cost, "검사 방식에 따른
    // 시간 소요"). 두 경로가 같은 메서드를 거치므로 잔여 시간을 건드리는 곳이 하나로 유지된다.
    //
    // 초기값(현실 1초 = 게임 1분, 총 240분)은 완전한 임의값 - 구현 계획.md Stage 5에 명시된
    // 대로 "초기값은 임의 설정 후 플레이 테스트로 조정"할 대상. 사용자 피드백(2026-07-20):
    // "검사 시간 기존의 2배로 조정해줘" - 240분 -> 480분(실시간 480초 = 8분 체감 소요).
    public class CaseTimeService : NativeRoutine
    {
        [Inject] private CaseSessionService _caseSessionService;

        private const float RealSecondsPerGameMinute = 1f;
        private const float InitialBudgetMinutes = 480f;

        // 잔여 시간이 이 값 아래로 처음 내려가는 순간 CLI에 한 번 경고한다 - 기생체 자극도
        // 암시(장기 반응 증가 등, 6단계 몫)와 같은 역할을 시간 쪽에서 대신 담당. 총 예산 대비
        // 비율(기존 60/240=25%)을 유지하도록 총 예산과 함께 2배로 맞춤 - 총량만 늘리고 이
        // 값을 그대로 두면 경고가 상대적으로 훨씬 늦게(전체의 12.5% 시점) 뜨게 된다.
        private const float LowTimeWarningMinutes = 120f;

        public ReactiveProperty<float> RemainingMinutes { get; } =
            new ReactiveProperty<float>(InitialBudgetMinutes);

        public bool IsExpired => RemainingMinutes.CurrentValue <= 0f;

        private float _accumRealSeconds;
        private bool _lowTimeWarned;
        private bool _expiredLogged;

        public override void UpdateProcess()
        {
            base.UpdateProcess();
            if (IsExpired) return;

            _accumRealSeconds += Time.deltaTime;
            if (_accumRealSeconds < RealSecondsPerGameMinute) return;

            var ticks = Mathf.FloorToInt(_accumRealSeconds / RealSecondsPerGameMinute);
            _accumRealSeconds -= ticks * RealSecondsPerGameMinute;
            ConsumeMinutes(ticks);
        }

        // 검사 실행(능동 소모)과 실시간 경과(수동 소모)가 공통으로 거치는 진입점.
        public void ConsumeMinutes(float minutes)
        {
            if (minutes <= 0f || IsExpired) return;

            RemainingMinutes.Value = Mathf.Max(0f, RemainingMinutes.CurrentValue - minutes);

            if (!_lowTimeWarned && RemainingMinutes.CurrentValue <= LowTimeWarningMinutes)
            {
                _lowTimeWarned = true;
                _caseSessionService?.Log("[시간 경고] 남은 업무 시간이 얼마 남지 않았습니다.");
            }

            if (!_expiredLogged && IsExpired)
            {
                _expiredLogged = true;
                _caseSessionService?.Log("[시간 소진] 할당된 업무 시간을 모두 사용했습니다. 더 이상 검사를 진행할 수 없습니다.");
            }
        }
    }
}
