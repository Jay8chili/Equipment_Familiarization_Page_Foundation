Shader "8chili/FrostedGlassSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint (SpriteRenderer)", Color) = (1,1,1,1)

        [Header(Frost)]
        _FrostScale      ("Frost Noise Scale", Range(1, 200)) = 60.0
        _FrostStrength   ("Frost Distortion", Range(0, 0.08)) = 0.035
        _BrightBoost     ("Bright Accentuation", Range(0, 3)) = 1.2
        _BrightThreshold ("Bright Threshold", Range(0, 1)) = 0.4
        _Saturation      ("Color Saturation", Range(0, 2)) = 1.3

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
            "RenderPipeline"   = "UniversalPipeline"
            "RenderType"       = "Transparent"
            "Queue"            = "Transparent"
            "IgnoreProjector"  = "True"
            "PreviewType"      = "Plane"
            "CanUseSpriteAtlas"= "True"
        }

        Pass
        {
            Name "FrostedGlassSprite"

            ZWrite Off
            Cull Off
            Blend One OneMinusSrcAlpha   // premultiplied-friendly, standard sprite blend

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma shader_feature_local _GRADIENT_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _Color;
                half4  _Tint;
                half   _FrostScale;
                half   _FrostStrength;
                half   _BrightBoost;
                half   _BrightThreshold;
                half   _Saturation;
                half4  _GradientColorA;
                half4  _GradientColorB;
                half   _GradientAngle;
                half   _GradientOffset;
                half   _GradientScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vpi = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vpi.positionCS;
                output.uv         = TRANSFORM_TEX(input.uv, _MainTex);
                output.color      = input.color * _Color;
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

            half4 frag(Varyings input) : SV_Target
            {
                half2 uv = input.uv;

                // Frost noise distortion in sprite UV space
                half2 noiseUV = uv * _FrostScale;
                half2 frostOffset = half2(
                    valueNoise(noiseUV) - 0.5,
                    valueNoise(noiseUV + 100.0) - 0.5
                ) * _FrostStrength;

                half2 sampleUV = uv + frostOffset;

                // Sample the sprite (acts as our "scene" to frost)
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sampleUV);
                half3 blurred = tex.rgb;

                // Use the *undistorted* alpha for the shape mask so edges stay crisp
                half spriteAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;

                // Bright accentuation
                half lum = luminance(blurred);
                half brightFactor = smoothstep(_BrightThreshold, 1.0, lum);
                blurred *= 1.0 + brightFactor * _BrightBoost;

                // Saturation boost
                half grey = luminance(blurred);
                half3 saturated = lerp(half3(grey, grey, grey), blurred, _Saturation);

                // Tint
                half3 tintColor;
                half  tintAlpha;

                #ifdef _GRADIENT_ON
                    half rad = _GradientAngle * 0.01745329; // deg -> rad
                    half2 dir = half2(cos(rad), sin(rad));
                    half t = dot(uv - 0.5, dir) * _GradientScale + 0.5 + _GradientOffset;
                    t = saturate(t);

                    tintColor = lerp(_GradientColorA.rgb, _GradientColorB.rgb, t);
                    tintAlpha = lerp(_GradientColorA.a, _GradientColorB.a, t);
                #else
                    tintColor = _Tint.rgb;
                    tintAlpha = _Tint.a;
                #endif

                half3 glassColor = lerp(saturated, tintColor, tintAlpha);

                // Apply SpriteRenderer color tint
                glassColor *= input.color.rgb;

                // Final alpha follows sprite shape and SpriteRenderer alpha
                half outA = spriteAlpha * input.color.a;

                // Premultiplied output (matches Blend One OneMinusSrcAlpha)
                return half4(glassColor * outA, outA);
            }
            ENDHLSL
        }
    }

    FallBack "Sprites/Default"
}
