using System;
using System.Collections.Generic;
using Haare.Client.Routine;
using Haare.Client.UI;
using Script.Service;
using Script.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Script.UI.Panels
{
    // 화면 구성 "좌측 중앙" - 시각 메모 공간(전체 기획 정리.md 11장: "사진 배치/의심 부위
    // 표시/이미지 비교/시각적 메모/중요 자료 고정"). 사진 배치·이미지 비교는 그 자료를 만드는
    // 시스템(촬영 검사)이 아직 없어서 구현 불가능하지만(기획 대조 문서 10장 참고), "의심 부위
    // 표시"는 사진 없이도 구현 가능한 최소 기능이라 이번에 채운다 - 장기 목록을 체크리스트로
    // 보여주고, 클릭할 때마다 플레이어가 직접 "의심됨" 표시를 토글한다. 순수 메모장이라 게임
    // 판정(FinalReportPanel 제출)에는 전혀 영향을 주지 않는다 - 플레이어 자신의 추론을
    // 정리하는 도구일 뿐이다.
    [PanelAttribute("Prefabs/UI/Panels/VisualMemoPanel")]
    public class VisualMemoPanel : MonoRoutine, ICustomPanel
    {
        [SerializeField] private RectTransform listContent;
        [SerializeField] private TMP_FontAsset rowFont;
        [SerializeField] private float rowHeight = 24f;

        private static readonly Color MarkedColor = new Color(1f, 0.85f, 0.4f, 1f);
        private static readonly Color NormalColor = new Color(0.75f, 0.85f, 0.8f, 1f);

        private readonly HashSet<string> _markedOrgans = new HashSet<string>();
        private readonly List<TMP_Text> _rowTexts = new List<TMP_Text>();

        public SceneUIManager uiManager { get; set; }
        public GameObject panel { get; set; }

        public void OpenPanel()
        {
            gameObject.SetActive(true);
            panel = gameObject;
        }

        public void BindEvent()
        {
            BuildRows();
        }

        // ExamControlPanel의 드롭다운 행 조립과 같은 패턴 - 개수가 고정이라도 프리팹에 손으로
        // 채우기보다 코드 한 군데서 관리하는 쪽이 유지보수하기 쉽다.
        private void BuildRows()
        {
            if (listContent == null) return;

            _rowTexts.Clear();
            var uiLayer = listContent.gameObject.layer;

            foreach (var organ in ExamService.Organs)
            {
                var target = organ;

                var rowGo = new GameObject(organ, typeof(RectTransform)) { layer = uiLayer };
                var rowRect = (RectTransform)rowGo.transform;
                rowRect.SetParent(listContent, false);
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
                text.fontSize = 14;
                text.alignment = TextAlignmentOptions.MidlineLeft;
                text.raycastTarget = false;
                if (rowFont != null) text.font = rowFont;
                _rowTexts.Add(text);

                var relay = rowGo.AddComponent<ClickRelay>();
                relay.onClick = () => ToggleOrgan(target);

                RefreshRowText(text, organ);
            }
        }

        private void ToggleOrgan(string organ)
        {
            if (!_markedOrgans.Add(organ))
            {
                _markedOrgans.Remove(organ);
            }

            var index = Array.IndexOf(ExamService.Organs, organ);
            if (index >= 0 && index < _rowTexts.Count)
            {
                RefreshRowText(_rowTexts[index], organ);
            }
        }

        private void RefreshRowText(TMP_Text text, string organ)
        {
            var marked = _markedOrgans.Contains(organ);
            text.text = marked ? $"[의심] {organ}" : organ;
            text.color = marked ? MarkedColor : NormalColor;
            text.fontStyle = marked ? FontStyles.Bold : FontStyles.Normal;
        }
    }
}
