using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using System.Collections.Generic;

public class GrabHighlightPass : ScriptableRenderPass
{
    private Material _overlayMaterial;

    private static readonly int ID_Amount        = Shader.PropertyToID("_HighlightAmount");
    private static readonly int ID_RimColor      = Shader.PropertyToID("_RimColor");
    private static readonly int ID_StrokeColor   = Shader.PropertyToID("_StrokeColor");
    private static readonly int ID_StrokeWidth   = Shader.PropertyToID("_StrokeWidth");
    private static readonly int ID_StrokeEnabled = Shader.PropertyToID("_StrokeEnabled");

    public static int LastFrameRendererCount;
    public static int LastFrameDrawCallCount;
    public static string LastFrameSkipReason = "not run yet";

    private int _grabbableLayerMask = -1;
    private bool _materialInitialised = false;

    private static readonly List<ShaderTagId> s_ShaderTags = new List<ShaderTagId>
    {
        new ShaderTagId("UniversalForward"),
        new ShaderTagId("UniversalForwardOnly"),
        new ShaderTagId("SRPDefaultUnlit"),
        new ShaderTagId("LightweightForward"),
    };

    public GrabHighlightPass(Material overlayMaterial)
    {
        _overlayMaterial = overlayMaterial;
        renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
    }

    private void EnsureInitialised()
    {
        if (_grabbableLayerMask == -1)
        {
            int layer = LayerMask.NameToLayer("Grabbable");
            _grabbableLayerMask = layer == -1 ? 0 : (1 << layer);
            if (layer == -1)
                Debug.LogError("[GrabHighlight] 'Grabbable' layer not found.");
        }

        if (!_materialInitialised && _overlayMaterial != null)
        {
            _materialInitialised = true;
        }
    }

    class PassData
    {
        public RendererListHandle RendererListHandle;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameContext)
    {
        EnsureInitialised();

        if (_overlayMaterial == null) { LastFrameSkipReason = "Material null"; return; }
        if (_grabbableLayerMask == 0) { LastFrameSkipReason = "Layer not found"; return; }

        var active = GrabHighlightRegistry.ActiveRenderers;
        int count = active.Count;
        LastFrameRendererCount = count;

        if (count == 0) { LastFrameSkipReason = "Registry empty"; return; }

        // Find highest amount and push it directly onto the material
        // RendererList+overrideMaterial does not support MaterialPropertyBlock
        // so we drive _HighlightAmount on the material itself
        float highestAmount = 0f;
        Color rimColor = Color.cyan;
        for (int i = 0; i < count; i++)
        {
            if (active[i].Amount > highestAmount)
            {
                highestAmount = active[i].Amount;
                rimColor = active[i].RimColor;
            }
        }

        if (highestAmount < 0.001f) { LastFrameSkipReason = "All amounts 0"; return; }

        // Set values directly on the material — safe because this material
        // is our own overlay material, not the object's original material.
        // Rim pass (Pass 1)
        _overlayMaterial.SetFloat(ID_Amount,   highestAmount);
        _overlayMaterial.SetColor(ID_RimColor, rimColor);

        // Stroke pass (Pass 0) — mirror rim colour into stroke colour so both
        // animate together.  The material inspector can override _StrokeColor
        // independently if desired; we only push here when the entry has a
        // non-default stroke colour (extend RendererEntry later if needed).
        // _StrokeWidth and _StrokeEnabled are authored in the material and are
        // NOT overridden here — they are inspector-driven design-time values.

        LastFrameSkipReason = "running";

        // Exactly following the official Unity 6 docs example
        UniversalRenderingData renderingData = frameContext.Get<UniversalRenderingData>();
        UniversalCameraData cameraData = frameContext.Get<UniversalCameraData>();
        UniversalLightData lightData = frameContext.Get<UniversalLightData>();
        UniversalResourceData resourceData = frameContext.Get<UniversalResourceData>();

        FilteringSettings filterSettings = new FilteringSettings(
            RenderQueueRange.all,
            _grabbableLayerMask);

        DrawingSettings drawSettings = RenderingUtils.CreateDrawingSettings(
            s_ShaderTags, renderingData, cameraData, lightData,
            SortingCriteria.CommonTransparent);

        drawSettings.overrideMaterial = _overlayMaterial;
        drawSettings.overrideMaterialPassIndex = -1;  // -1 = render ALL passes in the override material

        var rendererListParams = new RendererListParams(
            renderingData.cullResults, drawSettings, filterSettings);

        using (var builder = renderGraph.AddRasterRenderPass<PassData>(
                   "GrabHighlight", out var passData))
        {
            passData.RendererListHandle = renderGraph.CreateRendererList(rendererListParams);

            builder.UseRendererList(passData.RendererListHandle);

            // AccessFlags.Write on depth — matches the official Unity docs example
            builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
            builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture, AccessFlags.Write);

            builder.AllowPassCulling(false);

            builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
            {
                context.cmd.DrawRendererList(data.RendererListHandle);
                LastFrameDrawCallCount++;
            });
        }
    }
}