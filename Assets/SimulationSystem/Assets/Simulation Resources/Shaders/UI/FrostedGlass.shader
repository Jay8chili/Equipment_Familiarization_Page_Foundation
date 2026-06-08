Shader "8chili/FrostedGlass"
{
    Properties
    {
        [Header(Frost)]
        _FrostScale      ("Frost Noise Scale", Range(1, 200)) = 60.0
        _FrostStrength   ("Frost Distortion", Range(0, 0.08)) = 0.035
        _BrightBoost     ("Bright Accentuation", Range(0, 3)) = 1.2
        _BrightThreshold ("Bright Threshold", Range(0, 1)) = 0.4
        _Saturation      ("Color Saturation", Range(0, 2)) = 1.3
        _CornerRadius    ("Corner Radius (px)", Range(0, 200)) = 16.0
        _Size            ("Quad Size (px)", Vector) = (400, 300, 0, 0)

        [Header(Tint Solid)]
        _Tint            ("Tint Color", Color) = (0, 0, 0, 0.55)

        [Header(Tint Gradient)]
        [Toggle(_GRADIENT_ON)] _UseGradient ("Use Gradient", Float) = 0
        _GradientColorA  ("Gradient Start", Color) = (0, 0, 0, 0.7)
        _GradientColorB  ("Gradient End", Color) = (0.06, 0.11, 0.16, 0.4)
        _GradientAngle   ("Gradient Angle", Range(0, 360)) = 135
        _GradientOffset  ("Gradient Offset", Range(-1, 1)) = 0
        _GradientScale   ("Gradient Spread", Range(0.1, 5)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"  = "UniversalPipeline"
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "FrostedGlass"

            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma shader_feature_local _GRADIENT_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BlurredSceneTex);
            SAMPLER(sampler_BlurredSceneTex);

            CBUFFER_START(UnityPerMaterial)
                half4  _Tint;
                half   _FrostScale;
                half   _FrostStrength;
                half   _BrightBoost;
                half   _BrightThreshold;
                half   _Saturation;
                half   _CornerRadius;
                half4  _Size;
                half4  _GradientColorA;
                half4  _GradientColorB;
                half   _GradientAngle;
                half   _GradientOffset;
                half   _GradientScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 screenPos   : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vpi = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vpi.positionCS;
                output.screenPos  = ComputeScreenPos(vpi.positionCS);
                output.uv         = input.uv;
                return output;
            }

            half hash21(half2 p)
            {
                p = frac(p * half2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            half valueNoise(half2 p)
            {
                half2 i = floor(p);
                half2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                half a = hash21(i);
                half b = hash21(i + half2(1, 0));
                half c = hash21(i + half2(0, 1));
                half d = hash21(i + half2(1, 1));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            half luminance(half3 c)
            {
                return dot(c, half3(0.2126, 0.7152, 0.0722));
            }

            half roundedRectSDF(half2 pixelPos, half2 size, half radius)
            {
                half2 d = abs(pixelPos - size * 0.5) - size * 0.5 + radius;
                return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0) - radius;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half2 uv = input.uv;

                // Pixel-space SDF for corner rounding
                half2 pixelPos = uv * _Size.xy;
                half dist = roundedRectSDF(pixelPos, _Size.xy, _CornerRadius);
                half mask = 1.0 - smoothstep(-0.5, 0.5, dist);
                clip(mask - 0.01);

                // Frost noise distortion
                half2 noiseUV = uv * _FrostScale;
                half2 frostOffset = half2(
                    valueNoise(noiseUV) - 0.5,
                    valueNoise(noiseUV + 100.0) - 0.5
                ) * _FrostStrength;

                // Screen UV with frost distortion
                half2 screenUV = input.screenPos.xy / input.screenPos.w;
                screenUV += frostOffset;

                // Single texture read from pre-blurred RT
                half3 blurred = SAMPLE_TEXTURE2D(_BlurredSceneTex, sampler_BlurredSceneTex, screenUV).rgb;

                // Bright accentuation
                half lum = luminance(blurred);
                half brightFactor = smoothstep(_BrightThreshold, 1.0, lum);
                blurred *= 1.0 + brightFactor * _BrightBoost;

                // Saturation boost
                half grey = luminance(blurred);
                half3 saturated = lerp(half3(grey, grey, grey), blurred, _Saturation);

                // Tint — gradient first, solid tint on top
                half3 glassColor;

                #ifdef _GRADIENT_ON
                    half rad = _GradientAngle * 0.01745329;
                    half2 dir = half2(cos(rad), sin(rad));
                    half t = dot(uv - 0.5, dir) * _GradientScale + 0.5 + _GradientOffset;
                    t = saturate(t);

                    half3 gradientColor = lerp(saturated, lerp(_GradientColorA.rgb, _GradientColorB.rgb, t), lerp(_GradientColorA.a, _GradientColorB.a, t));
                    glassColor = lerp(gradientColor, _Tint.rgb, _Tint.a);
                #else
                    glassColor = lerp(saturated, _Tint.rgb, _Tint.a);
                #endif

                return half4(glassColor, mask);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}