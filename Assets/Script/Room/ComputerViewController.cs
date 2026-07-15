using DG.Tweening;
using Haare.Client.Routine;
using Haare.Client.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using VContainer;

namespace Script.Room
{
    // WASD로 방(3D)과 컴퓨터 화면(2D 캔버스) 사이를 "프레디의 피자가게 4"식 4포인트 카메라 전환으로 오간다.
    // 인스펙터에 카메라 포인트 참조를 노출해야 하므로 NativeRoutine이 아닌 MonoRoutine(MonoBehaviour)을 사용한다.
    //
    // 컴퓨터 화면 캔버스는 카메라를 바꾸지 않고 항상 ScreenSpaceCamera로 ComputerScreenCamera가
    // 렌더텍스처에 그려서 screenON 메쉬에 입힌다(방 시점/컴퓨터 시점 공통, Overlay로 전환하지 않음) —
    // 그래서 UI를 감싸는 가짜 모니터 베젤이 필요 없고, 진짜 3D 모니터 메쉬가 그 역할을 한다.
    // 대신 컴퓨터 시점(W)일 때만 마우스 클릭을 받아야 하는데, 렌더텍스처를 거쳐 3D 메쉬에 입혀진
    // 캔버스는 Unity 표준 GraphicRaycaster/EventSystem 마우스 좌표 매핑이 안 맞는다(마우스는 Main
    // Camera 화면 좌표인데 캔버스는 ComputerScreenCamera 좌표계라서). 그래서 Main Camera로 화면
    // 메쉬 콜라이더를 직접 쏴서 맞은 지점의 UV를 ComputerScreenCamera 기준 가상 좌표로 바꿔
    // GraphicRegistry/RectTransformUtility로 직접 히트테스트하고 ExecuteEvents로 눌러준다.
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

        [SerializeField] private Camera computerScreenCamera;
        [SerializeField] private Renderer screenRenderer;
        [SerializeField] private Collider screenCollider;

        [SerializeField] private int screenRTWidth = 1024;
        [SerializeField] private int screenRTHeight = 768;

        [SerializeField] private float transitionDuration = 0.6f;
        [SerializeField] private Ease transitionEase = Ease.InOutQuad;

        private Canvas computerCanvas;
        private Camera mainCamera;
        private int screenLayerMask;

        private ViewPoint currentPoint = ViewPoint.Center;
        private Sequence activeTransition;
        private RenderTexture screenRT;

        private bool interactive;
        private GameObject hoveredObject;
        private GameObject pressedObject;

        // ComputerScreenCanvas(=BioSearchUIManager) 프리팹은 이제 BioSearchCompositionRoot가
        // RegisterComponentInNewPrefab으로 직접 Instantiate+등록한다. 여긴 같은 인스턴스를
        // SceneUIManager 타입으로 주입받아 Canvas만 꺼내 쓴다 - 캔버스를 여기서 또 Instantiate하면
        // 인스턴스가 두 개가 되어버린다.
        [Inject]
        private void Construct(SceneUIManager sceneUiManager)
        {
            computerCanvas = sceneUiManager.GetComponent<Canvas>();
        }

        protected override void Constructor()
        {
            mainCamera = GetComponent<Camera>();
            // 화면 메쉬 앞을 다른 모니터 파츠 콜라이더가 가로막지 않도록, 레이캐스트를
            // screenON 전용 레이어(ComputerScreen)로만 제한한다. 실측 결과 레이어를 안 나누면
            // 케이싱 쪽 콜라이더("CRTMonitor")가 먼저 맞아 screenON에 절대 안 닿았다(로그로 확인).
            screenLayerMask = LayerMask.GetMask("ComputerScreen");

            // screenRTWidth/Height 기본값(1024x768, 4:3 가정)이 실제 screenON 메쉬 비율과 달라서
            // 캔버스 내용이 화면 밖으로 밀려 잘려 보이는 문제가 있었다 - 메쉬의 실제 가로세로
            // 비율을 읽어서 렌더텍스처/캔버스 해상도에 반영한다.
            ApplyScreenAspect();

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

            // 카메라를 바꾸지 않으므로 캔버스는 항상 ScreenSpaceCamera + ComputerScreenCamera로 고정.
            if (computerCanvas != null)
            {
                computerCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                computerCanvas.worldCamera = computerScreenCamera;
            }

            if (centerPoint != null)
            {
                transform.SetPositionAndRotation(centerPoint.position, centerPoint.rotation);
            }

            interactive = false;
        }

        // screenON 메쉬는 얇은 평판이라 로컬 바운드 세 축(x,y,z) 중 하나(법선/두께 방향)만
        // 유독 작다. 그 축을 두께로 보고 제외한 나머지 두 축 중, 월드 스페이스로 변환했을 때
        // Vector3.up과 더 나란한 쪽을 세로(height)로 판단한다 - 메쉬가 어느 로컬 축을
        // "위"로 모델링했는지 하드코딩으로 가정하지 않기 위해서다.
        private void ApplyScreenAspect()
        {
            var meshFilter = screenRenderer != null ? screenRenderer.GetComponent<MeshFilter>() : null;
            var mesh = meshFilter != null ? meshFilter.sharedMesh : null;
            if (mesh == null) return;

            var size = mesh.bounds.size;
            var extents = new[] { size.x, size.y, size.z };

            var depthAxis = 0;
            for (var i = 1; i < 3; i++)
            {
                if (extents[i] < extents[depthAxis]) depthAxis = i;
            }

            var axisA = -1;
            var axisB = -1;
            for (var i = 0; i < 3; i++)
            {
                if (i == depthAxis) continue;
                if (axisA < 0) axisA = i;
                else axisB = i;
            }

            var worldA = screenRenderer.transform.TransformDirection(AxisDirection(axisA));
            var worldB = screenRenderer.transform.TransformDirection(AxisDirection(axisB));

            int widthAxis, heightAxis;
            if (Mathf.Abs(Vector3.Dot(worldA, Vector3.up)) >= Mathf.Abs(Vector3.Dot(worldB, Vector3.up)))
            {
                heightAxis = axisA;
                widthAxis = axisB;
            }
            else
            {
                heightAxis = axisB;
                widthAxis = axisA;
            }

            var width = extents[widthAxis];
            var height = extents[heightAxis];
            if (width <= 0f || height <= 0f) return;

            var aspect = width / height;
            var pixelBudget = (float)screenRTWidth * screenRTHeight;
            screenRTHeight = Mathf.Max(64, Mathf.RoundToInt(Mathf.Sqrt(pixelBudget / aspect)));
            screenRTWidth = Mathf.Max(64, Mathf.RoundToInt(screenRTHeight * aspect));

            var scaler = computerCanvas != null ? computerCanvas.GetComponent<CanvasScaler>() : null;
            if (scaler != null)
            {
                scaler.referenceResolution = new Vector2(screenRTWidth, screenRTHeight);
            }
        }

        private static Vector3 AxisDirection(int axis)
        {
            switch (axis)
            {
                case 0: return Vector3.right;
                case 1: return Vector3.up;
                default: return Vector3.forward;
            }
        }

        protected override void UpdateProcess()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.wKey.wasPressedThisFrame) TransitionTo(ViewPoint.Computer, computerPoint);
                else if (keyboard.aKey.wasPressedThisFrame) TransitionTo(ViewPoint.Left, leftPoint);
                else if (keyboard.dKey.wasPressedThisFrame) TransitionTo(ViewPoint.Right, rightPoint);
                else if (keyboard.sKey.wasPressedThisFrame) TransitionTo(ViewPoint.Center, centerPoint);
            }

            if (interactive) ProcessScreenPointer();
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

        private void SetComputerInteractive(bool value)
        {
            interactive = value;
            if (!value)
            {
                UpdateHover(null, default);
                pressedObject = null;
            }
        }

        // Main Camera로 화면 메쉬를 직접 쏴서, 맞은 지점의 UV를 ComputerScreenCamera 기준 가상
        // 스크린 좌표로 바꾼 뒤 캔버스 그래픽을 수동으로 히트테스트하고 포인터 이벤트를 흉내낸다.
        private void ProcessScreenPointer()
        {
            var mouse = Mouse.current;
            if (mouse == null || mainCamera == null || screenCollider == null || computerCanvas == null) return;

            GameObject hit = null;
            Vector2 virtualPosition = default;

            var ray = mainCamera.ScreenPointToRay(mouse.position.ReadValue());
            var gotPhysicsHit = Physics.Raycast(ray, out var hitInfo, Mathf.Infinity, screenLayerMask);
            var hitScreen = gotPhysicsHit && hitInfo.collider == screenCollider;

            if (hitScreen)
            {
                var uv = hitInfo.textureCoord;
                virtualPosition = new Vector2(uv.x * screenRTWidth, uv.y * screenRTHeight);
                hit = RaycastCanvasGraphics(virtualPosition);
            }

            UpdateHover(hit, virtualPosition);
            UpdateClick(hit, virtualPosition, mouse);
        }

        // GraphicRaycaster.Raycast()가 내부적으로 하는 것과 같은 일(등록된 그래픽 중 스크린 포인트를
        // 포함하고 가장 위(depth가 큰)에 있는 것 찾기)을 직접 한다 - 표준 GraphicRaycaster는 마우스
        // 좌표를 eventData에서 그대로 읽어 화면/디스플레이 기준으로 재해석하려 들어서(멀티 디스플레이
        // 대응 로직), 우리가 만든 가상 좌표를 넣었을 때 그 경로를 타면 오히려 부정확해질 수 있다.
        private GameObject RaycastCanvasGraphics(Vector2 screenPoint)
        {
            var graphics = GraphicRegistry.GetRaycastableGraphicsForCanvas(computerCanvas);
            if (graphics == null) return null;

            GameObject best = null;
            var bestDepth = -1;
            for (var i = 0; i < graphics.Count; i++)
            {
                var graphic = graphics[i];
                if (graphic == null || !graphic.raycastTarget) continue;
                if (!RectTransformUtility.RectangleContainsScreenPoint(
                        graphic.rectTransform, screenPoint, computerScreenCamera, graphic.raycastPadding))
                    continue;
                if (!graphic.Raycast(screenPoint, computerScreenCamera)) continue;
                if (graphic.depth <= bestDepth) continue;

                bestDepth = graphic.depth;
                best = graphic.gameObject;
            }

            return best;
        }

        private void UpdateHover(GameObject hit, Vector2 screenPoint)
        {
            if (hit == hoveredObject) return;

            var eventData = new PointerEventData(EventSystem.current) { position = screenPoint };
            if (hoveredObject != null)
                ExecuteEvents.ExecuteHierarchy(hoveredObject, eventData, ExecuteEvents.pointerExitHandler);
            if (hit != null)
                ExecuteEvents.ExecuteHierarchy(hit, eventData, ExecuteEvents.pointerEnterHandler);

            hoveredObject = hit;
        }

        private void UpdateClick(GameObject hit, Vector2 screenPoint, Mouse mouse)
        {
            var eventData = new PointerEventData(EventSystem.current) { position = screenPoint };

            if (mouse.leftButton.wasPressedThisFrame)
            {
                pressedObject = hit;
                if (hit != null) ExecuteEvents.ExecuteHierarchy(hit, eventData, ExecuteEvents.pointerDownHandler);
            }
            else if (mouse.leftButton.wasReleasedThisFrame)
            {
                if (hit != null) ExecuteEvents.ExecuteHierarchy(hit, eventData, ExecuteEvents.pointerUpHandler);
                if (hit != null && hit == pressedObject)
                    ExecuteEvents.ExecuteHierarchy(hit, eventData, ExecuteEvents.pointerClickHandler);

                pressedObject = null;
            }
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
