using Haare.Client.Routine;
using Haare.Client.UI;
using UnityEngine;

namespace Script.UI.Panels
{
    // 화면 구성 "좌측 중앙" - 시각적 메모 공간. Stage 1에서는 빈 보드만 표시한다.
    [PanelAttribute("Prefabs/UI/Panels/VisualMemoPanel")]
    public class VisualMemoPanel : MonoRoutine, ICustomPanel
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
