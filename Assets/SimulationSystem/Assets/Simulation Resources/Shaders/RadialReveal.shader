// RadialReveal.shader
// Apply this to a large inverted sphere (ScaleX/Z/Y = -1 on a Unity sphere)
// that envelops your entire scene. The shader starts fully opaque black,
// then punches an expanding radial hole outward from the origin as
// _RevealRadius increases — revealing everything underneath.
//
// Properties driven at runtime by SceneRevealController.cs:
//   _RevealRadius   – world-space radius of the transparent "hole"
//   _EdgeSoftness   – width of the feathered border in world units
//   _Color          – cover color (default: pitch black)

Shader "VR/RadialReveal"
{
    Properties
    {
        _Color          ("Cover Color",     Color)   = (0, 0, 0, 1)
        _RevealRadius   ("Reveal Radius",   Float)   = 0.0
        _EdgeSoftness   ("Edge Softness",   Float)   = 1.5
        // World-space XZ position of the reveal origin (set by SceneRevealController)
        _RevealCenter   ("Reveal Center",   Vector)  = (0, 0, 0, 0)
        // Optional vignette ring that pulses at the reveal edge
        _RingBrightness ("Ring Brightness", Float)   = 0.35
        _RingWidth      ("Ring Width",      Float)   = 0.4
    }

    SubShader
    {
        // ------------------------------------------------------------------
        // Render on top of everything so the cover never z-fights with scene
        // geometry.  Queue 4500 = after Transparent but before Overlay.
        // ------------------------------------------------------------------
        Tags
        {
            "Queue"           = "Overlay"
            "RenderType"      = "Transparent"
            "IgnoreProjector" = "True"
        }

        // No depth writes – purely cosmetic overlay
        ZWrite Off
        // Always draw on top of scene (cover sphere is inside-out so we
        // use Cull Front so the interior faces render).
        Cull Front
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0

            #include "UnityCG.cginc"

            // ---- Uniforms ------------------------------------------------
            fixed4  _Color;
            float   _RevealRadius;
            float   _EdgeSoftness;
            float4  _RevealCenter;      // world-space XZ origin (player position)
            float   _RingBrightness;
            float   _RingWidth;

            // ---- Vertex I/O ----------------------------------------------
            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID      // VR single-pass instancing
            };

            struct v2f
            {
                float4 pos       : SV_POSITION;
                float3 worldPos  : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO           // VR stereo output
            };

            // ---- Vertex shader -------------------------------------------
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.pos      = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            // ---- Fragment shader -----------------------------------------
            fixed4 frag(v2f i) : SV_Target
            {
                // Distance in the XZ plane FROM the player's standing position.
                // This makes the hole expand outward from wherever the user stands.
                float2 offset = i.worldPos.xz - _RevealCenter.xz;
                float  dist   = length(offset);

                // ── Core radial mask ──────────────────────────────────────
                // smoothstep goes 1→0 as dist crosses (_RevealRadius - softness)
                // to _RevealRadius, so fragments *inside* the radius become
                // transparent and those outside stay opaque.
                float halfSoft   = _EdgeSoftness * 0.5;
                float coverAlpha = smoothstep(
                    _RevealRadius - halfSoft,   // inner edge (start fading)
                    _RevealRadius + halfSoft,   // outer edge (fully opaque)
                    dist
                );

                // ── Optional luminous ring at the reveal frontier ─────────
                // A narrow band that glows just inside the opaque region.
                float ringDist   = abs(dist - _RevealRadius);
                float ringAlpha  = smoothstep(_RingWidth, 0.0, ringDist);
                // Ring is only visible where the cover is still partially drawn
                ringAlpha *= coverAlpha * (1.0 - coverAlpha) * 4.0;

                fixed4 col = _Color;
                // Brighten the cover color at the ring
                col.rgb  += ringAlpha * _RingBrightness;
                col.a     = saturate(coverAlpha);

                return col;
            }
            ENDCG
        }
    }

    FallBack Off
}
