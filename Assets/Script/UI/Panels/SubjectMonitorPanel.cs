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
        private static readonly Color TenseScreenColor = new Color(0.25f, 0.2f, 0.05f, 1f);
        private static readonly Color CriticalScreenColor = new Color(0.3f, 0.12f, 0.03f, 1f);
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
            _mutationService.EmergencyStepsCompleted.Subscribe(_ => Refresh()).AddTo(disposables);
            _mutationService.Agitation.Subscribe(_ => Refresh()).AddTo(disposables);
            Refresh();
        }

        private void Refresh()
        {
            var mutated = _mutationService.IsMutated.CurrentValue;
            var resolved = _mutationService.IsResolved.CurrentValue;
            var agitation = _mutationService.Agitation.CurrentValue;

            if (screenImage != null)
            {
                screenImage.color = mutated
                    ? MutatedScreenColor
                    : agitation switch
                    {
                        AgitationLevel.Critical => CriticalScreenColor,
                        AgitationLevel.Tense => TenseScreenColor,
                        _ => NormalScreenColor
                    };
            }

            if (statusLabel != null)
            {
                // 4개 버튼(MutationService.EmergencyStepNames)으로 분리된 뒤로는 몇 개가
                // 끝났는지도 같이 보여준다 - 계기판에서 어떤 버튼을 더 눌러야 하는지는
                // EmergencyPanelController의 버튼별 표시등(빨강/초록)으로 구분된다.
                var step = _mutationService.EmergencyStepsCompleted.CurrentValue;
                var total = MutationService.EmergencyStepNames.Length;

                // "⚠"은 NEXONLv1GothicBold SDF 폰트 애셋에 그 글리프가 없어서 네모(tofu)로
                // 깨져 보인다("▾" 화살표가 Stage 1에서 같은 이유로 깨졌던 것과 동일한 문제) -
                // 폰트에 이미 있는 대괄호 표기로 대체.
                statusLabel.text = resolved
                    ? "사고 종료"
                    : mutated
                        ? $"<color=#FF4444>[경고] 변이 감지 - 계기판에서 비상 대응하십시오 ({step}/{total})</color>"
                        : agitation switch
                        {
                            // 임계치를 넘기 전 단계적 전조 - 개발 구현 지시서 8장 완료 기준
                            // "위험의 전조를 인식"을 충족시키는 부분(기획 대조 문서 15장
                            // 우선순위 1위 gap).
                            AgitationLevel.Critical => "<color=#FF8844>[경고] 검사체 반응이 극도로 불안정합니다</color>",
                            AgitationLevel.Tense => "<color=#DDCC66>[관찰] 검사체 반응이 다소 불안정합니다</color>",
                            _ => "NO SIGNAL"
                        };
            }
        }
    }
}
