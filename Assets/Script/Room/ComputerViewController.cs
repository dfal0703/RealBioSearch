using DG.Tweening;
using Haare.Client.Routine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Script.Room
{
    // WASD로 방(3D)과 컴퓨터 화면(2D 캔버스) 사이를 "프레디의 피자가게 4"식 4포인트 카메라 전환으로 오간다.
    // 인스펙터에 카메라 포인트/캔버스 참조를 노출해야 하므로 NativeRoutine이 아닌 MonoRoutine(MonoBehaviour)을 사용한다.
    public class ComputerViewController : MonoRoutine
    {
        private enum ViewPoint
        {
            Center,
            Computer,
            Left,
            Right
        }

        [SerializeField] private Transform centerPoint;
        [SerializeField] private Transform computerPoint;
        [SerializeField] private Transform leftPoint;
        [SerializeField] private Transform rightPoint;

        [SerializeField] private Canvas computerCanvas;
        [SerializeField] private GraphicRaycaster computerRaycaster;
        [SerializeField] private Camera computerScreenCamera;
        [SerializeField] private Renderer screenRenderer;

        [SerializeField] private int screenRTWidth = 1024;
        [SerializeField] private int screenRTHeight = 768;

        [SerializeField] private float transitionDuration = 0.6f;
        [SerializeField] private Ease transitionEase = Ease.InOutQuad;

        private ViewPoint currentPoint = ViewPoint.Center;
        private Sequence activeTransition;
        private RenderTexture screenRT;

        protected override void Constructor()
        {
            // RenderTexture를 .renderTexture 에셋으로 미리 구워두면 Unity 6 URP Render Graph가
            // 깊이/포맷 조합에 따라 "Invalid imported texture" 예외를 매 프레임 던지는 경우가 있어
            // (에디터에서 만든 것과 손으로 구성한 에셋의 내부 필드가 완전히 같지 않으면 발생),
            // 런타임에 코드로 생성해서 그 문제 자체를 피한다.
            screenRT = new RenderTexture(screenRTWidth, screenRTHeight, 24, RenderTextureFormat.ARGB32)
            {
                name = "ComputerScreenRT"
            };
            screenRT.Create();

            if (computerScreenCamera != null)
            {
                computerScreenCamera.targetTexture = screenRT;
            }

            if (screenRenderer != null)
            {
                // .material은 최초 접근 시 인스턴스 복제본을 만들어주므로 원본 프리팹 머티리얼은 안 건드린다.
                var mat = screenRenderer.material;
                mat.SetTexture("_BaseMap", screenRT);
                mat.SetTexture("_EmissionMap", screenRT);
                mat.SetTexture("_MainTex", screenRT);
            }

            if (centerPoint != null)
            {
                transform.SetPositionAndRotation(centerPoint.position, centerPoint.rotation);
            }

            SetComputerInteractive(false);
        }

        protected override void UpdateProcess()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.wKey.wasPressedThisFrame) TransitionTo(ViewPoint.Computer, computerPoint);
            else if (keyboard.aKey.wasPressedThisFrame) TransitionTo(ViewPoint.Left, leftPoint);
            else if (keyboard.dKey.wasPressedThisFrame) TransitionTo(ViewPoint.Right, rightPoint);
            else if (keyboard.sKey.wasPressedThisFrame) TransitionTo(ViewPoint.Center, centerPoint);
        }

        private void TransitionTo(ViewPoint point, Transform target)
        {
            if (target == null || currentPoint == point) return;

            var leavingComputer = currentPoint == ViewPoint.Computer;
            currentPoint = point;

            if (leavingComputer) SetComputerInteractive(false);

            activeTransition?.Kill();
            activeTransition = DOTween.Sequence()
                .Join(transform.DOMove(target.position, transitionDuration))
                .Join(transform.DORotateQuaternion(target.rotation, transitionDuration))
                .SetEase(transitionEase);

            if (point == ViewPoint.Computer)
            {
                activeTransition.OnComplete(() => SetComputerInteractive(true));
            }
        }

        // 방 시점에선 ComputerScreenCamera가 렌더텍스처로만 캔버스를 그려 screenON 메쉬에 표시하고,
        // 컴퓨터 시점(W)에 들어가면 같은 캔버스를 화면 전체 Overlay로 전환해 실제 마우스 입력을 받게 한다.
        private void SetComputerInteractive(bool interactive)
        {
            if (computerCanvas == null) return;

            computerCanvas.renderMode = interactive ? RenderMode.ScreenSpaceOverlay : RenderMode.ScreenSpaceCamera;
            if (!interactive) computerCanvas.worldCamera = computerScreenCamera;

            if (computerRaycaster != null) computerRaycaster.enabled = interactive;
        }

        private void OnDestroy()
        {
            if (screenRT != null)
            {
                if (computerScreenCamera != null && computerScreenCamera.targetTexture == screenRT)
                    computerScreenCamera.targetTexture = null;

                screenRT.Release();
                Destroy(screenRT);
            }
        }
    }
}
