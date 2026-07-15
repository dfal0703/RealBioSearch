using System.Threading;
using Cysharp.Threading.Tasks;
using Haare.Client.UI;
using UnityEngine;

namespace Script.UI
{
    // ComputerScreenCanvas 프리팹 루트에 붙는 씬 전용 SceneUIManager.
    // BioSearchCompositionRoot가 RegisterComponentInNewPrefab으로 이 프리팹을 인스턴스화하고
    // SceneUIManager 타입으로 등록한다 - CoreLifetimeScope가 CoreUIManager를 등록하는 것과 같은 패턴.
    public class BioSearchUIManager : SceneUIManager
    {
        // 화면 구성 6영역 슬롯 - 패널을 어디에 얼마나 크게 배치할지는 여기 RectTransform들의
        // 앵커/사이즈로 정해진다(인스펙터/Scene 뷰에서 바로 드래그해 조절 가능). 패널 프리팹
        // 자체는 위치를 안 들고 있고, BioSearchUIPresenter가 로드 직후 해당 슬롯 밑으로 옮겨서
        // 꽉 채운다 - 그래서 레이아웃을 바꾸려고 패널 프리팹을 열 필요 없이 이 프리팹 하나만
        // 열면 된다.
        [Header("Panel Slots")]
        [SerializeField] private RectTransform subjectMonitorSlot;
        [SerializeField] private RectTransform visualMemoSlot;
        [SerializeField] private RectTransform healthStatusSlot;
        [SerializeField] private RectTransform finalReportSlot;
        [SerializeField] private RectTransform examControlSlot;
        [SerializeField] private RectTransform librarySlot;
        [SerializeField] private RectTransform cliSlot;

        // SafePanel(세이프존 여백) 밖, 캔버스 전체를 덮는 오버레이 - 어떤 패널의 라이브러리
        // 항목을 열든 그 패널의 좁은 사분면에 갇히지 않고 화면 전체 위에 뜨게 하려고 슬롯
        // 시스템과 분리해서 별도로 둔다. 이 필드는 절대 직접 열지 않는 템플릿(항상 비활성) -
        // 여러 팝업을 동시에 띄울 수 있어야 해서 호출할 때마다 SpawnNotepadPopup()이 이걸
        // Instantiate로 복제한 새 인스턴스를 돌려준다.
        [Header("Overlays")]
        [SerializeField] private NotepadPopup notepadPopupTemplate;

        private int _popupSpawnCount;

        // 새 팝업 창을 하나 만들어서 돌려준다(호출자가 곧바로 Open() 호출). 여러 개를 동시에
        // 열었을 때 전부 정확히 같은 자리에 겹치면 구분이 안 되니, 스폰할 때마다 살짝 대각선으로
        // 어긋나게 배치한다(8개 지나면 순환) - 어차피 드래그로 자유롭게 옮길 수 있으니 정교한
        // 배치 로직은 필요 없다.
        public NotepadPopup SpawnNotepadPopup()
        {
            if (notepadPopupTemplate == null) return null;

            var popup = Instantiate(notepadPopupTemplate, notepadPopupTemplate.transform.parent);
            var cascade = _popupSpawnCount++ % 8;
            popup.SetCascadeOffset(new Vector2(cascade * 24f, -cascade * 24f));
            return popup;
        }

        public RectTransform SubjectMonitorSlot => subjectMonitorSlot;
        public RectTransform VisualMemoSlot => visualMemoSlot;
        public RectTransform HealthStatusSlot => healthStatusSlot;
        public RectTransform FinalReportSlot => finalReportSlot;
        public RectTransform ExamControlSlot => examControlSlot;
        public RectTransform LibrarySlot => librarySlot;
        public RectTransform CliSlot => cliSlot;

        public override async UniTask Initialize(CancellationToken cts)
        {
            await base.Initialize(cts);
        }
    }
}
