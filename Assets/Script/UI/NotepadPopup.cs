using System;
using TMPro;
using UnityEngine;

namespace Script.UI
{
    // 라이브러리에서 텍스트형 자료(문서/대화)를 열람할 때 뜨는 메모장 스타일 팝업.
    // Addressable+DI까지 필요 없는 단순 오버레이라 Haare ICustomPanel 시스템을 안 쓴다. 이
    // 컴포넌트가 붙은 프리팹 인스턴스 자체는 항상 비활성 "템플릿"이고(BioSearchUIManager
    // 참고), 실제로 화면에 뜨는 건 파일을 열 때마다 그 템플릿을 Instantiate로 복제한 별도
    // 인스턴스들이다 - 그래야 팝업을 여러 개 동시에 띄울 수 있다.
    //
    // 의도적으로 비모달이다 - 전체화면을 가리는 백드롭이 없다(있었다가 뺐음). 이 팝업을 만든
    // 이유 자체가 "메모장을 보면서 동시에 화면의 다른 부분(CLI, 다른 라이브러리 파일 등)을
    // 계속 조작할 수 있어야 한다"는 요구라서, 뒤 화면 클릭을 막는 요소가 있으면 안 된다.
    // 닫는 방법은 오직 오른쪽 위 닫기 버튼뿐.
    public class NotepadPopup : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private GameObject closeButton;
        [SerializeField] private RectTransform window;

        // 프리팹에서 이 오브젝트는 처음부터 비활성 상태(m_IsActive: 0)로 시작한다 - Unity는
        // 비활성 오브젝트의 Awake를 활성화되기 전까진 안 부르므로, 클릭 배선은 Open()이 맨 처음
        // SetActive(true)를 호출하는 순간 자동으로 한 번 실행된다. 그래서 여기서 SetActive를
        // 다시 건드리면 안 된다(Open()의 활성화 도중에 재진입해서 다시 꺼버리게 됨).
        private void Awake()
        {
            AttachClick(closeButton, Close);
        }

        private static void AttachClick(GameObject target, Action callback)
        {
            if (target == null) return;
            var relay = target.GetComponent<ClickRelay>();
            if (relay == null) relay = target.AddComponent<ClickRelay>();
            relay.onClick = callback;
        }

        // BioSearchUIManager.SpawnNotepadPopup()이 새로 복제한 인스턴스를 겹치지 않게 살짝
        // 어긋난 위치에 놓을 때 쓴다.
        public void SetCascadeOffset(Vector2 offset)
        {
            if (window != null) window.anchoredPosition = offset;
        }

        public void Open(string title, string content)
        {
            if (titleText != null) titleText.text = title;
            if (bodyText != null) bodyText.text = content;
            gameObject.SetActive(true);
        }

        // 이제 팝업 여러 개가 동시에 열릴 수 있어서(각각 SpawnNotepadPopup()이 만든 독립
        // 인스턴스) 재사용할 "하나의" 팝업 개념이 없다 - 닫을 때 그냥 파괴한다.
        public void Close()
        {
            Destroy(gameObject);
        }
    }
}
