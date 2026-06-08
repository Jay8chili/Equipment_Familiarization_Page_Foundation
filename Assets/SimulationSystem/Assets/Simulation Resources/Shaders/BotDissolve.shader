// URP Dissolve Shader
// Uses a noise texture to clip pixels progressively.
// _DissolveAmount: 0 = fully visible, 1 = fully dissolved.
// An emissive edge glow appears at the dissolve boundary.

Shader "Bot/Dissolve"
{
    Properties
    {
        _BaseMap        ("Albedo",           2D)    = "white" {}
        _BaseColor      ("Base Color",       Color) = (1,1,1,1)
        _NoiseMap       ("Dissolve Noise",   2D)    = "white" {}
        _DissolveAmount ("Dissolve Amount",  Range(0,1)) = 0
        _EdgeWidth      ("Edge Glow Width",  Range(0,0.2)) = 0.04
        _EdgeColor      ("Edge Glow Color",  Color) = (0.3, 0.8, 1.0, 1)
        _EdgeIntensity  ("Edge Intensity",   Range(1, 20)) = 6
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Opaque"
            "RenderPipeline"  = "UniversalPipeline"
            "Queue"           = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ── Textures ──────────────────────────────────────
            TEXTURE2D(_BaseMap);   SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NoiseMap);  SAMPLER(sampler_NoiseMap);

            // ── Uniforms ──────────────────────────────────────
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _NoiseMap_ST;
                float4 _BaseColor;
                float4 _EdgeColor;
                float  _DissolveAmount;
                float  _EdgeWidth;
                float  _EdgeIntensity;
            CBUFFER_END

            // ── Vertex ────────────────────────────────────────
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float2 noiseUV     : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float3 positionWS  : TEXCOORD3;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionWS  = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.noiseUV     = TRANSFORM_TEX(IN.uv, _NoiseMap);
                return OUT;
            }

            // ── Fragment ──────────────────────────────────────
            half4 frag(Varyings IN) : SV_Target
            {
                // Sample dissolve noise (use R channel)
                float noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, IN.noiseUV).r;

                // Clip pixels whose noise value is below the dissolve threshold
                float dissolveEdge = _DissolveAmount;
                clip(noise - dissolveEdge);

                // Edge glow: pixels just above the clip threshold get emissive colour
                float edgeFactor = 1.0 - saturate((noise - dissolveEdge) / _EdgeWidth);
                float3 edgeGlow  = _EdgeColor.rgb * edgeFactor * _EdgeIntensity;

                // Simple Lambert + ambient lighting
                float4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;

                InputData  inputData  = (InputData)0;
                inputData.positionWS  = IN.positionWS;
                inputData.normalWS    = normalize(IN.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                inputData.shadowCoord = 0;
                inputData.fogCoord    = 0;
                inputData.bakedGI     = SampleSHPixel(inputData.normalWS, inputData.positionWS);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo      = baseColor.rgb;
                surfaceData.alpha       = baseColor.a;
                surfaceData.emission    = edgeGlow;
                surfaceData.smoothness  = 0.3;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                return color;
            }
            ENDHLSL
        }

        // Shadow caster — respects the same dissolve clip so shadows dissolve too
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex   shadowVert
            #pragma fragment shadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _NoiseMap_ST;
                float4 _BaseColor;
                float4 _EdgeColor;
                float  _DissolveAmount;
                float  _EdgeWidth;
                float  _EdgeIntensity;
            CBUFFER_END

            struct ShadowAttributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct ShadowVaryings   { float4 positionHCS : SV_POSITION; float2 noiseUV : TEXCOORD0; };

            ShadowVaryings shadowVert(ShadowAttributes IN)
            {
                ShadowVaryings OUT;
                float3 posWS  = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionHCS = TransformWorldToHClip(ApplyShadowBias(posWS, normWS, _MainLightPosition.xyz));
                OUT.noiseUV     = TRANSFORM_TEX(IN.uv, _NoiseMap);
                return OUT;
            }

            half4 shadowFrag(ShadowVaryings IN) : SV_Target
            {
                float noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, IN.noiseUV).r;
                clip(noise - _DissolveAmount);
                return 0;
            }
            ENDHLSL
        }
    }
}
