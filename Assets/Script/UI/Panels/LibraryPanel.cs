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
    // 화면 구성 "우측 상단" - 검사체에게 첨부된 모든 자료를 실제 파일 탐색기처럼 폴더/파일
    // 2단계로 열람한다. 폴더는 CaseFileEntryType 하나당 하나씩, 그 타입의 항목이 하나라도
    // 생기는 순간 비로소 나타난다(예: 첫 대화가 시작돼야 "대화 로그" 폴더가 생김 - 전체 기획
    // 정리.md 5장/9장 참고). CaseSessionService.OnLibraryUpdated(ReplaySubject)가 부팅 중 이미
    // 등록된 진단 보고서를 포함해 지금까지 쌓인 자료를 전부 재생해주므로, 이 패널이 늦게
    // 구독을 시작해도 놓치는 항목이 없다.
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

        // 폴더 표시 순서 - 전체 기획 정리.md 9장의 자료 유형 순서(문서 → 대화·오디오 → 이미지 →
        // 수치·그래프)를 따른다.
        private static readonly CaseFileEntryType[] FolderOrder =
        {
            CaseFileEntryType.Document, CaseFileEntryType.Dialogue, CaseFileEntryType.Audio,
            CaseFileEntryType.Image, CaseFileEntryType.Numeric
        };

        // 최근 항목이 위로 오도록 항상 리스트 맨 앞에 꽂는다(구현 계획.md Stage 3 7번 항목).
        private readonly List<CaseFileEntry> _entries = new List<CaseFileEntry>();

        // null = 폴더 목록(최상위), 값이 있으면 그 타입 폴더 안(파일 목록) 보는 중.
        private CaseFileEntryType? _currentFolder;

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

            if (_currentFolder == null)
            {
                RenderFolderList();
            }
            else
            {
                RenderFileList(_currentFolder.Value);
            }
        }

        private void RenderFolderList()
        {
            if (emptyLabel != null) emptyLabel.SetActive(_entries.Count == 0);

            var presentTypes = _entries.Select(e => e.type).ToHashSet();
            foreach (var type in FolderOrder)
            {
                if (!presentTypes.Contains(type)) continue;

                var folderType = type;
                CreateRow($"{FolderLabel(folderType)}/", () =>
                {
                    _currentFolder = folderType;
                    RebuildFileList();
                });
            }
        }

        private void RenderFileList(CaseFileEntryType type)
        {
            if (emptyLabel != null) emptyLabel.SetActive(false);

            CreateRow(".. (뒤로)", () =>
            {
                _currentFolder = null;
                RebuildFileList();
            });

            foreach (var entry in _entries.Where(e => e.type == type))
            {
                var target = entry;
                CreateRow(FileName(target), () => OpenEntry(target));
            }
        }

        // 행(폴더든 파일이든) 하나를 통째로 런타임에 조립한다 - 프리팹으로 미리 만들어두면 목록
        // 개수가 바뀔 때마다 Addressable 인스턴스화를 거쳐야 해서, 개수가 가변인 이런 목록엔
        // 코드로 직접 만드는 쪽이 더 단순하고 프리팹 쪽에 손으로 써야 하는 YAML도 줄어든다.
        private void CreateRow(string label, System.Action onClick)
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
            image.color = new Color(1f, 1f, 1f, 0.04f);
            image.raycastTarget = true;

            var textGo = new GameObject("Label", typeof(RectTransform)) { layer = uiLayer };
            var textRect = (RectTransform)textGo.transform;
            textRect.SetParent(rowRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8, 0);
            textRect.offsetMax = new Vector2(-8, 0);

            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 14;
            text.color = rowColor;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;
            if (rowFont != null) text.font = rowFont;

            var relay = rowGo.AddComponent<ClickRelay>();
            relay.onClick = onClick;
        }

        // 실제 파일 탐색기 느낌을 내려고 유형에 맞는 확장자를 붙인다 - 지금 실제로 만들어지는
        // 자료는 Document/Dialogue(둘 다 텍스트)뿐이라 .txt만 붙고, 나머지 유형은 그 콘텐츠를
        // 만드는 시스템(녹음/촬영/검사)이 생기기 전까지 확장자 없이 표시된다.
        private static string FileName(CaseFileEntry entry)
        {
            return IsTextEntry(entry) ? $"{entry.title}.txt" : entry.title;
        }

        private static bool IsTextEntry(CaseFileEntry entry)
        {
            return entry.type == CaseFileEntryType.Document || entry.type == CaseFileEntryType.Dialogue;
        }

        private static string FolderLabel(CaseFileEntryType type)
        {
            switch (type)
            {
                case CaseFileEntryType.Document: return "문서";
                case CaseFileEntryType.Dialogue: return "대화 로그";
                case CaseFileEntryType.Audio: return "음성";
                case CaseFileEntryType.Image: return "이미지";
                case CaseFileEntryType.Numeric: return "수치·그래프";
                default: return type.ToString();
            }
        }

        private void OpenEntry(CaseFileEntry entry)
        {
            var popup = _uiManager != null ? _uiManager.SpawnNotepadPopup() : null;
            if (popup == null) return;

            var content = IsTextEntry(entry) ? entry.content : "이 파일 형식은 아직 열람을 지원하지 않습니다.";
            popup.Open(FileName(entry), content);
        }
    }
}
