using System.Collections.Generic;
using System.Linq;
using Haare.Client.Routine;
using Haare.Client.UI;
using R3;
using Script.Data;
using Script.Service;
using Script.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace Script.UI.Panels
{
    // 화면 구성 "우측 상단" - 검사체에게 첨부된 모든 자료를 실제 파일 탐색기처럼 열람한다.
    // 최상위 폴더는 CaseFileEntryType 하나당 하나씩, 그 타입의 항목이 하나라도 생기는 순간
    // 비로소 나타난다(예: 첫 대화가 시작돼야 "대화 로그" 폴더가 생김 - 전체 기획 정리.md
    // 5장/9장 참고). 그 안에는 다시 CaseFileEntry.subfolder 기준 하위 폴더(예: 검사 결과를
    // 장기별로 묶은 "폐/", "심장/")가 있을 수 있다 - 총 최대 유형 폴더 → 하위 폴더 → 파일의
    // 3단계. CaseSessionService.OnLibraryUpdated(ReplaySubject)가 부팅 중 이미 등록된 진단
    // 보고서를 포함해 지금까지 쌓인 자료를 전부 재생해주므로, 이 패널이 늦게 구독을 시작해도
    // 놓치는 항목이 없다.
    [PanelAttribute("Prefabs/UI/Panels/LibraryPanel")]
    public class LibraryPanel : MonoRoutine, ICustomPanel
    {
        [SerializeField] private RectTransform fileListContent;
        [SerializeField] private GameObject emptyLabel;
        [SerializeField] private TMP_FontAsset rowFont;
        [SerializeField] private Color rowColor = new Color(0.75f, 0.85f, 0.8f, 1f);
        [SerializeField] private float rowHeight = 28f;

        [Inject] private CaseSessionService _caseSessionService;

        // NotepadPopup은 더 이상 이 패널 프리팹의 자식이 아니다 - LibraryPanel의 좁은 사분면
        // 안에 갇히지 않고 화면 전체 위에 뜨도록 ComputerScreenCanvas 쪽 오버레이로 옮겼다.
        // 여러 개를 동시에 열 수 있어야 해서 파일을 열 때마다 BioSearchUIManager.
        // SpawnNotepadPopup()으로 새 인스턴스를 하나씩 만든다. BioSearchCompositionRoot가
        // BioSearchUIManager를 SceneUIManager로도 등록해두므로 같은 싱글턴이 주입된다.
        [Inject] private BioSearchUIManager _uiManager;

        // 폴더 표시 순서 - 기본은 전체 기획 정리.md 9장의 자료 유형 순서(문서 → 대화·오디오 →
        // 이미지 → 수치·그래프)를 따르되, ExamResult(검사 결과)는 기획서의 4대 분류엔 없는
        // 별도 폴더라 문서 바로 뒤에 배치(사용자 요청: "문서에 넣지 말고 검사 결과 폴더를
        // 따로 만들어달라"). Incident(사고 기록, 6단계)는 검사 결과와 개념적으로 가까운
        // "사건의 기록"이라 그 바로 뒤에 배치. 원래 있던 "음성"(Audio) 폴더는 사용자 지시
        // (2026-07-24) "수치그래프와 음성을 따로 두지 말고 하나로 합쳐"에 따라 제거하고
        // Numeric 폴더 하나로 통합했다(확장 기획 문서 2.3.2).
        private static readonly CaseFileEntryType[] FolderOrder =
        {
            CaseFileEntryType.Document, CaseFileEntryType.ExamResult, CaseFileEntryType.Incident,
            CaseFileEntryType.Dialogue, CaseFileEntryType.Image, CaseFileEntryType.Numeric
        };

        // 최근 항목이 위로 오도록 항상 리스트 맨 앞에 꽂는다(구현 계획.md Stage 3 7번 항목).
        private readonly List<CaseFileEntry> _entries = new List<CaseFileEntry>();

        // 탐색 위치를 2단계로 추적한다 - _currentType이 null이면 최상위(유형 폴더 목록),
        // _currentType만 있고 _currentSubfolder가 null이면 그 유형 폴더 안(하위 폴더 + 그
        // 유형에 바로 있는 파일), 둘 다 있으면 그 하위 폴더 안(파일만). 예: ExamService가
        // 검사 결과를 장기별 하위 폴더로 등록해서 "문서/폐/관찰 검사 결과.txt"처럼 보이게 한다
        // (사용자 피드백: "경로 좀 명확히 정리해줘" - 전엔 검사 결과가 전부 문서 폴더에 평평하게
        // 쌓였음).
        private CaseFileEntryType? _currentType;
        private string _currentSubfolder;

        public SceneUIManager uiManager { get; set; }
        public GameObject panel { get; set; }

        public void OpenPanel()
        {
            gameObject.SetActive(true);
            panel = gameObject;
        }

        public void BindEvent()
        {
            if (_caseSessionService == null) return;

            _caseSessionService.OnLibraryUpdated
                .Subscribe(AddOrUpdateEntry)
                .AddTo(disposables);
        }

        // CaseSessionService.AppendToLog는 이미 아는 항목이면 내용만 바꾼 뒤 같은 참조를 다시
        // 발행한다(라이브러리 시스템 자체가 그 방식으로 "대화 로그" 하나에 계속 이어 붙임) -
        // 그래서 여기서도 참조 기준으로 중복을 걸러야 같은 파일이 여러 번 목록에 안 잡힌다.
        // ReplaySubject라 늦게 붙는 구독자에게 과거 발행분이 그대로 다시 오는 경우도 이걸로
        // 함께 걸러진다.
        private void AddOrUpdateEntry(CaseFileEntry entry)
        {
            if (!_entries.Contains(entry))
            {
                _entries.Insert(0, entry);
            }

            RebuildFileList();
        }

        private void RebuildFileList()
        {
            if (fileListContent == null) return;

            for (var i = fileListContent.childCount - 1; i >= 0; i--)
            {
                Destroy(fileListContent.GetChild(i).gameObject);
            }

            if (_currentType == null)
            {
                RenderRootFolderList();
            }
            else if (_currentSubfolder == null)
            {
                RenderTypeFolder(_currentType.Value);
            }
            else
            {
                RenderSubfolder(_currentType.Value, _currentSubfolder);
            }
        }

        private void RenderRootFolderList()
        {
            if (emptyLabel != null) emptyLabel.SetActive(_entries.Count == 0);

            var presentTypes = _entries.Select(e => e.type).ToHashSet();
            foreach (var type in FolderOrder)
            {
                if (!presentTypes.Contains(type)) continue;

                var folderType = type;
                // 그 폴더 안에 강조 항목이 하나라도 있으면 폴더 행부터 눈에 띄게 - 열어보지
                // 않아도 "여기 볼 게 있다"는 걸 알 수 있게(사용자 요청: 튜토리얼 문서 강조).
                var hasHighlighted = _entries.Any(e => e.type == folderType && e.isHighlighted);
                CreateRow($"{FolderLabel(folderType)}/", () =>
                {
                    _currentType = folderType;
                    _currentSubfolder = null;
                    RebuildFileList();
                }, hasHighlighted);
            }
        }

        // 유형 폴더 안 - 하위 폴더(예: 장기별)가 있으면 그것부터 보여주고, 하위 폴더 없이 그
        // 유형에 바로 있는 파일(예: 진단 접수 보고서)은 그 아래에 나열한다. 하위 폴더 표시
        // 순서는 최근에 생긴 게 위로 오도록 _entries 순서(최신 삽입 우선)를 그대로 따른다.
        private void RenderTypeFolder(CaseFileEntryType type)
        {
            if (emptyLabel != null) emptyLabel.SetActive(false);

            CreateRow(".. (뒤로)", () =>
            {
                _currentType = null;
                RebuildFileList();
            });

            var entriesOfType = _entries.Where(e => e.type == type).ToList();

            var subfolders = entriesOfType
                .Where(e => !string.IsNullOrEmpty(e.subfolder))
                .Select(e => e.subfolder)
                .Distinct();
            foreach (var subfolder in subfolders)
            {
                var target = subfolder;
                CreateRow($"{target}/", () =>
                {
                    _currentSubfolder = target;
                    RebuildFileList();
                });
            }

            foreach (var entry in entriesOfType.Where(e => string.IsNullOrEmpty(e.subfolder)))
            {
                var target = entry;
                CreateRow(FileName(target), () => OpenEntry(target), target.isHighlighted);
            }
        }

        private void RenderSubfolder(CaseFileEntryType type, string subfolder)
        {
            if (emptyLabel != null) emptyLabel.SetActive(false);

            CreateRow(".. (뒤로)", () =>
            {
                _currentSubfolder = null;
                RebuildFileList();
            });

            foreach (var entry in _entries.Where(e => e.type == type && e.subfolder == subfolder))
            {
                var target = entry;
                CreateRow(FileName(target), () => OpenEntry(target), target.isHighlighted);
            }
        }

        // 강조 행의 배경/글자 색 - 나머지 UI가 전부 어둡고 차분한 톤이라 눈에 띄는 금색 계열을
        // 골랐다(사용자 요청: 튜토리얼 문서를 라이브러리에서 강조).
        private static readonly Color HighlightBackgroundColor = new Color(0.55f, 0.42f, 0.1f, 0.35f);
        private static readonly Color HighlightTextColor = new Color(1f, 0.85f, 0.4f, 1f);

        // 행(폴더든 파일이든) 하나를 통째로 런타임에 조립한다 - 프리팹으로 미리 만들어두면 목록
        // 개수가 바뀔 때마다 Addressable 인스턴스화를 거쳐야 해서, 개수가 가변인 이런 목록엔
        // 코드로 직접 만드는 쪽이 더 단순하고 프리팹 쪽에 손으로 써야 하는 YAML도 줄어든다.
        private void CreateRow(string label, System.Action onClick, bool highlight = false)
        {
            // ComputerScreenCamera가 UI 레이어만 렌더링하도록 컬링 마스크가 잡혀 있을 수 있어서
            // (이 캔버스의 다른 오브젝트들은 전부 m_Layer: 5) - new GameObject()의 기본 레이어(0)
            // 그대로 두면 화면에 아예 안 그려질 위험이 있다. 부모(fileListContent)의 레이어를
            // 그대로 물려받게 강제한다.
            var uiLayer = fileListContent.gameObject.layer;

            var rowGo = new GameObject(label, typeof(RectTransform)) { layer = uiLayer };
            var rowRect = (RectTransform)rowGo.transform;
            rowRect.SetParent(fileListContent, false);
            rowRect.sizeDelta = new Vector2(0, rowHeight);

            var image = rowGo.AddComponent<Image>();
            image.color = highlight ? HighlightBackgroundColor : new Color(1f, 1f, 1f, 0.04f);
            image.raycastTarget = true;

            var textGo = new GameObject("Label", typeof(RectTransform)) { layer = uiLayer };
            var textRect = (RectTransform)textGo.transform;
            textRect.SetParent(rowRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8, 0);
            textRect.offsetMax = new Vector2(-8, 0);

            var text = textGo.AddComponent<TextMeshProUGUI>();
            // "★"은 원본 NEXONLv1GothicBold.ttf에 실제로 글리프가 있다(fontTools cmap 확인,
            // U+2605 존재) - 예전에 깨졌던 진짜 원인은 SDF 아틀라스가 1장(2048x2048)뿐이라
            // 다 차서 새 글리프를 못 넣은 것이었고(17장 원인 규명), 그건 이미 multi-atlas
            // 활성화로 고쳐졌다. 그래서 대괄호 대체가 아니라 별표를 다시 써도 된다.
            text.text = highlight ? $"★ {label}" : label;
            text.fontSize = 14;
            text.color = highlight ? HighlightTextColor : rowColor;
            text.fontStyle = highlight ? FontStyles.Bold : FontStyles.Normal;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;
            if (rowFont != null) text.font = rowFont;

            var relay = rowGo.AddComponent<ClickRelay>();
            relay.onClick = onClick;
        }

        // 실제 파일 탐색기 느낌을 내려고 유형에 맞는 확장자를 붙인다 - Document/ExamResult/
        // Dialogue/Incident는 순수 텍스트라 .txt를 붙이고, Image/Numeric은 확장 기획
        // 문서 파트 A(2026-07-24) 이후 콘텐츠는 있지만(ASCII 그래프+요약 태그) "일반 문서와는
        // 다른 형태"라는 시각적 구분을 위해 의도적으로 확장자를 안 붙인다.
        private static string FileName(CaseFileEntry entry)
        {
            return IsTextEntry(entry) ? $"{entry.title}.txt" : entry.title;
        }

        private static bool IsTextEntry(CaseFileEntry entry)
        {
            return entry.type == CaseFileEntryType.Document
                   || entry.type == CaseFileEntryType.ExamResult
                   || entry.type == CaseFileEntryType.Dialogue
                   || entry.type == CaseFileEntryType.Incident;
        }

        private static string FolderLabel(CaseFileEntryType type)
        {
            switch (type)
            {
                case CaseFileEntryType.Document: return "문서";
                case CaseFileEntryType.ExamResult: return "검사 결과";
                case CaseFileEntryType.Incident: return "사고 기록";
                case CaseFileEntryType.Dialogue: return "대화 로그";
                case CaseFileEntryType.Image: return "이미지";
                case CaseFileEntryType.Numeric: return "수치·그래프";
                default: return type.ToString();
            }
        }

        // 확장 기획(자료 생성계 정규화, 2026-07-24) 이전에는 Image/Numeric 타입이
        // 콘텐츠를 만드는 시스템이 없어 "미지원" 안내로 막아뒀지만, 이제 ExamService가 이
        // 두 타입도 실제 텍스트 콘텐츠(ASCII 그래프 + 요약 태그)를 채워 넣으므로 더 이상
        // 타입으로 열람을 막을 이유가 없다 - 모든 타입을 동일하게 content 그대로 보여준다.
        private void OpenEntry(CaseFileEntry entry)
        {
            var popup = _uiManager != null ? _uiManager.SpawnNotepadPopup() : null;
            if (popup == null) return;

            popup.Open(FileName(entry), entry.content);
        }
    }
}
