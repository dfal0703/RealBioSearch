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

    // 검사(부위, 방식) 조합 한 건의 결과 - 개발 구현 지시서 6장 "검사 결과는 정답을 직접
    // 알려주지 않는다"에 따라 결과문 자체는 힌트일 뿐 "감염됨/안됨"을 명시하지 않는다.
    [Serializable]
    public class ExamResultEntry
    {
        public string organ;
        public string method;
        public string result;

        // 확장 기획(자료 생성계 정규화, 2026-07-24) - Audio/Numeric/Image 원자료 그래프의
        // 기준값. 저작자가 사례를 만들 때 이 값을 채워두면 ExamService가 강도(intensity)에
        // 비례하는 배율만 곱해 그대로 사용한다(그래프 "모양"은 정답 근거이므로 실행마다
        // 랜덤하게 바뀌면 안 됨). 비워두면(길이 0) ExamService가 결정론적 노이즈로 채운
        // 평탄한 기준선을 대신 사용한다.
        public float[] seriesBaseline = new float[0];
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

        // 이 사례가 튜토리얼 챕터인지(개발 구현 지시서 10장 "8단계 - 튜토리얼 챕터 통합").
        // true면 CaseSessionService가 부팅 시 튜토리얼 안내 문서를 라이브러리에 등록하고
        // 강조 표시한다 - 나중에 튜토리얼이 아닌 사례가 추가되면 이 값만 false로 두면 된다.
        public bool isTutorial;

        // Stage 3: CLI의 "ask <keyword>" 명령이 참조하는 대화 스크립트.
        public List<DialogueEntry> dialogueScript = new List<DialogueEntry>();

        // Stage 4: ExamService.RunExam(organ, method)가 참조하는 검사 결과표. 정의 안 된
        // 조합은 ExamService 쪽에서 "특이 소견 없음"으로 처리한다.
        public List<ExamResultEntry> examResults = new List<ExamResultEntry>();
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
