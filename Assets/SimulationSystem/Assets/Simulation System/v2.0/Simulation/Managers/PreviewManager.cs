using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Video;
using SimulationSystem.V02.StateInteractions;

/// <summary>
/// PreviewManager
/// ──────────────
/// Inspects the current interaction in the active SimulationState and plays
/// an appropriate preview clip on a VideoPlayer.
///
/// DETECTION LOGIC (checked in order):
///   DetectInteraction →
///       Any ObjectToDetect tagged "Hand"       → detectWithHandClip
///       2+ grabbables, !detectSeparately        → detectWith2GrabsClip
///       1  grabbable                            → detectWithGrabClip
///       else (no grabbables)                    → detectWithHandClip (fallback)
///   GrabInteraction                             → grabClip
///   UIInteraction                               → uiClip
///   IdleInteraction                             → idleClip
///   GazeInteraction                             → gazeClip
///
/// USAGE:
///   Wire the Preview button's onClick to PreviewManager.ShowPreview().
///   Wire a Close / Done button to PreviewManager.HidePreview().
/// </summary>
public class PreviewManager : MonoBehaviour
{
    // ── Singleton ────────────────────────────────────────────────────────────

    public static PreviewManager Instance;

    // ── Inspector — Clips ────────────────────────────────────────────────────

    [Header("Preview Clips")]
    [Tooltip("Detect interaction where ObjectToDetect is a bare hand (tagged 'Hand').")]
    public VideoClip detectWithHandClip;

    [Tooltip("Detect interaction with a single grabbable object.")]
    public VideoClip detectWithGrabClip;

    [Tooltip("Detect interaction with 2+ grabbable objects simultaneously (!detectSeparately).")]
    public VideoClip detectWith2GrabsClip;

    [Tooltip("Standalone GrabInteraction (not inside a Detect).")]
    public VideoClip grabClip;

    [Tooltip("UIInteraction (button hold / bot-handled UI).")]
    public VideoClip uiClip;

    [Tooltip("IdleInteraction (timer or manual advance).")]
    public VideoClip idleClip;

    [Tooltip("GazeInteraction (look-at to complete).")]
    public VideoClip gazeClip;

    // ── Inspector — Playback ─────────────────────────────────────────────────

    [Header("Playback")]
    [Tooltip("VideoPlayer used to play the preview clips.")]
    public VideoPlayer videoPlayer;

    [Tooltip("GameObject holding the preview UI panel (video + close button). " +
             "Enabled on ShowPreview, disabled on HidePreview.")]
    public GameObject previewPanel;

    // ── Inspector — Events ───────────────────────────────────────────────────

    [Header("Events")]
    public UnityEvent OnPreviewStarted;
    public UnityEvent OnPreviewEnded;

    // ── State ────────────────────────────────────────────────────────────────

    private bool _isPreviewing;
    public bool isPreviewing => _isPreviewing;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        /*if (previewPanel != null)
            previewPanel.SetActive(false);*/
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Pause the simulation, detect the current interaction type,
    /// pick the correct clip, and play it.
    /// Wire this to your Preview button's onClick.
    /// </summary>
    public void ShowPreview()
    {
        if (_isPreviewing) return;

        // Pause the simulation so interactions freeze
        SimulationManager.Instance?.PauseSimulation();

        // Resolve which clip to play
        VideoClip clip = ResolveClip();

        if (clip == null)
        {
            Debug.LogWarning("[PreviewManager] No clip resolved for the current interaction.");
            return;
        }

        // Show the preview UI and play
        _isPreviewing = true;

        if (previewPanel != null)
            previewPanel.SetActive(true);

        if (videoPlayer != null)
        {
            videoPlayer.clip = clip;
            videoPlayer.Stop();
            videoPlayer.Play();

            // Auto-hide when the clip finishes (optional — player can also close manually)
            videoPlayer.loopPointReached -= OnClipFinished;
            videoPlayer.loopPointReached += OnClipFinished;
        }

        OnPreviewStarted?.Invoke();
        Debug.Log($"[PreviewManager] Playing preview: {clip.name}");
    }

    /// <summary>
    /// Stop the preview and resume the simulation.
    /// Wire this to your Close / Done button's onClick.
    /// </summary>
    public void HidePreview()
    {
        if (!_isPreviewing) return;

        _isPreviewing = false;

        if (videoPlayer != null)
        {
            videoPlayer.Stop();
            videoPlayer.loopPointReached -= OnClipFinished;
        }

        if (previewPanel != null)
            previewPanel.SetActive(false);

        // Resume the simulation
        SimulationManager.Instance?.ResumeSimulation();

        OnPreviewEnded?.Invoke();
        Debug.Log("[PreviewManager] Preview ended — simulation resumed.");
    }

    /// <summary>
    /// Toggle between showing and hiding the preview.
    /// Convenient for a single button.
    /// </summary>
    public void TogglePreview()
    {
        if (_isPreviewing)
            HidePreview();
        else
            ShowPreview();
    }

    // ── Clip Resolution ──────────────────────────────────────────────────────

    /// <summary>
    /// Inspect the current interaction from SimulationState and return
    /// the appropriate VideoClip.
    /// </summary>
    private VideoClip ResolveClip()
    {
        var state = SimulationManager.Instance?.currentState;
        if (state == null) return null;

        Interactions current = state.currentInteraction;
        if (current == null) return null;

        // ── DetectInteraction ────────────────────────────────────────────
        if (current is DetectInteraction detect)
            return ResolveDetectClip(detect);

        // ── GrabInteraction ──────────────────────────────────────────────
        if (current is GrabInteraction)
            return grabClip;

        // ── UIInteraction ────────────────────────────────────────────────
        if (current is UIInteraction)
            return uiClip;

        // ── IdleInteraction ──────────────────────────────────────────────
        if (current is IdleInteraction)
            return idleClip;

        // ── GazeInteraction ──────────────────────────────────────────────
        if (current is GazeInteraction)
            return gazeClip;

        Debug.LogWarning($"[PreviewManager] Unknown interaction type: {current.GetType().Name}");
        return null;
    }

    /// <summary>
    /// Sub-resolution for DetectInteraction based on what's in ObjectsToBeDetectedList.
    /// </summary>
    private VideoClip ResolveDetectClip(DetectInteraction detect)
    {
        var objects = detect.ObjectsToBeDetectedList;

        // Empty or null list — fall back to hand detect
        if (objects == null || objects.Count == 0)
            return detectWithHandClip;

        // Check if any object is tagged "Hand" — bare hand detection
        foreach (var obj in objects)
        {
            if (obj != null && obj.CompareTag("Hand"))
                return detectWithHandClip;
        }

        // Count how many objects have a GrabInteraction component
        int grabbableCount = 0;
        foreach (var obj in objects)
        {
            if (obj != null && obj.GetComponent<GrabInteraction>() != null)
                grabbableCount++;
        }

        // No grabbables at all — treat as hand detect
        if (grabbableCount == 0)
            return detectWithHandClip;

        // 2+ grabbables that must enter simultaneously (not separately)
        if (grabbableCount >= 2 && !detect.detectSeparately)
            return detectWith2GrabsClip;

        // Single grabbable (or multiple but detectSeparately = true, 
        // meaning each one is an independent detect — single grab flow)
        return detectWithGrabClip;
    }

    // ── Internal ─────────────────────────────────────────────────────────────

    private void OnClipFinished(VideoPlayer vp)
    {
        // Auto-hide when clip reaches the end (non-looping clips)
        if (!vp.isLooping)
            HidePreview();
    }
}
