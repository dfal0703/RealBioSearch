using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Haare.Util.Loader;
using Haare.Util.Logger;
using UnityEngine;

namespace Script.Data
{
    // 대화(ask 명령) 한 항목 - keyword는 CLI에서 부분 일치로 찾는 검색어, response는 그때 나오는
    // 검사체의 답변. JsonUtility가 다뤄야 해서 전부 public 필드.
    [Serializable]
    public class DialogueEntry
    {
        public string keyword;
        public string response;
    }

    // 튜토리얼 사례 1건의 "정답지" - 감염 여부/부위 같은 숨겨진 채점 기준. CaseFile과 달리
    // 플레이 중 절대 안 바뀌는 저작 데이터라 DataManager(로컬 세이브 경로)를 안 거치고 매번
    // Addressable에서 직접 읽는다.
    [Serializable]
    public class CaseFileDefinitionData
    {
        public string caseId;
        public bool isInfected;
        public string infectedRegion;

        // Stage 3: CLI의 "ask <keyword>" 명령이 참조하는 대화 스크립트.
        public List<DialogueEntry> dialogueScript = new List<DialogueEntry>();
    }

    public static class CaseFileDefinition
    {
        private const string AddressablePath = "Data/CaseFileDefinition";

        public static async UniTask<CaseFileDefinitionData> LoadAsync()
        {
            var textAsset = await AssetLoader.LoadAsset<TextAsset>(AddressablePath);
            if (textAsset == null)
            {
                LogHelper.Error(LogHelper.DATAMANAGER, $"CaseFileDefinition 로드 실패: {AddressablePath}");
                return null;
            }

            return JsonUtility.FromJson<CaseFileDefinitionData>(textAsset.text);
        }
    }
}
