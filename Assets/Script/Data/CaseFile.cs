using System;
using System.Collections.Generic;
using Haare.Scripts.Client.Data;

namespace Script.Data
{
    public enum CaseStatus
    {
        Waiting,
        InProgress,
        Closed
    }

    // 전체 기획 정리.md 9장 "검사 결과와 파일 시스템"의 4대 분류. Numeric은 수치·그래프 자료용 —
    // 지금은 Document/Dialogue만 실제로 만들고, Audio/Image/Numeric은 그 콘텐츠를 만드는
    // 시스템(녹음/촬영/검사)이 생기는 다음 스테이지에서 채워진다.
    public enum CaseFileEntryType
    {
        Document,
        Dialogue,
        Audio,
        Image,
        Numeric
    }

    // 사례 라이브러리에 누적되는 자료 한 건. JsonUtility가 다뤄야 해서 전부 public 필드.
    [Serializable]
    public class CaseFileEntry
    {
        public string id;
        public CaseFileEntryType type;
        public string title;
        public string content;
        public string timestamp;
    }

    // DataManager.GetModel<T>()가 리플렉션으로 역직렬화하는 순수 DTO. JsonUtility가 다뤄야 해서
    // 전부 public 필드 + 단순 타입만 사용한다(딕셔너리 금지).
    [Serializable]
    public class CaseFileData : IData
    {
        public string caseId;

        // 진단 보고서 (전체 기획 정리.md 4장 "검사체 접수" 기준)
        public string subjectName;
        public string subjectGender;
        public string subjectAge;
        public string subjectIdentity; // 신분
        public string subjectOccupation; // 직업
        public string requestBackground; // 검사 요청 경위
        public string existingConditions; // 기존 질환
        public string reportedSymptoms; // 신고된 증상
        public string recentBehaviorAnomalies; // 최근 행동 이상

        public CaseStatus status;

        // 사례에 쌓이는 모든 자료(진단 보고서/대화 로그/추후 음성·이미지·수치)는 전부 여기 하나로
        // 통합한다 - "자료는 사례 라이브러리에 일관되게 누적"(개발 구현 지시서 3단계 완료 기준)
        // 요구상 소스를 둘로 나눠두면 어긋나서, Stage 2의 attachedFiles/dialogueLog를 이걸로
        // 대체했다.
        public List<CaseFileEntry> library = new List<CaseFileEntry>();

        public string finalVerdictDraft;
    }

    // DataManager가 실제로 캐시/주입하는 살아있는 모델. [DataModelAttribute]가 가리키는
    // CaseFileData 하나를 받는 public 생성자가 반드시 있어야 한다(DataManager.GetModel<T>()가
    // 리플렉션으로 그 생성자를 찾아 호출함).
    [DataModelAttribute(typeof(CaseFileData), "Data/CaseFileTemplate", "CaseFile.json")]
    public class CaseFile : IDataModel
    {
        public CaseFileData Data { get; }

        public CaseFile(CaseFileData data)
        {
            Data = data;
        }
    }
}
