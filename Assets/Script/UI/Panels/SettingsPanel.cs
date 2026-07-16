using Haare.Client.Routine;
using Haare.Client.UI;
using Script.UI;
using TMPro;
using UnityEngine;

namespace Script.UI.Panels
{
    // 전역(Core) 패널 - CoreUIManager로 띄워서 시작씬/본편씬 양쪽에서 같은 인스턴스 타입을
    // 공유한다(사용자 요청: "타이틀에도 설정 있어야 해"). 이 프로젝트에 오디오 믹서/BGM·SFX
    // 구분이 아예 없어서 믹서 기반 다중 볼륨은 과설계 - AudioListener.volume 하나로 "실제로
    // 작동하는" 마스터 볼륨만 제공한다. 창모드는 Screen.fullScreen으로 직접 제어. 둘 다
    // PlayerPrefs에 저장해 다음 실행에도 유지(설정 화면의 가장 기본적인 기대 동작).
    [PanelAttribute("Prefabs/UI/Panels/SettingsPanel")]
    public class SettingsPanel : MonoRoutine, ICustomPanel
    {
        private const string VolumePrefKey = "settings.masterVolume";
        private const string FullscreenPrefKey = "settings.fullscreen";

        // 볼륨 막대는 Image.Type.Filled(스프라이트 필요, 이전에 LoadingOverlay에서 겪은 문제)
        // 대신 채움 사각형의 anchorMax.x를 0..1로 직접 움직이는 방식을 쓴다 - 스프라이트 없이도
        // 항상 정확히 채워지는 더 단순한 방법이라 여기선 이쪽을 골랐다.
        [SerializeField] private RectTransform volumeTrack;
        [SerializeField] private RectTransform volumeFillRect;
        [SerializeField] private TMP_Text volumeValueLabel;

        [SerializeField] private RectTransform fullscreenToggleButton;
        [SerializeField] private TMP_Text fullscreenValueLabel;

        [SerializeField] private RectTransform closeButton;

        public SceneUIManager uiManager { get; set; }
        public GameObject panel { get; set; }

        public void OpenPanel()
        {
            gameObject.SetActive(true);
            panel = gameObject;
        }

        public void BindEvent()
        {
            var savedVolume = PlayerPrefs.GetFloat(VolumePrefKey, 1f);
            ApplyVolume(savedVolume);

            var slider = volumeTrack != null ? volumeTrack.GetComponent<DragSliderRelay>() : null;
            if (slider == null && volumeTrack != null) slider = volumeTrack.gameObject.AddComponent<DragSliderRelay>();
            if (slider != null)
            {
                slider.track = volumeTrack;
                slider.onValueChanged = ApplyVolume;
            }

            var savedFullscreen = PlayerPrefs.GetInt(FullscreenPrefKey, Screen.fullScreen ? 1 : 0) != 0;
            ApplyFullscreen(savedFullscreen);

            Wire(fullscreenToggleButton, () => ApplyFullscreen(!Screen.fullScreen));
            Wire(closeButton, () => uiManager.ClosePanel<SettingsPanel>());
        }

        private void ApplyVolume(float normalized)
        {
            var clamped = Mathf.Clamp01(normalized);
            AudioListener.volume = clamped;
            PlayerPrefs.SetFloat(VolumePrefKey, clamped);

            if (volumeFillRect != null) volumeFillRect.anchorMax = new Vector2(clamped, 1f);
            if (volumeValueLabel != null) volumeValueLabel.text = $"{Mathf.RoundToInt(clamped * 100f)}%";
        }

        private void ApplyFullscreen(bool value)
        {
            Screen.fullScreen = value;
            PlayerPrefs.SetInt(FullscreenPrefKey, value ? 1 : 0);
            if (fullscreenValueLabel != null) fullscreenValueLabel.text = value ? "전체화면" : "창모드";
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
