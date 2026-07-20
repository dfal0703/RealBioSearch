Shader "BioSearch/CRTPost"
{
    Properties
    {
        // ── 기본 CRT 파라미터 ───────────────────────────────────────────────
        _ScanlineIntensity ("Scanline Intensity", Range(0,2)) = 1.0
        _ScanlineCount     ("Scanlines per Screen Height", Range(100,2000)) = 900
        _MaskStrength      ("RGB Mask Strength", Range(0,1)) = 0.35
        _MaskPixelSize     ("RGB Triad Pixel Size", Range(1,4)) = 1.0
        _Curvature         ("Barrel Curvature", Range(0,1)) = 0.25
        _CornerRadius      ("Corner Roundness", Range(0,1)) = 0.08
        _Vignette          ("Vignette Strength", Range(0,2)) = 0.75
        _Aberration        ("Chromatic Aberration", Range(0,3)) = 0.6
        _Gamma             ("Gamma", Range(0.5,3)) = 1.2

        // ── 가끔 지지직(버스트) ─────────────────────────────────────────────
        _JitterBase        ("Base Jitter",   Range(0,2))   = 0.02
        _JitterBurst       ("Burst Jitter",  Range(0,3))   = 0.18
        _FlickerBase       ("Base Flicker",  Range(0,0.2)) = 0.005
        _FlickerBurst      ("Burst Flicker", Range(0,0.3)) = 0.06
        _BurstLen          ("Glitch Burst Length (s)", Range(0.05,0.35)) = 0.14
        _BurstChance       ("Burst Chance per Segment", Range(0,0.5)) = 0.08
        _Seed              ("Glitch Seed", Range(0,100)) = 13
        _TimeScale         ("Flicker/Jitter Speed (Hz)", Range(0,5)) = 1.2

        // ── 롤링 밴드(Hum Bar) ─────────────────────────────────────────────
        _BandStrength      ("Hum Bar Strength", Range(0,1)) = 0.25
        _BandThickness     ("Hum Bar Thickness", Range(0,0.5)) = 0.10
        _BandSpeed         ("Hum Bar Speed (-=up, +=down)", Range(-2,2)) = 0.20

        // ── 미세 픽셀 깨짐(고주파 마이크로 지터) ───────────────────────────
        _MicroJitterAmp    ("Micro Jitter Amp (px)", Range(0,1))   = 0.25
        _MicroJitterFreq   ("Micro Jitter Freq (Hz)", Range(0,120)) = 45
        _MicroJitterDuty   ("Micro Jitter Duty", Range(0,1))        = 0.35
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "CRTPost"
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex   Vert
            #pragma fragment Frag

            // URP core + fullscreen triangle
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            // ⚠ _BlitTexture / sampler_LinearClamp 선언 금지 (URP가 제공)

            // ── Params ───────────────────────────────────────────────────────
            float _ScanlineIntensity, _MaskStrength, _Curvature, _CornerRadius, _Vignette, _Aberration, _Gamma;
            float _ScanlineCount, _MaskPixelSize;

            float _JitterBase, _JitterBurst, _FlickerBase, _FlickerBurst;
            float _BurstLen, _BurstChance, _Seed, _TimeScale;

            float _BandStrength, _BandThickness, _BandSpeed;

            float _MicroJitterAmp, _MicroJitterFreq, _MicroJitterDuty;

            // ── VS structs ───────────────────────────────────────────────────
            struct CRTAttrib   { uint vertexID : SV_VertexID; };
            struct CRTVaryings { float4 positionHCS: SV_POSITION; float2 uv: TEXCOORD0; float2 uvNdc: TEXCOORD1; };

            CRTVaryings Vert (CRTAttrib v)
            {
                CRTVaryings o;
                o.positionHCS = GetFullScreenTriangleVertexPosition(v.vertexID);
                o.uv          = GetFullScreenTriangleTexCoord(v.vertexID);
                o.uvNdc       = o.uv * 2.0 - 1.0;
                return o;
            }

            // ── Helpers ──────────────────────────────────────────────────────
            static const float TAU = 6.2831853;
            float2 SrcTexelSize() { return 1.0 / _ScreenParams.xy; }
            float4 SampleSrc(float2 uv) { return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv); }

            float Hash11(float x) { return frac(sin(x * 12.9898 + 78.233) * 43758.5453); }

            // 확률적 버스트 게이트(세그먼트별 on/off)
            float BurstEnvelope(float t)
            {
                float segLen = max(_BurstLen, 1e-3);
                float segId  = floor(t / segLen);
                float h      = Hash11(segId + _Seed);                 // 0~1
                float inBurst= step(1.0 - saturate(_BurstChance), h); // 확률 on

                float ph   = frac(t / segLen);                        // 0~1
                float env  = smoothstep(0.00, 0.25, ph) * (1.0 - smoothstep(0.75, 1.00, ph));
                float amp  = lerp(0.8, 1.2, Hash11(segId * 3.17 + _Seed * 1.11));
                return inBurst * env * amp; // 0~1.2
            }

            float2 BarrelDistort(float2 uv, float k)
            {
                float2 cc = uv * 2.0 - 1.0;
                float r2 = dot(cc, cc);
                cc *= (1.0 + k * r2);
                return saturate((cc + 1.0) * 0.5);
            }

            float CornerMask(float2 uvNdc, float corner)
            {
                float2 a = abs(uvNdc);
                float m = max(a.x, a.y);
                float edge = smoothstep(1.0 - corner, 1.0, m);
                return 1.0 - edge;
            }

            // 픽셀 고정 RGB triad (해상도/스케일 안정)
            float3 ApertureMask(float2 uv, float strength)
            {
                float px = floor(uv.x * (_ScreenParams.x / max(_MaskPixelSize, 1.0)));
                float stripe = fmod(px, 3.0);
                float3 mask = (stripe < 0.5) ? float3(1.0, 0.45, 0.45) :
                              (stripe < 1.5) ? float3(0.45, 1.0, 0.45) :
                                               float3(0.45, 0.45, 1.0);
                return lerp(float3(1.0,1.0,1.0), mask, saturate(strength));
            }

            float Scanline(float2 uv, float lines, float intensity)
            {
                float s = 0.5 + 0.5 * cos(uv.y * lines * 3.14159265);
                return lerp(1.0, s, intensity);
            }

            float3 SampleCA(float2 uv, float amt)
            {
                float2 d = (uv - 0.5) * SrcTexelSize() * (120.0 * amt);
                float3 c;
                c.r = SampleSrc(uv + d).r;
                c.g = SampleSrc(uv).g;
                c.b = SampleSrc(uv - d).b;
                return c;
            }

            // ── 지터: 저주파(전체) + 고주파(행 기반 미세) 동시 적용 ───────────
            float2 JitteredUV(float2 uv, float t)
            {
                // 1) 저주파: 화면 전체 덜컥(버스트로 세기↑)
                float burst  = BurstEnvelope(t);
                float ampL   = _JitterBase + _JitterBurst * burst;
                float freqL  = max(_TimeScale, 1e-4);
                float baseWave = sin(t * TAU * freqL);
                float wobble   = baseWave * (0.6 + 0.4 * sin(uv.y * 40.0 + burst * 3.14));
                uv.x += wobble * ampL * SrcTexelSize().x * 60.0;

                // 2) 고주파: 행(스캔라인) 기반 미세 지터(픽셀 깨짐 느낌)
                float row    = floor(uv.y * _ScreenParams.y);
                float ph     = frac(t * max(_MicroJitterFreq, 1e-3));     // 0~1
                float rowRnd = Hash11(row * 1.73 + _Seed);                // 행마다 상이
                float tri    = abs(frac(ph + rowRnd) - 0.5) * 2.0;        // 0~1 (triangle)
                float sign   = (fmod(row, 2.0) < 0.5) ? -1.0 : 1.0;       // 짝/홀행 반전

                // 가끔만 켜지도록 시간 세그먼트 게이트
                float seg    = floor(t * 4.0);                            // 0.25s 단위
                float gate   = step(1.0 - _MicroJitterDuty, Hash11(seg + _Seed * 5.0));

                // 픽셀 단위 진폭 → 텍셀 크기와 곱해 서브픽셀 이동
                float microPx = (tri - 0.5) * sign * _MicroJitterAmp * gate;
                uv.x += microPx * SrcTexelSize().x;

                return uv;
            }

            // 롤링 밴드(Hum Bar): 아래/위로 흐르는 밝기·채널 변화
            void ApplyHumBar(inout float3 col, float2 uv, float t)
            {
                float sp = _BandSpeed;
                float by = frac(t * abs(sp));              // 0~1 내려가는 기준
                by = (sp >= 0.0) ? by : (1.0 - by);        // speed<0면 위로

                float dist = abs(uv.y - by);
                float m = 1.0 - smoothstep(_BandThickness*0.5, _BandThickness, dist);

                float2 texel = SrcTexelSize();
                float shift = 1.25 * texel.x;              // 아주 작은 수평 시프트
                float3 shifted;
                shifted.r = SampleSrc(uv + float2( shift, 0)).r;
                shifted.g = col.g;
                shifted.b = SampleSrc(uv + float2(-shift, 0)).b;

                col = lerp(col, shifted * 1.08, m * _BandStrength);
            }

            float4 Frag (CRTVaryings i) : SV_Target
            {
                float t  = _TimeParameters.x;

                // 렌즈 왜곡
                float2 uv = BarrelDistort(i.uv, _Curvature);

                // CRT 룩
                float2 uvJ = JitteredUV(uv, t);
                float3 col = SampleCA(uvJ, _Aberration);
                col *= Scanline(uvJ, _ScanlineCount, _ScanlineIntensity) * ApertureMask(uvJ, _MaskStrength);

                // 플리커(밝기 요동): 버스트에만 강해짐
                float burst  = BurstEnvelope(t);
                float freq   = max(_TimeScale, 1e-4);
                float baseFlick =
                      0.63 * sin(t * TAU * (1.00 * freq) + 0.1)
                    + 0.27 * sin(t * TAU * (2.17 * freq) + 1.3)
                    + 0.10 * sin(t * TAU * (3.73 * freq) + 2.0);
                baseFlick *= 0.5;
                baseFlick *= (0.85 + 0.15 * sin(i.uv.y * 20.0));
                float flickAmp = _FlickerBase + _FlickerBurst * burst;
                col += baseFlick * flickAmp;

                // 롤링 밴드 적용(감마 전)
                ApplyHumBar(col, uv, t);

                // 감마/코너/비네트
                col = pow(saturate(col), 1.0 / _Gamma);
                float corner = CornerMask(i.uvNdc, _CornerRadius);
                float vign   = 1.0 - _Vignette * smoothstep(0.3, 1.0, length(i.uvNdc));
                col *= corner * vign;

                return float4(col, 1);
            }
            ENDHLSL
        }
    }
}
