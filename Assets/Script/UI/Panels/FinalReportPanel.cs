using Haare.Client.Routine;
using Haare.Client.UI;
using UnityEngine;

namespace Script.UI.Panels
{
    // 화면 구성 "좌측 최하단" - 감염 여부/부위 선택 + 최종 보고서 제출.
    // Stage 1에서는 텍스트 플레이스홀더만 표시한다.
    [PanelAttribute("Prefabs/UI/Panels/FinalReportPanel")]
    public class FinalReportPanel : MonoRoutine, ICustomPanel
    {
        public SceneUIManager uiManager { get; set; }
        public GameObject panel { get; set; }

        public void OpenPanel()
        {
            gameObject.SetActive(true);
            panel = gameObject;
        }
    }
}
