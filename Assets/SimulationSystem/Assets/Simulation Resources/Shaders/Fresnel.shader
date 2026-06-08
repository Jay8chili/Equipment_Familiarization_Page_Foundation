// Custom/Fresnel — URP HLSL
// ─────────────────────────
// Additive transparent Fresnel rim shader.
// Intended as a highlight / interaction material that is swapped onto
// renderers at runtime (e.g. GrabInteraction.SetHighlightShader).
//
// FEATURES
//   • Fresnel rim with configurable power and HDR colour.
//   • Soft body fill colour (alpha-blended under the rim).
//   • Sine-wave pulse applied to rim brightness.
//   • SRP Batcher compatible (single CBUFFER).
//   • Works in VR (single-pass instanced via UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX).

Shader "Custom/Fresnel"
{
    Properties
    {
        // ── Body fill ────────────────────────────────────────────────────────
        [Header(Body)]
        [HDR] _BodyColor        ("Body Color",          Color)        = (0.2, 0.8, 1.0, 0.08)

        // ── Fresnel rim ──────────────────────────────────────────────────────
        [Header(Fresnel Rim)]
        [HDR] _FresnelColor     ("Fresnel Color",       Color)        = (0.2, 0.8, 1.0, 1.0)
        [PowerSlider(2)]
        _FresnelPower           ("Fresnel Power",       Range(0.5, 8))= 2.5
        _FresnelStrength        ("Fresnel Strength",    Range(0, 6))  = 2.0

        // ── Pulse ────────────────────────────────────────────────────────────
        [Header(Pulse)]
        _PulseSpeed             ("Pulse Speed",         Range(0, 10)) = 2.5
        _PulseAmount            ("Pulse Amount",        Range(0,  1)) = 0.25
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Transparent+1"
        }

        Pass
        {
            Name "Fresnel"
            Tags { "LightMode" = "UniversalForward" }

            // Additive blend — glows on top of the underlying geometry
            Blend  SrcAlpha One
            ZWrite Off
            ZTest  LEqual
            Cull   Back

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            // Single-pass instanced stereo (Quest / PC VR)
            #pragma multi_compile_instancing
            #pragma multi_compile _ UNITY_SINGLE_PASS_STEREO STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ── Uniform buffer (SRP Batcher) ─────────────────────────────────
            CBUFFER_START(UnityPerMaterial)
                float4 _BodyColor;
                float4 _FresnelColor;
                float  _FresnelPower;
                float  _FresnelStrength;
                float  _PulseSpeed;
                float  _PulseAmount;
            CBUFFER_END

            // ── Vertex input / output ────────────────────────────────────────
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ── Vertex shader ────────────────────────────────────────────────
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionWS  = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            // ── Fragment shader ──────────────────────────────────────────────
            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                // View direction and NdotV
                float3 viewDir = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float  NdotV   = saturate(dot(normalize(IN.normalWS), viewDir));

                // Fresnel: bright at grazing angles (NdotV ≈ 0), dark at centre
                float fresnel  = pow(1.0 - NdotV, _FresnelPower);

                // Sine-wave pulse — drives rim brightness only
                float pulse    = 1.0 + _PulseAmount * sin(_Time.y * _PulseSpeed);

                // Rim contribution
                float  rimMask = saturate(fresnel * _FresnelStrength * pulse);
                float3 rimCol  = _FresnelColor.rgb * rimMask;

                // Soft body fill (fades toward the centre of the mesh)
                float  bodyMask = _BodyColor.a * (1.0 - NdotV * NdotV);

                // Combine — additive blend handles the final "glow" look
                float3 finalCol   = rimCol + _BodyColor.rgb * bodyMask;
                float  finalAlpha = saturate(rimMask + bodyMask);

                return half4(finalCol, finalAlpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
