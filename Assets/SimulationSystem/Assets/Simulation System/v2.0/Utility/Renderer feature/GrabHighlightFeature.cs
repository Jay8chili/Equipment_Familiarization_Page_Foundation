using UnityEngine;
using UnityEngine.Rendering.Universal;

public class GrabHighlightFeature : ScriptableRendererFeature
{
    [Tooltip("Material using the Custom/GrabHighlightOverlay shader")]
    public Material OverlayMaterial;

    private GrabHighlightPass _pass;
    //private int _frameCount;

    public override void Create()
    {
        if (OverlayMaterial == null)
        {
            Debug.LogError("[GrabHighlightFeature] OverlayMaterial is not assigned in the URP Renderer asset!");
            return;
        }
        _pass = new GrabHighlightPass(OverlayMaterial);
        Debug.Log("[GrabHighlightFeature] Created successfully.");
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_pass == null || OverlayMaterial == null) return;
        if (renderingData.cameraData.cameraType == CameraType.Reflection) return;

        renderer.EnqueuePass(_pass);

        // Log debug info every 60 frames so the console isn't spammed
        /*_frameCount++;
        if (_frameCount % 60 == 0)
        {
            Debug.Log($"[GrabHighlight] Frame {_frameCount} | " +
                      $"Registered renderers: {GrabHighlightPass.LastFrameRendererCount} | " +
                      $"Draw calls: {GrabHighlightPass.LastFrameDrawCallCount} | " +
                      $"Status: {GrabHighlightPass.LastFrameSkipReason}");
        }*/
    }

    protected override void Dispose(bool disposing)
    {
        _pass = null;
    }
}