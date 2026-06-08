// Combined Interaction Shader — Bot/Interaction
// ─────────────────────────────────────────────
// TWO MODES, ONE MATERIAL SLOT:
//
//  Outline mode  (_Visible = 1, _Progress = 0)
//    → Pulsing fresnel rim only. Shown while the player hasn't started yet.
//
//  Progress mode (_Progress > 0, _Visible = 0)
//    → Bottom-to-top fill with frontier ring.
//    → Fresnel rim stays on as an edge glow during the fill.
//
// Both modes are additive so they layer on top of the base material.

Shader "Bot/InteractionForTransparentMat"
{
    Properties
    {
        // ── Progress fill ────────────────────────────────────────────────────
        [Header(Progress)]
        _FillColor        ("Fill Color",          Color)        = (0.2, 0.8, 1.0, 1)
        _Progress         ("Progress",            Range(0,1))   = 0
        _FrontierWidth    ("Frontier Width",      Range(0,0.1)) = 0.02
        _FrontierBright   ("Frontier Brightness", Range(1,8))   = 3.0
        _UnfilledAlpha    ("Unfilled Tint Alpha",  Range(0,0.3)) = 0.05

        // ── Fresnel rim ──────────────────────────────────────────────────────
        [Header(Fresnel)]
        _RimColor         ("Rim Color",           Color)        = (0.2, 0.8, 1.0, 1)
        _RimPower         ("Rim Power",           Range(0.5,8)) = 3.0
        _RimStrength      ("Rim Strength",        Range(0,4))   = 1.5

        // ── Pulse (applied to rim in outline mode, subtler in progress mode) ─
        [Header(Pulse)]
        _PulseSpeed       ("Pulse Speed",         Range(0,10))  = 2.0
        _PulseAmount      ("Pulse Amount",        Range(0,1))   = 0.3

        // ── Mode control (set from code via MaterialPropertyBlock) ───────────
        // _Visible  1 = outline mode active    0 = hidden / progress mode
        _Visible          ("Visible (outline)",   Float)        = 0
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
            Name "BotInteraction"
            Tags { "LightMode" = "UniversalForward" }

            Blend  SrcAlpha OneMinusSrcAlpha   // normal alpha — solid when alpha is high
            ZWrite Off
            ZTest  LEqual
            Cull   Back

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _FillColor;
                float  _Progress;
                float  _FrontierWidth;
                float  _FrontierBright;
                float  _UnfilledAlpha;

                float4 _RimColor;
                float  _RimPower;
                float  _RimStrength;

                float  _PulseSpeed;
                float  _PulseAmount;

                float  _Visible;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float  localY      : TEXCOORD2;   // raw object-space Y for fill
                float  normalizedY : TEXCOORD3;   // 0=bottom 1=top, computed in vert
            };

            // Per-object bounds min/max Y — passed via shader keywords isn't
            // reliable at runtime, so we derive a 0-1 range from the vertex
            // position itself. Unity guarantees positionOS is in local space.
            // We can't know the exact mesh extents per-draw, so we drive fill
            // from world-space Y relative to the object pivot instead.
            // Objects are usually authored centred on their pivot, so we use
            // a signed normalised value: 0 = at pivot, ±1 = one unit away.
            // The caller can adjust _Progress to taste.

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionWS  = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.localY      = IN.positionOS.y;          // raw local Y
                // Normalize 0-1 across whatever Y range the vertex sits in.
                // We pass it through and use localY directly in the frag shader;
                // the fill threshold is expressed in local Y space.
                OUT.normalizedY = IN.positionOS.y;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // ── Early out: completely invisible ──────────────────────────
                // Hide when _Visible=0 AND _Progress=0
                if (_Visible < 0.01 && _Progress < 0.001)
                    return half4(0, 0, 0, 0);

                // ── Shared: fresnel ──────────────────────────────────────────
                float3 viewDir = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float  NdotV   = saturate(dot(normalize(IN.normalWS), viewDir));
                float  fresnel = pow(1.0 - NdotV, _RimPower);

                // ── Pulse ────────────────────────────────────────────────────
                float pulse = 1.0 + _PulseAmount * sin(_Time.y * _PulseSpeed);

                // ════════════════════════════════════════════════════════════
                // OUTLINE MODE  (_Visible=1, _Progress≈0)
                // ════════════════════════════════════════════════════════════
                if (_Visible > 0.5 && _Progress < 0.001)
                {
                    // Fresnel rim — strong at silhouette edges
                    float rim     = pow(1.0 - NdotV, _RimPower) * _RimStrength * pulse;

                    // Solid body fill — gives the mesh a visible surface when
                    // there is no base material behind this one. Facing polys
                    // get a semi-opaque tint; edge polys get the full rim glow.
                    float bodyAlpha = 0.35 * pulse;                 // solid face tint
                    float rimAlpha  = saturate(rim);                // edge glow on top
                    float alpha     = saturate(bodyAlpha + rimAlpha);

                    return half4(_RimColor.rgb * pulse, alpha);
                }

                // ════════════════════════════════════════════════════════════
                // PROGRESS MODE  (_Progress > 0)
                // ════════════════════════════════════════════════════════════

                // --- Fill geometry ------------------------------------------
                // Determine fill in local Y space.
                // We need the mesh's Y extents. Because we don't have them
                // as uniforms, we use a convention: the mesh should be authored
                // so its lowest point is at y = -0.5 and highest at y = +0.5
                // (i.e. centred on pivot, 1 unit tall).  If your mesh differs,
                // set _Progress to compensate, or add _MeshMinY/_MeshMaxY props.
                float localY    = IN.localY;
                float fillLevel = lerp(-0.5, 0.5, _Progress);  // in local Y

                // Smooth step at the very bottom to avoid a hard edge at progress=0
                float bottomFade = smoothstep(0.0, 0.12, _Progress);

                bool  inFill     = localY <= fillLevel;
                float frontier   = smoothstep(_FrontierWidth, 0.0,
                                              abs(localY - fillLevel));

                // Filled region colour
                float3 fillCol   = _FillColor.rgb
                                 + _FillColor.rgb * frontier * (_FrontierBright - 1.0);
                float  fillAlpha = inFill
                                 ? lerp(_UnfilledAlpha + 0.5, 0.85, bottomFade)
                                 : _UnfilledAlpha;
                fillAlpha       += frontier * 0.4;
                fillAlpha        = saturate(fillAlpha) * bottomFade;

                // Frontier brightens the fill colour
                float3 col       = inFill ? fillCol : _FillColor.rgb * 0.3;

                // --- Fresnel rim (subtler pulse during progress) -------------
                float rimPulse   = 1.0 + (_PulseAmount * 0.4) * sin(_Time.y * _PulseSpeed);
                float rim        = fresnel * _RimStrength * rimPulse;
                float rimAlpha   = saturate(rim);

                // Combine: fill alpha drives the fill, rim adds on top
                float  finalAlpha = saturate(fillAlpha + rimAlpha * 0.7);
                float3 finalCol   = lerp(_RimColor.rgb * rimPulse, col, fillAlpha / max(finalAlpha, 0.001));

                return half4(finalCol, finalAlpha);
            }
            ENDHLSL
        }
    }
}
