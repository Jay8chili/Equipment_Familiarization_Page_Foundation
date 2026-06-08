Shader "Custom/GrabHighlightOverlay"
{
    Properties
    {
        _RimColor        ("Rim Color",        Color)           = (0.0, 0.85, 1.0, 1.0)
        _RimPower        ("Rim Sharpness",    Range(0.5, 8.0)) = 3.0
        _RimWidth        ("Rim Width",        Range(0.0, 1.0)) = 0.6
        _EmissionBoost   ("Emission Boost",   Range(0.0, 4.0)) = 2.5
        _PulseSpeed      ("Pulse Speed",      Range(0.0, 6.0)) = 2.0
        _PulseStrength   ("Pulse Strength",   Range(0.0, 0.5)) = 0.2
        _HighlightAmount ("Highlight Amount", Range(0.0, 1.0)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Transparent+100"
        }

        Pass
        {
            Name "GrabHighlightOverlay"

            Blend SrcAlpha One
            ZWrite Off
            ZTest Always
            Cull Back

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ STEREO_INSTANCING_ON
            #pragma multi_compile _ UNITY_SINGLE_PASS_STEREO
            #pragma instancing_options renderinglayer

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _RimColor;
                float  _RimPower;
                float  _RimWidth;
                float  _EmissionBoost;
                float  _PulseSpeed;
                float  _PulseStrength;
                float  _HighlightAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = pos.positionCS;
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS  = GetWorldSpaceNormalizeViewDir(pos.positionWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float amount = smoothstep(0.0, 0.05, _HighlightAmount);
                clip(amount - 0.001);

                float3 N = normalize(IN.normalWS);
                float3 V = normalize(IN.viewDirWS);

                float NdotV   = saturate(dot(N, V));
                float fresnel = pow(1.0 - NdotV, _RimPower);
                fresnel       = smoothstep(1.0 - _RimWidth, 1.0, fresnel);

                float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseStrength;
                float alpha = fresnel * amount * pulse;
                half3 color = _RimColor.rgb * _EmissionBoost * pulse;

                return half4(color, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
