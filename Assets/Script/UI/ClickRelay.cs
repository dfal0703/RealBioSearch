using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Script.UI
{
    // ComputerViewController의 화면 클릭 릴레이는 ExecuteEvents.ExecuteHierarchy로
    // IPointerClickHandler를 직접 찾아서 호출한다(표준 Button/Selectable 없이도 동작) - 그
    // 경로에 아무 UI 오브젝트나 최소 비용으로 태울 수 있게 만든 범용 클릭 콜백 컴포넌트.
    // Button을 쓰면 Navigation/ColorBlock/SpriteState 등 안 쓰는 필드까지 프리팹에 손으로
    // 채워야 해서, 런타임에 AddComponent로 붙이는 용도로는 이쪽이 더 가볍고 안전하다.
    public class ClickRelay : MonoBehaviour, IPointerClickHandler
    {
        public Action onClick;

        public void OnPointerClick(PointerEventData eventData)
        {
            onClick?.Invoke();
        }
    }
}
