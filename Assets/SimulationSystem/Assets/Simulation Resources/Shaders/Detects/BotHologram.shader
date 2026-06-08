// URP Hologram Sphere Sweep Shader
// Works on any sphere mesh (Unity built-in sphere is fine).
// _ExpandProgress (0..1) sweeps the hologram from the bottom pole to the top pole.
// Keyframe _ExpandProgress directly in the Unity Animation window on the material.
//
// Progress meaning:
//   0.0  = nothing visible (clip plane below entire sphere)
//   0.0 -> 1.0 = reveal sweeps upward from -Y to +Y
//   1.0  = fully revealed, then alpha fades out

Shader "Bot/Hologram"
{
    Properties
    {
        // ── Core control ─────────────────────────────────────────────────────
        _ExpandProgress  ("Expand Progress",        Range(0,1))       = 0
        _FadeSharpness   ("Fade Sharpness",         Range(1,10))      = 3.0

        // ── Color ────────────────────────────────────────────────────────────
        _HoloColor       ("Hologram Color",         Color)            = (0.2, 0.8, 1.0, 1)
        _HoloIntensity   ("Hologram Intensity",     Range(0,6))       = 2.0

        // ── Sweep direction ───────────────────────────────────────────────────
        // 0 = bottom (-Y) to top (+Y)   1 = top to bottom   2 = -X to +X   3 = -Z to +Z
        [KeywordEnum(Y_POS, Y_NEG, X_POS, Z_POS)] _SweepAxis ("Sweep Axis", Float) = 0

        // ── Sweep edge ring ───────────────────────────────────────────────────
        _EdgeWidth       ("Sweep Edge Width",       Range(0,0.3))     = 0.06
        _EdgeBrightness  ("Sweep Edge Brightness",  Range(0,12))      = 6.0

        // ── Fresnel rim ───────────────────────────────────────────────────────
        _RimPower        ("Rim Power",              Range(0.5,8))     = 2.5
        _RimStrength     ("Rim Strength",           Range(0,3))       = 1.0

        // ── Scanlines ─────────────────────────────────────────────────────────
        _ScanlineCount   ("Scanline Count",         Range(5,300))     = 60
        _ScanlineSpeed   ("Scanline Speed",         Range(0,5))       = 0.6
        _ScanlineBright  ("Scanline Brightness",    Range(0,1))       = 0.25
        _ScanlineWidth   ("Scanline Width",         Range(0.01,0.99)) = 0.45

        // ── Flicker ───────────────────────────────────────────────────────────
        _FlickerSpeed    ("Flicker Speed",          Range(0,20))      = 5.0
        _FlickerAmount   ("Flicker Amount",         Range(0,0.5))     = 0.10
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Transparent"
        }

        Pass
        {
            Name "HologramSphere"
            Tags { "LightMode" = "UniversalForward" }

            Blend  SrcAlpha One
            ZWrite Off
            Cull   Back

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _HoloColor;
                float  _ExpandProgress;
                float  _FadeSharpness;
                float  _HoloIntensity;
                float  _SweepAxis;
                float  _EdgeWidth;
                float  _EdgeBrightness;
                float  _RimPower;
                float  _RimStrength;
                float  _ScanlineCount;
                float  _ScanlineSpeed;
                float  _ScanlineBright;
                float  _ScanlineWidth;
                float  _FlickerSpeed;
                float  _FlickerAmount;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionOS  : TEXCOORD0;   // local-space position for sweep math
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float2 uv          : TEXCOORD3;
            };

            // ── Helpers ───────────────────────────────────────────────────────

            float Hash(float n) { return frac(sin(n) * 43758.5453123); }

            float Flicker(float speed)
            {
                float t = _Time.y * speed;
                return lerp(Hash(floor(t)), Hash(floor(t) + 1.0),
                            smoothstep(0.4, 0.6, frac(t)));
            }

            // Returns the sweep axis value from local position.
            // Unity sphere: local coords run -0.5..+0.5 on each axis.
            // We remap to -1..+1 so the math is axis-agnostic.
            float SweepCoord(float3 posOS)
            {
                float3 n = posOS * 2.0; // -1..+1 range (unit sphere radius = 0.5)
                if      (_SweepAxis < 0.5) return  n.y;   // Y_POS: bottom to top
                else if (_SweepAxis < 1.5) return -n.y;   // Y_NEG: top to bottom
                else if (_SweepAxis < 2.5) return  n.x;   // X_POS: -X to +X
                else                       return  n.z;   // Z_POS: -Z to +Z
            }

            // ── Vertex ────────────────────────────────────────────────────────
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                // Sphere does NOT deform — no vertex scaling needed
                OUT.positionOS  = IN.positionOS.xyz;
                OUT.positionWS  = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv          = IN.uv;
                return OUT;
            }

            // ── Fragment ──────────────────────────────────────────────────────
            half4 frag(Varyings IN) : SV_Target
            {
                if (_ExpandProgress <= 0.001) return half4(0,0,0,0);

                // ── 1. Sweep clip ─────────────────────────────────────────────
                // sweepCoord runs -1 (start pole) to +1 (end pole).
                // frontier runs -1 to +1 as progress goes 0 to 1.
                float sweepCoord = SweepCoord(IN.positionOS);
                float frontier   = (_ExpandProgress * 2.0) - 1.0; // remap 0..1 -> -1..+1

                // Discard pixels that haven't been reached yet
                clip(frontier - sweepCoord);

                // ── 2. Master alpha (bell curve — rise then fade) ─────────────
                float expandAlpha = sin(_ExpandProgress * 3.14159);
                expandAlpha       = pow(saturate(expandAlpha), 1.0 / _FadeSharpness);

                // ── 3. Sweep edge ring ────────────────────────────────────────
                // Bright glow band just behind the frontier
                float distToFrontier = frontier - sweepCoord;        // 0 at frontier
                float edgeGlow       = saturate(1.0 - distToFrontier
                                                / max(_EdgeWidth, 0.001));
                edgeGlow             = pow(edgeGlow, 2.0) * _EdgeBrightness;

                // ── 4. Fresnel rim ────────────────────────────────────────────
                float3 viewDir = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float  NdotV   = saturate(dot(normalize(IN.normalWS), viewDir));
                float  fresnel = pow(1.0 - NdotV, _RimPower) * _RimStrength;

                // ── 5. Scanlines (along sweep axis in local space) ────────────
                float scanCoord  = sweepCoord * _ScanlineCount * 0.5
                                 - _Time.y * _ScanlineSpeed;
                float scanMask   = step(frac(scanCoord), _ScanlineWidth);
                float scanValue  = lerp(1.0, 1.0 + _ScanlineBright, scanMask);

                // ── 6. Flicker ────────────────────────────────────────────────
                float flicker = 1.0 - _FlickerAmount
                              + _FlickerAmount * Flicker(_FlickerSpeed);

                // ── 7. Compose ────────────────────────────────────────────────
                float3 color = _HoloColor.rgb * _HoloIntensity * scanValue;
                color       += edgeGlow  * _HoloColor.rgb;
                color       += fresnel   * _HoloColor.rgb;
                color       *= flicker;

                float alpha  = expandAlpha * _HoloColor.a;
                alpha       += fresnel * expandAlpha * 0.3;
                alpha        = saturate(alpha) * flicker;

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
