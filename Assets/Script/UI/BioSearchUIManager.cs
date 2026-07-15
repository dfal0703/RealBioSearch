using System.Threading;
using Cysharp.Threading.Tasks;
using Haare.Client.UI;

namespace Script.UI
{
    // ComputerScreenCanvas 프리팹 루트에 붙는 씬 전용 SceneUIManager.
    // BioSearchCompositionRoot가 RegisterComponentInNewPrefab으로 이 프리팹을 인스턴스화하고
    // SceneUIManager 타입으로 등록한다 - CoreLifetimeScope가 CoreUIManager를 등록하는 것과 같은 패턴.
    public class BioSearchUIManager : SceneUIManager
    {
        public override async UniTask Initialize(CancellationToken cts)
        {
            await base.Initialize(cts);
        }
    }
}
