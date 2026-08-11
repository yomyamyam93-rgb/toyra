// 나무를 **단색 두 가지**로 칠한다 — 잎 하나, 줄기 하나. 그라데이션을 없앤다.
//
// ★★왜 셰이더로 하나 (2026-08-09 사용자 "나뭇잎 부분 색상들, 그라데이션 느낌 나지않게
//   단색으로 칠해줘, 빠지는곳없이"). 실측해 보니 나무 5종은 **렌더러 하나 · 재질 하나**이고
//   잎과 줄기가 **2048 텍스처 한 장**에 같이 들어 있다 (`base_color`, 정점색 없음, 서브메시 1).
//   → 재질 이름이나 오브젝트 이름으로는 잎을 못 고른다. 전에 쓰던 `Leaf`/`Canopy` 이름
//     판정은 **한 그루도 안 걸리고 있었다.** 그래서 "다 못 칠했다" 가 아니라 거의 안 칠해졌다.
//   → **텍셀 색으로 가른다.** 초록빛이 도는 텍셀 = 잎, 나머지 = 줄기. 빠지는 데가 없다.
//
// ★그라데이션의 출처는 둘이다 — ①텍스처 안의 명암 ②빛의 연속 변화.
//   ①은 색을 통째로 갈아치워 없애고, ②는 단계로 끊는다(툰). 둘 다 없애야 「단색」이 된다.
Shader "토이라/단색나무"
{
    Properties
    {
        _BaseMap("원본 그림 (잎·줄기 가르는 데만 쓴다)", 2D) = "white" {}
        _LeafColor("잎 색", Color) = (0.34, 0.53, 0.25, 1)
        _BarkColor("줄기 색", Color) = (0.40, 0.29, 0.20, 1)
        _GreenBias("초록으로 치는 문턱", Range(-0.1, 0.2)) = 0.02
        _Steps("빛 단계 수", Range(1,4)) = 2
        // ★★0.55 → 0.30 (2026-08-11 사용자 "나무 색상 대비좀 더 넣어줄래? 어둡게 잘리는
        //   부분이 더 어둡게 표현되어야할거같은데"). 그늘이 밑색의 55% 면 밝은 쪽과 거의
        //   안 갈려서 단색 두 벌이 통짜로 보인다 — 30% 면 잎의 굴곡이 실루엣으로 읽힌다
        _ShadowLift("그늘 밝기", Range(0,1)) = 0.30
        _Cutoff("알파 자르기", Range(0,1)) = 0.5
        [Toggle] _DoClip("알파로 자를까", Float) = 0
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

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _LeafColor;
                float4 _BarkColor;
                float _GreenBias, _Steps, _ShadowLift, _Cutoff, _DoClip;
            CBUFFER_END

            struct A { float4 pos:POSITION; float3 nrm:NORMAL; float2 uv:TEXCOORD0; };
            struct V { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float3 wpos:TEXCOORD1; float3 wnrm:TEXCOORD2; float fog:TEXCOORD3; };

            V vert(A v)
            {
                V o;
                o.wpos = TransformObjectToWorld(v.pos.xyz);
                o.pos  = TransformWorldToHClip(o.wpos);
                o.wnrm = TransformObjectToWorldNormal(v.nrm);
                o.uv   = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fog  = ComputeFogFactor(o.pos.z);
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                half4 src = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                if (_DoClip > 0.5h) clip(src.a - _Cutoff);

                // ★잎이냐 줄기냐 — 초록이 붉음·푸름보다 얼마나 앞서나로만 가른다.
                //   밝기는 안 본다. 그늘진 잎도 초록이 앞서므로 같이 잡힌다
                half green = src.g - max(src.r, src.b);
                half3 albedo = green > _GreenBias ? _LeafColor.rgb : _BarkColor.rgb;

                float4 sc = TransformWorldToShadowCoord(i.wpos);
                Light L = GetMainLight(sc);
                half3 n = normalize(i.wnrm);
                half ndl = saturate(dot(n, L.direction));

                // ★단계로 끊는다 — 이어지면 그라데이션이 도로 생긴다
                half s = max(_Steps, 1);
                half band = floor(ndl * s) / s;
                band = lerp(_ShadowLift, 1.0h, band);
                band *= lerp(_ShadowLift, 1.0h, L.shadowAttenuation);

                // ★주변광이 세면 그늘이 도로 밝아져 대비가 죽는다 — 0.55 → 0.34 (2026-08-11)
                half3 col = albedo * (L.color * band + SampleSH(n) * 0.34h);
                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma vertex sv
            #pragma fragment sf
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; float4 _LeafColor; float4 _BarkColor;
                float _GreenBias, _Steps, _ShadowLift, _Cutoff, _DoClip;
            CBUFFER_END
            // ★★★**그림자 바이어스를 반드시 걸어야 한다** (2026-08-09 사용자 "나무에서
            //   하얀빛이 반짝반짝 버그"). 그냥 `TransformObjectToHClip` 으로 깊이를 쓰면
            //   나무가 **제 몸에 제 그림자**를 잘못 드리워 픽셀마다 켜졌다 꺼졌다 한다
            //   (shadow acne). 툰이라 2단계로 뚝 갈려서 그게 **흰 점의 반짝임**으로 튄다.
            //   → URP 가 하는 그대로: 표면을 빛 쪽·법선 쪽으로 살짝 밀어 깊이를 적는다.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;

            struct A { float4 pos:POSITION; float3 nrm:NORMAL; float2 uv:TEXCOORD0; };
            struct V { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
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
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }
            half4 sf(V i):SV_Target
            {
                // ★그림자도 같은 문턱으로 자른다 — 안 그러면 잎 사이 빈 곳까지 그림자가 진다
                if (_DoClip > 0.5h) clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        // ★★★**법선 패스가 없으면 화면에 흰 덩어리가 뜬다** (2026-08-09 사용자 "나무에서
        //   하얗게 반짝". 실측: 이 프로젝트의 SSAO 는 `Source = Depth Normals` 다).
        //   이 패스가 없는 물체는 카메라 법선 텍스처에 **아무것도 안 적는다.** SSAO 가
        //   그 자리에서 쓰레기 값을 읽어 계산이 터지면 **흰색**으로 나온다.
        //   나무에서 나오고, 카메라를 움직이면 자리가 변하는 것도 화면 기준 효과라서다.
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex nv
            #pragma fragment nf
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; float4 _LeafColor; float4 _BarkColor;
                float _GreenBias, _Steps, _ShadowLift, _Cutoff, _DoClip;
            CBUFFER_END
            struct A { float4 pos:POSITION; float3 nrm:NORMAL; float2 uv:TEXCOORD0; };
            struct V { float4 pos:SV_POSITION; float3 wnrm:TEXCOORD0; float2 uv:TEXCOORD1; };
            V nv(A v)
            {
                V o;
                o.pos = TransformObjectToHClip(v.pos.xyz);
                o.wnrm = TransformObjectToWorldNormal(v.nrm);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                return o;
            }
            half4 nf(V i):SV_Target
            {
                if (_DoClip > 0.5h) clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a - _Cutoff);
                return half4(normalize(i.wnrm), 0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask 0
            HLSLPROGRAM
            #pragma vertex dv
            #pragma fragment df
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; float4 _LeafColor; float4 _BarkColor;
                float _GreenBias, _Steps, _ShadowLift, _Cutoff, _DoClip;
            CBUFFER_END
            struct A { float4 pos:POSITION; float2 uv:TEXCOORD0; };
            struct V { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; };
            V dv(A v){ V o; o.pos = TransformObjectToHClip(v.pos.xyz); o.uv = TRANSFORM_TEX(v.uv, _BaseMap); return o; }
            half4 df(V i):SV_Target
            {
                if (_DoClip > 0.5h) clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
