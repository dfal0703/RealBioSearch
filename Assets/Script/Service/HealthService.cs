using System.Collections.Generic;
using Haare.Client.Routine;
using R3;
using UnityEngine;
using VContainer;

namespace Script.Service
{
    // 전체 기획 정리.md 7장 5단계 표시: 안정/경미한 이상/불안정/위험/위급.
    public enum HealthState
    {
        Stable,
        MinorAbnormality,
        Unstable,
        Critical,
        Emergency
    }

    // 화면에는 정확한 체력 수치 대신 5단계 상태만 노출한다("정확한 체력 수치는 공개하지
    // 않습니다" - 전체 기획 정리.md 7장). 내부적으로는 0~100 숨겨진 체력 값을 유지하고, 검사
    // 방식+강도별 위험도 테이블에 따라 검사를 실행할 때마다 깎는다 - "극단적인 검사는
    // 검사체의 체력을 감소시킨다"(같은 장). 위험도 테이블은 검사 시스템 자체의 속성이지
    // 튜토리얼 사례 콘텐츠가 아니라서 CaseFileDefinition이 아니라 여기 정적으로 정의한다
    // ("시스템과 콘텐츠 분리" 원칙, 개발 구현 지시서 2장).
    public class HealthService : NativeRoutine
    {
        [Inject] private CaseSessionService _caseSessionService;

        private const float MaxHealth = 100f;

        // 방식별 기본 위험도 - 관찰 < 음향 < 압력·진동 < 촬영·투과 < 전기 < 채취 < 극단적
        // 자극 순(문서에 명시된 서열은 아니지만 "극단적 자극 검사"에 가까울수록 위험하다는
        // 전제, ExamService.Methods와 같은 서열을 공유).
        private static readonly Dictionary<string, float> BaseDamage = new Dictionary<string, float>
        {
            { "관찰 검사", 2f },
            { "음향 검사", 6f },
            { "압력·진동 검사", 8f },
            { "촬영·투과 검사", 10f },
            { "전기 검사", 14f },
            { "채취 검사", 17f },
            { "극단적 자극 검사", 24f }
        };

        private static readonly Dictionary<string, float> IntensityMultiplier = new Dictionary<string, float>
        {
            { "약", 0.5f },
            { "중", 1f },
            { "강", 2f }
        };

        public ReactiveProperty<float> Health { get; } = new ReactiveProperty<float>(MaxHealth);

        public HealthState CurrentState => ToState(Health.CurrentValue);

        private HealthState _lastLoggedState = HealthState.Stable;

        public void ApplyExamRisk(string method, string intensity)
        {
            var baseDamage = BaseDamage.TryGetValue(method, out var b) ? b : 4f;
            var multiplier = IntensityMultiplier.TryGetValue(intensity, out var m) ? m : 1f;
            var damage = baseDamage * multiplier;

            Health.Value = Mathf.Max(0f, Health.CurrentValue - damage);

            // 매 검사마다 스팸하지 않고, 5단계가 실제로 바뀔 때만 "체감되는" 변화로 알린다.
            var state = CurrentState;
            if (state == _lastLoggedState) return;

            _lastLoggedState = state;
            _caseSessionService?.Log($"[건강 상태] 검사체 상태가 '{StateLabel(state)}' 단계로 변화했습니다.");
        }

        public static string StateLabel(HealthState state)
        {
            switch (state)
            {
                case HealthState.Stable: return "안정";
                case HealthState.MinorAbnormality: return "경미한 이상";
                case HealthState.Unstable: return "불안정";
                case HealthState.Critical: return "위험";
                default: return "위급";
            }
        }

        private static HealthState ToState(float hp)
        {
            if (hp <= 0f) return HealthState.Emergency;
            if (hp < 20f) return HealthState.Critical;
            if (hp < 45f) return HealthState.Unstable;
            if (hp < 70f) return HealthState.MinorAbnormality;
            return HealthState.Stable;
        }
    }
}
