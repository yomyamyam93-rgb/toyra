// 실루엣 마스크 — 물체를 **한 덩어리 값**으로 찍는다.
//
// ★같은 물체의 조각들은 같은 값, 다른 물체는 다른 값. 그래야 안쪽에는 선이 안 생기고
//   겹쳤을 때는 둘이 갈린다.
//
// ★★인스턴싱을 지원해야 한다 (2026-08-04). 잔디는 `DrawMeshInstanced` 로 그리는데,
//   셰이더에 인스턴싱 선언이 없으면 **아예 안 그려진다.** 그래서 마스크에 잔디가 빠졌고,
//   결과적으로 펫의 테두리가 잔디 위에 덧그려졌다.
//
// ★알파 잘라내기도 필요하다. 잔디는 판 모양이 아니라 **풀잎 모양**으로 가려야 한다.
Shader "Toyra/Outline"
{
    Properties
    {
        _Id ("덩어리 값", Float) = 0.5
        _BaseMap ("모양 (알파)", 2D) = "white" {}
        _UseAlpha ("알파로 잘라낼까", Float) = 0
        _Cutoff ("알파 문턱", Range(0,1)) = 0.5
        // ★마스크에서만 몸을 살짝 부풀린다 (2026-08-04 사용자
        //   "모델링을 애초에 한 픽셀 두껍게 표현하게 하든가"). 그러면 테두리를
        //   **부푼 껍질에** 긋게 되어, 화면에서는 모델 **바깥**에 선이 생긴다.
        //   모델이 깎이지 않으면서 잔디 가림도 그대로 된다.
        _Expand ("부풀리기 (m)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "Mask"
            // ★LightMode 를 반드시 밝혀야 한다 (2026-08-04). 없으면 URP 가 이 패스를
            //   그리기 목록에 안 넣는 경우가 있고, 실제로 `DrawMeshInstanced` 로 그린
            //   잔디가 마스크에 **한 장도 안 들어갔다.**
            Tags { "LightMode" = "UniversalForward" }
            Cull Off  ZWrite On  ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            // CBUFFER 밖에 둬야 MaterialPropertyBlock 으로 개체마다 바꿀 수 있다
            float _Id;
            float _UseAlpha;
            float _Cutoff;
            float _Expand;

            struct A
            {
                float4 pos : POSITION;
                float3 nrm : NORMAL;
                float2 uv  : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct V
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            V vert(A i)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_TRANSFER_INSTANCE_ID(i, o);
                // 몸을 법선 방향으로 조금 부풀린다 (마스크에서만 — 화면엔 원래 몸이 그려진다)
                float3 wp = TransformObjectToWorld(i.pos.xyz);
                float3 wn = normalize(TransformObjectToWorldNormal(i.nrm));
                o.pos = TransformWorldToHClip(wp + wn * _Expand);
                o.uv = i.uv;
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                if (_UseAlpha > 0.5)
                {
                    half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a;
                    clip(a - _Cutoff);
                }
                return half4(_Id, _Id, _Id, 1);
            }
            ENDHLSL
        }
    }
}
