using Haare.Client.Routine;
using Haare.Client.UI;
using R3;
using Script.UI;
using TMPro;
using UnityEngine;

namespace Script.UI.Panels
{
    // 시작씬의 유일한 패널 - 제목 + 시작/설정/종료 3버튼. 다른 패널들과 같은 방식(Image+
    // TextMeshProUGUI+ClickRelay 런타임 조립, LibraryPanel.CreateRow 참고)으로 버튼을 만든다 -
    // 다만 여기는 목록이 아니라 고정된 3개뿐이라 프리팹에 미리 배치해둔 실제 RectTransform에
    // ClickRelay만 붙이는 방식을 쓴다(LibraryPanel처럼 매번 새로 만들 필요가 없는 고정 UI라서).
    [PanelAttribute("Prefabs/UI/Panels/TitlePanel")]
    public class TitlePanel : MonoRoutine, ICustomPanel
    {
        [SerializeField] private RectTransform startButton;
        [SerializeField] private RectTransform settingsButton;
        [SerializeField] private RectTransform quitButton;
        [SerializeField] private TMP_Text versionLabel;

        public Subject<Unit> OnStartClicked { get; } = new Subject<Unit>();
        public Subject<Unit> OnSettingsClicked { get; } = new Subject<Unit>();
        public Subject<Unit> OnQuitClicked { get; } = new Subject<Unit>();

        public SceneUIManager uiManager { get; set; }
        public GameObject panel { get; set; }

        public void OpenPanel()
        {
            gameObject.SetActive(true);
            panel = gameObject;
        }

        public void BindEvent()
        {
            Wire(startButton, () => OnStartClicked.OnNext(Unit.Default));
            Wire(settingsButton, () => OnSettingsClicked.OnNext(Unit.Default));
            Wire(quitButton, () => OnQuitClicked.OnNext(Unit.Default));

            if (versionLabel != null) versionLabel.text = $"v{Application.version}";
        }

        private static void Wire(RectTransform button, System.Action onClick)
        {
            if (button == null) return;
            var relay = button.GetComponent<ClickRelay>();
            if (relay == null) relay = button.gameObject.AddComponent<ClickRelay>();
            relay.onClick = onClick;
        }
    }
}
