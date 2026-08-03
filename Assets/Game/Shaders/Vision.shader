// 시야 — 보는 방향만 밝고 나머지는 어둡다 (좀보이드 방식)
//
// ★화면 한 장을 덮고, 픽셀마다 **그 픽셀이 세상 어디인지**를 깊이에서 되살려 어둡게
//   할지 정한다. 바닥에 원을 까는 방식이 아니라서 나무·바위처럼 서 있는 것도 같이 어두워진다.
//
// ★그라데이션은 **각도(도 단위)** 로 재고 부드럽게 잇는다. 코사인으로 재면 정면은
//   거의 안 변하다가 옆에서 급격히 꺾여 경계가 어색해진다 — 그게 처음 판의 문제였다.
Shader "Toyra/Vision"
{
    SubShader
    {
        Tags { "RenderType" = "Overlay" "RenderPipeline" = "UniversalPipeline" "Queue" = "Overlay" }
        Pass
        {
            Cull Off  ZWrite Off  ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            float4 _VisionPos;      // xyz = 눈의 위치
            float4 _VisionDir;      // xz = 보는 방향(정규화)
            float4 _VisionAngle;    // x = 반각(도) · y = 가장자리 폭(도)
            float4 _VisionDist;     // x = 보이는 거리 · y = 끝이 흐려지는 폭 · z = 코앞 반경 · w = 코앞 흐려짐
            float4 _VisionDark;     // x = 어둠 세기 · y = 밝은 곳도 멀수록 어두워지는 정도

            struct A { float4 pos : POSITION; };
            struct V { float4 pos : SV_POSITION; float4 sp : TEXCOORD0; };

            V vert(A i)
            {
                V o;
                o.pos = float4(i.pos.xy * 2.0, 0.0, 1.0);   // 메시가 무엇이든 화면을 덮는다
                o.sp = o.pos;
                return o;
            }

            half4 frag(V i) : SV_Target
            {
                float2 uv = i.sp.xy / i.sp.w * 0.5 + 0.5;
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0 - uv.y;
                #endif

                float d = SampleSceneDepth(uv);
                if (d <= 1e-6) return half4(0, 0, 0, _VisionDark.x);      // 하늘은 어둡게

                float3 wp = ComputeWorldSpacePosition(uv, d, UNITY_MATRIX_I_VP);

                float2 v = wp.xz - _VisionPos.xz;
                float dist = length(v);
                float2 dir = dist > 1e-4 ? v / dist : _VisionDir.xz;

                // ① 각도 — 도(°) 로 재야 고르게 흐려진다
                float ang = degrees(acos(clamp(dot(dir, _VisionDir.xz), -1.0, 1.0)));
                float cone = 1.0 - smoothstep(_VisionAngle.x - _VisionAngle.y,
                                              _VisionAngle.x + _VisionAngle.y, ang);

                // ② 거리 — 끝에서 부드럽게 사라진다
                float far = 1.0 - smoothstep(_VisionDist.x - _VisionDist.y, _VisionDist.x, dist);

                // ③ 코앞은 등 뒤라도 안다 (몸으로 느끼는 범위)
                float near = 1.0 - smoothstep(_VisionDist.z, _VisionDist.z + _VisionDist.w, dist);

                // 부채꼴과 코앞을 **부드럽게 합친다** (둘 중 큰 값을 고르면 경계가 진다)
                float lit = 1.0 - (1.0 - cone * far) * (1.0 - near);

                // 밝은 안쪽도 멀수록 조금씩 어두워진다 — 완전히 평평하면 판때기처럼 보인다
                lit *= 1.0 - _VisionDark.y * saturate(dist / max(1.0, _VisionDist.x));

                return half4(0, 0, 0, _VisionDark.x * (1.0 - saturate(lit)));
            }
            ENDHLSL
        }
    }
}
