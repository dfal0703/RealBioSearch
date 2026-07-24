using DG.Tweening;
using Haare.Client.Routine;
using Haare.Client.UI;
using R3;
using Script.Service;
using Script.UI;
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

        // 사용자 요청(2026-07-24): "비상 대응 발생시, 컴퓨터 화면 매터리얼 전체를 빨간색으로
        // 깜빡거리게 해줘" - SubjectMonitorPanel은 렌더텍스처 "안" 좌측 상단 패널 색만 바꾸는
        // 정적인 경고라 컴퓨터 시점(W)이 아니면 안 보인다. 이건 화면 메쉬(screenON) 자체의
        // 머티리얼을 깜빡이게 해서, 방의 어느 시점(A/D/S)에서 봐도 비상 상황이 물리적으로
        // 눈에 띄게 만든다.
        private Material screenMaterial;
        private MutationService _mutationService;
        private bool _flashResetDone = true;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly Color EmergencyFlashColor = new Color(1f, 0.1f, 0.1f, 1f);
        private const float EmergencyFlashSpeed = 4f;

        // 사용자 요청(2026-07-24): "다소 불안정 상태에서도, 아주 느린 간격으로(5초) 화면
        // 빨간색으로 깜빡거리게 해줘" - 완전한 변이 전 전조 단계(AgitationLevel.Tense/
        // Critical)에서도 은은하게 암시하되, 완전 변이의 빠른 깜빡임과는 확실히 다른 "아주
        // 느린" 속도로 구분한다. Mathf.PingPong(t, 1) 한 사이클(0->1->0)의 주기는
        // 2/speed이므로, 5초 주기를 원하면 speed = 2/5.
        private const float TenseFlashPeriodSeconds = 5f;
        private const float TenseFlashSpeed = 2f / TenseFlashPeriodSeconds;

        private ViewPoint currentPoint = ViewPoint.Center;
        private Sequence activeTransition;
        private RenderTexture screenRT;

        private bool interactive;

        // EmergencyPanelController(계기판 3D 클릭)가 컴퓨터 화면 조작 중엔 자기 레이캐스트를
        // 쉬어야 하는지 판단하는 데 쓴다 - 두 상호작용이 동시에 마우스를 두고 경합하면 안 됨.
        public bool IsComputerInteractive => interactive;

        // 사용자 요청(2026-07-20): "카메라 이동 중에는 입력이 안되도록 해줘" - 시점 전환
        // 트윈(activeTransition)이 재생되는 동안엔 WASD로 새 전환을 또 걸거나(카메라가 중간에
        // 방향을 홱 트는 어색함), 화면/계기판 클릭이 들어가면 안 된다. EmergencyPanelController도
        // 이 값을 참고해 이동 중엔 레이캐스트를 쉰다(IsComputerInteractive와 같은 용도).
        public bool IsTransitioning { get; private set; }

        private GameObject hoveredObject;
        private GameObject pressedObject;
        private GameObject draggedObject;
        private Vector2 lastDragVirtualPosition;

        // ComputerScreenCanvas(=BioSearchUIManager) 프리팹은 이제 BioSearchCompositionRoot가
        // RegisterComponentInNewPrefab으로 직접 Instantiate+등록한다. 여긴 같은 인스턴스를
        // SceneUIManager 타입으로 주입받아 Canvas만 꺼내 쓴다 - 캔버스를 여기서 또 Instantiate하면
        // 인스턴스가 두 개가 되어버린다.
        [Inject]
        private void Construct(SceneUIManager sceneUiManager, MutationService mutationService)
        {
            computerCanvas = sceneUiManager.GetComponent<Canvas>();

            // EmergencyPanelController.Construct()와 같은 이유 - 값이 채워지는 시점에 바로
            // 구독해야 이미 지나간 상태 변화를 놓치지 않는다. 실제 색 갱신은 매 프레임 필요한
            // 애니메이션(깜빡임)이라 UpdateProcess()의 UpdateEmergencyFlash()에서 수행하고,
            // 여기서는 IsResolved로 돌아왔을 때 화면을 즉시 원래 색으로 복원하는 것만 담당한다.
            _mutationService = mutationService;
            _mutationService.IsResolved.Subscribe(resolved =>
            {
                if (resolved) ApplyScreenTint(Color.white);
            }).AddTo(disposables);
            _mutationService.ResponseFailed.Subscribe(failed =>
            {
                if (failed) ApplyScreenTint(Color.white);
            }).AddTo(disposables);
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
                screenMaterial = mat;
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
            // 카메라 시점(WASD)이나 전환 상태와 무관하게 항상 갱신 - 계기판(A 시점)을 보고
            // 있어도 화면 메쉬가 붉게 깜빡이는 게 보여야 "물리적으로 눈에 띄는" 경고가 된다.
            UpdateEmergencyFlash();

            // 사용자 요청: 카메라가 시점 전환 중일 때는 어떤 입력도 받지 않는다 - WASD로 새
            // 전환을 또 걸거나 화면을 클릭하는 걸 막는다.
            if (IsTransitioning) return;

            // CLIPanel이 자유 텍스트 입력을 받는 동안은 WASD가 타이핑 문자로 쓰여야 하므로
            // 시점 전환 키로 가로채면 안 된다(CliInputFocus 참고).
            var keyboard = Keyboard.current;
            if (keyboard != null && !CliInputFocus.IsActive)
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

            IsTransitioning = true;
            activeTransition?.Kill();
            activeTransition = DOTween.Sequence()
                .Join(transform.DOMove(target.position, transitionDuration))
                .Join(transform.DORotateQuaternion(target.rotation, transitionDuration))
                .SetEase(transitionEase)
                .OnComplete(() =>
                {
                    IsTransitioning = false;
                    if (point == ViewPoint.Computer) SetComputerInteractive(true);
                });
        }

        private void SetComputerInteractive(bool value)
        {
            interactive = value;
            if (!value)
            {
                UpdateHover(null, default);
                pressedObject = null;
                CliInputFocus.IsActive = false;
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
            UpdateScroll(hit, virtualPosition, mouse);
            UpdateDrag(hitScreen, hit, virtualPosition, mouse);
        }

        // 팝업 제목표시줄(PopupDragHandler)처럼 눌러서 끄는 UI를 지원한다. 처음 누른 프레임의
        // 히트 오브젝트를 기억해두고, 버튼을 떼기 전까지는 마우스가 어디를 지나든(다른 그래픽
        // 위여도) 계속 그 오브젝트에만 델타를 흘려보낸다 - 표준 드래그 동작과 동일.
        private void UpdateDrag(bool hitScreen, GameObject hit, Vector2 screenPoint, Mouse mouse)
        {
            if (mouse.leftButton.wasPressedThisFrame)
            {
                draggedObject = hitScreen ? hit : null;
                lastDragVirtualPosition = screenPoint;
                return;
            }

            if (!mouse.leftButton.isPressed)
            {
                draggedObject = null;
                return;
            }

            if (draggedObject == null || !hitScreen)
            {
                // 드래그 중 화면 메쉬 밖으로 커서가 잠깐 벗어나도, 돌아왔을 때 그 사이의 큰 점프를
                // 델타로 흘려보내지 않도록 기준 위치만 다시 맞춰준다.
                if (hitScreen) lastDragVirtualPosition = screenPoint;
                return;
            }

            var delta = screenPoint - lastDragVirtualPosition;
            lastDragVirtualPosition = screenPoint;
            if (delta == Vector2.zero) return;

            var eventData = new PointerEventData(EventSystem.current) { position = screenPoint, delta = delta };
            ExecuteEvents.ExecuteHierarchy(draggedObject, eventData, ExecuteEvents.dragHandler);
        }

        // ProcessScreenPointer()가 interactive(=W로 컴퓨터 시점에 들어온 상태)일 때만 호출되므로
        // 휠 스크롤도 자연히 그 상태에서만 작동한다 - 클릭/호버와 같은 흐름을 그대로 탄다.
        // ScrollRect(LibraryPanel의 ListText가 이걸 쓴다)가 IScrollHandler를 이미 구현하고 있어서
        // ExecuteEvents로 넘겨주기만 하면 별도 스크롤 로직이 필요 없다.
        private void UpdateScroll(GameObject hit, Vector2 screenPoint, Mouse mouse)
        {
            if (hit == null) return;

            // Input System의 휠 델타는 OS 단위(윈도우 기준 한 틱 = 120)다. 예전엔 "한 틱 ≈ 1"
            // 스케일을 노리고 120으로 나눴었는데, 실제로 uGUI가 기대하는 스케일은 그게 아니었다 -
            // Unity 자체 InputSystemUIInputModule 소스(m_ScrollDeltaPerTick, 기본값 6.0f) 주석에
            // "예전엔 윈도우 기준 한 틱=120이었고 이걸 20으로 나눴다"고 명시돼 있다. 즉 표준
            // PointerEventData.scrollDelta는 "한 틱 ≈ 6" 스케일이 맞는 것 - 120으로 나누면
            // 의도한 것보다 6배 작은 값이 나가서, ScrollRect의 Scroll Sensitivity를 아무리
            // 올려도 원래 있어야 할 속도의 일부만 나왔다(사용자 피드백 "스크롤이 너무 천천히
            // 내려가"의 실제 원인). Unity 공식 값과 같은 20으로 수정.
            var rawScroll = mouse.scroll.ReadValue();
            if (rawScroll == Vector2.zero) return;

            var eventData = new PointerEventData(EventSystem.current)
            {
                position = screenPoint,
                scrollDelta = rawScroll / 20f
            };
            ExecuteEvents.ExecuteHierarchy(hit, eventData, ExecuteEvents.scrollHandler);
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

        // 세 단계로 나뉜다 - (1) 완전 변이(IsMutated && !IsResolved): 빠른 깜빡임(기존 그대로).
        // (2) 전조 단계(Agitation.Tense/Critical, 아직 변이 전): 아주 느린(5초 주기) 깜빡임 -
        // 사용자 요청 "다소 불안정 상태에서도, 아주 느린 간격으로(5초) 화면 빨간색으로
        // 깜빡거리게 해줘". (3) 그 외(Calm 또는 사고 종료 후): 원래 화면으로 복원하고 더
        // 이상 매 프레임 머티리얼을 건드리지 않는다(_flashResetDone 가드).
        private void UpdateEmergencyFlash()
        {
            if (screenMaterial == null || _mutationService == null) return;

            var mutated = _mutationService.IsMutated.CurrentValue;
            var resolved = _mutationService.IsResolved.CurrentValue;
            // 사용자 요청(2026-07-24) "대응 실패는 검사 실패와 동일한 리스크" - 10초 제한
            // 시간을 넘겨 실패 처리된 뒤에도 resolved와 마찬가지로 사례가 끝난 상태이므로
            // 더 이상 깜빡일 이유가 없다.
            var ended = resolved || _mutationService.ResponseFailed.CurrentValue;

            if (mutated && !ended)
            {
                _flashResetDone = false;
                // Mathf.PingPong으로 0<->1을 삼각파로 오가며 원래 화면(흰색 틴트=RT 그대로)과
                // 경고색 사이를 보간한다 - 딱딱 끊기는 On/Off보다 부드럽게 깜빡이면서도 충분히
                // 눈에 띈다.
                var t = Mathf.PingPong(Time.time * EmergencyFlashSpeed, 1f);
                ApplyScreenTint(Color.Lerp(Color.white, EmergencyFlashColor, t));
                return;
            }

            // 사고가 이미 끝났으면(resolved 또는 실패) 자극도 값이 남아있어도 전조 깜빡임을
            // 켜지 않는다 - 위기가 끝난 뒤에도 화면이 계속 깜빡이면 "아직도 위험하다"는
            // 잘못된 신호가 된다.
            var agitation = !ended ? _mutationService.Agitation.CurrentValue : AgitationLevel.Calm;
            if (agitation != AgitationLevel.Calm)
            {
                _flashResetDone = false;
                var t = Mathf.PingPong(Time.time * TenseFlashSpeed, 1f);
                ApplyScreenTint(Color.Lerp(Color.white, EmergencyFlashColor, t));
                return;
            }

            if (!_flashResetDone)
            {
                ApplyScreenTint(Color.white);
                _flashResetDone = true;
            }
        }

        // 파이프라인에 따라 URP는 "_BaseColor", 빌트인/레거시 셰이더는 "_Color"를 쓴다 -
        // Constructor()가 "_BaseMap"/"_MainTex"를 둘 다 채워두는 것과 같은 방어적 이유로,
        // 있는 프로퍼티만 골라서 설정한다.
        private void ApplyScreenTint(Color color)
        {
            if (screenMaterial == null) return;
            if (screenMaterial.HasProperty(BaseColorId)) screenMaterial.SetColor(BaseColorId, color);
            if (screenMaterial.HasProperty(ColorId)) screenMaterial.SetColor(ColorId, color);
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
