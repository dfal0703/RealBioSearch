using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Haare.Client.Routine;
using Haare.Scripts.Client.Data;
using Haare.Util.Logger;
using R3;
using Script.Data;
using VContainer;

namespace Script.Service
{
    // 검사체 한 명의 접수→진행→종료 생명주기(개발 구현 지시서 2단계). NativeRoutine이라
    // 인스펙터 배선이 없고, VContainer가 물어봐줘야 실제로 생성된다 - BioSearchUIPresenter가
    // [Inject] 필드로 이 타입을 물고 있는 게 그 "즉시 생성 강제" 트리거다(GamePresenter가
    // SceneService/DataManager를 물고 있는 것과 같은 방식).
    public class CaseSessionService : NativeRoutine
    {
        [Inject] private DataManager _dataManager;

        public CaseFile CurrentCase { get; private set; }
        public CaseFileDefinitionData CurrentDefinition { get; private set; }

        // CLIPanel 등 UI 쪽에서 부팅 로그를 화면에도 반영할 수 있도록 발행한다. Subject가 아니라
        // ReplaySubject인 이유: CaseSessionService는 부팅 시퀀스 초반(패널 로딩 전)에 이미
        // "검사 요청 수신" 한 줄을 쏴버리는데, CLIPanel은 Addressable 로드+주입을 거쳐 훨씬
        // 나중에(맨 마지막 패널로) 구독을 시작한다 - 평범한 Subject는 지난 값을 재생 안 해줘서
        // 그 한 줄을 영영 놓친다(실제로 이 문제로 화면에 아무것도 안 떴었음). ReplaySubject는
        // 새 구독자에게 지금까지 쌓인 값을 전부 재생해준다.
        public ReplaySubject<string> OnLog { get; } = new ReplaySubject<string>();

        // LibraryPanel이 부팅 순서상 CaseSessionService보다 늦게 구독을 시작하므로(진단 보고서
        // 자동 등록이 Initialize 중에 바로 일어남) OnLog와 같은 이유로 Subject가 아니라
        // ReplaySubject를 쓴다 - 그래야 늦게 붙는 구독자도 이미 쌓인 자료를 전부 받는다.
        public ReplaySubject<CaseFileEntry> OnLibraryUpdated { get; } = new ReplaySubject<CaseFileEntry>();

        public override async UniTask Initialize(CancellationToken cts)
        {
            CurrentCase = await _dataManager.GetModel<CaseFile>();
            CurrentDefinition = await CaseFileDefinition.LoadAsync();

            if (CurrentCase == null)
            {
                LogHelper.Error(LogHelper.SERVICE, "CaseFile 로드 실패 - 사례를 시작할 수 없음");
                await base.Initialize(cts);
                return;
            }

            // 대기 상태로 로드됐다면(최초 접수 직후) 여기서 바로 업무 활성화 + 진단 보고서를
            // 라이브러리에 자동 등록한다. status가 이미 InProgress/Closed로 저장돼 있다면
            // (재접속) 이 블록을 다시 안 타므로 보고서가 중복 등록되지 않는다.
            if (CurrentCase.Data.status == CaseStatus.Waiting)
            {
                CurrentCase.Data.status = CaseStatus.InProgress;
                await AddLibraryEntry(CaseFileEntryType.Document, "진단 접수 보고서", BuildDiagnosisReport());

                // 사용자 요청(2026-07-16): "튜토리얼 텍스트 파일 문서를 라이브러리에 하나
                // 띄워주고, 튜토리얼 챕터 시에만 이 문서를 강조해줘." 이 사례가 튜토리얼일
                // 때만(isTutorial) 등록하고 강조 플래그를 켠다 - 나중에 튜토리얼이 아닌 사례가
                // 추가되면 정의 데이터의 isTutorial만 false로 두면 이 블록 자체를 안 탄다.
                if (CurrentDefinition?.isTutorial == true)
                {
                    await AddLibraryEntry(CaseFileEntryType.Document, "튜토리얼 안내", BuildTutorialGuideText(),
                        highlight: true);
                }
            }

            Log($"검사 요청 수신 - 사례 {CurrentCase.Data.caseId} ({CurrentCase.Data.subjectName})");

            await base.Initialize(cts);
        }

        // CLIPanel 등 UI 쪽 시스템 메시지 로그에 한 줄 남기는 공통 진입점 - OnLog를 여기서만
        // 발행하게 묶어서, 다른 서비스(ExamService 등)가 CaseSessionService 내부 상태를 직접
        // 안 건드리고도 시스템 메시지를 남길 수 있게 한다.
        public void Log(string message)
        {
            LogHelper.Log(LogHelper.SERVICE, message);
            OnLog.OnNext(message);
        }

        private string BuildDiagnosisReport()
        {
            var d = CurrentCase.Data;
            return $"사례번호: {d.caseId}\n" +
                   $"이름: {d.subjectName} ({d.subjectGender}, {d.subjectAge}세)\n" +
                   $"신분: {d.subjectIdentity} / 직업: {d.subjectOccupation}\n" +
                   $"요청 경위: {d.requestBackground}\n" +
                   $"기존 질환: {d.existingConditions}\n" +
                   $"신고된 증상: {d.reportedSymptoms}\n" +
                   $"최근 행동 이상: {d.recentBehaviorAnomalies}";
        }

        // TutorialGuideService가 상황별로 CLI에 흘려주는 짧은 힌트와 달리, 여기 이 문서는
        // 라이브러리에 항상 남아있는 참고용 요약본이다 - 두 안내가 겹치는 건 의도된 중복
        // (하나는 흘러가는 로그, 하나는 언제든 다시 열어볼 수 있는 문서).
        private static string BuildTutorialGuideText()
        {
            // "■"도 폰트 애셋에 글리프가 없어 깨지는 부류라("▾"/"★"/"⚠"/"→"와 같은 문제)
            // 대괄호 표기로 대체.
            return "[바이오서치 업무 안내]\n\n" +
                   "1. CLI에 'report'를 입력해 진단 보고서를 다시 확인할 수 있습니다.\n" +
                   "2. CLI에 'ask <키워드>'를 입력해 검사체와 대화하고 의심 부위를 추론하세요.\n" +
                   "3. 중앙 패널에서 검사 부위 · 방식 · 강도를 선택해 검사를 실행하세요.\n" +
                   "4. 검사 결과는 우측 상단 라이브러리 '검사 결과' 폴더에서 확인할 수 있습니다.\n" +
                   "5. 근거가 충분하다고 판단되면 좌측 하단에서 감염 여부와 부위를 판정해 최종 보고서를 제출하세요.\n" +
                   "6. 검사체가 변이하면 계기판의 비상 버튼으로 즉시 대응하세요.";
        }

        // 사례 라이브러리를 바꾸는 유일한 진입점(개발 구현 지시서 3단계: "자료는 사례 라이브러리에
        // 일관되게 누적"). DialogueService(ask 명령)와 위의 진단 보고서 자동 등록이 모두 이걸 통해서만
        // library를 건드린다.
        //
        // 의도적으로 로컬 저장(_dataManager.SaveData)을 안 한다 - 아직 튜토리얼 사례 하나로
        // 반복 테스트하는 단계라, 세션마다 쌓인 질문/답변이 Application.persistentDataPath의
        // CaseFile.json에 영구히 누적되면서 다음 플레이 때도 이전 테스트 내용이 그대로 남는
        // 문제가 있었다(사용자 피드백: "라이브러리가 일회용 데이터가 되게 해줘"). 라이브러리는
        // 이 세션(CurrentCase.Data, 메모리)에만 존재하고 에디터를 다시 플레이하면 사라진다 -
        // 실제 저장이 필요해지면(플레이어블 빌드 단계) 여기 SaveData 호출을 되살리면 된다.
        public UniTask AddLibraryEntry(CaseFileEntryType type, string title, string content, string subfolder = "",
            bool highlight = false)
        {
            if (CurrentCase == null) return UniTask.CompletedTask;

            var entry = new CaseFileEntry
            {
                id = Guid.NewGuid().ToString("N"),
                type = type,
                title = title,
                content = content,
                timestamp = DateTime.Now.ToString("HH:mm:ss"),
                subfolder = subfolder ?? "",
                isHighlighted = highlight
            };

            CurrentCase.Data.library.Add(entry);
            OnLibraryUpdated.OnNext(entry);

            return UniTask.CompletedTask;
        }

        // 전체 기획 정리.md 5장: "모든 대화는 자동으로 대화 로그 파일에 기록됩니다" - 대화 한
        // 번마다 새 라이브러리 항목을 만드는 게 아니라, (type, title)이 같은 기존 항목이 있으면
        // 그 항목의 content 뒤에 이어 붙인다. 그래서 "대화 로그" 항목은 사례당 하나만 존재하고,
        // 처음 대화가 시작될 때(=아직 그 항목이 없을 때) 비로소 만들어진다 - 라이브러리 UI에서
        // 그 타입의 "폴더"가 그 시점에 처음 나타나는 것도 이걸로 자연히 설명됨(폴더 = 항목이
        // 하나라도 있는 타입).
        public UniTask AppendToLog(CaseFileEntryType type, string title, string line, string subfolder = "")
        {
            if (CurrentCase == null) return UniTask.CompletedTask;

            subfolder ??= "";
            var existing = CurrentCase.Data.library
                .FirstOrDefault(e => e.type == type && e.title == title && e.subfolder == subfolder);
            if (existing == null)
            {
                return AddLibraryEntry(type, title, line, subfolder);
            }

            existing.content = $"{existing.content}\n{line}";
            existing.timestamp = DateTime.Now.ToString("HH:mm:ss");

            // 같은 id로 다시 발행 - 구독자(LibraryPanel)는 이미 아는 항목이면 목록에 새로 추가하지
            // 않고 참조가 갱신됐다는 신호로만 받아들여 다시 그린다. (저장은 위 AddLibraryEntry와
            // 같은 이유로 의도적으로 안 함 - "일회용 데이터" 참고)
            OnLibraryUpdated.OnNext(existing);

            return UniTask.CompletedTask;
        }

        // 최종 보고서 제출 처리. 서비스 쪽 상태 전환만 갖춰두고, 실제 제출 버튼 UI 연결은
        // FinalReportPanel이 아직 플레이스홀더라 다음 기능 단위로 미룬다. 저장은 다른 라이브러리
        // 메서드와 같은 이유로 의도적으로 안 함(위 AddLibraryEntry 주석 참고) - 안 그러면 한 번
        // 테스트로 "Closed"가 영구히 저장돼서 다음 플레이 때 재시작이 아니라 이미 끝난 사례로
        // 시작해버린다.
        public UniTask CompleteCase(string verdictDraft)
        {
            if (CurrentCase == null) return UniTask.CompletedTask;

            CurrentCase.Data.finalVerdictDraft = verdictDraft;
            CurrentCase.Data.status = CaseStatus.Closed;

            Log($"사례 {CurrentCase.Data.caseId} 종료 완료");

            return UniTask.CompletedTask;
        }
    }
}
