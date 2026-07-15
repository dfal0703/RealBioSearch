using System;
using TMPro;
using UnityEngine;

namespace Script.UI
{
    // 라이브러리에서 텍스트형 자료(문서/대화)를 열람할 때 뜨는 메모장 스타일 팝업.
    // Addressable+DI까지 필요 없는 단순 오버레이라 Haare ICustomPanel 시스템을 안 쓰고
    // LibraryPanel이 프리팹 참조로 직접 들고 Open/Close만 호출한다.
    public class NotepadPopup : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private GameObject closeButton;
        [SerializeField] private GameObject backdrop;

        // 프리팹에서 이 오브젝트는 처음부터 비활성 상태(m_IsActive: 0)로 시작한다 - Unity는
        // 비활성 오브젝트의 Awake를 활성화되기 전까진 안 부르므로, 클릭 배선은 Open()이 맨 처음
        // SetActive(true)를 호출하는 순간 자동으로 한 번 실행된다. 그래서 여기서 SetActive를
        // 다시 건드리면 안 된다(Open()의 활성화 도중에 재진입해서 다시 꺼버리게 됨).
        private void Awake()
        {
            AttachClick(closeButton, Close);
            AttachClick(backdrop, Close);
        }

        private static void AttachClick(GameObject target, Action callback)
        {
            if (target == null) return;
            var relay = target.GetComponent<ClickRelay>();
            if (relay == null) relay = target.AddComponent<ClickRelay>();
            relay.onClick = callback;
        }

        public void Open(string title, string content)
        {
            if (titleText != null) titleText.text = title;
            if (bodyText != null) bodyText.text = content;
            gameObject.SetActive(true);
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }
    }
}
