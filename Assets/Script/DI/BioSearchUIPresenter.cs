using Cysharp.Threading.Tasks;
using Haare.Client.Core.DI;
using Haare.Client.UI;
using Haare.Util.Logger;
using Script.UI.Panels;
using UnityEngine;

namespace Script.DI
{
    // Stage 1 부팅 시퀀스 - 화면 구성 7개 패널을 IsStack:false로 프리로드하고 즉시 연다.
    // UIPresenter.OpenPanelWithFade<T>()는 스택 최상단 검사 버그가 있어 쓰지 않는다
    // (Haare_프레임워크_정리.txt 6장 - Demo 공식 프레젠터들도 안 씀).
    public class BioSearchUIPresenter : UIPresenter
    {
        public override void PostInitialize()
        {
            base.PostInitialize();
            BootSequence().Forget();
        }

        private async UniTask BootSequence()
        {
            await OpenStatic<SubjectMonitorPanel>();
            await OpenStatic<VisualMemoPanel>();
            await OpenStatic<HealthStatusPanel>();
            await OpenStatic<FinalReportPanel>();
            await OpenStatic<ExamControlPanel>();
            await OpenStatic<LibraryPanel>();
            await OpenStatic<CLIPanel>();

            await PostInitializeAsync();
            LogHelper.Log(LogHelper.FRAMEWORK, "BioSearchUIPresenter boot sequence complete");
        }

        private async UniTask OpenStatic<T>() where T : Component, ICustomPanel
        {
            await _sceneUiManager.LoadPanel<T>(null, false, false);
            _sceneUiManager.RentPanel<T>().OpenPanel();
        }
    }
}
