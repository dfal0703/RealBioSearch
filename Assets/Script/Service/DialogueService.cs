using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Haare.Client.Routine;
using Script.Data;
using VContainer;

namespace Script.Service
{
    // CLI(자유 텍스트 입력)에서 실제로 실행되는 명령어들. 개발 구현 지시서 3단계 "조사와 자료
    // 관리" 중 이번 패스에서 구현하는 범위는 help/report/ask 세 개뿐이고, 녹음·장비·기관 연락
    // 관련 명령은 그 시스템 자체가 없어서 의도적으로 제외했다(구현 계획.md Stage 3 참고).
    public class DialogueService : NativeRoutine
    {
        [Inject] private CaseSessionService _caseSessionService;

        // CLIPanel의 Tab 자동완성이 명령어 이름 접두어 매칭에 그대로 쓴다.
        public static readonly string[] Commands = { "help", "report", "ask" };

        // 전체 기획 정리.md 5장 "모든 대화는 자동으로 대화 로그 파일에 기록됩니다" - 질문마다
        // 새 라이브러리 항목을 만들지 않고 이 제목의 항목 하나에 계속 이어 붙인다
        // (CaseSessionService.AppendToLog 참고).
        private const string DialogueLogTitle = "대화 로그";

        // "ask " 뒤 키워드 자동완성 목록. CaseFileDefinition은 CaseSessionService가 이미 로드해둔
        // 것을 그대로 참조 - CLIPanel이 CaseFileDefinitionData 내부 구조를 몰라도 되게 여기서 감싼다.
        public IReadOnlyList<string> GetAskKeywords()
        {
            var script = _caseSessionService.CurrentDefinition?.dialogueScript;
            return script?.Select(e => e.keyword).ToList() ?? new List<string>();
        }

        // CLIPanel이 사용자가 Enter를 친 한 줄을 그대로 넘기면, 그 결과로 로그에 찍을 응답
        // 문자열을 돌려준다. ask 명령은 질문/답변을 대화 로그 라이브러리 항목 하나에 계속
        // 이어 붙인다(CaseSessionService.AppendToLog).
        public async UniTask<string> Execute(string rawInput)
        {
            var input = rawInput?.Trim() ?? string.Empty;
            if (input.Length == 0) return string.Empty;

            var spaceIndex = input.IndexOf(' ');
            var command = spaceIndex < 0 ? input : input.Substring(0, spaceIndex);
            var argument = spaceIndex < 0 ? string.Empty : input.Substring(spaceIndex + 1).Trim();

            switch (command)
            {
                case "help":
                    return "사용 가능한 명령어 - help: 도움말, report: 진단 보고서 다시 보기, ask <키워드>: 검사체에게 질문";

                case "report":
                    return await ExecuteReport();

                case "ask":
                    return await ExecuteAsk(argument);

                default:
                    return $"'{command}'는 알 수 없는 명령어입니다. help를 입력해 사용 가능한 명령어를 확인하세요.";
            }
        }

        private UniTask<string> ExecuteReport()
        {
            var library = _caseSessionService.CurrentCase?.Data.library;
            var reportEntry = library?.FirstOrDefault(e => e.type == CaseFileEntryType.Document);
            var result = reportEntry != null
                ? reportEntry.content
                : "등록된 진단 보고서가 없습니다.";
            return UniTask.FromResult(result);
        }

        private async UniTask<string> ExecuteAsk(string keyword)
        {
            // 사고(변이 강제 종료) 등으로 사례가 이미 Closed된 뒤에는 검사체가 더 이상
            // 대화 상대가 아니다 - 개발 구현 지시서 8장 "검사 중단 및 상황 종료"가 검사뿐
            // 아니라 업무 전체의 종료를 뜻하므로, ExamService의 변이 차단과 같은 전제로 ask도
            // 막는다(help/report는 종료 후에도 과거 기록 열람 용도로 남겨둠).
            if (_caseSessionService.CurrentCase?.Data.status == CaseStatus.Closed)
            {
                return "사례가 이미 종료되어 더 이상 검사체에게 질문할 수 없습니다.";
            }

            if (string.IsNullOrEmpty(keyword))
            {
                return "질문할 키워드를 입력하세요. 예: ask 기침";
            }

            var script = _caseSessionService.CurrentDefinition?.dialogueScript;
            var match = script?.FirstOrDefault(e => keyword.Contains(e.keyword) || e.keyword.Contains(keyword));

            var response = match != null
                ? match.response
                : "...그건 잘 모르겠어요.";

            // 사용자 요청(2026-07-16): 질문 후 바로 대답하지 않고 검사체가 "생각하는" 듯한
            // 텀을 준다. 질문 자체(CLIPanel의 "> ask ..." echo)는 이미 즉시 로그에 찍힌 뒤라
            // 이 지연은 답변만 늦춘다.
            await UniTask.Delay(TimeSpan.FromSeconds(4));

            var line = $"[{DateTime.Now:HH:mm:ss}] 질문: {keyword}\n답변: {response}";
            await _caseSessionService.AppendToLog(CaseFileEntryType.Dialogue, DialogueLogTitle, line);

            return response;
        }
    }
}
