using Haare.Client.Routine;
using Haare.Client.UI;
using UnityEngine;

namespace Script.UI.Panels
{
    // 화면 구성 "중앙" - 검사 부위/방식/강도 드롭다운 + 실행 버튼.
    // Stage 1에서는 자리만 잡아둔 플레이스홀더고, 실제 드롭다운/버튼 로직은 Stage 4(ExamService)에서 붙인다.
    [PanelAttribute("Prefabs/UI/Panels/ExamControlPanel")]
    public class ExamControlPanel : MonoRoutine, ICustomPanel
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
