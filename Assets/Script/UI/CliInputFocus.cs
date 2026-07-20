using UnityEngine;

namespace Script.UI
{
    // CLIPanel의 자유 텍스트 입력 중에는 WASD가 방/컴퓨터 시점 전환 키가 아니라 타이핑 입력으로
    // 쓰여야 한다. CLIPanel과 ComputerViewController는 서로 다른 DI 그래프 경로(하나는 Addressable
    // 패널, 하나는 씬 컴포넌트)라 이벤트 하나 배선하기보다 정적 플래그로 상태를 공유하는 쪽이
    // 훨씬 단순하다 - 값은 한 프레임 안에서 "지금 CLI가 타이핑을 받고 있는가"만 나타낸다.
    public static class CliInputFocus
    {
        public static bool IsActive { get; set; }

        // CLIPanel과 PauseInputController(Room)는 둘 다 매 프레임 독립적으로 Keyboard.current의
        // escapeKey를 직접 폴링한다(중앙 입력 라우터가 없음) - 그래서 CLI가 포커스된 상태에서
        // Esc를 누르면, 두 컴포넌트의 UpdateProcess 실행 순서에 따라 결과가 갈리는 경합이
        // 생긴다: CLIPanel이 먼저 실행돼 IsActive를 false로 바꾸면, 뒤이어 실행되는
        // PauseInputController가 "이미 false"인 값을 보고 같은 Esc 입력에 또 반응해 일시정지까지
        // 같이 열려버린다(같은 키 입력 한 번으로 CLI 포커스 해제 + 일시정지가 동시에 발동하는
        // 버그). CLIPanel이 Esc를 실제로 처리한 프레임을 여기 기록해두고, 다른 쪽은 "이번
        // 프레임에 이미 CLI가 이 Esc를 썼는지"를 확인해서 양보하면, 두 컴포넌트의 실행 순서와
        // 무관하게 항상 CLI가 우선한다.
        private static int _escapeConsumedFrame = -1;

        public static void MarkEscapeConsumed()
        {
            _escapeConsumedFrame = Time.frameCount;
        }

        public static bool WasEscapeConsumedThisFrame()
        {
            return _escapeConsumedFrame == Time.frameCount;
        }
    }
}
