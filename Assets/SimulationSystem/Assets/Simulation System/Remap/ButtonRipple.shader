Shader "Custom/UI/ButtonRipple"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color          ("Tint",              Color) = (1, 1, 1, 1)
        _RippleColor    ("Ripple Color",      Color) = (0.72, 0.82, 0.96, 0.8)
        _RippleCenter   ("Ripple Center (UV)",Vector)= (0.5, 0.5, 0, 0)
        _RippleRadius   ("Ripple Radius",     Float) = 0.0
        _RippleWidth    ("Ring Width",        Float) = 0.15
        _RippleSoft     ("Edge Softness",     Float) = 0.08
        _FillAlpha      ("Fill Behind Ripple", Range(0,1)) = 0.0
        _FillColor      ("Fill Color",        Color) = (0.85, 0.78, 0.95, 0.4)

        // ── Unity UI required ────────────────────────────────────────────
        _StencilComp    ("Stencil Comparison", Float) = 8
        _Stencil        ("Stencil ID",         Float) = 0
        _StencilOp      ("Stencil Operation",  Float) = 0
        _StencilWriteMask("Stencil Write Mask",Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask       ("Color Mask",        Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType"      = "Transparent"
            "PreviewType"     = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref   [_Stencil]
            Comp  [_StencilComp]
            Pass  [_StencilOp]
            ReadMask  [_StencilReadMask]
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

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos        : SV_POSITION;
                fixed4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                float4 worldPos   : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4    _Color;
            fixed4    _TextureSampleAdd;   // Unity UI text support
            float4    _ClipRect;           // Unity UI rect clipping

            fixed4    _RippleColor;
            float4    _RippleCenter;
            float     _RippleRadius;
            float     _RippleWidth;
            float     _RippleSoft;
            float     _FillAlpha;
            fixed4    _FillColor;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.pos      = UnityObjectToClipPos(v.vertex);
                o.worldPos = v.vertex;
                o.uv       = v.uv;
                o.color    = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Base image + tint + vertex color (Unity UI standard)
                fixed4 base = (tex2D(_MainTex, i.uv) + _TextureSampleAdd) * i.color;

                // Distance from ripple center in UV space
                float dist = length(i.uv - _RippleCenter.xy);

                // ── Ripple ring ──────────────────────────────────
                float outerEdge = _RippleRadius;
                float innerEdge = _RippleRadius - _RippleWidth;

                float outer = 1.0 - smoothstep(outerEdge - _RippleSoft, outerEdge, dist);
                float inner = smoothstep(innerEdge - _RippleSoft, innerEdge + _RippleSoft, dist);
                float ring  = outer * inner;

                // Fade as ring expands
                float fade = saturate(1.0 - _RippleRadius * 0.6);

                // ── Fill behind ripple ───────────────────────────
                float fill = (1.0 - smoothstep(innerEdge - _RippleSoft, innerEdge, dist)) * _FillAlpha;

                // ── Composite ────────────────────────────────────
                fixed4 col = base;

                // Fill underneath
                col.rgb = lerp(col.rgb, _FillColor.rgb, fill * _FillColor.a);
                col.a   = max(col.a, fill * _FillColor.a);

                // Ring on top
                float ringAlpha = ring * fade * _RippleColor.a;
                col.rgb = lerp(col.rgb, _RippleColor.rgb, ringAlpha);
                col.a   = max(col.a, ringAlpha);

                // ── Unity UI clipping ────────────────────────────
                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(i.worldPos.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDCG
        }
    }
}
