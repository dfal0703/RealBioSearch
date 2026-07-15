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

            // 대기 상태로 로드됐다면(최초 접수 직후) 여기서 바로 업무 활성화 처리.
            if (CurrentCase.Data.status == CaseStatus.Waiting)
            {
                CurrentCase.Data.status = CaseStatus.InProgress;
                await _dataManager.SaveData<CaseFile, CaseFileData>(CurrentCase, CurrentCase.Data);
            }

            var message = $"검사 요청 수신 - 사례 {CurrentCase.Data.caseId} ({CurrentCase.Data.subjectName})";
            LogHelper.Log(LogHelper.SERVICE, message);
            OnLog.OnNext(message);

            await base.Initialize(cts);
        }

        // 최종 보고서 제출 처리. 서비스 쪽 상태 전환/저장만 갖춰두고, 실제 제출 버튼 UI 연결은
        // FinalReportPanel이 아직 플레이스홀더라 다음 기능 단위로 미룬다.
        public async UniTask CompleteCase(string verdictDraft)
        {
            if (CurrentCase == null) return;

            CurrentCase.Data.finalVerdictDraft = verdictDraft;
            CurrentCase.Data.status = CaseStatus.Closed;

            await _dataManager.SaveData<CaseFile, CaseFileData>(CurrentCase, CurrentCase.Data);

            var message = $"사례 {CurrentCase.Data.caseId} 종료 및 저장 완료";
            LogHelper.Log(LogHelper.SERVICE, message);
            OnLog.OnNext(message);
        }
    }
}
