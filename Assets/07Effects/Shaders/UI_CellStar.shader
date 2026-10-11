// 별 칸 효과(UI용): 칸 테두리의 안쪽으로 번지는 밝은 빛(가짜 블룸) + 칸 전체를 가로지르는 쓸림광(빛 띠).
// 칸 이미지 위에 같은 칸 스프라이트로 겹쳐 쓴다(_MainTex의 알파가 칸 모양). 칸 밖으로는 번지지 않는다.
// Image.color.r = 칸마다 다른 쓸림 타이밍 시드, Image.color.a = 전체 밝기.
Shader "UI/CellStar"
{
    Properties
    {
        [HideInInspector] _MainTex ("Cell Sprite", 2D) = "white" {}
        [HideInInspector] _Color ("Tint (r = per-cell seed, a = master)", Color) = (1,1,1,1)

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
        Blend SrcAlpha One
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

            fixed4 _RimColor, _SweepColor;
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
                float seed = IN.color.r;
                float center = cellAlpha(uv);

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
                float pos = lerp(-1.1, 1.1, frac(_Time.y * _SweepSpeed + seed));
                float band = exp(-pow((t - pos) / _SweepWidth, 2.0));

                float3 rgb = _RimColor.rgb * rim * _RimIntensity * (1.0 + _SweepRimBoost * band)
                           + _SweepColor.rgb * band * _SweepStrength * center
                           + _RimColor.rgb * _FillGlow * center;
                fixed4 color = fixed4(rgb, IN.color.a); // 더하기 합성: rgb * a

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
