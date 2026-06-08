Shader "Custom/GrabHighlightOverlay"
{
    Properties
    {
        _RimColor        ("Rim Color",        Color)           = (0.0, 0.85, 1.0, 1.0)
        _RimPower        ("Rim Sharpness",    Range(0.5, 8.0)) = 3.0
        _RimWidth        ("Rim Width",        Range(0.0, 1.0)) = 0.6

        [Header(Highlight Mode)]
        [Toggle] _OutlineOnly ("Outline Only", Float) = 1.0
        _BaseAlpha       ("Full Highlight Base Opacity", Range(0.0, 1.0)) = 0.3

        [Header(Stroke  Outline Only Mode)]
        [Toggle] _StrokeEnabled ("Stroke Enabled",  Float) = 1.0
        // World-space extrusion in metres. 0.003 ≈ thin, 0.01 ≈ chunky in VR.
        _StrokeWidth     ("Stroke Width (m)", Range(0.0, 0.05)) = 0.003
        _StrokeColor     ("Stroke Color",      Color)            = (0.0, 0.85, 1.0, 1.0)

        [Header(Animation)]
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

        // ─────────────────────────────────────────────────────────────────
        // PASS 0 — Inverted-hull stroke (rendered first so rim sits on top)
        //
        // Technique: cull front faces, extrude back-faces along their normals
        // in object space.  No screen-space depth reads → tile-GPU friendly,
        // works on Quest 3 / Quest 3S without any extra render textures.
        //
        // Enabled only when both _OutlineOnly == 1 and _StrokeEnabled == 1.
        // The clip() inside the fragment kills it cheaply when either flag
        // is off, so the URP RendererList always has one object's worth of
        // draw calls and the driver never sees a pass-count change at runtime.
        // ─────────────────────────────────────────────────────────────────
        Pass
        {
            Name "GrabHighlightStroke"

            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Front          // draw only back-faces so they peek outside the silhouette

            HLSLPROGRAM
            #pragma vertex   vert_stroke
            #pragma fragment frag_stroke
            #pragma multi_compile_instancing
            #pragma multi_compile _ STEREO_INSTANCING_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ── Uniforms ──────────────────────────────────────────────────
            half4  _StrokeColor;
            float  _StrokeWidth;
            float  _StrokeEnabled;
            float  _OutlineOnly;
            float  _HighlightAmount;
            float  _EmissionBoost;
            float  _PulseSpeed;
            float  _PulseStrength;

            Varyings vert_stroke(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                // ── World-space extrusion (VR-correct) ────────────────────
                //
                // WHY NOT CLIP-SPACE IN VR:
                //   Clip-space extrusion uses UNITY_MATRIX_VP, which is
                //   DIFFERENT for each eye in stereo rendering.  This makes
                //   the stroke shift as the camera moves and gives each eye
                //   a slightly different outline — visible as a swimming /
                //   ghosting artifact in VR.  Clip-space outlines are a
                //   non-VR technique.
                //
                // WHY WORLD-SPACE IS CORRECT:
                //   Push the vertex outward in world space by a fixed metre
                //   value.  World space is shared between both eyes and is
                //   independent of camera position or rotation, so the
                //   stroke is rock-solid as the user moves their head.
                //
                // WHY THIS ALSO FIXES THE "BORDER TOO BIG" ISSUE:
                //   TransformObjectToWorldNormal() applies the inverse-
                //   transpose of the model matrix.  This is the correct
                //   normal transform for any combination of:
                //     • non-uniform scale on the GameObject
                //     • Unity's import scale factor (cm → m, etc.)
                //     • unfrozen Maya transforms baked into the .fbx
                //   The output is always a unit-length world-space normal
                //   pointing in the geometrically correct direction,
                //   regardless of how the mesh was authored or imported.
                //   _StrokeWidth is then in real-world metres and consistent
                //   across every mesh in the scene.

                // 1. Vertex into world space
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);

                // 2. Correct world-space normal via inverse-transpose model matrix
                float3 normalWS = normalize(TransformObjectToWorldNormal(IN.normalOS));

                // 3. Push outward in world space by _StrokeWidth metres
                posWS += normalWS * _StrokeWidth;

                // 4. Project the extruded world-space vertex into clip space
                OUT.positionCS = TransformWorldToHClip(posWS);
                return OUT;
            }

            half4 frag_stroke(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                // Kill the pass if either flag is off — cheapest possible exit
                clip(_OutlineOnly   - 0.5);
                clip(_StrokeEnabled - 0.5);

                float amount = smoothstep(0.0, 0.05, _HighlightAmount);
                clip(amount - 0.001);

                float pulse     = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseStrength;
                half3 color     = _StrokeColor.rgb * pulse * _EmissionBoost;
                float alpha     = _HighlightAmount * _StrokeColor.a;

                return half4(color, alpha);
            }
            ENDHLSL
        }

        // ─────────────────────────────────────────────────────────────────
        // PASS 1 — Original rim / fresnel highlight (unchanged logic)
        // ─────────────────────────────────────────────────────────────────
        Pass
        {
            Name "GrabHighlightOverlay"

            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Back
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ STEREO_INSTANCING_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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

            // ── Uniforms ──────────────────────────────────────────────────
            half4  _RimColor;
            float  _RimPower;
            float  _RimWidth;
            float  _OutlineOnly;
            float  _BaseAlpha;
            float  _EmissionBoost;
            float  _PulseSpeed;
            float  _PulseStrength;
            float  _HighlightAmount;

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

                float currentBaseAlpha = lerp(_BaseAlpha, 0.0, _OutlineOnly);
                float shapeAlpha       = max(currentBaseAlpha, fresnel);

                float pulse      = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseStrength;
                float finalAlpha = shapeAlpha * _HighlightAmount;

                half3 color = _RimColor.rgb * pulse * _EmissionBoost;

                return half4(color, finalAlpha * _RimColor.a);
            }
            ENDHLSL
        }
    }
}
