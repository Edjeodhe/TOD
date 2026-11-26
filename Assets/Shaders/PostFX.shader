// 화면 후처리 (웹판 shader.js). 유령 신부 톤 + 굶지마 종이·잉크·보일링 + 비네트.
// 웹판이 캔버스에 직접 칠하던 화면 효과(당기기 섬광·임팩트 프레임·피격 붉은 테두리·욕망 테두리)도 여기서 먼저 섞는다.
// 웹판은 sRGB 값으로 계산했으므로 Linear 색공간에서 sRGB 로 바꿔 계산한 뒤 되돌린다.
// 전역 값(GameController 가 매 프레임 설정): _TOD_Desire, _TOD_Motion, _TOD_Flash(rgb,a), _TOD_Snap, _TOD_Hurt,
//   _TOD_Border, _TOD_CanvasHeight, _TOD_Enabled,
//   _TOD_Purified (정화된 화면 비네트 약화 0~1), _TOD_Warp (욕망 80% 이상 일렁임 0~1),
//   _TOD_WarpTint (붉은 막 rgb, a = 최대 양), _TOD_WarpParams (z = 숨쉬기 속도. 흔들림은 쓰지 않음)
Shader "TOD/PostFX"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            Name "TOD PostFX"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _TOD_Desire, _TOD_Motion, _TOD_Snap, _TOD_Hurt, _TOD_Border, _TOD_CanvasHeight, _TOD_Enabled;
            float4 _TOD_Flash;
            float _TOD_Purified, _TOD_Warp;
            float4 _TOD_WarpTint, _TOD_WarpParams;
            float4 _TOD_FateSwirl, _TOD_FateTint;   // 22차 미래 보기: 구슬(xy = 화면 uv 중심, z = 반지름(화면 높이 비율), w = 당김 세기), 바깥 물들임(rgb Linear, a = 양)
            float _TOD_FateBlur;                    // 22차 2차: 줌 속도만큼 구슬 바깥이 방사형으로 번지는 양 (반지름 비율)
            float _TOD_FateStill;                   // 22차 2차: 1 이면 화면이 미동도 없게 (촛불 깜빡임까지 멈춤)
            float4 _TOD_Look;                       // 24차: x 잉크 외곽선, y 색 보정, z 종이 결, w 비네트 세기 (GameDatabase.postFx, 1 = 예전)

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3. - 2. * f);
                return lerp(lerp(hash(i), hash(i + float2(1, 0)), u.x), lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), u.x), u.y);
            }
            float fbm(float2 p) { float v = 0., a = .5; for (int i = 0; i < 4; i++) { v += a * noise(p); p *= 2.03; a *= .5; } return v; }
            float lum(float3 c) { return dot(c, float3(.299, .587, .114)); }

            float3 Fetch(float2 uv) { return LinearToSRGB(SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb); }

            // 웹판에서 캔버스에 덧칠하던 화면 효과. canvas = 캔버스 px 좌표 (중심 500,295 기준).
            float3 Overlays(float3 c, float2 canvas)
            {
                c += _TOD_Flash.rgb * _TOD_Flash.a;                                  // 당기기 섬광 (lighter)
                c = lerp(c, float3(1., .957, .973), _TOD_Snap);                      // 임팩트 프레임
                float k = _TOD_Hurt;                                                 // 피격: 옅은 붉은 막 + 붉은 테두리
                c = lerp(c, float3(200., 30., 40.) / 255., .1 * k);
                float hurtEdge = saturate((length(canvas - float2(500., 295.)) - 180.) / 420.);
                c = lerp(c, float3(170., 20., 30.) / 255., .55 * k * hurtEdge);
                float border = saturate((length(canvas - float2(500., 295.)) - 160.) / 410.);   // 욕망 50% 이상
                c = lerp(c, float3(123., 31., 80.) / 255., _TOD_Border * border);
                return c;
            }

            // 22차 미래 보기 (2차): 정면으로 빨려 드는 당김. 구슬 바깥 화소가 구슬에서 더 먼 곳을 가져와 둘레가 구슬 쪽으로 곧게 당겨 보인다.
            // 회전(휘감김)과 시간에 따른 일렁임은 없다: 값이 그대로면 화면도 그대로.
            float2 FateWarp(float2 uv)
            {
                float s = _TOD_FateSwirl.w;
                if (s <= 0.) return uv;
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 c = _TOD_FateSwirl.xy;
                float2 d = (uv - c) * float2(aspect, 1.);
                float r = length(d), R = max(_TOD_FateSwirl.z, .01);
                float out_ = smoothstep(R * .97, R * 1.12, r);              // 구슬 안은 그대로
                float near = exp(-max(r - R, 0.) / (R * .9 + .08));        // 구슬에 가까울수록 1
                d *= 1. + s * near * out_ * 1.35;
                return c + d / float2(aspect, 1.);
            }

            // 줌 블러: 구슬 중심을 향한 직선을 따라 여러 번 가져와 섞는다(앞으로 달려드는 속도감). 구슬에서 멀수록 더 번진다. screen = 일그러지기 전 화면 uv
            float3 FateFetch(float2 uv, float2 screen)
            {
                float b = _TOD_FateBlur;
                if (b <= 0.) return Fetch(uv);
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 c = _TOD_FateSwirl.xy;
                float r = length((screen - c) * float2(aspect, 1.)), R = max(_TOD_FateSwirl.z, .01);
                float k = b * smoothstep(R * .97, R * 1.12, r) * saturate(.25 + (r - R) / R * .75);
                if (k <= 0.) return Fetch(uv);
                float2 d = uv - c;
                float3 acc = 0.;
                float j = frac(52.9829189 * frac(dot(screen * _ScreenParams.xy, float2(.06711056, .00583715))));   // 화소마다 고정된 어긋남 (겹 잔상 대신 고른 번짐, 시간에 따라 바뀌지 않음)
                [unroll] for (int i = 0; i < 12; i++) acc += Fetch(c + d * (1. - k * ((i + j) / 12. - .35)));
                return acc / 12.;
            }

            // 구슬 바깥 주변을 죄종 색으로: 구슬 가장자리가 가장 짙고 화면 끝으로 갈수록 옅다
            float3 FateTint(float3 c, float2 uv)
            {
                if (_TOD_FateTint.a <= 0.) return c;
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float r = length((uv - _TOD_FateSwirl.xy) * float2(aspect, 1.)), R = max(_TOD_FateSwirl.z, .01);
                float out_ = smoothstep(R * .98, R * 1.06, r);
                float halo = exp(-max(r - R, 0.) / (R * .55 + .05));
                float3 tint = LinearToSRGB(_TOD_FateTint.rgb);
                float3 dyed = lum(c) * tint * 1.25 + tint * .12;
                c = lerp(c, dyed, _TOD_FateTint.a * out_ * (.55 + .45 * halo));
                return c + tint * halo * out_ * .35 * _TOD_FateTint.a;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = FateWarp(input.texcoord);
                float3 source = FateTint(FateFetch(uv, input.texcoord), input.texcoord);
                if (_TOD_Enabled < .5) return half4(SRGBToLinear(saturate(source)), 1);
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 res = float2(_TOD_CanvasHeight * aspect, _TOD_CanvasHeight);
                float2 st = float2(uv.x, 1. - uv.y);
                float2 canvas = float2(500. + (uv.x - .5) * res.x, st.y * res.y);
                float time = _Time.y, beat = floor(time * 8.), motion = _TOD_Motion, desire = saturate(_TOD_Desire);

                // (2026-11-23) 화면을 흔드는 효과(손그림 보일링·욕망 물결)는 뺐다: 서브픽셀로 흔들면 화면 전체가 흐려진다.
                float2 q = uv;
                float warp = saturate(_TOD_Warp);
                float3 c = Overlays(FateTint(FateFetch(q, input.texcoord), input.texcoord), canvas);
                if (warp > 0.)
                {
                    // 욕망 80% 이상: 붉은 막만 남긴다 (가장자리가 더 짙고 숨 쉬듯 진해졌다 옅어진다). SetGlobalColor 는 Linear 로 넘기므로 sRGB 로 되돌린다.
                    float rim = saturate((length(canvas - float2(500., 295.)) - 120.) / 420.);
                    float breathe = .75 + .25 * sin(time * max(_TOD_WarpParams.z, .01) * .5);
                    c = lerp(c, LinearToSRGB(_TOD_WarpTint.rgb), _TOD_WarpTint.a * warp * breathe * (.45 + .55 * rim));
                }

                // 잉크 외곽: 밝기 Sobel → 경계를 어둡게 덧칠.
                // (2026-11-24) 붓결(fbm + beat)로 1초에 8번 끊던 것을 뺐다: 윤곽선이 지글거렸다. 이제 고정된 세기로만 덧칠한다.
                float2 px = 1. / _ScreenParams.xy;            // 화면 픽셀 단위 (캔버스 단위면 1080p 에서 외곽이 두껍고 흐려진다)
                float tl = lum(Fetch(q + px * float2(-1, 1))), tc = lum(Fetch(q + px * float2(0, 1)));
                float tr = lum(Fetch(q + px * float2(1, 1))), ml = lum(Fetch(q + px * float2(-1, 0)));
                float mr = lum(Fetch(q + px * float2(1, 0))), bl = lum(Fetch(q + px * float2(-1, -1)));
                float bc = lum(Fetch(q + px * float2(0, -1))), br = lum(Fetch(q + px * float2(1, -1)));
                float gx = -tl - 2. * ml - bl + tr + 2. * mr + br;
                float gy = -tl - 2. * tc - tr + bl + 2. * bc + br;
                float edge = smoothstep(.12, .42, length(float2(gx, gy)));
                c = lerp(c, float3(.07, .05, .08), edge * .43 * _TOD_Look.x);

                // 유령 신부 톤: 채도 낮추고 그림자는 청회색, 밝은 곳은 바랜 분홍/상아. (24차: _TOD_Look.y 만큼만)
                float3 raw = c;
                float l = lum(c);
                c = lerp(l.xxx, c, .8);
                float3 shadowTint = float3(.9, .97, 1.18), lightTint = float3(1.06, .98, .95);
                c *= lerp(shadowTint, lightTint, smoothstep(.08, .6, l));
                c = lerp(c, c * float3(.9, .95, 1.08), .35 * (1. - smoothstep(.0, .35, l)));
                c = c * 1.04 + .03;
                c = lerp(raw, c, _TOD_Look.y);

                // 종이: 큰 얼룩 + 가는 섬유결 (24차: _TOD_Look.z 만큼)
                float blot = fbm(st * 3.2);
                float fiber = fbm(st * float2(520., 140.)) * .6 + fbm(st * float2(140., 520.)) * .4;
                c *= 1. - (.07 * blot + .075 * (fiber - .5)) * _TOD_Look.z;

                // 비네트 + 욕망 수치가 높으면 붉게 조여 온다. (24차: 기본 세기 _TOD_Look.w, 욕망이 높을 때 붉게 조이는 것은 그대로)
                float2 d = (st - .5) * float2(1.05, 1.25);
                float vig = smoothstep(.4, .95, length(d)) * (1. - saturate(_TOD_Purified)) * lerp(_TOD_Look.w, 1., desire);
                c = lerp(c, float3(.05, .03, .06) + float3(.18, 0., .07) * desire, vig * (.55 + .25 * desire));

                // 촛불 깜빡임 (화면 전체 밝기가 천천히 조금). (2026-11-24) 픽셀마다 1초에 12번 바뀌던 필름 그레인은 지글거려서 뺐다.
                c *= 1. + (noise(float2(time * 2.3, 0.)) - .5) * .035 * motion * (1. - saturate(_TOD_FateStill));   // 미래 보기 영상 동안은 멈춤
                return half4(SRGBToLinear(saturate(c)), 1);
            }
            ENDHLSL
        }
    }
}
