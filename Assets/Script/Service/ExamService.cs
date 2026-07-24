using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cysharp.Threading.Tasks;
using Haare.Client.Routine;
using Script.Data;
using UnityEngine;
using VContainer;

namespace Script.Service
{
    // 개발 구현 지시서 6장 "4단계 - 핵심 검사 루프". 검사 부위/방식 조합마다 다른 결과를
    // 돌려주는 것 자체가 핵심 - "검사 결과가 하나의 정답 파일로 제공되는 게 아니라 서로 다른
    // 형태의 파일을 대조해 판단"(전체 기획 정리.md 9장) + "검사 결과는 정답을 직접 알려주지
    // 않는다"(개발 구현 지시서 6장).
    //
    // 기획 대조 문서(2026-07-16) 이후 - 초기엔 장기 5개/방식 3개로 축소했었는데, 사용자
    // 지시("의도적으로 축소한 부분 다시 전체로 늘려")로 전체 기획 정리.md 6장이 나열한 장기
    // 11종/검사 방식 8종 중 "문진"을 뺀 7종을 전부 반영했다 - 문진은 CLI의 'ask' 대화
    // 명령이 이미 그 역할을 담당하고 있어서 드롭다운에 중복 추가하지 않기로 사용자와 확인함
    // (같은 검사 개념을 UI 두 곳에 두면 혼란만 커짐).
    public class ExamService : NativeRoutine
    {
        [Inject] private CaseSessionService _caseSessionService;
        [Inject] private CaseTimeService _caseTimeService;
        [Inject] private HealthService _healthService;
        [Inject] private MutationService _mutationService;

        public static readonly string[] Organs =
        {
            "뇌", "심장", "폐", "간", "위", "대장", "신장", "안구", "피부", "근육", "신경계"
        };

        // 위험도 서열(관찰 < 음향 < 압력·진동 < 촬영·투과 < 전기 < 채취 < 극단적 자극)은
        // TimeCostMinutes/LoadingBaseSeconds/HealthService.BaseDamage/MutationService.
        // StimulationBase 네 테이블이 전부 공유하는 전제 - "극단적 자극 검사"는 전체 기획
        // 정리.md 6장 원문("발견 가능성은 높지만 상해·사망·변이 위험이 큽니다")대로 항상
        // 최고값.
        public static readonly string[] Methods =
        {
            "관찰 검사", "음향 검사", "압력·진동 검사", "촬영·투과 검사", "전기 검사", "채취 검사", "극단적 자극 검사"
        };

        // 개발 구현 지시서 7장 "5단계 - 검사 위험과 상태 변화": "검사 방식에 따른 시간 소요".
        // 체력 위험도(HealthService)와 마찬가지로 시스템 레벨 값이라 여기 정적으로 정의한다.
        private static readonly Dictionary<string, float> TimeCostMinutes = new Dictionary<string, float>
        {
            { "관찰 검사", 15f },
            { "음향 검사", 25f },
            { "압력·진동 검사", 30f },
            { "촬영·투과 검사", 40f },
            { "전기 검사", 45f },
            { "채취 검사", 50f },
            { "극단적 자극 검사", 60f }
        };

        // C:\Users\songs\Documents\GitHub\BioSearch(같은 Haare 벤더링을 쓰는 별개 프로젝트)의
        // ScanCommandManager와 같은 논리 - "스캔"(그 프로젝트) / "검사 실행"(이 프로젝트) 둘 다
        // 파라미터 기반 실시간 로딩을 UI에 보여준다. 그 프로젝트는 폴더 크기(itemCount)로 소요
        // 시간을 정했지만, 여기는 사용자 요청대로 "고강도, 위험한 종류의 검사일수록 오래
        // 걸리게" - 방식 위험도(HealthService/MutationService와 같은 서열: 관찰 < 음향 <
        // 촬영·투과) × 강도 배율을 곱해 초 단위 로딩 시간을 만든다. ExamControlPanel이 검사
        // 실행 버튼을 채우는 로딩 연출(RunLoadingAsync)에 필요한 시간을 여기서 계산해 넘겨준다
        // - 위험도 서열 자체는 시스템 값이라 UI 쪽이 아니라 여기(ExamService)에 정의.
        // 사용자 피드백(2026-07-16): "좀더 오래 걸렸으면 좋겠어" - 최초 값(1.0/2.0/3.0)이
        // 체감상 너무 짧아 약 3배로 늘림.
        private static readonly Dictionary<string, float> LoadingBaseSeconds = new Dictionary<string, float>
        {
            { "관찰 검사", 3.0f },
            { "음향 검사", 6.0f },
            { "압력·진동 검사", 7.5f },
            { "촬영·투과 검사", 10.0f },
            { "전기 검사", 12.0f },
            { "채취 검사", 14.0f },
            { "극단적 자극 검사", 18.0f }
        };

        private static readonly Dictionary<string, float> LoadingIntensityMultiplier = new Dictionary<string, float>
        {
            { "약", 0.6f },
            { "중", 1f },
            { "강", 1.6f }
        };

        public static float GetLoadingSeconds(string method, string intensity)
        {
            var baseSeconds = LoadingBaseSeconds.TryGetValue(method, out var b) ? b : 1.5f;
            var multiplier = LoadingIntensityMultiplier.TryGetValue(intensity, out var m) ? m : 1f;
            return baseSeconds * multiplier;
        }

        // 확장 기획(자료 생성계 정규화, 2026-07-24) - 검사 방식 -> 생성할 원자료 타입 매핑.
        // HealthService.BaseDamage/MutationService.StimulationBase와 같은 강도 배율 관례를
        // 그대로 따른다(약 0.5 / 중 1 / 강 2). 원래는 "음향 검사"→Audio, "극단적 자극 검사"→
        // Audio+Numeric 두 항목이었는데, 사용자 지시(2026-07-24) "수치그래프와 음성을 따로
        // 두지 말고 하나로 합쳐"에 따라 Audio 타입 자체를 없애고 전부 Numeric으로 통합했다
        // (확장 기획 문서 2.3.2). 방식마다 항목을 하나만 만들면 되므로 타입 배열이 아니라
        // 단일 타입 매핑으로 단순화 - "촬영·투과 검사"만 여전히 Image로 남는다(음성/수치와는
        // 다른 별개의 자료 형태라 이번 병합 대상이 아님).
        private static readonly Dictionary<string, CaseFileEntryType> RawDataTypes =
            new Dictionary<string, CaseFileEntryType>
            {
                { "관찰 검사", CaseFileEntryType.Numeric },
                { "음향 검사", CaseFileEntryType.Numeric },
                { "압력·진동 검사", CaseFileEntryType.Numeric },
                { "촬영·투과 검사", CaseFileEntryType.Image },
                { "전기 검사", CaseFileEntryType.Numeric },
                { "채취 검사", CaseFileEntryType.Numeric },
                { "극단적 자극 검사", CaseFileEntryType.Numeric }
            };

        // 방식이 Numeric으로 통합됐어도, 원래 "음성"이었던 방식(음향 검사/극단적 자극 검사)은
        // 여전히 파형+주파수 요약 태그를 담아야 자료 형태의 의미가 유지된다 - 타입이 아니라
        // 방식(method) 기준으로 태그 생성 방식을 고른다.
        private static readonly HashSet<string> AudioLikeMethods = new HashSet<string>
        {
            "음향 검사", "극단적 자극 검사"
        };

        private static readonly Dictionary<string, float> RawDataIntensityMultiplier = new Dictionary<string, float>
        {
            { "약", 0.5f },
            { "중", 1f },
            { "강", 2f }
        };

        private const int SeriesLength = 8;

        // 결과는 CaseFileEntryType.ExamResult(검사 결과 전용 최상위 폴더)로 라이브러리에
        // 등록한다 - 처음엔 전체 기획 정리.md 9장의 "문서 파일" 분류에 "검사 결과 보고서"가
        // 있다고 보고 Document + subfolder(장기별)로 묶었는데, 사용자가 "문서에 넣지 말고
        // 검사 결과 폴더를 따로 만들어달라"고 명시적으로 요청해서 최상위 폴더 자체를 분리했다
        // (CaseFile.cs의 CaseFileEntryType 주석 참고). 이 텍스트 판독 소견과 별개로, 검사
        // 방식에 맞는 원자료(Numeric/Image)도 GenerateRawDataEntries()가 추가로 만든다
        // (확장 기획 문서 파트 A, 2026-07-24 - 예전엔 이 타입들이 콘텐츠 없이 타입만 있어서
        // LibraryPanel에서 "아직 열람 지원 안 함"으로 막혀 있었다).
        // 장기(organ)는 계속 subfolder로 넘겨서 "검사 결과/폐/관찰 검사 결과.txt"처럼 장기별로
        // 묶는다.
        //
        // 강도(intensity)는 파일명에 대괄호로 붙인다(예: "관찰 검사 [중] 결과") - 같은 부위·
        // 방식을 강도만 바꿔 재검사하면 파일이 구분 안 되고 겹쳐버리는 걸 막는 것과, 사용자
        // 요청("어떤 강도였는지 잘 표기해줘")을 둘 다 만족. 부위는 폴더 경로가 이미 보여주므로
        // 파일명에는 안 넣지만, 파일을 열었을 때(NotepadPopup)는 폴더 밖 맥락 없이도 바로
        // 알 수 있도록 본문 맨 위에 부위/방식/강도를 전부 다시 명시한다 - 강도는 문서(결과
        // 텍스트) 자체는 안 바꾸지만(그건 콘텐츠 영역), 아래에서 HealthService.ApplyExamRisk로
        // 넘어가 실제 위험도에는 반영된다(5단계).
        public async UniTask<string> RunExam(string organ, string method, string intensity)
        {
            // 사용자 지시(2026-07-20): "검사 판정 성공이나 실패 후 더이상 검사 불가능 하게 해" -
            // FinalReportPanel.Submit()이 이미 CaseSessionService.CompleteCase()로 사례 상태를
            // Closed로 바꿔두므로, 여기서도 같은 상태를 확인해서 제출 이후의 추가 검사를 막는다
            // (정답/오답 여부와 무관하게 "제출했다"는 사실 자체가 기준 - BuildResultText가
            // 판정 성공/실패를 나누는 건 순전히 결과 문구 표시용이고, 사례 종료 여부는 하나뿐).
            if (_caseSessionService?.CurrentCase?.Data.status == CaseStatus.Closed)
            {
                _caseSessionService.Log("[검사 불가] 최종 보고서가 이미 제출되어 사례가 종료되었습니다.");
                return "이미 최종 보고서가 제출되어 검사를 실행할 수 없습니다.";
            }

            // 업무 시간이라는 자원은 소진되면 물리적으로 더 못 하는 게 자연스러워, 체력과 달리
            // 여기서 검사 자체를 막는다(구현현황 문서 Stage 5 계획 "범위 확정" 참고). 라이브러리
            // 등록도 하지 않는다 - 실행되지 않은 검사이므로.
            if (_caseTimeService != null && _caseTimeService.IsExpired)
            {
                _caseSessionService.Log("[검사 불가] 업무 시간이 소진되어 더 이상 검사를 진행할 수 없습니다.");
                return "업무 시간이 소진되어 검사를 실행할 수 없습니다.";
            }

            // 변이라는 위기 상황에서 태연히 다음 검사를 실행할 순 없다는 전제 - 비상 대응
            // 전까지는 검사 자체를 막는다(개발 구현 지시서 8장 "검사 중단 및 상황 종료").
            if (_mutationService != null && _mutationService.IsMutated.CurrentValue)
            {
                _caseSessionService.Log("[검사 불가] 검사체가 변이한 상태입니다. 먼저 비상 상황에 대응하십시오.");
                return "검사체가 변이한 상태라 검사를 실행할 수 없습니다.";
            }

            var result = FindResult(organ, method);
            var title = $"{method} [{intensity}] 결과";
            var content = $"[검사 부위] {organ}\n[검사 방식] {method}\n[검사 강도] {intensity}\n\n{result}";

            await _caseSessionService.AddLibraryEntry(CaseFileEntryType.ExamResult, title, content, organ);
            await GenerateRawDataEntries(organ, method, intensity);

            var timeCost = TimeCostMinutes.TryGetValue(method, out var cost) ? cost : 20f;
            _caseTimeService?.ConsumeMinutes(timeCost);
            _healthService?.ApplyExamRisk(method, intensity);
            _mutationService?.ApplyExamStimulation(organ, method, intensity);

            _caseSessionService.Log($"[검사 완료] {organ} - {method} (강도: {intensity}) - 라이브러리에 등록됨");

            return result;
        }

        private string FindResult(string organ, string method)
        {
            var results = _caseSessionService.CurrentDefinition?.examResults;
            var match = results?.FirstOrDefault(e => e.organ == organ && e.method == method);
            return match != null ? match.result : "특이 소견 없음. 정상 범위 내.";
        }

        // 확장 기획(자료 생성계 정규화, 2026-07-24) - ExamResult(판독 소견 텍스트)와 별개로,
        // 검사 방식에 맞는 형태(Numeric/Image)의 원자료를 라이브러리에 추가로 등록한다.
        // "서로 다른 형태의 파일을 대조"(전체 기획 정리.md 9장)하는 경험을 위한 것 - 방식이
        // 매핑돼 있지 않으면(RawDataTypes에 없는 방식) 아무것도 만들지 않는다.
        private async UniTask GenerateRawDataEntries(string organ, string method, string intensity)
        {
            if (!RawDataTypes.TryGetValue(method, out var type)) return;

            var baseline = FindBaseline(organ, method);
            var intensityScale = RawDataIntensityMultiplier.TryGetValue(intensity, out var m) ? m : 1f;

            var seed = $"{organ}|{method}|{intensity}|{type}";
            var series = BuildSeries(seed, baseline, intensityScale);
            var tags = type == CaseFileEntryType.Image
                ? BuildImageSummaryTags(series)
                : AudioLikeMethods.Contains(method)
                    ? BuildAudioSummaryTags(series)
                    : Array.Empty<string>();

            var title = $"{method} [{intensity}] {RawDataLabel(type)}";
            var content = BuildRawDataContent(organ, method, intensity, type, series, tags);

            await _caseSessionService.AddLibraryEntry(type, title, content, organ,
                seriesData: series, summaryTags: tags);
        }

        private float[] FindBaseline(string organ, string method)
        {
            var results = _caseSessionService.CurrentDefinition?.examResults;
            var match = results?.FirstOrDefault(e => e.organ == organ && e.method == method);
            return match?.seriesBaseline != null && match.seriesBaseline.Length > 0 ? match.seriesBaseline : null;
        }

        // baseline이 있으면(저작자가 정한 값) 강도 배율만 곱해 그대로 쓴다 - 그래프 "모양"
        // 자체가 판정 근거이므로 실행마다 랜덤하게 바뀌면 안 된다(확장 기획 문서 2.6). 없으면
        // 결정론적 노이즈(seed 고정)로 채운 평탄한 기준선을 대신 사용한다 - 같은 조합을 다시
        // 검사해도 항상 같은 그래프가 나온다.
        private static float[] BuildSeries(string seed, float[] baseline, float intensityScale)
        {
            var series = new float[SeriesLength];
            for (var i = 0; i < SeriesLength; i++)
            {
                var baseValue = baseline != null
                    ? baseline[i % baseline.Length]
                    : 20f + DeterministicNoise(seed, i) * 6f;
                series[i] = baseValue * intensityScale;
            }

            return series;
        }

        // GetHashCode()는 프로세스마다 값이 달라질 수 있어(문자열 해시 랜덤화) 쓰지 않는다 -
        // FNV-1a로 직접 계산해 같은 seed는 항상 같은 값을 내도록 보장한다.
        private static float DeterministicNoise(string seed, int index)
        {
            unchecked
            {
                var hash = 2166136261u;
                var text = seed + index;
                foreach (var c in text)
                {
                    hash ^= c;
                    hash *= 16777619u;
                }

                return (hash % 1000u) / 1000f;
            }
        }

        // 순수 ASCII 문자만 사용한다 - 특수 유니코드 기호가 SDF 폰트 아틀라스 한계로 깨졌던
        // 전례(기획 대조 문서 16~17장) 때문에, 그래프 표현도 '#'/'-' 조합으로만 그린다.
        private static string BuildSeriesText(float[] series)
        {
            var sb = new StringBuilder();
            var max = 1f;
            foreach (var v in series) max = Mathf.Max(max, v);

            for (var i = 0; i < series.Length; i++)
            {
                var ratio = Mathf.Clamp01(series[i] / max);
                var filled = Mathf.RoundToInt(ratio * 10);
                var bar = new string('#', filled) + new string('-', 10 - filled);
                sb.AppendLine($"t{i:00} [{bar}] {series[i]:0.0}");
            }

            return sb.ToString();
        }

        // 음향 검사의 "파형+주파수 요약" 통합 표현(2.3.1) - 실제 스펙트럼 분석 없이 시계열
        // 통계(평균/변동폭)에서 대역·이상 여부를 결정론적으로 도출한다.
        private static string[] BuildAudioSummaryTags(float[] series)
        {
            var avg = series.Average();
            var band = avg < 15f ? "저주파 우세" : avg < 30f ? "중대역 우세" : "고주파 우세";
            var variance = series.Max() - series.Min();
            var anomaly = variance > 15f ? "비정상 변동/복수 음성 의심" : "안정적 파형";
            return new[] { band, anomaly };
        }

        private static string[] BuildImageSummaryTags(float[] series)
        {
            var variance = series.Max() - series.Min();
            var densityTag = variance > 20f
                ? "밀도 뚜렷한 이물질 음영 감지"
                : variance > 8f
                    ? "밀도 경미한 편차"
                    : "밀도 이상 없음";
            return new[] { densityTag };
        }

        private static string BuildRawDataContent(string organ, string method, string intensity,
            CaseFileEntryType type, float[] series, string[] tags)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[검사 부위] {organ}");
            sb.AppendLine($"[검사 방식] {method}");
            sb.AppendLine($"[검사 강도] {intensity}");
            sb.AppendLine();
            sb.AppendLine($"[{RawDataLabel(type)}]");
            sb.Append(BuildSeriesText(series));

            if (tags.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"[요약] {string.Join(", ", tags)}");
            }

            return sb.ToString();
        }

        private static string RawDataLabel(CaseFileEntryType type)
        {
            switch (type)
            {
                case CaseFileEntryType.Image: return "촬영 판독 카드";
                case CaseFileEntryType.Numeric: return "수치·파형 그래프";
                default: return type.ToString();
            }
        }
    }
}
