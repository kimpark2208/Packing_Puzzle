// 빛남 상태의 꽃 뒤에 깔리는 후광(UI용). 꽃 그림(_MainTex)의 알파를 셰이더가 직접 번지게 해서 그린다(따로 만들 마스크 이미지가 없다).
// 이 이미지는 꽃 이미지보다 1.6배(셰이더 안의 상수 _PadScale) 크게 두고, 꽃과 같은 스프라이트를 쓴다. Image.color.r = 칸마다 다른 맥박 시드, Image.color.a = 전체 밝기.
Shader "UI/FlowerGlow"
{
    Properties
    {
        [HideInInspector] _MainTex ("Flower Sprite", 2D) = "white" {}
        [HideInInspector] _Color ("Tint (r = per-cell seed, a = master)", Color) = (1,1,1,1)

        _GlowColor ("Glow Color", Color) = (1, 0.88, 0.45, 1)
        _GlowIntensity ("Glow Intensity", Range(0, 6)) = 2.4
        _GlowPower ("Glow Falloff (higher = tighter)", Range(0.3, 4)) = 1.3
        _BlurRadius ("Blur Radius (flower size ratio)", Range(0.02, 0.3)) = 0.11
        _PulseSpeed ("Pulse Speed", Float) = 2.2
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.22

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
        Blend SrcAlpha One // 더해서 밝히는 후광
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

            fixed4 _GlowColor;
            float _GlowIntensity, _GlowPower, _BlurRadius, _PulseSpeed, _PulseAmount;

            // 글로우 이미지는 꽃 이미지보다 이 배수만큼 크게 둔다(씬의 GlowArea 이미지 크기와 같아야 한다). 인스펙터에서 바꿀 수 없게 상수로 둔다.
            static const float _PadScale = 1.6;

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

            // 꽃 그림의 알파(그림 밖은 0)
            float flowerAlpha(float2 p)
            {
                float inside = step(0.0, p.x) * step(p.x, 1.0) * step(0.0, p.y) * step(p.y, 1.0);
                return tex2Dlod(_MainTex, float4(p, 0, 0)).a * inside;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // 이 이미지는 꽃 이미지보다 _PadScale배 크므로, 꽃 그림 좌표로 되돌린다
                float2 su = (IN.texcoord - 0.5) * _PadScale + 0.5;
                float seed = IN.color.r;

// 가운데 + 4겹 고리(6방향씩)로 알파를 모아 번지게 한다. 픽셀마다 고리를 무작위로 돌려서(노이즈) 여섯 갈래 모양이 보이지 않게 한다.
                float acc = flowerAlpha(su);
                float wsum = 1.0;
                float jitter = frac(52.9829189 * frac(dot(IN.vertex.xy, float2(0.06711056, 0.00583715)))) * 6.2831;
                [unroll] for (int ring = 1; ring <= 4; ring++)
                {
                    float radius = _BlurRadius * ring;
                    float w = exp(-0.2 * ring * ring);
                    float off = jitter + ring * 0.7;
                    [unroll] for (int k = 0; k < 6; k++)
                    {
                        float a = k * 1.0472 + off;
                        acc += w * flowerAlpha(su + radius * float2(cos(a), sin(a)));
                        wsum += w;
                    }
                }

                float halo = pow(saturate(acc / wsum), _GlowPower) * _GlowIntensity;
                float pulse = 1.0 + _PulseAmount * sin(_Time.y * _PulseSpeed + seed * 6.2831);
                fixed4 color = fixed4(_GlowColor.rgb, saturate(halo * pulse) * IN.color.a);

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
