// 숨쉬기 효과(UI용): 이미지가 앵커(RectTransform의 Pivot)를 기준으로 서서히 부풀었다가 원래 크기로 돌아오기를 되풀이한다(캐릭터가 숨쉬는 것처럼).
// 이 머티리얼을 캐릭터 Image에 쓰고, 같은 오브젝트에 BreathingAnchor 컴포넌트를 붙인다. 앵커는 Pivot이라서 몸통 아래(예: 아래 가운데)에 두면 발은 가만히 있고 윗몸만 부푼다.
// UI 정점은 CPU에서 한데 모여 오브젝트마다의 Pivot을 알 수 없어서, BreathingAnchor가 "앵커에서 그 정점까지의 거리"를 정점마다 TEXCOORD1에 넣어 준다. 이 셰이더는 그 거리에 부푸는 정도를 곱해 정점을 밀어낸다.
// 부푸는 정도는 앵커에서 멀수록 크다(머리 쪽이 더 움직인다). 모양이 같은 기본 UI 셰이더(UI/Default)의 마스크·클리핑을 그대로 지원한다.
Shader "UI/Breathing"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Breathing)]
        _Speed ("Breaths Per Second", Range(0.05, 2)) = 0.3
        _AmountX ("Swell X (fraction of distance from anchor)", Range(0, 0.1)) = 0.008
        _AmountY ("Swell Y (fraction of distance from anchor)", Range(0, 0.1)) = 0.02
        _Phase ("Phase Offset (0~1, desync characters)", Range(0, 1)) = 0

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [HideInInspector] _TextureSampleAdd ("Texture Sample Add", Vector) = (0,0,0,0)
        [HideInInspector] [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }

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
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float2 anchorOffset : TEXCOORD1; // BreathingAnchor가 넣는, 앵커에서 이 정점까지의 거리(캔버스 단위)
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
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float _Speed, _AmountX, _AmountY, _Phase;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                // 0(원래 크기) ~ 1(가장 부푼 크기)을 부드럽게 오간다
                float breath = 0.5 + 0.5 * sin(6.2831853 * (_Time.y * _Speed + _Phase));
                float4 pos = v.vertex;
                pos.xy += v.anchorOffset * float2(_AmountX, _AmountY) * breath;

                OUT.worldPosition = pos;
                OUT.vertex = UnityObjectToClipPos(pos);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

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
