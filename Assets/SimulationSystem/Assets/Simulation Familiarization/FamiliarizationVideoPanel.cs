// ════════════════════════════════════════════════════════════════════════════
//  FamiliarizationVideoPanel.cs
//
//  Attach to the VideoPanel child of the step UI prefab.
//  Wraps Unity VideoPlayer — plays a clip once, fires OnVideoCompleted when done.
//
//  SETUP
//      1. Add VideoPlayer component to this GO.
//      2. Create a RenderTexture asset, assign to VideoPlayer.targetTexture
//         and to the RawImage.texture on the same or child GO.
//      3. Add FamiliarizationVideoPanel and wire videoPlayer + rawImage.
//      4. VideoClip is assigned per step by the wizard (PartStepData.videoClip).
// ════════════════════════════════════════════════════════════════════════════

using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class FamiliarizationVideoPanel : MonoBehaviour
{
    [Header("References")]
    public VideoPlayer videoPlayer;
    public RawImage    rawImage;

    // ── Internal ──────────────────────────────────────────────────────────
    public event Action OnVideoCompleted;

    // ─────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        gameObject.SetActive(false);

        if (videoPlayer == null)
            videoPlayer = GetComponentInChildren<VideoPlayer>();

        if (videoPlayer != null)
        {
            videoPlayer.playOnAwake  = false;
            videoPlayer.isLooping    = false;
            videoPlayer.loopPointReached += OnLoopPointReached;
        }
    }

    private void OnDestroy()
    {
        if (videoPlayer != null)
            videoPlayer.loopPointReached -= OnLoopPointReached;
    }

    // ─────────────────────────────────────────────────────────────────────
    // PUBLIC API
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>Length of the assigned clip in seconds. 0 if no clip.</summary>
    public float VideoLength => videoPlayer != null && videoPlayer.clip != null
        ? (float)videoPlayer.clip.length
        : 0f;

    /// <summary>
    /// Assigns the clip and plays it once. If clip is null the panel stays hidden.
    /// </summary>
    public void Play(VideoClip clip)
    {
        if (clip == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        if (rawImage != null)
            rawImage.enabled = true;

        videoPlayer.clip   = clip;
        videoPlayer.frame  = 0;
        videoPlayer.Play();
    }

    /// <summary>Stops playback and hides the panel.</summary>
    public void Stop()
    {
        if (videoPlayer != null)
            videoPlayer.Stop();

        gameObject.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────────────

    private void OnLoopPointReached(VideoPlayer vp)
    {
        vp.Stop();
        OnVideoCompleted?.Invoke();
    }
}
