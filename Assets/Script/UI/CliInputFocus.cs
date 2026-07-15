namespace Script.UI
{
    // CLIPanel의 자유 텍스트 입력 중에는 WASD가 방/컴퓨터 시점 전환 키가 아니라 타이핑 입력으로
    // 쓰여야 한다. CLIPanel과 ComputerViewController는 서로 다른 DI 그래프 경로(하나는 Addressable
    // 패널, 하나는 씬 컴포넌트)라 이벤트 하나 배선하기보다 정적 플래그로 상태를 공유하는 쪽이
    // 훨씬 단순하다 - 값은 한 프레임 안에서 "지금 CLI가 타이핑을 받고 있는가"만 나타낸다.
    public static class CliInputFocus
    {
        public static bool IsActive { get; set; }
    }
}
