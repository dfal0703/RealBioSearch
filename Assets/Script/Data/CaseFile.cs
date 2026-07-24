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
    // ExamService.GenerateRawDataEntries()가 검사 방식에 맞춰 Image/Numeric을 실제로 채운다
    // (확장 기획 문서 파트 A, 2026-07-24 - 그 전까지는 타입만 있고 콘텐츠가 없었다).
    //
    // 원래는 "음성"(Audio)이 별도 값으로 있었으나, 사용자 지시(2026-07-24) "수치그래프와
    // 음성을 따로 두지 말고 하나로 합쳐"에 따라 제거하고 Numeric으로 완전히 통합했다 - 음향
    // 검사의 파형도 결국 시계열 숫자 데이터라 별도 타입/폴더를 유지할 실익이 없다는 판단
    // (확장 기획 문서 2.3.1/2.3.2 참고). 이 프로젝트는 CaseFileData를 세션 중에만 메모리에
    // 두고 디스크에 저장하지 않으므로(`CaseSessionService` 주석 참고) enum 값을 지워도 저장된
    // 데이터의 정수 표현이 깨질 걱정이 없다.
    //
    // ExamResult는 기획서의 4대 분류엔 없는 다섯 번째 값 - 원래는 "검사 결과 보고서"가 문서
    // 파일 분류에 속한다고 보고 Document + subfolder(장기별)로 묶었는데, 사용자가 "문서에
    // 넣지 말고 검사 결과 폴더를 따로 만들어달라"고 명시적으로 요청해서 최상위 폴더 자체를
    // 분리했다.
    public enum CaseFileEntryType
    {
        Document,
        Dialogue,
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

        // 확장 기획(자료 생성계 정규화, 2026-07-24) - Numeric 타입(음향/전기/압력·진동 등
        // 계측·파형 데이터를 전부 통합, 아래 CaseFileEntryType 주석 참고)의 원시 시계열 값.
        // ExamService가 검사 방식별로 채우고, 비어있으면(길이 0) 그래프 없이 content 텍스트만
        // 표시한다(Document/Dialogue/ExamResult/Incident는 항상 빈 배열 - 하위 호환).
        public float[] seriesData = new float[0];

        // 같은 문서 - Numeric/Image 타입의 판독 요약 태그(예: "저주파 우세", "밀도 이상 없음").
        // 사용자 지시(2026-07-24) "오디오와 파형으로 구분되던 것을 오디오+파형으로 하자" →
        // 이어서 "수치그래프와 음성을 따로 두지 말고 하나로 합쳐"에 따라, 음향 검사 결과도
        // 별도 라이브러리 항목/타입으로 쪼개지 않고 Numeric 항목 하나에 파형(seriesData)과
        // 요약 태그(summaryTags)를 함께 담는다(확장 기획 문서 2.3.1/2.3.2 참고).
        public string[] summaryTags = new string[0];
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
        public string submittedProof; // 제출된 무고 증명 자료 - 기획 대조 문서 3장/18장 gap #3

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
