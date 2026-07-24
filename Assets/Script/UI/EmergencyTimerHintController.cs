using System.Threading;
using Cysharp.Threading.Tasks;
using Haare.Client.Routine;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Script.UI
{
    // 사용자 요청(2026-07-24): "10초를 [SubjectMonitorPanel에] 띄우지 말고, 화면 전체
    // UI(wasd 키 가이드 설명)과 같은 공간의 우측 위에 해줘" - WasdHintController와 정확히
    // 같은 이유로 CoreCanvas(전역, DontDestroyOnLoad)에 둔다. MutationService는 ssh 씬
    // DI 그래프에만 등록돼 있어 여기서 직접 주입받을 수 없으므로(CliInputFocus와 같은 스코프
    // 문제), EmergencyResponseSignal 정적 브릿지를 매 프레임 폴링한다.
    public class EmergencyTimerHintController : MonoRoutine
    {
        [SerializeField] private GameObject visualRoot;
        [SerializeField] private TMP_Text timerText;

        // WasdHintController.Initialize()와 동일한 이유 - isInSceneOnly 기본값(true)을 그대로
        // 두면 씬 전환(Title <-> ssh)마다 Processor가 이 컴포넌트를 UnRegister/Finalize해서
        // UpdateProcess 구독 자체가 끊긴다(24장에서 실제로 겪은 버그와 동일한 매커니즘).
        public override async UniTask Initialize(CancellationToken cts)
        {
            base.isInSceneOnly = false;
            await base.Initialize(cts);
        }

        protected override void UpdateProcess()
        {
            base.UpdateProcess();
            if (visualRoot == null) return;

            var shouldShow = SceneManager.GetActiveScene().name == "ssh" && EmergencyResponseSignal.Active;
            if (visualRoot.activeSelf != shouldShow)
            {
                visualRoot.SetActive(shouldShow);
            }

            if (shouldShow && timerText != null)
            {
                timerText.text = Mathf.CeilToInt(Mathf.Max(0f, EmergencyResponseSignal.SecondsRemaining)).ToString();
            }
        }
    }
}
