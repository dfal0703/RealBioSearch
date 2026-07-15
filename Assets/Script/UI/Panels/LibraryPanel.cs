using Haare.Client.Routine;
using Haare.Client.UI;
using UnityEngine;

namespace Script.UI.Panels
{
    // 화면 구성 "우측 상단" - 검사체에게 첨부된 자료(보고서/대화 로그/음성/파형/이미지 등) 열람.
    // Stage 1에서는 빈 리스트만 표시한다.
    [PanelAttribute("Prefabs/UI/Panels/LibraryPanel")]
    public class LibraryPanel : MonoRoutine, ICustomPanel
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
