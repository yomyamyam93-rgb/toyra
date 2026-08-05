// 땅 전용 셰이더 — **업계가 바닥을 만드는 방식** 그대로 (2026-08-05).
//
// ★두 층을 곱한다:
//     ①큰 층 = `_BaseMap` — 코드가 그린 땅 그림 (잔디 색·흙길·물웅덩이). 1440m 를 한 장으로.
//     ②잔 층 = `_GrassTex` / `_DirtTex` / `_SandTex` — 몇 미터마다 되풀이되는 표면 결.
//   ①이 「어디가 무엇인가」를 정하고 ②가 「가까이서 어떤 결인가」를 정한다.
//
// ★★잔 층이 **한 장이 아니다.** 잔디 자리엔 잔디결, 흙길엔 흙결, 물가엔 모래결이 걸린다.
//   무엇이 어디인지는 `_MaskMap` 이 알려 준다 (R=잔디 G=흙 B=모래, 알파 0=물).
//   유니티 기본 재질은 결을 까는 자리가 **하나뿐**이라 이게 안 됐다 — 그래서 전용 셰이더다.
//
// ★결은 **회색 기준(0.5)**이다. 0.5 면 밑색 그대로, 그보다 밝으면 밝게. 색조는 밑색이 쥔다.
//   결 그림에 색이 들어 있으면 땅색이 그쪽으로 물들어 탁해진다 — 오늘 그걸로 여러 번 데었다.
//
// ★물에는 결을 안 얹는다 (마스크 알파). 물은 결이 있으면 물로 안 보인다.
Shader "Toyra/Ground"
{
    Properties
    {
        _BaseMap("땅 그림", 2D) = "white" {}
        _MaskMap("칸 마스크 (R잔디 G흙 B모래 A물아님)", 2D) = "black" {}
        _GrassTex("잔디 결", 2D) = "gray" {}
        _DirtTex("흙 결", 2D) = "gray" {}
        _SandTex("모래 결", 2D) = "gray" {}
        _DetailTiling("결 반복 (1m 당 몇 장)", Float) = 0.5
        _DetailStrength("결 세기", Range(0,1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);  SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MaskMap);  SAMPLER(sampler_MaskMap);
            TEXTURE2D(_GrassTex); SAMPLER(sampler_GrassTex);
            TEXTURE2D(_DirtTex);  SAMPLER(sampler_DirtTex);
            TEXTURE2D(_SandTex);  SAMPLER(sampler_SandTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _DetailTiling;
                float _DetailStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                float  fogCoord   : TEXCOORD3;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv         = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogCoord   = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half4 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, IN.uv);

                // ★결의 uv 는 **월드 좌표**다 — 땅 그림 해상도와 아무 상관이 없어진다.
                //   그래서 5cm 든 4m 든 원하는 크기로 깔 수 있다.
                float2 duv = IN.positionWS.xz * _DetailTiling;

                half g = SAMPLE_TEXTURE2D(_GrassTex, sampler_GrassTex, duv).g;
                half d = SAMPLE_TEXTURE2D(_DirtTex,  sampler_DirtTex,  duv).g;
                half s = SAMPLE_TEXTURE2D(_SandTex,  sampler_SandTex,  duv).g;

                // 칸 종류로 섞는다. 셋 다 0 이면(물) 결이 없다
                half w = mask.r + mask.g + mask.b;
                half detail = 0.5h;
                if (w > 0.001h) detail = (g * mask.r + d * mask.g + s * mask.b) / w;

                // ★0.5 = 그대로. 세기만큼만 밑색을 흔든다 — 색조는 밑색이 그대로 쥔다
                half amt = saturate(w) * mask.a * _DetailStrength;
                half f = lerp(1.0h, detail * 2.0h, amt);
                half3 albedo = base.rgb * f;

                // ── 빛 (해 + 그림자 + 주변광). 땅은 완전 평지라 이만큼이면 충분하다
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half3 n = normalize(IN.normalWS);
                half ndl = saturate(dot(n, mainLight.direction));
                half3 lit = mainLight.color * (ndl * mainLight.shadowAttenuation);
                half3 ambient = SampleSH(n);

                half3 col = albedo * (lit + ambient);
                col = MixFog(col, IN.fogCoord);
                return half4(col, 1.0h);
            }
            ENDHLSL
        }

        // ★깊이 패스가 있어야 한다 — `Vision.shader`(시야)가 깊이 텍스처를 읽는다.
        //   이게 없으면 땅이 깊이에서 빠져 시야 부채꼴이 어긋난다.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DepthIn  { float4 positionOS : POSITION; };
            struct DepthOut { float4 positionCS : SV_POSITION; };

            DepthOut depthVert(DepthIn IN)
            {
                DepthOut OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 depthFrag(DepthOut IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
