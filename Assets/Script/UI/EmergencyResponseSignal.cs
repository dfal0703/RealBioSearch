namespace Script.UI
{
    // MutationService(ssh 씬 DI 그래프)와 CoreCanvas의 전역 UI 컴포넌트
    // (EmergencyTimerHintController, 전역 DI 그래프)는 CliInputFocus와 정확히 같은 이유로
    // 서로 다른 VContainer 스코프에 있어 직접 주입할 수 없다 - 정적 플래그로만 상태를
    // 공유한다. 사용자 지시(2026-07-24): "10초를 [SubjectMonitorPanel에] 띄우지 말고,
    // 화면 전체 UI(wasd 키 가이드 설명)과 같은 공간의 우측 위에 해줘" - WasdHintController와
    // 같은 전역 자리에 비상 대응 타이머를 두기 위한 브릿지.
    public static class EmergencyResponseSignal
    {
        public static bool Active { get; set; }
        public static float SecondsRemaining { get; set; }
    }
}
