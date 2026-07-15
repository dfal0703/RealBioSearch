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

    // DataManager.GetModel<T>()가 리플렉션으로 역직렬화하는 순수 DTO. JsonUtility가 다뤄야 해서
    // 전부 public 필드 + 단순 타입만 사용한다(딕셔너리 금지, 리스트는 문자열 리스트로만).
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

        // "제출된 무고 증명 자료"를 포함해, 사례에 붙는 파일들은 전부 여기로 - 대화 로그
        // 파일/음성/이미지 등 실제 내용은 Stage 3에서 채워짐, 지금은 파일명/설명 문자열만.
        public List<string> attachedFiles = new List<string>();
        public List<string> dialogueLog = new List<string>();

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
