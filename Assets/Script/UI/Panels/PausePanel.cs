using Cysharp.Threading.Tasks;
using Demo.UI;
using Haare.Client.Routine;
using Haare.Client.Routine.Service.SceneService;
using Haare.Client.UI;
using Script.UI;
using UnityEngine;
using VContainer;

namespace Script.UI.Panels
{
    // 전역(Core) 패널 - ssh 씬에서만 실제로 열리지만, SettingsPanel과 같은 이유로 CoreUIManager에
    // 띄운다(씬 전환과 무관하게 존재해야 "타이틀로 돌아가기" 전환 도중에도 자연스럽게 유지됨).
    // 여는 주체는 Room/PauseInputController(ssh 씬 전용 ESC 감지) - 이 패널 자신은 "이미 열려
    // 있을 때 어떻게 반응하는가"만 담당한다.
    [PanelAttribute("Prefabs/UI/Panels/PausePanel")]
    public class PausePanel : MonoRoutine, ICustomPanel
    {
        [SerializeField] private RectTransform resumeButton;
        [SerializeField] private RectTransform settingsButton;
        [SerializeField] private RectTransform quitToTitleButton;
        [SerializeField] private RectTransform quitGameButton;

        [Inject] private CoreUIManager _coreUIManager;
        [Inject] private SceneService _sceneService;
        [Inject] private IObjectResolver _resolver;

        public SceneUIManager uiManager { get; set; }
        public GameObject panel { get; set; }

        public void OpenPanel()
        {
            gameObject.SetActive(true);
            panel = gameObject;
        }

        public void BindEvent()
        {
            Wire(resumeButton, Resume);
            Wire(settingsButton, () => OpenSettings().Forget());
            Wire(quitToTitleButton, () => QuitToTitle().Forget());
            Wire(quitGameButton, QuitGame);
        }

        // PauseInputController가 ESC로 다시 토글할 때도 이 메서드를 그대로 쓴다 - "일시정지 해제"의
        // 유일한 진입점을 여기 하나로 묶어서 timeScale 복원을 빠뜨릴 여지를 없앤다.
        public void Resume()
        {
            Time.timeScale = 1f;
            uiManager.ClosePanel<PausePanel>();
        }

        private async UniTaskVoid OpenSettings()
        {
            var settings = _coreUIManager.RentPanel<SettingsPanel>();
            if (settings == null)
            {
                await _coreUIManager.LoadPanel<SettingsPanel>(_resolver, null, false, true);
                settings = _coreUIManager.RentPanel<SettingsPanel>();
            }

            settings.OpenPanel();
        }

        private async UniTaskVoid QuitToTitle()
        {
            // CoreUIManager의 패널 스택은 LIFO라 ClosePanel<T>()는 자신이 스택 맨 위일 때만
            // 동작한다(SceneUiManager.ClosePanel 참고) - 그래서 LoadingFadePanel을 그 위에
            // 또 쌓기 전에, 지금 맨 위인 PausePanel부터 먼저 닫아야 한다(순서를 바꾸면
            // LoadingFadePanel이 위에 쌓인 채로 ClosePanel<PausePanel>()이 조용히 실패해서
            // 이 패널이 파괴되지 않고 Title 씬까지 살아남는 버그가 생긴다).
            var coreUIManager = _coreUIManager;
            var sceneService = _sceneService;
            uiManager.ClosePanel<PausePanel>();

            await coreUIManager.LoadPanel<LoadingFadePanel>();
            var fade = coreUIManager.RentPanel<LoadingFadePanel>();
            fade.OpenPanel();

            // FadeIn()이 내부적으로 Time.deltaTime으로 진행도를 계산하는데(CustomImage.Fade),
            // timeScale이 0인 상태로는 그 자체가 절대 끝나지 않는다(자기 자신이 멈춰있는 시간을
            // 기다리는 데드락) - 그래서 복원은 반드시 FadeIn보다 먼저 와야 한다. 알파가 아직
            // 0인 시점에 재개되므로 화면이 잠깐 다시 움직이는 게 보이는 부작용은 사실상 없다.
            Time.timeScale = 1f;

            await fade.FadeIn();

            await sceneService.LoadScene(SceneName.Title);
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static void Wire(RectTransform button, System.Action onClick)
        {
            if (button == null) return;
            var relay = button.GetComponent<ClickRelay>();
            if (relay == null) relay = button.gameObject.AddComponent<ClickRelay>();
            relay.onClick = onClick;
        }
    }
}
