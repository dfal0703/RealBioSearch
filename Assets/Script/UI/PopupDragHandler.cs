using UnityEngine;
using UnityEngine.EventSystems;

namespace Script.UI
{
    // 팝업 창(NotepadPopup)의 제목표시줄에 붙여서 드래그로 창 위치를 옮긴다.
    // ComputerViewController의 커스텀 클릭 릴레이가 이제 IDragHandler까지 ExecuteEvents로
    // 넘겨주므로(UpdateDrag 참고) 표준 Unity 드래그 인터페이스 하나만 구현하면 된다 - Begin/End는
    // 이 단순한 "누른 채 끌기"에는 필요 없어서 뺐다.
    public class PopupDragHandler : MonoBehaviour, IDragHandler
    {
        [SerializeField] private RectTransform windowToMove;

        // 화면 밖으로 얼마나 삐져나갈 수 있는지가 아니라, "창 크기의 이 비율만큼은 항상 화면
        // 안에 남아야 한다"는 값이다 - 창이 완전히(또는 거의) 캔버스 밖으로 나가버리면 다시
        // 클릭할 방법이 없어져서 영원히 못 지우는 상태가 되는 걸 막는다(실제로 재현된 버그).
        // 고정 픽셀 값(예: 60px) 대신 비율로 두는 이유: 팝업이 3D 모니터 메쉬에 렌더텍스처로
        // 입혀져서 실제 화면에선 훨씬 작게 보이므로, 고정 픽셀 마진은 체감상 "거의 사라진
        // 것"처럼 보였다(사용자 피드백) - 창 크기 대비 비율이면 항상 눈에 띄게 남는다.
        [SerializeField, Range(0.1f, 0.9f)] private float minVisibleFraction = 0.5f;

        public void OnDrag(PointerEventData eventData)
        {
            if (windowToMove == null) return;

            var desired = windowToMove.anchoredPosition + eventData.delta;
            windowToMove.anchoredPosition = ClampToParent(desired);
        }

        // windowToMove와 그 부모(NotepadPopup 루트, 캔버스 전체를 덮음)가 둘 다 pivot (0.5,0.5)
        // 라는 전제로 계산한다 - 그래서 RectTransform.rect가 곧 [-size/2, size/2] 범위가 되고,
        // anchoredPosition은 그대로 "부모 중심 기준 오프셋"으로 취급할 수 있다.
        private Vector2 ClampToParent(Vector2 desiredPosition)
        {
            if (windowToMove.parent is not RectTransform parentRect) return desiredPosition;

            var halfParent = parentRect.rect.size * 0.5f;
            var windowSize = windowToMove.rect.size;
            var halfWindow = windowSize * 0.5f;
            var margin = windowSize * minVisibleFraction;

            var minX = -halfParent.x - halfWindow.x + margin.x;
            var maxX = halfParent.x + halfWindow.x - margin.x;
            var minY = -halfParent.y - halfWindow.y + margin.y;
            // 위쪽만 예외 - 제목표시줄(=드래그 손잡이이자 닫기 버튼 위치)이 화면 위로 한
            // 픽셀도 넘어가지 않게 완전히 막는다(다른 세 방향은 절반까지 삐져나가는 걸 허용).
            var maxY = halfParent.y - halfWindow.y;

            // 팝업 자체가 화면보다 커서 min > max가 되는 극단적인 경우엔 중앙으로 고정.
            var x = minX <= maxX ? Mathf.Clamp(desiredPosition.x, minX, maxX) : 0f;
            var y = minY <= maxY ? Mathf.Clamp(desiredPosition.y, minY, maxY) : 0f;
            return new Vector2(x, y);
        }
    }
}
