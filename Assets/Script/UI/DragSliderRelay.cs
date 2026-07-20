using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Script.UI
{
    // ClickRelay와 같은 목적(표준 Slider 대신 최소 비용으로 런타임에 붙일 수 있는 드래그
    // 컴포넌트) - 이 프로젝트에 슬라이더 UI 전례가 없어서 새로 만든다. track의 가로 폭을
    // 기준으로 누르거나 끄는 위치를 0..1로 정규화해 onValueChanged로 흘려보낸다. Settings
    // 패널은 표준 Screen Space Overlay Canvas + EventSystem 위에서 동작해서(ssh 씬의 3D 모니터
    // 메쉬 클릭 릴레이와 달리) IPointerDownHandler/IDragHandler가 그대로 동작한다.
    public class DragSliderRelay : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        public RectTransform track;
        public Action<float> onValueChanged;

        public void OnPointerDown(PointerEventData eventData) => UpdateValue(eventData);
        public void OnDrag(PointerEventData eventData) => UpdateValue(eventData);

        private void UpdateValue(PointerEventData eventData)
        {
            if (track == null) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                track, eventData.position, eventData.pressEventCamera, out var local);

            var normalized = Mathf.Clamp01((local.x - track.rect.xMin) / track.rect.width);
            onValueChanged?.Invoke(normalized);
        }
    }
}
