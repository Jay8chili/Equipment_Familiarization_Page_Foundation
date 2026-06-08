// URP Interaction Fresnel Shader
// Assign as a second material slot on grab or detect objects.
// _Visible (0 = off, 1 = on) is set from Interactions.cs.
// Shows on interaction suspend, hides on interaction start and complete.

Shader "Bot/InteractionOutline"
{
    Properties
    {
        _Visible     ("Visible",       Range(0,1))   = 0
        _RimColor    ("Rim Color",     Color)        = (0.2, 0.8, 1.0, 1)
        _RimPower    ("Rim Power",     Range(0.5,8)) = 3.0
        _RimStrength ("Rim Strength",  Range(0,4))   = 1.5
        _PulseSpeed  ("Pulse Speed",   Range(0,10))  = 2.0
        _PulseAmount ("Pulse Amount",  Range(0,1))   = 0.3
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

            Blend  SrcAlpha One
            ZWrite Off
            ZTest  LEqual
            Cull   Back

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _RimColor;
                float  _Visible;
                float  _RimPower;
                float  _RimStrength;
                float  _PulseSpeed;
                float  _PulseAmount;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS  = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                if (_Visible < 0.01) return half4(0,0,0,0);

                float pulse   = 1.0 + _PulseAmount * sin(_Time.y * _PulseSpeed);
                float3 viewDir = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float  NdotV   = saturate(dot(normalize(IN.normalWS), viewDir));
                float  fresnel = pow(1.0 - NdotV, _RimPower) * _RimStrength * pulse;

                return half4(_RimColor.rgb * pulse, saturate(fresnel) * _Visible);
            }
            ENDHLSL
        }
    }
}
