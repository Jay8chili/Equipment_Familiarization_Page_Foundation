Shader "Custom/ThrusterBeam"
{
    Properties
    {
        [HDR] _ThrusterColor   ("Thruster Color (HDR)", Color) = (0, 0.67, 1, 1)
        _Intensity             ("Intensity",            Float)  = 3.0
        _ScrollSpeed           ("Scroll Speed",         Float)  = 1.5
        _NoiseScale            ("Noise Scale",          Float)  = 4.0
        _EdgeSoftness          ("Edge Softness",        Float)  = 2.0
        _RimPower              ("Rim Power",            Float)  = 1.5
        _TipFalloff            ("Tip Falloff",          Float)  = 2.0
        _PulseSpeed            ("Pulse Speed",          Float)  = 4.0
        _PulseAmount           ("Pulse Amount",         Float)  = 0.15
        _CoreBrightness        ("Core Brightness",      Float)  = 2.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ThrusterBeam"

            Blend One One
            ZWrite Off
            Cull Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ThrusterColor;
                float _Intensity;
                float _ScrollSpeed;
                float _NoiseScale;
                float _EdgeSoftness;
                float _RimPower;
                float _TipFalloff;
                float _PulseSpeed;
                float _PulseAmount;
                float _CoreBrightness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionOS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float3 viewDirWS   : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float2 Hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)),
                           dot(p, float2(269.5, 183.3)));
                return -1.0 + 2.0 * frac(sin(p) * 43758.5453123);
            }

            float GradientNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
                float a = dot(Hash2(i + float2(0,0)), f - float2(0,0));
                float b = dot(Hash2(i + float2(1,0)), f - float2(1,0));
                float c = dot(Hash2(i + float2(0,1)), f - float2(0,1));
                float d = dot(Hash2(i + float2(1,1)), f - float2(1,1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float FBM(float2 p)
            {
                float v = 0.0;
                v += 0.60 * GradientNoise(p);
                v += 0.40 * GradientNoise(p * 2.1 + float2(5.2, 1.3));
                return saturate(v * 0.5 + 0.5);
            }

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv          = IN.uv;
                OUT.positionOS  = IN.positionOS.xyz;
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);

                float3 posWS  = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.viewDirWS = GetWorldSpaceNormalizeViewDir(posWS);

                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float2 uv = IN.uv;

                // 1. Scrolling noise
                float2 scrolledUV = uv * _NoiseScale
                                  + float2(0.0, -_Time.y * _ScrollSpeed);
                float noise = FBM(scrolledUV);
                noise = lerp(0.3, 1.0, noise);

                // 2. Radial rim mask
                float2 centeredUV = uv - 0.5;
                float  radialDist = length(centeredUV);
                float  rimMask    = 1.0 - saturate(radialDist * 2.0);
                rimMask = pow(rimMask, _RimPower);

                // 3. Tip-to-base axial falloff
                float axialT  = saturate((IN.positionOS.y + 1.0) * 0.5);
                float tipFade = pow(1.0 - axialT, _TipFalloff);

                // 4. Fresnel
                float fresnel = pow(1.0 - saturate(dot(IN.normalWS, IN.viewDirWS)),
                                    _EdgeSoftness);

                // 5. Pulse
                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount;

                // 6. Core highlight
                float coreMask = saturate(1.0 - radialDist * 4.0);
                coreMask       = pow(coreMask, 2.0) * _CoreBrightness;

                // 7. Combine
                float shape    = rimMask * tipFade;
                float beam     = shape * noise * pulse;
                float fullBeam = beam + coreMask * shape;
                fullBeam      += fresnel * shape * 0.4;
                fullBeam       = saturate(fullBeam);

                // 8. Output
                half3 col = _ThrusterColor.rgb * fullBeam * _Intensity;
                return half4(col, fullBeam);
            }

            ENDHLSL
        }
    }

    FallBack Off
}
