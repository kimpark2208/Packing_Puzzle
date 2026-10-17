// 꽃잎 그림(_MainTex)의 바깥 테두리만 그리는 UI 셰이더(그림 안쪽은 비운다). 꽃잎 자리를 흰 테두리로 깜빡이게 보여 줄 때 쓴다.
// 이 이미지는 꽃잎 이미지보다 1.4배(셰이더 안의 상수 _PadScale) 크게 두고, 꽃잎과 같은 스프라이트를 쓴다. 테두리 색은 Image.color(알파로 깜빡임).
Shader "UI/PetalOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Petal Sprite", 2D) = "white" {}
        _Color ("Outline Color", Color) = (1,1,1,1)
        _Thickness ("Thickness (sprite size ratio)", Range(0.005, 0.08)) = 0.025

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
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
            float _Thickness;

            // 이 이미지는 꽃잎 이미지보다 이 배수만큼 크게 둔다(ScatterPuzzlePanel의 OutlinePadScale과 같아야 한다).
            static const float _PadScale = 1.4;

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

            // 꽃잎 그림의 알파(그림 밖은 0)
            float petalAlpha(float2 p)
            {
                float inside = step(0.0, p.x) * step(p.x, 1.0) * step(0.0, p.y) * step(p.y, 1.0);
                return tex2Dlod(_MainTex, float4(p, 0, 0)).a * inside;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 su = (IN.texcoord - 0.5) * _PadScale + 0.5; // 꽃잎 그림 좌표로 되돌린다

                // 둘레의 알파 중 가장 큰 값(두 겹 고리, 16방향)을 구하고, 그림 자신의 알파를 빼면 그림 바깥의 테두리만 남는다
                float own = petalAlpha(su);
                float around = 0.0;
                [unroll] for (int k = 0; k < 16; k++)
                {
                    float a = k * 0.392699;
                    float2 dir = float2(cos(a), sin(a));
                    around = max(around, petalAlpha(su + _Thickness * dir));
                    around = max(around, petalAlpha(su + _Thickness * 0.5 * dir));
                }
                float edge = saturate(around - own);

                fixed4 color = fixed4(IN.color.rgb, edge * IN.color.a);
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
