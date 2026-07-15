using Haare.Client.Routine;
using Haare.Client.UI;
using UnityEngine;

namespace Script.UI.Panels
{
    // 화면 구성 "좌측 상단" - 기획서의 "3D 검사실 카메라"에 대응.
    // Stage 1에서는 플레이스홀더 스프라이트 + 모니터 프레임만 표시한다.
    [PanelAttribute("Prefabs/UI/Panels/SubjectMonitorPanel")]
    public class SubjectMonitorPanel : MonoRoutine, ICustomPanel
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
