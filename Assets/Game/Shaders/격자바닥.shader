// 격자 바닥 — 칸마다 색이 다르고, 옆면은 어둡게. 정점 색을 그대로 쓴다.
//
// ★★레퍼런스(복셀 아이소메트릭)의 정체는 **면 방향 대비**다 (2026-08-09):
//     · 윗면 = 밝은 초록, 칸마다 조금씩 다른 색
//     · 옆면 = 뚝 떨어지는 갈색
//   그림자로는 이 느낌이 안 난다. **면이 실제로 갈려 있어야** 한다.
//
// ★조명은 단계로 끊는다(툰) — 그라데이션이 있으면 「평평한 색 덩어리」로 안 읽힌다.
Shader "토이라/격자바닥"
{
    Properties
    {
        _Steps ("빛 단계 수", Range(1,6)) = 3
        // ★0.55 → 0.32 (2026-08-09 사용자 "그림자가 진하게 나와 우리껏 흐릿하고").
        //   이 값이 곧 그림자의 밝기다 — 높으면 그림자가 있으나 마나가 된다.
        _ShadowLift ("그림자 바닥 밝기", Range(0,1)) = 0.32
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Steps;
                float _ShadowLift;
            CBUFFER_END

            TEXTURE2D(_CloudTex); SAMPLER(sampler_CloudTex);
            float4 _CloudParams;      // (1/구름크기, 흐른x, 흐른z, 짙기) — 전역

            struct A { float4 pos:POSITION; float3 nrm:NORMAL; float4 col:COLOR; };
            struct V { float4 pos:SV_POSITION; float3 wpos:TEXCOORD0; float3 wnrm:TEXCOORD1; float4 col:COLOR; float fog:TEXCOORD2; };

            V vert(A v)
            {
                V o;
                o.wpos = TransformObjectToWorld(v.pos.xyz);
                o.pos  = TransformWorldToHClip(o.wpos);
                o.wnrm = TransformObjectToWorldNormal(v.nrm);
                o.col  = v.col;
                o.fog  = ComputeFogFactor(o.pos.z);
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                float4 sc = TransformWorldToShadowCoord(i.wpos);
                Light L = GetMainLight(sc);
                half3 n = normalize(i.wnrm);
                half ndl = saturate(dot(n, L.direction));

                // ★단계로 끊는다 — 이게 툰의 정체다
                half s = max(_Steps, 1);
                half band = floor(ndl * s) / s;
                band = lerp(_ShadowLift, 1.0h, band);          // 그림자도 바닥은 남긴다 (색이 죽지 않게)
                band *= lerp(_ShadowLift, 1.0h, L.shadowAttenuation);

                // 구름 그림자 — 땅과 **같은 전역 값**을 쓴다 (안 그러면 이음매가 보인다)
                half cloud = 1.0h;
                if (_CloudParams.w > 0.001h)
                {
                    float2 cuv = i.wpos.xz * _CloudParams.x + _CloudParams.yz;
                    cloud = lerp(1.0h, SAMPLE_TEXTURE2D(_CloudTex, sampler_CloudTex, cuv).r, _CloudParams.w);
                }

                half3 amb = SampleSH(n);
                half3 col = i.col.rgb * (L.color * band * cloud + amb * 0.55h * lerp(1.0h, cloud, 0.5h));
                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }

        // ★★★**깊이 패스가 없으면 시야 덮개가 엉뚱한 데를 밝힌다** (2026-08-09).
        //   `Toyra/Vision` 덮개는 `_CameraDepthTexture` 를 읽어 **월드 자리를 되짚는다.**
        //   이 패스가 없으면 땅이 깊이에 안 적히고, 덮개가 그 자리에서 쓰레기 깊이를 읽어
        //   시야 원을 딴 데 그린다. 「어느 지점에서 생긴다」가 이것이다.
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma vertex dv
            #pragma fragment df
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 pos:POSITION; };
            struct V { float4 pos:SV_POSITION; };
            V dv(A v){ V o; o.pos = TransformObjectToHClip(v.pos.xyz); return o; }
            half4 df(V i):SV_Target { return 0; }
            ENDHLSL
        }

        // ★법선 패스 — SSAO 가 `Depth Normals` 를 쓰므로 없으면 그 자리 값이 쓰레기가 된다
        //   (2026-08-09 나무에서 흰 덩어리로 실제 사고가 났다)
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex nv
            #pragma fragment nf
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 pos:POSITION; float3 nrm:NORMAL; };
            struct V { float4 pos:SV_POSITION; float3 wnrm:TEXCOORD0; };
            V nv(A v){ V o; o.pos = TransformObjectToHClip(v.pos.xyz); o.wnrm = TransformObjectToWorldNormal(v.nrm); return o; }
            half4 nf(V i):SV_Target { return half4(normalize(i.wnrm), 0); }
            ENDHLSL
        }

        // 그림자를 드리우려면 이 패스가 있어야 한다
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma vertex sv
            #pragma fragment sf
            // ★그림자 바이어스 — 안 걸면 제 몸에 제 그림자가 져서 픽셀이 반짝인다
            //   (2026-08-09 나무에서 실제로 그 버그가 났다)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            struct A { float4 pos:POSITION; float3 nrm:NORMAL; };
            struct V { float4 pos:SV_POSITION; };
            V sv(A v)
            {
                V o;
                float3 wpos = TransformObjectToWorld(v.pos.xyz);
                float3 wnrm = TransformObjectToWorldNormal(v.nrm);
                float4 cs = TransformWorldToHClip(ApplyShadowBias(wpos, wnrm, _LightDirection));
            #if UNITY_REVERSED_Z
                cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
            #else
                cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                o.pos = cs;
                return o;
            }
            half4 sf(V i):SV_Target { return 0; }
            ENDHLSL
        }
    }
}
