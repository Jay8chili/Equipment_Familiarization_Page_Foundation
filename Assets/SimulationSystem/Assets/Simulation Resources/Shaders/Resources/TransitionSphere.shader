Shader "Custom/TransitionSphere"
{
    Properties
    {
        _FillLevel  ("Fill Level",    Range(0, 1)) = 0
        _FillColor  ("Fill Color",    Color)       = (0, 0, 0, 1)
        _EdgeSoftness ("Edge Softness", Range(0, 0.25)) = 0.06
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Overlay+100"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Cull   Front                        // render inner faces only
        ZWrite Off
        ZTest  Always                       // always on top of scene
        Blend  SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "TransitionFill"

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ── per-material ────────────────────────────────────────────
            CBUFFER_START(UnityPerMaterial)
                float  _FillLevel;
                float4 _FillColor;
                float  _EdgeSoftness;
            CBUFFER_END

            // ── structs ─────────────────────────────────────────────────
            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float  normalizedY : TEXCOORD0;   // 0 = bottom, 1 = top
            };

            // ── vertex ──────────────────────────────────────────────────
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);

                // Unity sphere primitive Y range is [-0.5, 0.5]
                OUT.normalizedY = IN.positionOS.y + 0.5;
                return OUT;
            }

            // ── fragment ────────────────────────────────────────────────
            half4 frag(Varyings IN) : SV_Target
            {
                // Expand fill edge so 0 = fully clear, 1 = fully filled
                float edge = _FillLevel * (1.0 + _EdgeSoftness * 2.0)
                           - _EdgeSoftness;

                // Below edge → opaque, above → transparent, smooth border
                float alpha = 1.0 - smoothstep(edge - _EdgeSoftness,
                                               edge + _EdgeSoftness,
                                               IN.normalizedY);

                return half4(_FillColor.rgb, _FillColor.a * alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
