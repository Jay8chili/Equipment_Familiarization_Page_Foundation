Shader "Custom/URP/InteractionProgress"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.2,0.2,0.2,1)
        _FillColor ("Fill Color", Color) = (0,1,1,1)
        _EmissionIntensity ("Emission Intensity", Float) = 2
        _Progress ("Progress", Range(0,1)) = 0
    }

    SubShader
    {
        Tags 
        { 
            "RenderType"="Opaque"
            "RenderPipeline"="UniversalRenderPipeline"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _FillColor;
                float _EmissionIntensity;
                float _Progress;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // Vertical fill based on UV.y
                float edge = 0.02;
                float fillMask = smoothstep(_Progress - edge, _Progress + edge, IN.uv.y);
                fillMask = 1 - fillMask;

                float3 baseCol = _BaseColor.rgb;
                float3 fillCol = _FillColor.rgb * _EmissionIntensity;

                float3 finalColor = lerp(baseCol, fillCol, fillMask);

                return half4(finalColor, 1);
            }

            ENDHLSL
        }
    }
}