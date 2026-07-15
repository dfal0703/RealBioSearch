using Cysharp.Threading.Tasks;
using Haare.Client.Core.DI;
using Haare.Client.UI;
using Haare.Util.Logger;
using Script.UI;
using Script.UI.Panels;
using UnityEngine;
using VContainer;

namespace Script.DI
{
    // Stage 1 부팅 시퀀스 - 화면 구성 7개 패널을 IsStack:false로 프리로드하고 즉시 연다.
    // UIPresenter.OpenPanelWithFade<T>()는 스택 최상단 검사 버그가 있어 쓰지 않는다
    // (Haare_프레임워크_정리.txt 6장 - Demo 공식 프레젠터들도 안 씀).
    public class BioSearchUIPresenter : UIPresenter
    {
        // 패널이 어디에 얼마나 크게 배치되는지는 여기가 아니라 ComputerScreenCanvas 프리팹의
        // BioSearchUIManager가 들고 있는 슬롯 RectTransform들이 정한다(인스펙터에서 조절).
        [Inject] private BioSearchUIManager _uiManager;

        public override void PostInitialize()
        {
            base.PostInitialize();
            BootSequence().Forget();
        }

        private async UniTask BootSequence()
        {
            await OpenStatic<SubjectMonitorPanel>(_uiManager.SubjectMonitorSlot);
            await OpenStatic<VisualMemoPanel>(_uiManager.VisualMemoSlot);
            await OpenStatic<HealthStatusPanel>(_uiManager.HealthStatusSlot);
            await OpenStatic<FinalReportPanel>(_uiManager.FinalReportSlot);
            await OpenStatic<ExamControlPanel>(_uiManager.ExamControlSlot);
            await OpenStatic<LibraryPanel>(_uiManager.LibrarySlot);
            await OpenStatic<CLIPanel>(_uiManager.CliSlot);

            await PostInitializeAsync();
            LogHelper.Log(LogHelper.FRAMEWORK, "BioSearchUIPresenter boot sequence complete");
        }

        private async UniTask OpenStatic<T>(RectTransform slot) where T : Component, ICustomPanel
        {
            await _sceneUiManager.LoadPanel<T>(null, false, false);
            var panel = _sceneUiManager.RentPanel<T>();
            FillSlot(panel, slot);
            panel.OpenPanel();
        }

        // 패널을 지정된 슬롯 밑으로 옮기고 그 슬롯 전체를 꽉 채우도록 앵커를 편다 - 패널 프리팹
        // 자신의 원래 앵커값은 무시된다, 실제 위치/크기는 슬롯이 정한다.
        private static void FillSlot(Component panel, RectTransform slot)
        {
            if (slot == null || panel.transform is not RectTransform rect) return;

            rect.SetParent(slot, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
