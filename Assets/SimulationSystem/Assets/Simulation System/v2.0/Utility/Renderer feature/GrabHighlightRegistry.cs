using System.Collections.Generic;
using UnityEngine;

public static class GrabHighlightRegistry
{
    public static readonly int ID_Amount   = Shader.PropertyToID("_HighlightAmount");
    public static readonly int ID_RimColor = Shader.PropertyToID("_RimColor");

    public class RendererEntry
    {
        public Renderer              Renderer;
        public MaterialPropertyBlock MPB;
        public float                 Amount;
        public Color                 RimColor;

        private MeshFilter          _meshFilter;
        private SkinnedMeshRenderer _skinned;

        public RendererEntry(Renderer r, Color rimColor)
        {
            Renderer  = r;
            MPB       = new MaterialPropertyBlock();
            Amount    = 0f;
            RimColor  = rimColor;
            _meshFilter = r.GetComponent<MeshFilter>();
            _skinned    = r as SkinnedMeshRenderer;
        }

        public Mesh GetMesh()
        {
            if (_skinned    != null) return _skinned.sharedMesh;
            if (_meshFilter != null) return _meshFilter.sharedMesh;
            return null;
        }

        // Call this before each draw to sync values into the MPB
        public void SyncMPB()
        {
            MPB.SetFloat(ID_Amount,   Amount);
            MPB.SetColor(ID_RimColor, RimColor);
        }
    }

    public static readonly List<RendererEntry> ActiveRenderers = new List<RendererEntry>();

    public static RendererEntry Register(Renderer r, Color rimColor)
    {
        foreach (var e in ActiveRenderers)
            if (e.Renderer == r) return e;

        var entry = new RendererEntry(r, rimColor);
        ActiveRenderers.Add(entry);
        return entry;
    }

    public static void Unregister(Renderer r)
    {
        ActiveRenderers.RemoveAll(e => e.Renderer == r);
    }
}
