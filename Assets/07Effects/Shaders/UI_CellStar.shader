// 별 칸 효과(UI용): 칸 테두리의 안쪽으로 번지는 밝은 빛(가짜 블룸) + 칸 전체를 가로지르는 쓸림광(빛 띠).
// 칸 이미지에 이 머티리얼을 직접 쓴다. 칸의 모양은 스프라이트 알파(_MainTex)다. 칸 밖으로는 번지지 않는다.
// 색은 "바탕색(Cell Color) → 원래 칸 색" 순으로 정한다: 바탕색의 알파가 낮을수록 원래 칸 색(스프라이트 × Image.color)이 비쳐 섞인다. 알파 0이면 원래 칸 색 그대로다.
// 그 위에 림과 쓸림 효과를 더하기로 합성한다(밝을수록 더 빛난다. Image.color의 알파와 상관없이 같은 밝기로 더해진다).
Shader "UI/CellStar"
{
    Properties
    {
        [HideInInspector] _MainTex ("Cell Sprite", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)

        [Header(Cell)]
        _CellColor ("Cell Color (alpha = cover over original cell color)", Color) = (1, 1, 1, 0)

        [Header(Rim Glow)]
        _RimColor ("Rim Color", Color) = (1, 0.9, 0.5, 1)
        _RimWidth ("Rim Width (cell uv)", Range(0.01, 0.3)) = 0.06
        _RimIntensity ("Rim Intensity", Range(0, 6)) = 2.2
        _RimPower ("Rim Falloff (higher = tighter)", Range(0.3, 4)) = 1.4
        _FillGlow ("Inner Fill Brightening", Range(0, 1)) = 0.12

        [Header(Sweep Light)]
        _SweepColor ("Sweep Color", Color) = (1, 0.96, 0.8, 1)
        _SweepAngle ("Sweep Direction (degrees)", Range(0, 360)) = 35
        _SweepSpeed ("Sweep Speed (cycles per sec)", Float) = 0.45
        _SweepWidth ("Sweep Width", Range(0.02, 0.5)) = 0.1
        _SweepStrength ("Sweep Strength", Range(0, 2)) = 0.7
        _SweepRimBoost ("Rim Boost While Sweeping", Range(0, 4)) = 1.6

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [HideInInspector] [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="False" }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha // 미리 곱한 알파: 칸 색은 알파로 덮고, 효과는 알파 없이 더해진다
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _ClipRect;
            float4 _MainTex_ST;

            fixed4 _CellColor, _RimColor, _SweepColor;
            float _RimWidth, _RimIntensity, _RimPower, _FillGlow;
            float _SweepAngle, _SweepSpeed, _SweepWidth, _SweepStrength, _SweepRimBoost;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            float cellAlpha(float2 p)
            {
                float inside = step(0.0, p.x) * step(p.x, 1.0) * step(0.0, p.y) * step(p.y, 1.0);
                return tex2Dlod(_MainTex, float4(p, 0, 0)).a * inside;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                fixed4 tex = tex2D(_MainTex, uv);
                fixed4 cell = tex * IN.color;     // 원래 칸의 색과 투명도(태그 색 × 스프라이트)
                float center = tex.a;             // 칸 모양

                // 테두리까지의 가까움: 주변(2겹 고리)의 알파 평균이 낮을수록 가장자리다
                float acc = center;
                float wsum = 1.0;
                [unroll] for (int ring = 1; ring <= 2; ring++)
                {
                    float radius = _RimWidth * ring * 0.5;
                    float off = ring * 0.39;
                    [unroll] for (int k = 0; k < 8; k++)
                    {
                        float a = k * 0.7854 + off;
                        acc += cellAlpha(uv + radius * float2(cos(a), sin(a)));
                        wsum += 1.0;
                    }
                }
                float edge = saturate(1.0 - acc / wsum) * center;      // 칸 안쪽 가장자리일수록 큼
                float rim = pow(saturate(edge * 2.2), _RimPower);

                // 쓸림광: 칸 전체를 한 방향으로 가로지르는 빛 띠(주기 사이에 쉬는 시간이 있다)
                float rad = radians(_SweepAngle);
                float2 dir = float2(cos(rad), sin(rad));
                float t = dot(uv - 0.5, dir);
                float pos = lerp(-1.1, 1.1, frac(_Time.y * _SweepSpeed));
                float band = exp(-pow((t - pos) / _SweepWidth, 2.0));

                float3 effect = _RimColor.rgb * rim * _RimIntensity * (1.0 + _SweepRimBoost * band)
                              + _SweepColor.rgb * band * _SweepStrength * center
                              + _RimColor.rgb * _FillGlow * center;

                // 바탕색을 원래 칸 색 위에 덮는다(바탕색의 알파가 낮을수록 원래 칸 색이 비친다). 값은 미리 곱한 형태다.
                float coverA = _CellColor.a * center;
                float baseA = coverA + cell.a * (1.0 - coverA);
                float3 basePremult = _CellColor.rgb * coverA + cell.rgb * cell.a * (1.0 - coverA);
                // 효과는 알파와 상관없이 그 위에 더해진다.
                fixed4 color = fixed4(basePremult + effect, baseA);

                #ifdef UNITY_UI_CLIP_RECT
                color *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(max(color.a, max(color.r, max(color.g, color.b))) - 0.001);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
