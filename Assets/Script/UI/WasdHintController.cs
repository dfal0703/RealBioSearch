using System.Threading;
using Cysharp.Threading.Tasks;
using Haare.Client.Routine;
using UnityEngine.SceneManagement;

namespace Script.UI
{
    // CoreCanvas는 전역(DontDestroyOnLoad)이라 Title 씬에서도 같이 떠 있다 - WASD는 ssh 씬의
    // ComputerViewController(3D 룸 시점 전환)에서만 의미가 있으므로, 활성 씬이 ssh일 때만
    // 보이도록 스스로 토글한다. 전용 이벤트 배선 없이 매 프레임 씬 이름만 비교하는 가벼운
    // 폴링 - MonoRoutine의 UpdateProcess는 GameObject의 활성 상태와 무관하게 계속 불리므로
    // (Processor 구독 기반이라 Unity의 Update()처럼 SetActive(false)에 막히지 않는다) 자기
    // 자신이 아니라 별도 visualRoot 자식만 껐다 켰다 한다 - 루트 자신을 끄면 Awake가 다시
    // 불릴 길이 없어 등록 자체가 끊긴다.
    //
    // 실제로 안 보였던 원인(2026-07-20): isInSceneOnly가 MonoRoutine 기본값 true로 남아있으면
    // Processor.CheckDeleteProcessesForScene()이 씬 전환(Title -> ssh) 때 이 컴포넌트를
    // UnRegister() -> Finalize()까지 태워서 disposables.Clear()로 UpdateProcess 구독 자체를
    // 끊어버린다 - GameObject는 DontDestroyOnLoad로 살아있지만 Processor.Onupdate 구독이
    // 끊겨서 UpdateProcess()가 다시는 안 불리니, ssh로 넘어간 뒤 visualRoot를 켜줄 코드
    // 자체가 실행되지 않았던 것. FPSLogger.Initialize()가 base.isInSceneOnly = false를 거는
    // 것과 정확히 같은 이유로 여기도 동일하게 걸어야 한다.
    public class WasdHintController : MonoRoutine
    {
        [UnityEngine.SerializeField] private UnityEngine.GameObject visualRoot;

        public override async UniTask Initialize(CancellationToken cts)
        {
            base.isInSceneOnly = false;
            await base.Initialize(cts);
        }

        protected override void UpdateProcess()
        {
            base.UpdateProcess();
            if (visualRoot == null) return;

            var shouldShow = SceneManager.GetActiveScene().name == "ssh";
            if (visualRoot.activeSelf != shouldShow)
            {
                visualRoot.SetActive(shouldShow);
            }
        }
    }
}
