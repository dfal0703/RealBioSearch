using System.Threading;
using Cysharp.Threading.Tasks;
using Haare.Client.Routine;
using R3;
using Script.Service;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace Script.Room
{
    // 개발 구현 지시서 8장 "6단계 - 기생체 변이와 비상 상황"의 "3D 공간의 비상 버튼 작동"을
    // 문자 그대로 구현한다 - 사용자가 씬에 배치하는 계기판(3D 오브젝트, 물리 버튼 4개를 한
    // 부모 아래 묶어둔 그룹)의 각 버튼 콜라이더를 Main Camera 기준으로 직접 레이캐스트해서
    // 클릭을 받는다. 인스펙터에 콜라이더 참조가 필요해 NativeRoutine이 아닌 MonoRoutine을
    // 쓴다(ComputerViewController와 같은 이유).
    //
    // 사용자 지시로 "버튼 하나를 4번 누르는 방식"에서 "4개의 각기 다른 버튼을 각각 누르는
    // 방식"으로 바꿨다 - buttonColliders/indicatorRenderers 배열의 인덱스가 곧
    // MutationService.EmergencyStepNames의 인덱스와 대응한다(0=비상 버튼 작동, 1=검사 장비
    // 긴급 정지, 2=검사실 봉쇄, 3=제압 절차 실행). 순서는 강제하지 않는다.
    //
    // 컴퓨터 화면(2D, ComputerViewController.ProcessScreenPointer)과 이 계기판(3D)은 같은
    // 마우스를 서로 다른 파이프라인으로 쓴다 - 컴퓨터 시점(W)에서 화면을 조작하는 중에는 이
    // 스크립트가 끼어들면 안 되므로, IsComputerInteractive가 true인 동안은 레이캐스트 자체를
    // 쉰다(반대도 마찬가지 - 화면 클릭 파이프라인은 애초에 interactive일 때만 돈다).
    public class EmergencyPanelController : MonoRoutine
    {
        [SerializeField] private Collider[] buttonColliders;
        [SerializeField] private Renderer[] indicatorRenderers;
        [SerializeField] private ComputerViewController computerViewController;

        private static readonly Color NormalColor = new Color(0.15f, 0.15f, 0.15f, 1f);
        private static readonly Color MutatedColor = new Color(0.9f, 0.1f, 0.1f, 1f);
        private static readonly Color StepDoneColor = new Color(0.2f, 0.7f, 0.25f, 1f);
        private static readonly Color ResolvedColor = new Color(0.15f, 0.4f, 0.15f, 1f);

        private MutationService _mutationService;
        private CaseSessionService _caseSessionService;
        private Camera mainCamera;

        // 사용자가 logs.md에 남겨준 진단 로그로 원인 확정(2026-07-20): "mainCamera=False" -
        // Camera.main이 Constructor() 시점엔 아직 null을 반환하고 있었다(태그는 정상인데도
        // 그 시점에 아직 tag lookup이 안 맞았던 것으로 보임 - ComputerViewController는 같은
        // Main Camera GameObject에 자기 자신의 GetComponent<Camera>()를 쓰기 때문에 이 문제를
        // 아예 겪지 않았고, 그래서 화면/CLI 클릭만 멀쩡했던 것). 한 번 캐시하고 끝내지 않고
        // null이면 매 프레임 다시 시도하도록 바꿔서 이 타이밍 문제를 스스로 회복하게 한다.
        protected override void Constructor()
        {
            mainCamera = Camera.main;
        }

        // isInSceneOnly=false는 25-6에서 시도했던 수정 - 실제 원인(Camera.main 타이밍)과는
        // 무관했지만, MonoRoutine.OnDestroy()가 씬 언로드 시 알아서 UnRegister하므로
        // isInSceneOnly=true로 둘 실익이 없다는 점 자체는 여전히 유효해 그대로 유지한다.
        public override async UniTask Initialize(CancellationToken cts)
        {
            isInSceneOnly = false;
            await base.Initialize(cts);
        }

        // MutationService는 [Inject] 필드가 아니라 메서드 주입으로 받는다 - 값이 채워지는
        // 시점에 바로 구독을 시작해야(늦게 구독하면 이미 지나간 상태 변화를 놓칠 수 있음)
        // ComputerViewController가 SceneUIManager를 받는 것과 같은 패턴.
        [Inject]
        private void Construct(MutationService mutationService, CaseSessionService caseSessionService)
        {
            _mutationService = mutationService;
            _caseSessionService = caseSessionService;
            _mutationService.IsMutated.Subscribe(_ => Refresh()).AddTo(disposables);
            _mutationService.IsResolved.Subscribe(_ => Refresh()).AddTo(disposables);
            _mutationService.EmergencyStepsCompleted.Subscribe(_ => Refresh()).AddTo(disposables);
            Refresh();
        }

        protected override void UpdateProcess()
        {
            if (computerViewController != null &&
                (computerViewController.IsComputerInteractive || computerViewController.IsTransitioning)) return;

            // Constructor() 시점에 Camera.main이 null이었을 경우를 대비한 자가 복구 - 한 번
            // 못 찾았다고 영원히 포기하지 않고, null인 동안은 매 프레임 다시 시도한다(찾고
            // 나면 더 이상 재시도하지 않으니 비용 부담 없음).
            if (mainCamera == null) mainCamera = Camera.main;

            if (buttonColliders == null || buttonColliders.Length == 0 || mainCamera == null || _mutationService == null) return;

            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

            var ray = mainCamera.ScreenPointToRay(mouse.position.ReadValue());
            if (!Physics.Raycast(ray, out var hit, Mathf.Infinity)) return;

            for (var i = 0; i < buttonColliders.Length; i++)
            {
                if (buttonColliders[i] == hit.collider)
                {
                    OnButtonClicked(i);
                    return;
                }
            }
        }

        // 변이가 아직 안 일어난 평상시엔 버튼을 눌러도 원래 아무 효과가 없다(비상 버튼이니
        // 당연함) - 클릭 자체는 인식되고 있다는 걸 보여주기 위해 CLI 로그만 남긴다.
        private void OnButtonClicked(int index)
        {
            if (_mutationService.IsResolved.CurrentValue) return;

            if (!_mutationService.IsMutated.CurrentValue)
            {
                _caseSessionService?.Log("[계기판] 특별한 이상이 감지되지 않아 별도 조치가 필요하지 않습니다.");
                return;
            }

            _mutationService.CompleteEmergencyStep(index).Forget();
        }

        private void Refresh()
        {
            if (indicatorRenderers == null) return;

            for (var i = 0; i < indicatorRenderers.Length; i++)
            {
                var indicator = indicatorRenderers[i];
                if (indicator == null) continue;

                Color color;
                if (_mutationService.IsResolved.CurrentValue)
                {
                    color = ResolvedColor;
                }
                else if (_mutationService.IsMutated.CurrentValue)
                {
                    // 이 버튼(i번)이 이미 눌렸으면 초록으로, 아직이면 빨강으로 - 어떤 버튼을
                    // 더 눌러야 하는지 한눈에 보이게.
                    color = _mutationService.IsStepCompleted(i) ? StepDoneColor : MutatedColor;
                }
                else
                {
                    color = NormalColor;
                }

                // .material은 최초 접근 시 인스턴스 복제본을 만들어주므로 원본 프리팹 머티리얼은
                // 안 건드린다(ComputerViewController.Constructor의 screenRenderer.material과 동일).
                indicator.material.color = color;
            }
        }
    }
}
