using Haare.Client.Routine;
using Haare.Client.UI;
using UnityEngine;

namespace Script.UI.Panels
{
    // 화면 구성 "우측 하단" - 검사 요청 수신/시스템 메시지/명령어 입력/대화 로그.
    // Stage 1에서는 텍스트 로그 영역 + 입력창 플레이스홀더만 표시하고, 명령 처리 로직은 아직 없다.
    [PanelAttribute("Prefabs/UI/Panels/CLIPanel")]
    public class CLIPanel : MonoRoutine, ICustomPanel
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
