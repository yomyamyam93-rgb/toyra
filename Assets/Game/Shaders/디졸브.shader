// 디졸브 — **메시 면이 실제로 지워지면서 사라진다** (2026-08-12 사용자 "마자 메시면이
// 지워지면서 사라지게").
//
// ★옛 프로젝트(`toyrassic/PetUnit.Dissolve`)에서 사용자가 한 번 고쳐 잡은 것이다:
//   *"모델링의 메시 면이 지워지면서 지워지는 면의 경계를 빛나는 파티클로 넣어달라는 거였어"*
//   (2026-07-30). 그전엔 몸을 `localScale` 로 줄이면서 입자를 뿌렸는데 「오그라든다」로 보였다.
//   → **몸 크기는 끝까지 안 변한다.** 문턱을 넘은 면이 `clip` 으로 잘려 나갈 뿐이다.
//
// ★아래에서 위로 훑고 올라간다. 잘리는 **경계 띠가 발광**한다.
// ★잘리는 모양은 **칸 단위**다 — 매끈한 물결이 아니라 조각조각 부서진다.
//   장난감 세계(6장)와 격자 규칙(11-1)에 맞고, 저해상도에서 훨씬 잘 읽힌다.
//
// ★펫 모델은 glTF 셰이더그래프를 쓴다 — `baseColorTexture` 만 옮겨 오면 그림이 같다
//   (`디졸브.cs` 가 옮긴다). 뼈는 신경 쓸 게 없다 — 스킨드 메시는 이미 적용돼 들어온다.
Shader "Toyra/디졸브"
{
    Properties
    {
        _BaseMap("바탕 그림", 2D) = "white" {}
        _BaseColor("색", Color) = (1, 1, 1, 1)
        _Dissolve("지운 정도 (0 = 그대로 · 1 = 다 지워짐)", Range(0, 1)) = 0
        _EdgeWidth("빛나는 경계 띠의 두께", Range(0.01, 0.4)) = 0.12
        [HDR] _EdgeColor("경계가 내는 빛", Color) = (2.2, 1.7, 0.9, 1)
        _Chunk("부서지는 조각 크기 (m)", Range(0.01, 0.4)) = 0.07
        _Jitter("조각이 흐트러지는 폭", Range(0, 1)) = 0.35
        _Y0("훑기 시작 높이 (m)", Float) = 0
        _Y1("훑기 끝 높이 (m)", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 150

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4  _BaseColor;
            half4  _EdgeColor;
            float4 _BaseMap_ST;
            float  _Dissolve;
            float  _EdgeWidth;
            float  _Chunk;
            float  _Jitter;
            float  _Y0;
            float  _Y1;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

        // 이 점이 얼마나 남았나 — **0 보다 작으면 지워진 자리**다. 0 근처가 빛나는 경계
        float Remain(float3 posWS)
        {
            float h = saturate((posWS.y - _Y0) / max(0.01, _Y1 - _Y0));   // 아래 0 · 위 1

            // 칸마다 값이 하나 — 매끈한 물결이 아니라 **조각**으로 부서진다
            float3 c = floor(posWS / max(0.01, _Chunk));
            float n = frac(sin(dot(c, float3(127.1, 311.7, 74.7))) * 43758.5453);

            float t = h + (n - 0.5) * _Jitter;
            // 0→1 을 넣으면 「하나도 안 지워짐 → 다 지워짐」이 되게 문턱을 넉넉히 편다
            float cut = lerp(-_Jitter * 0.5 - 0.01, 1.0 + _Jitter * 0.5 + 0.01, _Dissolve);
            return t - cut;
        }
        ENDHLSL

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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 uv         : TEXCOORD2;
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
                float d = Remain(IN.positionWS);
                clip(d);                                    // ★면이 실제로 사라진다

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half3 albedo = tex.rgb * _BaseColor.rgb;

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half3 nw = normalize(IN.normalWS);
                half ndl = saturate(dot(nw, mainLight.direction));
                half3 lit = mainLight.color * (ndl * mainLight.shadowAttenuation);
                half3 ambient = SampleSH(nw);

                half3 col = albedo * (lit + ambient);

                // ★잘리는 경계가 빛난다 — 「타들어간다」는 여기서 나온다
                half glow = 1.0h - smoothstep(0.0, max(0.005, _EdgeWidth), d);
                col += _EdgeColor.rgb * glow;

                col = MixFog(col, IN.fogCoord);
                return half4(col, 1.0h);
            }
            ENDHLSL
        }

        // 깊이 패스 — 시야(`Vision.shader`)가 깊이를 읽는다. **여기서도 잘라야** 지운 자리가 안 막힌다
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex depthVert
            #pragma fragment depthFrag

            struct DepthIn  { float4 positionOS : POSITION; };
            struct DepthOut { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            DepthOut depthVert(DepthIn IN)
            {
                DepthOut OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                return OUT;
            }

            half4 depthFrag(DepthOut IN) : SV_Target { clip(Remain(IN.positionWS)); return 0; }
            ENDHLSL
        }

        // ★법선 패스 — 없으면 SSAO 가 쓰레기 값을 읽어 흰 덩어리가 뜬다 (9-3 에서 세 번 밟은 함정)
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex normVert
            #pragma fragment normFrag

            struct NormIn  { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct NormOut { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };

            NormOut normVert(NormIn IN)
            {
                NormOut OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 normFrag(NormOut IN) : SV_Target
            {
                clip(Remain(IN.positionWS));
                return half4(normalize(IN.normalWS), 0);
            }
            ENDHLSL
        }

        // ★그림자도 같이 지워진다 — 안 그러면 **지워진 몸의 그림자만 온전히** 남는다
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex shadowVert
            #pragma fragment shadowFrag

            struct ShIn  { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct ShOut { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            ShOut shadowVert(ShIn IN)
            {
                ShOut OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionWS = pos.positionWS;
                OUT.positionCS = pos.positionCS;
                return OUT;
            }

            half4 shadowFrag(ShOut IN) : SV_Target { clip(Remain(IN.positionWS)); return 0; }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
