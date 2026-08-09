// 조준 표시 — 땅 위에 얹히는 얇은 빛. 정점 색을 그대로 쓴다.
//
// ★★HLSL 안에는 **아스키 이름만** 쓴다 (2026-08-09에 한글 변수명으로 셰이더를 두 번 깼다).
// ★깊이를 안 쓴다(ZTest Always) — 풀·자갈에 파묻히면 표시의 뜻이 사라진다.
Shader "토이라/조준선"
{
    Properties { }
    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" }

        Pass
        {
            Name "Aim"
            Blend SrcAlpha One          // 더하기 — 땅 위에 얹히는 빛
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct A { float4 pos:POSITION; float4 col:COLOR; };
            struct V { float4 pos:SV_POSITION; float4 col:COLOR; };

            V vert(A v) { V o; o.pos = TransformObjectToHClip(v.pos.xyz); o.col = v.col; return o; }
            half4 frag(V i) : SV_Target { return half4(i.col.rgb * i.col.a, i.col.a); }
            ENDHLSL
        }
    }
    Fallback Off
}
