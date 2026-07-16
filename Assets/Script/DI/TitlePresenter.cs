using Cysharp.Threading.Tasks;
using Haare.Client.Core.DI;
using Haare.Client.Routine.Service.SceneService;
using Haare.Util.Logger;
using R3;
using Script.UI.Panels;
using UnityEngine;
using VContainer;

namespace Script.DI
{
    // Title 씬 부팅 시퀀스 - TitlePanel 하나만 띄우고 시작/설정/종료 버튼을 구독한다.
    // BioSearchUIPresenter와 같은 이유로 UIPresenter(추상 클래스)를 상속해 _coreUIManager/
    // _sceneUiManager/_resolver/FadeIn()/FadeOut()를 그대로 물려받는다.
    public class TitlePresenter : UIPresenter
    {
        [Inject] private SceneService _sceneService;

        public override void PostInitialize()
        {
            base.PostInitialize();
            BootSequence().Forget();
        }

        private async UniTask BootSequence()
        {
            await _sceneUiManager.LoadPanel<TitlePanel>(_resolver, null, false, false);
            var panel = _sceneUiManager.RentPanel<TitlePanel>();
            panel.OpenPanel();

            panel.OnStartClicked.Subscribe(_ => StartGame().Forget()).AddTo(disposables);
            panel.OnSettingsClicked.Subscribe(_ => OpenSettings().Forget()).AddTo(disposables);
            panel.OnQuitClicked.Subscribe(_ => QuitGame()).AddTo(disposables);

            await PostInitializeAsync();

            // Pause의 "타이틀로 돌아가기"가 걸어둔 페이드(ssh -> Title)를 여기서 정리한다 -
            // Title이 최초 부팅 씬일 땐 LoadingFadePanel 자체가 없어서 RentPanel이 null이라
            // FadeOut 내부의 ClosePanel 호출이 아무 것도 못 찾고 조용히 지나간다(안전).
            if (_coreUIManager.RentPanel<Demo.UI.LoadingFadePanel>() != null)
            {
                await FadeOut();
            }

            LogHelper.Log(LogHelper.FRAMEWORK, "TitlePresenter boot sequence complete");
        }

        private async UniTaskVoid StartGame()
        {
            await FadeIn();
            await _sceneService.LoadScene(SceneName.ssh);
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

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
