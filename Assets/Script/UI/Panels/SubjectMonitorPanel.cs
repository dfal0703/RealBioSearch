using Haare.Client.Routine;
using Haare.Client.UI;
using R3;
using Script.Service;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Script.UI.Panels
{
    // 화면 구성 "좌측 상단" - 기획서의 "3D 검사실 카메라"에 대응(구현 계획.md 0장: 실제 3D
    // 검사체 모델이 없어 이 패널의 2D 연출로 대체). 개발 구현 지시서 8장 "6단계 - 기생체
    // 변이와 비상 상황"에서 이 패널은 변이의 "전조/경고"만 담당한다(화면 색이 붉게 바뀌고
    // 경고 문구가 뜸) - 실제 "비상 버튼 조작"은 사용자가 3D 룸에 배치한 계기판 오브젝트
    // (`Script/Room/EmergencyPanelController`)로 옮겼다(2D는 정보 처리, 3D는 검사체의
    // 존재감/반응이라는 원래 역할 분리를 그대로 따름 - 구현현황 문서 Stage 6 후속 수정 참고).
    [PanelAttribute("Prefabs/UI/Panels/SubjectMonitorPanel")]
    public class SubjectMonitorPanel : MonoRoutine, ICustomPanel
    {
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private Image screenImage;

        [Inject] private MutationService _mutationService;

        private static readonly Color NormalScreenColor = new Color(0.15f, 0.15f, 0.15f, 1f);
        private static readonly Color MutatedScreenColor = new Color(0.35f, 0.05f, 0.05f, 1f);

        public SceneUIManager uiManager { get; set; }
        public GameObject panel { get; set; }

        public void OpenPanel()
        {
            gameObject.SetActive(true);
            panel = gameObject;
        }

        public void BindEvent()
        {
            if (_mutationService == null) return;

            _mutationService.IsMutated.Subscribe(_ => Refresh()).AddTo(disposables);
            _mutationService.IsResolved.Subscribe(_ => Refresh()).AddTo(disposables);
            Refresh();
        }

        private void Refresh()
        {
            var mutated = _mutationService.IsMutated.CurrentValue;
            var resolved = _mutationService.IsResolved.CurrentValue;

            if (screenImage != null)
            {
                screenImage.color = mutated ? MutatedScreenColor : NormalScreenColor;
            }

            if (statusLabel != null)
            {
                statusLabel.text = resolved
                    ? "사고 종료"
                    : mutated
                        ? "<color=#FF4444>⚠ 변이 감지 - 계기판에서 비상 대응하십시오</color>"
                        : "NO SIGNAL";
            }
        }
    }
}
