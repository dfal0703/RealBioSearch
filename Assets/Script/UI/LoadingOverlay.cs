using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI
{
    // 검사 실행처럼 시간이 걸리는 연출 중 화면 전체를 가려 다른 조작을 막는 모달 로딩창
    // (C:\Users\songs\Documents\GitHub\BioSearch의 스캔 로딩 팝업과 같은 역할). NotepadPopup과
    // 달리 동시에 여러 개 뜰 일이 없어(검사는 한 번에 하나만 실행) 인스턴스를 매번 새로
    // Instantiate하지 않고 ComputerScreenCanvas에 미리 둔 템플릿 하나를 SetActive로 켜고
    // 끈다. 배경(Image, raycastTarget=true)이 캔버스 최상단 형제라 이 프로젝트의 커스텀
    // 클릭 릴레이(ComputerViewController.RaycastCanvasGraphics)가 항상 이 배경을 먼저 맞혀
    // 뒤에 있는 패널 클릭을 자연히 막는다 - 별도의 입력 잠금 코드가 필요 없다.
    public class LoadingOverlay : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private Image progressFill;

        private bool _spriteAssigned;

        public void Show(string message)
        {
            gameObject.SetActive(true);
            SetLabel(message);
            SetProgress(0f);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void SetLabel(string message)
        {
            if (label != null) label.text = message;
        }

        public void SetProgress(float value)
        {
            // Type.Filled가 실제로 동작하려면 sprite가 있어야 한다(FillSprite.cs 참고 -
            // sprite가 null이면 Image가 fillAmount를 완전히 무시하고 항상 꽉 찬 사각형만
            // 그린다). Awake()에서 한 번만 배정하던 걸 여기 지연 배정으로 바꿨다 - 이 오브젝트가
            // 프리팹에서 비활성으로 시작해 Awake 호출 시점이 SetActive(true) 타이밍에 종속되는
            // 불확실성을 아예 없애기 위해서(호출될 때마다 확인하고 한 번만 채워둔다).
            if (progressFill != null && !_spriteAssigned)
            {
                progressFill.sprite = FillSprite.Get();
                _spriteAssigned = true;
            }

            if (progressFill != null) progressFill.fillAmount = Mathf.Clamp01(value);
        }
    }
}
