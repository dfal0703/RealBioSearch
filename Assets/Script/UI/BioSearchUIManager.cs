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
