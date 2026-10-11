// 그늘 칸 효과(UI용): 칸 이미지에 이 머티리얼을 직접 쓴다. 보로노이 칸 사이의 결(베인)이 펄린 노이즈로 자연스럽게 일렁이며 화염처럼 위로 흐른다.
// 색은 "바탕색 + 베인색"으로 정해서 칸의 원래 색 위에 덮는다: 바탕색(또는 베인색)의 알파가 낮을수록 원래 칸 색이 비쳐 섞인다.
// 칸의 모양은 칸 스프라이트의 알파(_MainTex)이고, 원래 칸 색은 스프라이트 × Image.color(태그 색과 투명도)다.
Shader "UI/CellShade"
{
    Properties
    {
        [HideInInspector] _MainTex ("Cell Sprite", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)

        [Header(Colors)]
        _BaseColor ("Base Color (alpha = base opacity)", Color) = (0.12, 0.07, 0.16, 0.8)
        _VeinColor ("Vein Color (alpha = vein opacity)", Color) = (0.78, 0.5, 0.9, 1)

        [Header(Veins)]
        _VeinIntensity ("Vein Intensity", Range(0, 3)) = 1.3
        _VeinWidth ("Vein Width", Range(0.02, 1)) = 0.16
        _VeinSoftness ("Vein Edge Softness (0 = hard)", Range(0, 1)) = 0.8
        _CellSize ("Voronoi Cell Size", Range(0.05, 1)) = 0.3
        _Speed ("Speed", Range(0, 4)) = 0.6

        [Header(Perlin Noise)]
        _NoiseScale ("Noise Scale", Range(0.5, 8)) = 2.5
        _NoiseWarp ("Noise Warp Strength", Range(0, 1.5)) = 0.45

        [Header(Rim Shadow)]
        _RimShadowColor ("Rim Shadow Color", Color) = (0, 0, 0, 1)
        _RimShadowIntensity ("Rim Shadow Intensity", Range(0, 2)) = 0.6
        _RimShadowWidth ("Rim Shadow Width (cell uv)", Range(0.01, 0.4)) = 0.12
        _RimShadowBlur ("Rim Shadow Blur (0 = hard)", Range(0, 1)) = 0.7

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
        Blend SrcAlpha OneMinusSrcAlpha
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

            fixed4 _BaseColor, _VeinColor;
            float _VeinIntensity, _VeinWidth, _VeinSoftness, _CellSize, _Speed;
            float _NoiseScale, _NoiseWarp;
            fixed4 _RimShadowColor;
            float _RimShadowIntensity, _RimShadowWidth, _RimShadowBlur;

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

            float2 hash22(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            // 펄린(그래디언트) 노이즈: -1 ~ 1 근처 값. 격자점마다 무작위 방향의 기울기를 두고 부드럽게 섞는다.
            float2 gradientAt(float2 cell)
            {
                float angle = hash22(cell).x * 6.2831853;
                return float2(cos(angle), sin(angle));
            }

            float perlin(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0); // 부드러운 보간(quintic)
                float a = dot(gradientAt(i), f);
                float b = dot(gradientAt(i + float2(1, 0)), f - float2(1, 0));
                float c = dot(gradientAt(i + float2(0, 1)), f - float2(0, 1));
                float d = dot(gradientAt(i + float2(1, 1)), f - float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y) * 1.4142; // 대략 -1~1
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
                fixed4 cell = tex * IN.color;                 // 원래 칸의 색과 투명도(태그 색 × 스프라이트)
                float shape = tex.a;                          // 칸 모양
                float t = _Time.y * _Speed;

                // 펄린 노이즈로 좌표를 일렁이게 비튼다(두 갈래가 서로 다른 방향으로 천천히 움직인다)
                float2 q = uv * _NoiseScale;
                float2 warp = float2(perlin(q + float2(0.0, -t * 0.5) + 3.1),
                                     perlin(q + float2(t * 0.35, 0.0) + 17.7));

                // 보로노이: 가장 가까운 점(F1)과 두 번째로 가까운 점(F2)까지의 거리. 점이 일렁이고 무늬 전체가 위로 흐른다(화염처럼).
                float2 p = (uv + warp * _NoiseWarp * _CellSize) / _CellSize;
                p.y -= t * 0.45;
                float2 ip = floor(p);
                float2 fp = frac(p);
                float d1 = 8.0, d2 = 8.0;
                [unroll] for (int y = -1; y <= 1; y++)
                {
                    [unroll] for (int x = -1; x <= 1; x++)
                    {
                        float2 g = float2(x, y);
                        float2 h = hash22(ip + g);
                        float2 o = 0.5 + 0.5 * sin(t * 1.2 + 6.2831 * h);
                        float2 r = g + o - fp;
                        float d = dot(r, r);
                        if (d < d1) { d2 = d1; d1 = d; }
                        else if (d < d2) { d2 = d; }
                    }
                }
                float ridge = sqrt(d2) - sqrt(d1); // 칸 경계에서 0, 칸 안쪽으로 갈수록 커진다

                // 결: 경계에서 1, _VeinWidth만큼 떨어지면 0. smoothstep의 안쪽 끝을 _VeinSoftness로 정한다(0이면 칼날처럼 또렷, 1이면 가운데부터 부드럽게).
                float vein = 1.0 - smoothstep(_VeinWidth * (1.0 - _VeinSoftness), _VeinWidth, ridge);

                // 결이 타오르듯 깜빡이는 정도를 펄린 노이즈로 정한다. 아래쪽 결이 더 밝고 위로 갈수록 사그라든다.
                float flicker = saturate(0.7 + 0.6 * perlin(p * 0.7 + float2(0.0, -t * 1.2) + 41.3));
                float rise = lerp(1.0, 0.45, saturate(uv.y));
                float veinMix = saturate(vein * flicker * rise * _VeinIntensity);

                // 효과 색 = 바탕색 → 베인색. 효과의 알파는 바탕 알파 → 베인 알파로 섞고, 칸 모양으로 자른다.
                float3 effectRgb = lerp(_BaseColor.rgb, _VeinColor.rgb, veinMix);
                float effectA = lerp(_BaseColor.a, _VeinColor.a, veinMix) * shape;

                // 원래 칸 색 위에 효과를 덮는다: 효과 알파가 낮을수록 원래 칸 색이 비친다.
                float outA = effectA + cell.a * (1.0 - effectA);
                float3 outRgb = (effectRgb * effectA + cell.rgb * cell.a * (1.0 - effectA)) / max(outA, 0.0001);

                // 림 그림자: 칸 안쪽 가장자리를 어둡게 덮는다. 주변(2겹 고리)의 알파 평균이 낮을수록 가장자리다.
                float acc = shape;
                float wsum = 1.0;
                [unroll] for (int ring = 1; ring <= 2; ring++)
                {
                    float radius = _RimShadowWidth * ring * 0.5;
                    float off = ring * 0.39;
                    [unroll] for (int k = 0; k < 8; k++)
                    {
                        float a = k * 0.7854 + off;
                        acc += cellAlpha(uv + radius * float2(cos(a), sin(a)));
                        wsum += 1.0;
                    }
                }
                float edge = saturate(1.0 - acc / wsum) * shape;                       // 안쪽 가장자리일수록 큼(경계에서 약 0.5)
                float rim = smoothstep(0.0, lerp(0.03, 0.5, _RimShadowBlur), edge);    // 블러가 클수록 안쪽으로 부드럽게 옅어진다
                float rimMix = saturate(rim * _RimShadowIntensity * _RimShadowColor.a);
                outRgb = lerp(outRgb, _RimShadowColor.rgb, rimMix);
                outA = outA + rimMix * shape * (1.0 - outA);
                fixed4 color = fixed4(outRgb, outA);

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
