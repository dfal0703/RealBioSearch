using Cysharp.Threading.Tasks;
using Haare.Client.Routine;
using Haare.Client.UI;
using Script.UI;
using Script.UI.Panels;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace Script.Room
{
    // ssh 씬 전용 - ESC로 PausePanel(전역 CoreUIManager 패널)을 토글한다. 인스펙터 데이터가
    // 필요 없어 원래는 NativeRoutine이 기본값이어야 하지만(Haare_프레임워크_정리.txt 3장),
    // ComputerViewController가 이미 이 GameObject(Main Camera)에 배치돼 있으므로 같은
    // GameObject에 얹기 위해 MonoRoutine으로 만든다 - 새 GameObject를 씬에 늘리지 않아도 된다.
    public class PauseInputController : MonoRoutine
    {
        [Inject] private CoreUIManager _coreUIManager;
        [Inject] private IObjectResolver _resolver;

        protected override void UpdateProcess()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // CLIPanel이 이미 CliInputFocus.IsActive일 때 ESC를 선점해서 자동완성 닫기/포커스
            // 해제에 쓰고 있다(Assets/Script/UI/Panels/CLIPanel.cs) - 타이핑 중 Esc는 먼저 CLI
            // 포커스만 빠지게 하고, 일시정지는 그 다음 Esc부터 반응하도록 여기서 양보한다.
            if (CliInputFocus.IsActive) return;
            if (!keyboard.escapeKey.wasPressedThisFrame) return;

            // 두 컴포넌트 모두 같은 프레임에 독립적으로 Esc를 폴링하므로, CLIPanel의
            // UpdateProcess가 이 컴포넌트보다 먼저 실행되는 순서에서는 위 IsActive 체크만으로
            // 부족하다(그 사이 이미 false로 바뀐 뒤라서) - CliInputFocus.cs의 소비 토큰으로
            // 실행 순서와 무관하게 CLI가 이 Esc 입력을 우선 처리했는지 다시 한번 확인한다.
            if (CliInputFocus.WasEscapeConsumedThisFrame()) return;

            TogglePause().Forget();
        }

        private async UniTaskVoid TogglePause()
        {
            var existing = _coreUIManager.RentPanel<PausePanel>();
            if (existing != null)
            {
                existing.Resume();
                return;
            }

            Time.timeScale = 0f;
            await _coreUIManager.LoadPanel<PausePanel>(_resolver, null, false, true);
            _coreUIManager.RentPanel<PausePanel>().OpenPanel();
        }
    }
}
