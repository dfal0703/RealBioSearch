using Haare.Client.UI;
using Script.Room;
using Script.Service;
using Script.UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Script.DI
{
    // 프로젝트 전용 서비스를 등록하는 씬 배치형 자식 LifetimeScope.
    // 부모를 명시하지 않으면 VContainer가 자동으로 VContainerSettings의 루트 스코프(CoreLifeTimeScope 프리팹)를
    // 부모로 잡아준다(LifetimeScope.GetRuntimeParent -> VContainerSettings.GetOrCreateRootLifetimeScopeInstance).
    // 그래서 DataManager/SceneService/CoreUIManager 같은 프레임워크 서비스는 여기서 다시 등록하지 않는다 —
    // 다시 등록하면 루트 스코프와 별개의 인스턴스가 생겨버린다.
    public class BioSearchCompositionRoot : LifetimeScope
    {
        [SerializeField] private BioSearchUIManager bioSearchUIManagerPrefab;

        protected override void Configure(IContainerBuilder builder)
        {
            // 씬에 배치된 ComputerViewController를 DI 그래프에 편입시켜, 앞으로 다른 서비스가
            // FindObjectOfType 대신 [Inject]로 받을 수 있게 한다. 카메라 포인트 같은 공간 참조는
            // 여전히 Inspector 배선이 필요 — DI가 그것까지 대신해주진 않는다.
            builder.RegisterComponentInHierarchy<ComputerViewController>();

            // 사용자가 씬에 배치하는 계기판(3D 비상 버튼) 오브젝트 - 존재하면 DI 그래프에
            // 편입시켜 MutationService를 주입받게 한다. RegisterComponentInHierarchy는 씬에
            // 그 타입이 하나도 없으면 컨테이너 빌드 시점에 예외를 던져 부팅 자체가 깨지므로
            // (VContainer FindComponentProvider 소스 확인), 아직 배치 전이어도 안전하도록
            // 먼저 찾아보고 있을 때만 등록한다.
            var emergencyPanel = FindObjectOfType<EmergencyPanelController>();
            if (emergencyPanel != null)
            {
                builder.RegisterComponent(emergencyPanel);
            }

            // ComputerScreenCanvas(=BioSearchUIManager) 프리팹은 씬에 미리 두지 않고 여기서 직접
            // Instantiate+등록한다 - CoreLifetimeScope가 CoreUIManager를 RegisterComponentInNewPrefab으로
            // 등록하는 것과 동일한 패턴. RegisterComponentInHierarchy를 쓰지 않는 이유: LifetimeScope.Awake()는
            // [DefaultExecutionOrder(-5000)]라 일반 MonoBehaviour(ComputerViewController 포함)보다 먼저
            // 실행되므로, "실행 시점에 생성되는" 컴포넌트를 씬 하이어라키에서 찾는 방식은 시점상 성립하지 않는다.
            builder.RegisterComponentInNewPrefab(bioSearchUIManagerPrefab, Lifetime.Singleton)
                .As<SceneUIManager>()
                .AsSelf();

            builder.RegisterEntryPoint<BioSearchUIPresenter>();

            // NativeRoutine이라 인스펙터 배선이 없음 - BioSearchUIPresenter가 [Inject] 필드로
            // 물고 있어야 부팅 시점에 실제로 생성된다(구현현황 2026-07-15 Stage 2 계획 참고).
            builder.Register<CaseSessionService>(Lifetime.Singleton).AsSelf();

            // CLIPanel이 [Inject] 필드로 직접 물기 때문에 별도의 즉시 생성 강제가 필요 없다 -
            // CLIPanel이 로드/주입되는 시점에 자동으로 함께 생성된다.
            builder.Register<DialogueService>(Lifetime.Singleton).AsSelf();

            // ExamControlPanel이 [Inject] 필드로 직접 물기 때문에(위와 같은 이유) 별도의 즉시
            // 생성 강제가 필요 없다.
            builder.Register<ExamService>(Lifetime.Singleton).AsSelf();

            // 개발 구현 지시서 7장 "5단계 - 검사 위험과 상태 변화". ExamService가 [Inject]
            // 필드로 둘 다 물고, HealthStatusPanel도 표시를 위해 물기 때문에 별도의 즉시
            // 생성 강제가 필요 없다.
            builder.Register<CaseTimeService>(Lifetime.Singleton).AsSelf();
            builder.Register<HealthService>(Lifetime.Singleton).AsSelf();

            // 개발 구현 지시서 8장 "6단계 - 기생체 변이와 비상 상황". ExamService가 [Inject]
            // 필드로 물기 때문에 별도의 즉시 생성 강제가 필요 없다.
            builder.Register<MutationService>(Lifetime.Singleton).AsSelf();
        }
    }
}
