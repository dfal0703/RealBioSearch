using Haare.Client.Routine;
using Haare.Client.UI;
using UnityEngine;

namespace Script.UI.Panels
{
    // 화면 구성 "좌측 하단 상부" - 건강 상태 UI. 정확한 수치는 노출하지 않고 단계형 상태만 보여줄 예정.
    // Stage 1에서는 텍스트 플레이스홀더만 표시한다.
    [PanelAttribute("Prefabs/UI/Panels/HealthStatusPanel")]
    public class HealthStatusPanel : MonoRoutine, ICustomPanel
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
