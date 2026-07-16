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
    // 문자 그대로 구현한다 - 사용자가 씬에 배치하는 계기판(3D 오브젝트)의 특정 부위(버튼/레버
    // 콜라이더)를 Main Camera 기준으로 직접 레이캐스트해서 클릭을 받는다. 인스펙터에 콜라이더
    // 참조가 필요해 NativeRoutine이 아닌 MonoRoutine을 쓴다(ComputerViewController와 같은
    // 이유).
    //
    // 컴퓨터 화면(2D, ComputerViewController.ProcessScreenPointer)과 이 계기판(3D)은 같은
    // 마우스를 서로 다른 파이프라인으로 쓴다 - 컴퓨터 시점(W)에서 화면을 조작하는 중에는 이
    // 스크립트가 끼어들면 안 되므로, IsComputerInteractive가 true인 동안은 레이캐스트 자체를
    // 쉰다(반대도 마찬가지 - 화면 클릭 파이프라인은 애초에 interactive일 때만 돈다).
    public class EmergencyPanelController : MonoRoutine
    {
        [SerializeField] private Collider buttonCollider;
        [SerializeField] private Renderer indicatorRenderer;
        [SerializeField] private ComputerViewController computerViewController;

        private static readonly Color NormalColor = new Color(0.15f, 0.15f, 0.15f, 1f);
        private static readonly Color MutatedColor = new Color(0.9f, 0.1f, 0.1f, 1f);
        private static readonly Color ResolvedColor = new Color(0.15f, 0.4f, 0.15f, 1f);

        private MutationService _mutationService;
        private Camera mainCamera;

        protected override void Constructor()
        {
            mainCamera = Camera.main;
        }

        // MutationService는 [Inject] 필드가 아니라 메서드 주입으로 받는다 - 값이 채워지는
        // 시점에 바로 구독을 시작해야(늦게 구독하면 이미 지나간 상태 변화를 놓칠 수 있음)
        // ComputerViewController가 SceneUIManager를 받는 것과 같은 패턴.
        [Inject]
        private void Construct(MutationService mutationService)
        {
            _mutationService = mutationService;
            _mutationService.IsMutated.Subscribe(_ => Refresh()).AddTo(disposables);
            _mutationService.IsResolved.Subscribe(_ => Refresh()).AddTo(disposables);
            Refresh();
        }

        protected override void UpdateProcess()
        {
            if (computerViewController != null && computerViewController.IsComputerInteractive) return;
            if (buttonCollider == null || mainCamera == null || _mutationService == null) return;

            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

            var ray = mainCamera.ScreenPointToRay(mouse.position.ReadValue());
            if (Physics.Raycast(ray, out var hit, Mathf.Infinity) && hit.collider == buttonCollider)
            {
                OnButtonClicked();
            }
        }

        private void OnButtonClicked()
        {
            if (!_mutationService.IsMutated.CurrentValue || _mutationService.IsResolved.CurrentValue) return;
            _mutationService.ResolveEmergency().Forget();
        }

        private void Refresh()
        {
            if (indicatorRenderer == null) return;

            var color = _mutationService.IsResolved.CurrentValue
                ? ResolvedColor
                : _mutationService.IsMutated.CurrentValue
                    ? MutatedColor
                    : NormalColor;

            // .material은 최초 접근 시 인스턴스 복제본을 만들어주므로 원본 프리팹 머티리얼은
            // 안 건드린다(ComputerViewController.Constructor의 screenRenderer.material과 동일).
            indicatorRenderer.material.color = color;
        }
    }
}
