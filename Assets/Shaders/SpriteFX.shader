// SpriteRenderer 공용 셰이더. 웹판 Canvas 필터를 대신한다.
//  _Bright/_Sat : brightness()/saturate() (피격 번쩍임, 잘린 시체)
//  _Fill        : 단색 덮기 (회피 실루엣, 피격 붉은 기운, 뒤쪽 팔다리 그늘)
//  _CutPlane    : 월드 공간 반평면 클립 (두 동강 연출) — xy 법선, z 거리, w>0 이면 사용
//  _Shape       : 0 텍스처 / 1 원판 / 2 고리(두께 _Ring, uv 반지름 대비)
//  _Purify     : 1 이면 정화 고리 안쪽을 되살린다 (바닥 청크만 켠다). 전역 값은 ArenaRenderer 가 매 프레임 설정:
//                 _TOD_PurifyCircle (월드 중심 xy, 월드 반지름(없으면 <0), 세로 압축), _TOD_PurifyLook (밝기, 채도, 가장자리 폭),
//                 _TOD_PurifyFill (정화된 바닥 덮개 색, a = 양)
//  _SrcBlend/_DstBlend : 머티리얼마다 알파(5,10) 또는 가산(5,1)
Shader "TOD/SpriteFX"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Bright ("Brightness", Float) = 1
        _Sat ("Saturation", Float) = 1
        _Fill ("Fill (rgb, amount)", Color) = (0, 0, 0, 0)
        _CutPlane ("Cut plane (nx, ny, d, on)", Vector) = (0, 0, 0, 0)
        _Shape ("Shape", Float) = 0
        _Ring ("Ring thickness", Float) = 0.1
        _Purify ("Purify floor", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend [_SrcBlend] [_DstBlend]
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float _Bright;
                float _Sat;
                float4 _Fill;
                float4 _CutPlane;
                float _Shape;
                float _Ring;
                float _Purify;
                float _SrcBlend;
                float _DstBlend;
            CBUFFER_END

            float4 _TOD_PurifyCircle, _TOD_PurifyLook, _TOD_PurifyFill;

            Varyings vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SetUpSpriteInstanceProperties();
                float3 position = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings o;
                o.positionWS = TransformObjectToWorld(position);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = input.uv;
                o.color = input.color * unity_SpriteColor;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
                if (_Shape > 0.5)
                {
                    float r = length(i.uv * 2 - 1);
                    float edge = max(fwidth(r), 1e-4);
                    float outer = 1 - smoothstep(1 - edge, 1, r);
                    float inner = _Shape > 1.5 ? smoothstep(1 - _Ring - edge, 1 - _Ring, r) : 1;
                    c.a *= outer * inner;
                }
                if (_CutPlane.w > 0.5)
                {
                    clip(dot(i.positionWS.xy, _CutPlane.xy) - _CutPlane.z);
                }
                half lum = dot(c.rgb, half3(0.299, 0.587, 0.114));
                half3 raw = c.rgb;
                c.rgb = lerp(lum.xxx, c.rgb, _Sat) * _Bright;
                c.rgb = lerp(c.rgb, _Fill.rgb, _Fill.a);
                if (_Purify > 0.5 && _TOD_PurifyCircle.z > 0)
                {
                    // 고리 안쪽: 컨셉 덮개를 걷어내고 채도·밝기를 되살린다. 가장자리는 부드럽게.
                    float2 d = i.positionWS.xy - _TOD_PurifyCircle.xy;
                    d.y /= max(_TOD_PurifyCircle.w, 1e-3);
                    float soft = max(_TOD_PurifyLook.z, 1e-4);
                    float inside = 1 - smoothstep(_TOD_PurifyCircle.z - soft, _TOD_PurifyCircle.z, length(d));
                    half3 pure = lerp(lum.xxx, raw, _TOD_PurifyLook.y) * _TOD_PurifyLook.x;
                    pure = lerp(pure, _TOD_PurifyFill.rgb, _TOD_PurifyFill.a);
                    c.rgb = lerp(c.rgb, pure, inside);
                }
                return c;
            }
            ENDHLSL
        }
    }
}
