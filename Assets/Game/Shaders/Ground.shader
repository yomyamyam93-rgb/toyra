// 땅 전용 셰이더 — **업계가 바닥을 만드는 방식** 그대로 (2026-08-05).
//
// ★★두 갈래가 있다. 어느 쪽인지는 `_PhotoNum` 이 정한다.
//
//   ①**사진 갈래** (`_PhotoNum > 0`) — 2026-08-05 사용자 "픽셀버전으로 변형하지말고 그대로".
//     진짜 사진 여러 장이 **색을 쥔다.** 코드 팔레트는 물에만 남는다.
//     사진마다 자기 노이즈 밭이 있어서 자리마다 다른 게 진해진다 (`GroundPhotos` 참고).
//
//   ②**옛 갈래** (`_PhotoNum == 0`) — 코드가 칠한 팔레트 색 × 회색 명암 결.
//     사진이 하나도 없으면 여기로 돌아온다. 은퇴는 삭제가 아니라 스위치.
//
// ★★사진 갈래가 「어느 사진을 뽑나」를 정하는 법 — **꼭대기 근처만 섞는다.**
//   가중치가 제일 높은 놈(maxW)에서 `_PhotoBand` 안에 든 사진들만 색에 참여하고,
//   그 밖은 0 이다. 사진이 꼭대기에 가까워지면 **스르르 끼어들고** 멀어지면 스르르 빠진다.
//   ☆「제일 진한 둘만 골라 섞기」로 하면 2등과 3등이 뒤바뀌는 자리에서 색이 **뚝 끊긴다**
//     (둘의 가중치는 같은데 색이 다르니까). 이 방식은 그 자리에서 둘 다 0 에 가까워
//     끊길 것 자체가 없다.
//
// ★결의 uv 는 **월드 좌표**다 — 땅 그림 해상도와 아무 상관이 없어진다.
//   그래서 0.7m 텍셀 한계에 안 걸리고 사진이 원본 해상도 그대로 나온다.
//
// ★물에는 사진도 결도 안 얹는다 (마스크 알파). 물은 결이 있으면 물로 안 보인다.
Shader "Toyra/Ground"
{
    Properties
    {
        _BaseMap("땅 그림", 2D) = "white" {}
        _MaskMap("칸 마스크 (R잔디 G흙 B모래 A물아님)", 2D) = "black" {}

        [Header(Photo)]
        // ★★반드시 여기 선언해야 한다 (2026-08-05). Properties 에 없는 **텍스처**는
        //   `Material.SetTexture` 로 넣어도 재질이 안 들고 있는다 — 실제로 배열이 통째로
        //   비어 땅이 안 나왔다. (float·벡터배열은 안 적어도 들어가서 더 헷갈렸다)
        [NoScaleOffset] _PhotoArr("사진 배열", 2DArray) = "" {}
        _RockMap("바위지대 분포", 2D) = "black" {}
        _PhotoTiling("사진 반복 (1m 당 몇 장)", Float) = 0.25
        _PhotoBand("섞이는 폭 (작을수록 또렷하게 갈린다)", Range(0.02,0.6)) = 0.18
        _PhotoShade("큰 명암 흔들기", Range(0,0.5)) = 0.12

        [Header(Legacy gray detail)]
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
            #pragma target 3.5

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #include "GroundBlend.hlsl"      // ★배합 식은 여기 하나뿐 — 풀도 같은 걸 쓴다

            TEXTURE2D(_BaseMap);  SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MaskMap);  SAMPLER(sampler_MaskMap);
            TEXTURE2D(_RockMap);  SAMPLER(sampler_RockMap);
            TEXTURE2D(_GrassTex); SAMPLER(sampler_GrassTex);
            TEXTURE2D(_DirtTex);  SAMPLER(sampler_DirtTex);
            TEXTURE2D(_SandTex);  SAMPLER(sampler_SandTex);

            TEXTURE2D_ARRAY(_PhotoArr); SAMPLER(sampler_PhotoArr);

            // ★사진 낱장의 밭 — CBUFFER 밖에 둔다. `SetVectorArray` 로 넣는 값이다
            float4 _PhotoParams[PHOTO_MAX];   // (주파수 1/m, 오프셋x, 오프셋z, 편향)
            float4 _PhotoGroup[PHOTO_MAX];    // (무리 0잔디 1흙 2돌, uv배율, -, -)
            float  _PhotoNum;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float _DetailTiling;
                float _DetailStrength;
                float _PhotoTiling;
                float _PhotoBand;
                float _PhotoShade;
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

                half3 albedo;

                if (_PhotoNum > 0.5)
                {
                    // ── ① 사진 갈래
                    float2 wp = IN.positionWS.xz;
                    half rock = SAMPLE_TEXTURE2D(_RockMap, sampler_RockMap, IN.uv).r;

                    // 무리별 자리 — 잔디 자리는 바위지대에서 돌바닥에 자리를 내준다
                    half wGrass = mask.r * (1.0h - rock);
                    half wDirt   = mask.g + mask.b;      // 모래도 흙 무리를 쓴다 (모래 사진이 없다)
                    half wStone   = mask.r * rock;

                    // 사진마다 가중치를 재고 꼭대기를 찾는다
                    float w[PHOTO_MAX];
                    float peak = 0.0;
                    int n = (int)_PhotoNum;

                    [loop]
                    for (int i = 0; i < n; i++)
                    {
                        int g = (int)_PhotoGroup[i].x;
                        half slot = ToyraSlot(g, float3(wGrass, wDirt, wStone));

                        float v = 0.0;
                        if (slot > 0.001h)
                        {
                            float2 q = wp * _PhotoParams[i].x + _PhotoParams[i].yz;
                            v = (ToyraField2(q) + _PhotoParams[i].w) * slot;
                        }
                        w[i] = v;
                        peak = max(peak, v);
                    }

                    // 꼭대기 근처만 섞는다 — 그 밖은 0 이라 색에 안 낀다
                    float2 uv0 = wp * _PhotoTiling;
                    float2 dx = ddx(uv0), dy = ddy(uv0);

                    half3 sum3 = 0.0h;
                    float wsum = 0.0;

                    [loop]
                    for (int j = 0; j < n; j++)
                    {
                        float c = ToyraContrib(w[j], peak, _PhotoBand);
                        if (c <= 0.0) continue;

                        float s = _PhotoGroup[j].y;    // 사진마다 uv 배율을 흔들어 되풀이감을 줄인다
                        half3 col = SAMPLE_TEXTURE2D_ARRAY_GRAD(
                            _PhotoArr, sampler_PhotoArr, uv0 * s, j, dx * s, dy * s).rgb;

                        sum3 += col * c;
                        wsum += c;
                    }

                    half3 photoCol = (wsum > 0.0) ? sum3 / wsum : base.rgb;

                    // ★큰 명암만 얹는다 — 색은 안 건드린다. 이게 없으면 넓은 벌판이 평평해 보인다
                    float shade = ToyraField2(wp * 0.008 + 71.3);         // 얼룩 하나 약 125m
                    photoCol *= 1.0h + (shade - 0.5h) * 2.0h * _PhotoShade;

                    // 물은 칠한 색 그대로 (mask.a = 0 이 물이다)
                    albedo = lerp(base.rgb, photoCol, mask.a);
                }
                else
                {
                    // ── ② 옛 갈래: 팔레트 색 × 회색 명암 결
                    float2 duv = IN.positionWS.xz * _DetailTiling;

                    half g = SAMPLE_TEXTURE2D(_GrassTex, sampler_GrassTex, duv).g;
                    half d = SAMPLE_TEXTURE2D(_DirtTex,  sampler_DirtTex,  duv).g;
                    half s = SAMPLE_TEXTURE2D(_SandTex,  sampler_SandTex,  duv).g;

                    half w2 = mask.r + mask.g + mask.b;
                    half detail = 0.5h;
                    if (w2 > 0.001h) detail = (g * mask.r + d * mask.g + s * mask.b) / w2;

                    half amt = saturate(w2) * mask.a * _DetailStrength;
                    half f = lerp(1.0h, detail * 2.0h, amt);
                    albedo = base.rgb * f;
                }

                // ── 빛 (해 + 그림자 + 주변광). 땅은 완전 평지라 이만큼이면 충분하다
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half3 n2 = normalize(IN.normalWS);
                half ndl = saturate(dot(n2, mainLight.direction));
                half3 lit = mainLight.color * (ndl * mainLight.shadowAttenuation);
                half3 ambient = SampleSH(n2);

                half3 col2 = albedo * (lit + ambient);
                col2 = MixFog(col2, IN.fogCoord);
                return half4(col2, 1.0h);
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
