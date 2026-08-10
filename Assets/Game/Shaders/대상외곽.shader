// 최외곽 실루엣 외곽선 — 마스크 패스(원본 그대로, 스텐실=1, 색 안 씀)와
// 선 패스(노멀 부풀림, 스텐실!=1 에만)를 **재질 둘**로 나눠 큐(2001→2002)로 줄 세운다.
// 한 재질에 패스 둘이면 렌더러마다 마스크·선이 번갈아 그려져 안쪽 이음선이 새어 나온다.
// ★HLSL 안에 한글 변수명 금지 — 컴파일이 깨져 온 화면이 분홍이 된다.
Shader "토이라/대상외곽"
{
    Properties
    {
        _OutlineColor ("선 색", Color) = (0.35, 0.95, 0.45, 1)
        _OutlineWidth ("선 굵기 (화면 비율)", Range(0, 0.03)) = 0.006
        _Mode ("0=마스크 1=선", Float) = 1
        _Cull ("컬링", Float) = 2
        _ColorMask ("색마스크", Float) = 15
        _StencilComp ("스텐실 비교 (8=Always 6=NotEqual)", Float) = 8
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }

        Pass
        {
            Name "MaskOrLine"
            Cull [_Cull]
            ZWrite Off
            ZTest LEqual
            ColorMask [_ColorMask]
            Stencil
            {
                Ref 1
                Comp [_StencilComp]
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float  _OutlineWidth;
                float  _Mode;
            CBUFFER_END

            struct A { float4 pos : POSITION; float3 nrm : NORMAL; };
            struct V { float4 pos : SV_POSITION; };

            V vert (A v)
            {
                V o;
                float3 wpos = TransformObjectToWorld(v.pos.xyz);
                if (_Mode > 0.5)
                {
                    float3 wnrm = normalize(TransformObjectToWorldNormal(v.nrm));
                    float halfH = max(unity_OrthoParams.y, 0.001);
                    wpos += wnrm * max(_OutlineWidth * halfH, 0.01);
                }
                o.pos = TransformWorldToHClip(wpos);
                return o;
            }

            half4 frag (V i) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }
    }
    Fallback Off
}
