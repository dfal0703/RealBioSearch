using System;
using Cysharp.Threading.Tasks;
using Haare.Util.Loader;
using Haare.Util.Logger;
using UnityEngine;

namespace Script.Data
{
    // 튜토리얼 사례 1건의 "정답지" - 감염 여부/부위 같은 숨겨진 채점 기준. CaseFile과 달리
    // 플레이 중 절대 안 바뀌는 저작 데이터라 DataManager(로컬 세이브 경로)를 안 거치고 매번
    // Addressable에서 직접 읽는다. 대화 응답/검사 반응 같은 콘텐츠는 아직 필요 없어서
    // (Stage 3/4에서 실제로 쓰기 시작할 때 그 모양을 정한다) 지금은 채점 기준 두 필드만 둔다.
    [Serializable]
    public class CaseFileDefinitionData
    {
        public string caseId;
        public bool isInfected;
        public string infectedRegion;
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
