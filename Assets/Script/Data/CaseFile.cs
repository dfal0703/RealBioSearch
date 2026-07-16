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
    //
    // ExamResult는 기획서의 4대 분류엔 없는 다섯 번째 값 - 원래는 "검사 결과 보고서"가 문서
    // 파일 분류에 속한다고 보고 Document + subfolder(장기별)로 묶었는데, 사용자가 "문서에
    // 넣지 말고 검사 결과 폴더를 따로 만들어달라"고 명시적으로 요청해서 최상위 폴더 자체를
    // 분리했다. JsonUtility가 enum을 정수로 직렬화하므로 기존 값 사이에 끼워 넣지 않고 맨
    // 뒤에 추가 - 나중에 실제 저장(SaveData)을 다시 켜게 되면 중간에 끼워 넣었을 때 기존
    // 저장 파일의 번호가 밀리는 걸 방지.
    public enum CaseFileEntryType
    {
        Document,
        Dialogue,
        Audio,
        Image,
        Numeric,
        ExamResult,

        // 6단계 "기생체 변이와 비상 상황"에서 비상 대응 완료 시 생성되는 사고 보고서.
        // ExamResult와 같은 이유로 기존 값 사이에 끼워 넣지 않고 맨 뒤에 추가.
        Incident
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

        // type 폴더 밑에 한 단계 더 나누고 싶을 때만 채운다(예: 검사 결과 문서를 장기별로
        // 묶기 - "폐/관찰 검사 결과.txt"). 빈 문자열이면 type 폴더에 바로 있는 파일.
        // LibraryPanel이 이 값으로 하위 폴더 행을 만들어준다(사용자 피드백: "경로 좀 명확히
        // 정리해줘" - 검사 결과가 전부 문서 폴더에 평평하게 쌓이던 문제).
        public string subfolder = "";

        // 튜토리얼 챕터에서 플레이어가 놓치지 않도록 라이브러리에서 시각적으로 강조해야 하는
        // 항목인지(사용자 요청: "튜토리얼 텍스트 파일을 라이브러리에 띄우고, 튜토리얼
        // 챕터 시에만 강조"). CaseSessionService가 CaseFileDefinitionData.isTutorial이 true인
        // 사례에서만 이 값을 true로 등록한다 - 나중에 튜토리얼이 아닌 사례가 추가되면 자동으로
        // 강조가 꺼진다.
        public bool isHighlighted = false;
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
