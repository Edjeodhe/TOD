// 22차 미래 보기 점성구 (모이라 탁자의 큰 점성구 그림 moira_ball_big 에 씌움. 값은 FateOrbView 가 MaterialPropertyBlock 으로 넣는다).
//  _GlassTex  : 같은 캔버스의 유리만 흑백 그림 (moira_ball_glass). a = 유리 자리, r = 안개 밝기
//  _Tint      : 죄종 색. _Glow 만큼 유리를 그 색으로 물들인다 (떠오를 때)
//  _VideoTex  : 영상 RenderTexture. _Mix 만큼 유리 안에 원형으로 그린다 (가운데를 덮고 좌우는 잘림, 살짝 볼록 렌즈)
//  _GlassRect : 유리 원 (uv 중심 xy, uv 반지름 zw), _VideoAspect : 영상 가로/세로
//  _Bright, _Sat : MoiraRig 이 다른 파츠처럼 넣는 방 밝기와 채도 (받침과 유리 그림에만)
Shader "TOD/FateOrb"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _GlassTex ("Glass mask", 2D) = "black" {}
        _VideoTex ("Video", 2D) = "black" {}
        _Tint ("Sin color", Color) = (1, 1, 1, 1)
        _Glow ("Glow", Float) = 0
        _Mix ("Video mix", Float) = 0
        _GlassRect ("Glass circle (uv cx, cy, rx, ry)", Vector) = (0.5, 0.548, 0.492, 0.442)
        _VideoAspect ("Video aspect", Float) = 1.7778
        _Bulge ("Lens bulge", Float) = 0.25
        _Bright ("Brightness", Float) = 1
        _Sat ("Saturation", Float) = 1
        _Shape ("Shape (unused)", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_GlassTex); SAMPLER(sampler_GlassTex);
            TEXTURE2D(_VideoTex); SAMPLER(sampler_VideoTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _Glow, _Mix, _VideoAspect, _Bulge, _Bright, _Sat, _Shape;
                float4 _GlassRect;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SetUpSpriteInstanceProperties();
                float3 position = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                Varyings o;
                o.positionCS = TransformObjectToHClip(position);
                o.uv = input.uv;
                o.color = input.color * unity_SpriteColor;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 base = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half lumBase = dot(base.rgb, half3(0.299, 0.587, 0.114));
                half3 room = lerp(lumBase.xxx, base.rgb, _Sat) * _Bright * i.color.rgb;
                half4 glass = SAMPLE_TEXTURE2D(_GlassTex, sampler_GlassTex, i.uv);
                float g = glass.a;

                // 떠오를 때: 유리 안개를 죄종 색으로 물들이고 조금 밝힌다
                half3 tinted = glass.r * _Tint.rgb * 1.5 + room * 0.3;
                half3 c = lerp(room, tinted, saturate(_Glow) * g);

                // 영상: 유리 원 안 좌표 p (-1~1), 가장자리로 갈수록 덜 보이는 볼록 렌즈
                float2 p = (i.uv - _GlassRect.xy) / _GlassRect.zw;
                float r = length(p);
                float2 q = p * (1 - _Bulge + _Bulge * r * r);
                float2 vuv = 0.5 + 0.5 * float2(q.x / max(_VideoAspect, 0.01), q.y);
                half3 v = SAMPLE_TEXTURE2D(_VideoTex, sampler_VideoTex, vuv).rgb;
                float rim = smoothstep(0.62, 1.02, r);                       // 유리 가장자리: 어둡고 죄종 색
                half3 vid = v * (1 - 0.75 * rim) + _Tint.rgb * rim * 0.35;
                half spec = saturate((lumBase - 0.62) * 2.4) * (1 - rim * 0.5); // 원래 그림의 반짝임은 영상 위에 남긴다
                vid += spec * 0.55;
                c = lerp(c, vid, saturate(_Mix) * g);
                return half4(c, base.a * i.color.a);
            }
            ENDHLSL
        }
    }
}
