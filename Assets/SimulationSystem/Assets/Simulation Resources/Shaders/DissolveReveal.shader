// DissolveReveal.shader
// ─────────────────────────────────────────────────────────────────────────────
// This shader is an OVERLAY — it is APPENDED as an extra material slot on top
// of a Renderer's existing materials. It starts as a solid black cover and
// clips itself away as the reveal wave passes, exposing the original material
// beneath it. It never replaces anything.
//
// Rendering order:
//   Queue = Geometry+1  →  draws after the base material, covering it.
//   ZWrite Off          →  doesn't corrupt the depth buffer.
//   clip()              →  punches holes in the overlay as dissolve increases.
//
// Globals driven by SceneRevealController (no per-material setup needed):
//   _RevealCenter   – world-space XZ position of the player
//   _RevealRadius   – expanding wave radius in world units
//   _WaveBandwidth  – world-unit width of the dissolve transition band
// ─────────────────────────────────────────────────────────────────────────────

Shader "VR/DissolveReveal"
{
    Properties
    {
        // ── Cover appearance ──────────────────────────────────────────────────
        _CoverColor     ("Cover Color",         Color)  = (0, 0, 0, 1)

        // ── Tile settings ─────────────────────────────────────────────────────
        _TileSize       ("Tile Size (world units)", Float) = 0.5
        // How far ahead of the wave front tiles start randomly popping.
        // Larger = more tiles disappearing at once, looser/messier wave edge.
        _TileScatter    ("Tile Scatter",        Float)  = 3.0

        // ── Edge glow ─────────────────────────────────────────────────────────
        _EdgeColor      ("Edge Glow Color",     Color)  = (0.9, 0.5, 0.1, 1)
        _EdgeIntensity  ("Edge Glow Intensity", Float)  = 4.0

        // ── Per-material override ─────────────────────────────────────────────
        // Set to 1 to force this object fully revealed before the wave arrives.
        _DissolveAmount ("Dissolve Override",   Range(0,1)) = 0.0

        // NOTE: _RevealCenter, _RevealRadius, _WaveBandwidth are set globally
        // by SceneRevealController via Shader.SetGlobal* — NOT listed here so
        // material-local defaults cannot shadow the globals.
    }

    SubShader
    {
        // ── Render AFTER the base material so we sit on top of it ─────────────
        Tags
        {
            "Queue"           = "Geometry+1"
            "RenderType"      = "Opaque"
            "IgnoreProjector" = "True"
        }

        // Don't write depth — we're a cosmetic overlay
        ZWrite Off
        // Match the object's own geometry winding
        Cull Back
        // Additive blend on the edge glow; base cover is opaque until clipped
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0

            #include "UnityCG.cginc"

            // ── Uniforms ──────────────────────────────────────────────────────
            fixed4  _CoverColor;
            float   _TileSize;
            float   _TileScatter;
            fixed4  _EdgeColor;
            float   _EdgeIntensity;
            float   _DissolveAmount;

            // Globals set by SceneRevealController — NOT in Properties block
            float4  _RevealCenter;
            float   _RevealRadius;
            float   _WaveBandwidth;

            // ── Vertex I/O ────────────────────────────────────────────────────
            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ── Helpers ───────────────────────────────────────────────────────

            // Fast hash: maps a 2D integer grid cell to a pseudo-random float [0,1].
            // Each tile gets a stable unique value independent of frame/time.
            float hash2(float2 cell)
            {
                cell  = frac(cell * float2(127.1, 311.7));
                cell += dot(cell, cell.yx + 19.19);
                return frac((cell.x + cell.y) * cell.x);
            }

            // ── Vertex shader ─────────────────────────────────────────────────
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos      = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            // ── Fragment shader ───────────────────────────────────────────────
            fixed4 frag(v2f i) : SV_Target
            {
                // ── 1. Which tile cell does this pixel live in? ───────────────
                // Snap world XZ to a grid. All pixels in the same cell share
                // the same cell ID and will disappear together as one tile.
                float2 cell     = floor(i.worldPos.xz / max(_TileSize, 0.001));

                // ── 2. Give this tile a unique random pop-off delay ───────────
                // hash2 returns [0,1] — a stable jitter offset for this tile.
                // This offsets when within the wave band the tile disappears,
                // so tiles don't all vanish on the exact same frame.
                float  tileRand = hash2(cell);                          // [0, 1]

                // ── 3. Distance from the reveal origin to this tile's center ──
                float2 tileCenter = (cell + 0.5) * _TileSize;          // world XZ
                float  dist       = length(tileCenter - _RevealCenter.xz);

                // ── 4. Has the wave reached this tile yet? ────────────────────
                // waveT: 0 = wave just arrived, 1 = wave fully passed.
                // We scatter the arrival threshold by tileRand so tiles within
                // the scatter band pop off in a randomised order, not a hard ring.
                float scatteredRadius = _RevealRadius - tileRand * _TileScatter;
                float waveT           = (scatteredRadius - dist) /
                                        max(_WaveBandwidth, 0.001);
                waveT = saturate(waveT);

                // Apply per-material override
                float dissolve = max(waveT, _DissolveAmount);

                // ── 5. Hard clip the entire tile at once ──────────────────────
                // Use 0.99 not 1.0 — floating point means dissolve rarely hits
                // exactly 1.0, leaving tiles stuck at near-1 with full glow forever.
                clip(0.99 - dissolve);

                // ── 6. Edge glow on tiles that are about to pop ───────────────
                // Only glow in [0.65, 0.99) — clamp top end so it can't persist
                // on tiles that should already be gone.
                float glowMask = smoothstep(0.65, 0.95, dissolve)
                               * (1.0 - smoothstep(0.95, 0.99, dissolve));

                // ── 7. Tile border darkening (optional grid line effect) ───────
                // Darken pixels near the tile edge so you can see the grid.
                float2 tileFrac   = frac(i.worldPos.xz / max(_TileSize, 0.001));
                float2 borderDist = min(tileFrac, 1.0 - tileFrac);     // 0 at edge, 0.5 at centre
                float  border     = smoothstep(0.0, 0.04, min(borderDist.x, borderDist.y));

                // ── 8. Final colour ───────────────────────────────────────────
                fixed4 col  = _CoverColor * border;                     // darkened grid lines
                col.rgb    += _EdgeColor.rgb * glowMask * _EdgeIntensity;
                col.a       = 1.0;                                      // fully opaque until clipped

                return col;
            }
            ENDCG
        }
    }

    FallBack Off
}
