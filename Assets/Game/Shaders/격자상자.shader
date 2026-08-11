// 격자 상자 — **동굴의 바닥·벽·덮개가 땅의 격자 규칙을 따라 입는 옷** (2026-08-11 사용자
// "바닥이고 벽이고 모두 격자형식으로 표현됐음 좋겠는데 우리 넣은 규칙처럼").
//
// ★땅(`Toyra/Ground`)의 칸 규칙 그대로다: 1.4m 칸마다 밝기가 조금씩 다르다.
//   상자는 벽이 서 있으니 칸을 **세로축까지 3차원으로** 자른다 — 벽에도 격자가 생긴다.
// ★줄눈(칸 사이 가는 어두운 줄)이 격자를 눈에 보이게 한다. 땅은 높이 턱(칸흔들)이
//   그 역할을 하는데, 상자 표면에는 턱이 없으니 줄이 대신한다.
// ★면에 수직인 축의 경계는 안 그린다 — 면 전체가 우연히 경계에 걸리면 통째로 어두워진다.
Shader "Toyra/격자상자"
{
    Properties
    {
        _BaseColor("색", Color) = (0.5, 0.5, 0.5, 1)
        _CellSize("칸 크기 (m) — 땅의 격자칸과 같게", Float) = 1.4
        _CellVary("칸마다 밝기 흔들기", Range(0, 0.5)) = 0.14
        _LineWidth("줄눈 폭 (m)", Range(0.005, 0.2)) = 0.05
        _LineDark("줄눈 어둡기", Range(0, 0.8)) = 0.28
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 150

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _CellSize;
                float _CellVary;
                float _LineWidth;
                float _LineDark;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float  fogCoord   : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.fogCoord   = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 p = IN.positionWS / max(0.05, _CellSize);
                float3 cell = floor(p);

                // 칸마다 밝기 — 땅의 `_CellVary` 와 같은 수법을 3차원으로
                float h1 = frac(sin(dot(cell, float3(127.1, 311.7, 74.7))) * 43758.5453);
                half3 albedo = _BaseColor.rgb * (1.0h + (h1 - 0.5h) * _CellVary);

                // 줄눈 — 경계까지의 거리(m). 면에 수직인 축은 빼야 면이 통째로 어두워지지 않는다
                half3 n = abs(normalize(IN.normalWS));
                float3 dist = (0.5 - abs(frac(p) - 0.5)) * _CellSize + n * 999.0;
                float d = min(dist.x, min(dist.y, dist.z));
                float line1 = smoothstep(0.0, max(0.005, _LineWidth), d);
                albedo *= lerp(1.0h - _LineDark, 1.0h, line1);

                // 빛 — 땅 셰이더와 같은 최소 구성 (해 + 그림자 + 주변광)
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half3 nw = normalize(IN.normalWS);
                half ndl = saturate(dot(nw, mainLight.direction));
                half3 lit = mainLight.color * (ndl * mainLight.shadowAttenuation);
                half3 ambient = SampleSH(nw);

                half3 col = albedo * (lit + ambient);
                col = MixFog(col, IN.fogCoord);
                return half4(col, 1.0h);
            }
            ENDHLSL
        }

        // 깊이 패스 — 시야(`Vision.shader`)가 깊이를 읽는다 (땅 셰이더와 같은 이유)
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

        // ★법선 패스 — 없으면 SSAO(Depth Normals)가 쓰레기 값을 읽어 흰 덩어리가 뜬다
        //   (2026-08-11 — 이 프로젝트가 세 번째로 밟은 함정이라 새 셰이더엔 처음부터 넣는다)
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex normVert
            #pragma fragment normFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct NormIn  { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct NormOut { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            NormOut normVert(NormIn IN)
            {
                NormOut OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 normFrag(NormOut IN) : SV_Target { return half4(normalize(IN.normalWS), 0); }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
