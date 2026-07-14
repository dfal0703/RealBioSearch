using Script.Room;
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
        protected override void Configure(IContainerBuilder builder)
        {
            // 씬에 배치된 ComputerViewController를 DI 그래프에 편입시켜, 앞으로 다른 서비스가
            // FindObjectOfType 대신 [Inject]로 받을 수 있게 한다. 카메라 포인트/캔버스 같은
            // 공간 참조는 여전히 Inspector 배선이 필요 — DI가 그것까지 대신해주진 않는다.
            builder.RegisterComponentInHierarchy<ComputerViewController>();
        }
    }
}
