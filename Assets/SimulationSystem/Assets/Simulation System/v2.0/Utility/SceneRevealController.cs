// SceneRevealController.cs  (Per-Object Dissolve Version)
// ─────────────────────────────────────────────────────────────────────────────
// Drives the DissolveReveal shader across every Renderer in the scene.
// As _RevealRadius grows outward from the player, each object's shader
// receives the shared globals and dissolves in when the wave reaches it.
//
// SETUP
// ─────
//  1. Apply DissolveReveal.shader (or a variant of it) to every material
//     in your scene that should dissolve in.
//  2. Attach this script to any persistent GameObject (e.g. GameManager).
//  3. Optionally drag your VR camera into PlayerCamera; otherwise Camera.main
//     is used automatically.
//  4. Call RevealScene() to start the opening sequence.
//
// HOW IT WORKS
// ────────────
//  Rather than setting properties on individual materials (which would be
//  hundreds of SetFloat calls per frame), we use Shader.SetGlobalXxx so a
//  single call updates ALL materials using the shader simultaneously.
//  The wave position and player centre are global; each object's fragment
//  shader calculates its own dissolve progress from those globals.
// ─────────────────────────────────────────────────────────────────────────────

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

#if UNITY_EDITOR
using UnityEditor;
#endif

[AddComponentMenu("VR/Scene Reveal Controller")]
public class SceneRevealController : MonoBehaviour
{
    // ── Inspector ────────────────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("The VR headset / main camera transform. " +
             "Leave blank to use Camera.main automatically.")]
    public Transform PlayerCamera;

    [Header("Dissolve Material Setup")]
    [Tooltip("The material using DissolveReveal.shader that will be assigned " +
             "to every Renderer in RevealTargets when you use the context menu.")]
    public Material DissolveMaterial;

    [Tooltip("All Renderers in the scene that should dissolve in during the reveal. " +
             "Populate manually or use 'Collect All Scene Renderers' from the context menu.")]
    public List<Renderer> RevealTargets = new List<Renderer>();

    [Header("Reveal Settings")]
    [Tooltip("Duration in seconds used by the 'Perform Reveal' context menu button " +
             "and the default RevealScene() call.")]
    public float RevealDuration = 3f;

    [Tooltip("How far the wave travels in world units. Should reach (or exceed) " +
             "the farthest object in your scene.")]
    public float MaxRevealRadius = 80f;

    [Tooltip("World-unit width of the dissolve wave front. " +
             "Larger = more objects dissolving simultaneously.")]
    public float WaveBandwidth = 5f;

    [Header("Tile Settings")]
    [Tooltip("Size of each tile in world units. Smaller = finer grid, more tiles.")]
    public float TileSize = 0.5f;

    [Tooltip("How far ahead of the wave tiles start randomly popping. " +
             "Larger = looser, messier wave edge with more tiles popping at once.")]
    public float TileScatter = 3f;

    [Tooltip("Easing curve for the reveal animation " +
             "(X = normalised time, Y = normalised radius 0-1).")]
    public AnimationCurve RevealCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Events")]
    public UnityEvent OnRevealStarted;
    public UnityEvent OnRevealComplete;

    // ── Shader global property IDs ────────────────────────────────────────────

    static readonly int GlobRevealCenter = Shader.PropertyToID("_RevealCenter");
    static readonly int GlobRevealRadius = Shader.PropertyToID("_RevealRadius");
    static readonly int GlobWaveBandwidth = Shader.PropertyToID("_WaveBandwidth");
    static readonly int GlobTileSize = Shader.PropertyToID("_TileSize");
    static readonly int GlobTileScatter = Shader.PropertyToID("_TileScatter");

    // ── Private state ─────────────────────────────────────────────────────────

    Coroutine _activeRoutine;
    float _currentRadius = 0f;
    bool _revealActive = false;   // true once RevealScene has been called
    Vector3 _revealOrigin;            // player position snapshotted at reveal start

    // ── Properties ────────────────────────────────────────────────────────────

    /// <summary>True while an animated reveal / cover is running.</summary>
    public bool IsRevealing => _activeRoutine != null;

    /// <summary>Current wave radius in world units.</summary>
    public float CurrentRadius => _currentRadius;

    // ── Context Menu ──────────────────────────────────────────────────────────

    /// <summary>
    /// Right-click → "Collect All Scene Renderers"
    /// Scans the entire open scene for Renderers and populates RevealTargets.
    /// Run this once after your scene is dressed. Skips UI canvas renderers.
    /// </summary>
    [ContextMenu("Collect All Scene Renderers")]
    void ContextCollectRenderers()
    {
#if UNITY_EDITOR
        Undo.RecordObject(this, "Collect All Scene Renderers");
#endif
        RevealTargets.Clear();
        Renderer[] all = FindObjectsByType<Renderer>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        int skipped = 0;
        foreach (Renderer r in all)
        {
            // Skip UI canvas renderers and particle renderers if desired
            if (r is CanvasRenderer) { skipped++; continue; }
            RevealTargets.Add(r);
        }

        Debug.Log($"[SceneRevealController] Collected {RevealTargets.Count} renderers " +
                  $"({skipped} skipped). Assign DissolveMaterial then run " +
                  "'Apply Dissolve Material to All Targets'.");

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    /// <summary>
    /// Right-click → "Apply Dissolve Material to All Targets"
    /// APPENDS DissolveMaterial as an extra material slot on every Renderer in
    /// RevealTargets. The original materials are untouched — the dissolve overlay
    /// sits on top and clips itself away to reveal them.
    /// Safe to run multiple times; skips any Renderer that already carries it.
    /// </summary>
    [ContextMenu("Apply Dissolve Material to All Targets")]
    void ContextApplyMaterial()
    {
        if (DissolveMaterial == null)
        {
            Debug.LogError("[SceneRevealController] DissolveMaterial is not assigned. " +
                           "Drag your DissolveReveal material into the field first.");
            return;
        }

        if (RevealTargets == null || RevealTargets.Count == 0)
        {
            Debug.LogWarning("[SceneRevealController] RevealTargets list is empty. " +
                             "Run 'Collect All Scene Renderers' first.");
            return;
        }

        int appended = 0;
        int alreadyHas = 0;
        int nulls = 0;

        foreach (Renderer r in RevealTargets)
        {
            if (r == null) { nulls++; continue; }

            // Check whether the dissolve material is already present
            bool alreadyApplied = false;
            foreach (Material m in r.sharedMaterials)
            {
                if (m == DissolveMaterial) { alreadyApplied = true; break; }
            }

            if (alreadyApplied) { alreadyHas++; continue; }

#if UNITY_EDITOR
            Undo.RecordObject(r, "Append Dissolve Material");
#endif
            // Append: copy existing slots then add the dissolve overlay at the end
            Material[] original = r.sharedMaterials;
            Material[] expanded = new Material[original.Length + 1];
            original.CopyTo(expanded, 0);
            expanded[expanded.Length - 1] = DissolveMaterial;
            r.sharedMaterials = expanded;
            appended++;

#if UNITY_EDITOR
            EditorUtility.SetDirty(r);
#endif
        }

        Debug.Log($"[SceneRevealController] Dissolve overlay appended to {appended} " +
                  $"renderers. ({alreadyHas} already had it, {nulls} null entries skipped.)");
    }

    /// <summary>
    /// Right-click → "Remove Dissolve Material from All Targets"
    /// Strips DissolveMaterial from every Renderer in RevealTargets, restoring
    /// each one to its original material slots. Use this to cleanly undo the apply.
    /// </summary>
    [ContextMenu("Remove Dissolve Material from All Targets")]
    void ContextRemoveMaterial()
    {
        if (DissolveMaterial == null)
        {
            Debug.LogError("[SceneRevealController] DissolveMaterial is not assigned.");
            return;
        }

        int removed = 0;
        int nulls = 0;

        foreach (Renderer r in RevealTargets)
        {
            if (r == null) { nulls++; continue; }

            Material[] current = r.sharedMaterials;

            // Build a new list that excludes the dissolve material
            List<Material> stripped = new List<Material>(current.Length);
            bool found = false;
            foreach (Material m in current)
            {
                if (m == DissolveMaterial && !found)
                {
                    found = true;   // remove only the first occurrence
                    continue;
                }
                stripped.Add(m);
            }

            if (!found) continue;   // wasn't there, skip

#if UNITY_EDITOR
            Undo.RecordObject(r, "Remove Dissolve Material");
#endif
            r.sharedMaterials = stripped.ToArray();
            removed++;

#if UNITY_EDITOR
            EditorUtility.SetDirty(r);
#endif
        }

        Debug.Log($"[SceneRevealController] Dissolve overlay removed from {removed} " +
                  $"renderers. ({nulls} null entries skipped.)");
    }

    /// <summary>
    /// Right-click → "Perform Reveal (Play Mode)"
    /// Triggers RevealScene() using the inspector-set duration.
    /// Only works in Play Mode — use it to test the effect without code.
    /// </summary>
    [ContextMenu("Perform Reveal (Play Mode)")]
    void ContextPerformReveal()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[SceneRevealController] 'Perform Reveal' only works in " +
                             "Play Mode. Enter Play Mode first.");
            return;
        }
        RevealScene(RevealDuration);
    }

    /// <summary>
    /// Right-click → "Reset to Covered (Play Mode)"
    /// Snaps everything back to fully hidden so you can re-run the reveal.
    /// </summary>
    [ContextMenu("Reset to Covered (Play Mode)")]
    void ContextResetCover()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[SceneRevealController] 'Reset to Covered' only works " +
                             "in Play Mode.");
            return;
        }
        ResetCover();
        Debug.Log("[SceneRevealController] Scene reset to fully covered.");
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (PlayerCamera == null && Camera.main != null)
            PlayerCamera = Camera.main.transform;

        // Push radius=0 (fully covered) immediately.
        // Use transform.position as a safe fallback if camera isn't ready yet —
        // Start() will push the real camera position before any reveal fires.
        Vector3 initialCenter = PlayerCamera != null
                              ? PlayerCamera.position
                              : transform.position;

        Shader.SetGlobalVector(GlobRevealCenter,
            new Vector4(initialCenter.x, initialCenter.y, initialCenter.z, 0f));
        PushGlobals(0f);
    }

    void Start()
    {
        // Second chance to resolve the camera (some VR rigs initialize late).
        if (PlayerCamera == null && Camera.main != null)
            PlayerCamera = Camera.main.transform;

        // Ensure the center is correct now that all objects have Awake'd.
        if (!_revealActive && PlayerCamera != null)
        {
            Vector3 p = PlayerCamera.position;
            Shader.SetGlobalVector(GlobRevealCenter, new Vector4(p.x, p.y, p.z, 0f));
        }
    }

    void LateUpdate()
    {
        // Before the reveal begins, keep the center tracking the player so it
        // is always up to date when RevealScene() fires.
        // Once the reveal starts, _revealOrigin is snapshotted and frozen —
        // we never update the center again so the wave always expands from
        // exactly where the player was standing when they called RevealScene().
        if (!_revealActive && PlayerCamera != null)
        {
            Vector3 p = PlayerCamera.position;
            Shader.SetGlobalVector(GlobRevealCenter, new Vector4(p.x, p.y, p.z, 0f));
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Animate the dissolve wave outward from the player's position.
    /// Safe to call mid-animation — resumes from the current radius.
    /// </summary>
    /// <param name="duration">Time in seconds for a full 0 → max reveal. " +
    /// Defaults to the RevealDuration inspector field.</param>
    public void RevealScene(float duration = -1f)
    {
        if (duration < 0f) duration = RevealDuration;
        StopActive();

        // ── Snapshot the player's standing position right now ─────────────
        // This becomes the fixed origin of the radial wave. The player can
        // move freely after this point without shifting the reveal centre.
        if (PlayerCamera != null)
        {
            _revealOrigin = PlayerCamera.position;
            Shader.SetGlobalVector(GlobRevealCenter,
                new Vector4(_revealOrigin.x, _revealOrigin.y, _revealOrigin.z, 0f));
        }
        else
        {
            Debug.LogWarning("[SceneRevealController] PlayerCamera is null — " +
                             "reveal origin defaults to (0,0,0). " +
                             "Assign PlayerCamera in the Inspector.");
        }

        Debug.Log($"[SceneRevealController] RevealScene started. " +
                  $"Origin={_revealOrigin}, Duration={duration}s, MaxRadius={MaxRevealRadius}");

        _revealActive = true;
        _activeRoutine = StartCoroutine(
            AnimateRadius(_currentRadius, MaxRevealRadius, duration, revealing: true));
    }

    /// <summary>
    /// Animate the wave back to zero — all objects dissolve out again.
    /// Useful for scene transitions or re-entering a space.
    /// </summary>
    /// <param name="duration">Cover duration in seconds.</param>
    public void CoverScene(float duration = 1.5f)
    {
        StopActive();
        _activeRoutine = StartCoroutine(
            AnimateRadius(_currentRadius, 0f, duration, revealing: false));
    }

    /// <summary>
    /// Drive the reveal manually with a normalised value [0, 1].
    /// 0 = everything hidden, 1 = everything visible.
    /// Ideal for Unity Timeline or hand-authored animation curves.
    /// </summary>
    public void SetRevealProgress(float t)
    {
        StopActive();
        PushGlobals(Mathf.Lerp(0f, MaxRevealRadius, Mathf.Clamp01(t)));
    }

    /// <summary>Instantly hide everything (snap radius to 0).</summary>
    public void ResetCover()
    {
        StopActive();
        _revealActive = false;
        PushGlobals(0f);
        // Re-apply the overlay so the scene is fully covered again and the
        // reveal can be triggered a second time.
        ContextApplyMaterial();
        Debug.Log("[SceneRevealController] Reset to covered — dissolve overlay re-applied.");
    }

    /// <summary>Instantly show everything (snap radius to max).</summary>
    public void RevealInstant()
    {
        StopActive();
        PushGlobals(MaxRevealRadius);
    }

    // ── Coroutine ─────────────────────────────────────────────────────────────

    IEnumerator AnimateRadius(float from, float to, float duration, bool revealing)
    {
        if (revealing) OnRevealStarted?.Invoke();

        duration = Mathf.Max(duration, 0.01f);
        float elapsed = 0f;
        int frame = 0;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            PushGlobals(Mathf.Lerp(from, to, RevealCurve.Evaluate(t)));

            // Log every 60 frames so you can confirm the radius is advancing
            if (frame % 60 == 0)
                Debug.Log($"[SceneRevealController] Radius={_currentRadius:F1} / {to:F1}  " +
                          $"({t * 100f:F0}%)  Center={Shader.GetGlobalVector(GlobRevealCenter)}");
            frame++;

            yield return null;
        }

        PushGlobals(to);
        _activeRoutine = null;
        _revealActive = false;

        if (revealing && Mathf.Approximately(to, MaxRevealRadius))
        {
            OnRevealComplete?.Invoke();
            // Strip the overlay off every renderer now that the reveal is done.
            // This is the cleanest way to restore original materials — no shader
            // math needed, the overlay simply no longer exists on any renderer.
            ContextRemoveMaterial();
            Debug.Log("[SceneRevealController] Reveal complete — dissolve overlay removed from all renderers.");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void PushGlobals(float radius)
    {
        _currentRadius = radius;
        Shader.SetGlobalFloat(GlobRevealRadius, radius);
        Shader.SetGlobalFloat(GlobWaveBandwidth, WaveBandwidth);
        Shader.SetGlobalFloat(GlobTileSize, TileSize);
        Shader.SetGlobalFloat(GlobTileScatter, TileScatter);
    }

    void StopActive()
    {
        if (_activeRoutine == null) return;
        StopCoroutine(_activeRoutine);
        _activeRoutine = null;
    }

    // ── Gizmos ────────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Vector3 origin = PlayerCamera != null
                       ? PlayerCamera.position
                       : transform.position;

        // Wave front ring
        Handles.color = new Color(0.9f, 0.55f, 0.1f, 0.8f);
        Handles.DrawWireDisc(origin, Vector3.up, _currentRadius);

        // Inner edge of wave bandwidth
        Handles.color = new Color(0.9f, 0.55f, 0.1f, 0.2f);
        Handles.DrawWireDisc(origin, Vector3.up,
                             Mathf.Max(0f, _currentRadius - WaveBandwidth));

        // Max radius
        Handles.color = new Color(0.3f, 0.8f, 1f, 0.25f);
        Handles.DrawWireDisc(origin, Vector3.up, MaxRevealRadius);
    }
#endif
}