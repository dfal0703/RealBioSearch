using Haare.Client.UI;
using Script.UI;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Script.DI
{
    // Title 씬 전용 자식 LifetimeScope - BioSearchCompositionRoot와 같은 패턴(부모를 명시하지
    // 않으면 VContainerSettings의 루트 스코프를 자동으로 부모로 잡는다).
    public class TitleCompositionRoot : LifetimeScope
    {
        [SerializeField] private TitleUIManager titleUIManagerPrefab;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.RegisterComponentInNewPrefab(titleUIManagerPrefab, Lifetime.Singleton)
                .As<SceneUIManager>()
                .AsSelf();

            builder.RegisterEntryPoint<TitlePresenter>();
        }
    }
}
