// 뒤집힌 껍데기 외곽선 — 몸을 노멀 방향으로 부풀리고 **앞면을 버려** 뒷면만 그린다.
// 그러면 원래 몸 바깥으로 삐져나온 테두리만 보인다.
//
// ★왜 후처리가 아닌가 (2026-08-09): 이 프로젝트의 기존 `Outliner` 는 실루엣 복사본을
//   만들어 두고 **`PixelScreen` 이 칠하는** 구조다. 픽셀 화면을 끄기로 했으니
//   후처리에 안 기대는 방식이 필요하다. 이건 물체 하나에 패스 하나라 확실히 나온다.
//
// ★두께가 **화면 비례**다 — 아이소메트릭이라 카메라 거리가 고정이지만, 줌(직교 크기)을
//   바꾸면 선 굵기가 같이 변해야 한다. `unity_OrthoParams.y`(직교 반높이)로 나눠 맞춘다.
Shader "토이라/외곽선"
{
    Properties
    {
        _OutlineColor ("선 색", Color) = (0.08, 0.07, 0.10, 1)
        _OutlineWidth ("선 굵기 (화면 비율)", Range(0, 0.02)) = 0.0045
        _MinWidth ("최소 굵기 (m)", Range(0, 0.2)) = 0.004
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+10" }

        Pass
        {
            Name "Outline"
            Cull Front              // ★앞면을 버린다 — 뒷면만 남아 테두리가 된다
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float  _OutlineWidth;
                float  _MinWidth;
            CBUFFER_END

            struct A { float4 pos : POSITION; float3 nrm : NORMAL; };
            struct V { float4 pos : SV_POSITION; };

            V vert (A v)
            {
                V o;
                float3 wpos = TransformObjectToWorld(v.pos.xyz);
                float3 wnrm = normalize(TransformObjectToWorldNormal(v.nrm));

                // 직교 카메라면 반높이(unity_OrthoParams.y)가 곧 화면 크기다.
                // 그걸 곱해야 줌을 바꿔도 선이 같은 굵기로 보인다.
                float halfH = max(unity_OrthoParams.y, 0.001);
                float w = max(_OutlineWidth * halfH, _MinWidth);

                wpos += wnrm * w;
                o.pos = TransformWorldToHClip(wpos);
                return o;
            }

            half4 frag (V i) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }
    }
    Fallback Off
}
