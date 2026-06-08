using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Temporarily swaps all Renderer materials on this GameObject (and its children)
/// to the Bot/Dissolve shader, animates _DissolveAmount, then restores the originals.
///
/// Setup:
///   - Attach to any object you want to dissolve.
///   - Assign dissolveMaterial (an instance of the Bot/Dissolve shader) in the Inspector,
///     OR call SetDissolveMaterial() from code. Leave null to auto-create at runtime.
///   - Call DissolveOut() to disintegrate. Call DissolveIn() to re-materialise.
///   - AssistantManager.MoveObjectToDestination() drives these automatically when
///     discardOnComplete is true — you don't need to call them manually.
/// </summary>
public class DissolveController : MonoBehaviour
{
    [Header("Dissolve Settings")]
    [Tooltip("A material using the Bot/Dissolve shader. Leave null to auto-create one.")]
    public Material dissolveMaterial;

    [Tooltip("Noise texture used for the dissolve pattern. " +
             "Any greyscale noise works — Perlin, Voronoi, etc.")]
    public Texture2D noiseTexture;

    [Tooltip("Colour of the glowing edge at the dissolve boundary.")]
    public Color edgeColor = new Color(0.3f, 0.8f, 1.0f, 1f);

    [Range(0f, 0.2f)]
    public float edgeWidth = 0.04f;
    [Range(1f, 20f)]
    public float edgeIntensity = 6f;

    // ─────────────────────────────────────────────
    // INTERNALS
    // ─────────────────────────────────────────────

    private static readonly int ID_Dissolve = Shader.PropertyToID("_DissolveAmount");
    private static readonly int ID_Noise = Shader.PropertyToID("_NoiseMap");
    private static readonly int ID_EdgeColor = Shader.PropertyToID("_EdgeColor");
    private static readonly int ID_EdgeWidth = Shader.PropertyToID("_EdgeWidth");
    private static readonly int ID_EdgeIntens = Shader.PropertyToID("_EdgeIntensity");

    // Per-renderer original materials, saved before swapping
    private struct RendererSnapshot
    {
        public Renderer renderer;
        public Material[] originalMaterials;
    }

    private List<RendererSnapshot> _snapshots = new List<RendererSnapshot>();
    private List<Material> _dissolveInstances = new List<Material>(); // one per renderer slot
    private Coroutine _activeCoroutine;
    private bool _materialsSwapped = false;

    // ─────────────────────────────────────────────
    // PUBLIC API
    // ─────────────────────────────────────────────

    /// <summary>
    /// Animate from fully visible to fully dissolved over <paramref name="duration"/> seconds.
    /// Calls <paramref name="onComplete"/> when finished.
    /// </summary>
    public void DissolveOut(float duration, System.Action onComplete = null)
    {
        StopActive();
        _activeCoroutine = StartCoroutine(AnimateDissolve(0f, 1f, duration, onComplete));
    }

    /// <summary>
    /// Snap object to fully dissolved instantly (no animation), then animate back
    /// to fully visible over <paramref name="duration"/> seconds.
    /// Calls <paramref name="onComplete"/> when finished.
    /// </summary>
    public void DissolveIn(float duration, System.Action onComplete = null)
    {
        StopActive();
        _activeCoroutine = StartCoroutine(AnimateDissolve(1f, 0f, duration, onComplete));
    }

    /// <summary>Cancel any running dissolve animation and restore original materials.</summary>
    public void CancelDissolve()
    {
        StopActive();
        RestoreOriginalMaterials();
    }

    // ─────────────────────────────────────────────
    // CORE ANIMATION
    // ─────────────────────────────────────────────

    private IEnumerator AnimateDissolve(float from, float to, float duration, System.Action onComplete)
    {
        EnsureMaterialsReady();
        SwapToDissolve();
        SetDissolveAmount(from);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.SmoothStep(0f, 1f, t);   // smooth start and end
            SetDissolveAmount(Mathf.Lerp(from, to, eased));
            yield return null;
        }

        SetDissolveAmount(to);

        // Restore originals once fully visible again; keep dissolved when hidden
        if (Mathf.Approximately(to, 0f))
            RestoreOriginalMaterials();

        _activeCoroutine = null;
        onComplete?.Invoke();
    }

    // ─────────────────────────────────────────────
    // MATERIAL MANAGEMENT
    // ─────────────────────────────────────────────

    private void EnsureMaterialsReady()
    {
        // Auto-create dissolve material if none assigned
        if (dissolveMaterial == null)
        {
            var shader = Shader.Find("Bot/Dissolve");
            if (shader == null)
            {
                Debug.LogError("[DissolveController] Shader 'Bot/Dissolve' not found. " +
                               "Make sure BotDissolve.shader is in your project.");
                return;
            }
            dissolveMaterial = new Material(shader);
        }

        // Collect all Renderers (this object + children)
        if (_snapshots.Count == 0)
        {
            var renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            foreach (var r in renderers)
            {
                _snapshots.Add(new RendererSnapshot
                {
                    renderer = r,
                    originalMaterials = r.sharedMaterials
                });

                // One dissolve instance per material slot so multi-material objects work
                foreach (var _ in r.sharedMaterials)
                {
                    var inst = new Material(dissolveMaterial);
                    ApplySettings(inst);
                    _dissolveInstances.Add(inst);
                }
            }
        }
        else
        {
            // Refresh settings on existing instances in case they changed in Inspector
            foreach (var inst in _dissolveInstances) ApplySettings(inst);
        }
    }

    private void SwapToDissolve()
    {
        if (_materialsSwapped) return;
        _materialsSwapped = true;

        int instanceIndex = 0;
        foreach (var snap in _snapshots)
        {
            var replacements = new Material[snap.originalMaterials.Length];
            for (int i = 0; i < replacements.Length; i++)
            {
                // Copy the original's base texture into the dissolve instance albedo
                var orig = snap.originalMaterials[i];
                var inst = _dissolveInstances[instanceIndex + i];
                if (orig != null && orig.HasProperty("_BaseMap"))
                    inst.SetTexture("_BaseMap", orig.GetTexture("_BaseMap"));
                else if (orig != null && orig.HasProperty("_MainTex"))
                    inst.SetTexture("_BaseMap", orig.GetTexture("_MainTex"));
                replacements[i] = inst;
            }
            snap.renderer.materials = replacements;
            instanceIndex += snap.originalMaterials.Length;
        }
    }

    private void RestoreOriginalMaterials()
    {
        if (!_materialsSwapped) return;
        _materialsSwapped = false;

        foreach (var snap in _snapshots)
            if (snap.renderer != null)
                snap.renderer.sharedMaterials = snap.originalMaterials;
    }

    private void SetDissolveAmount(float amount)
    {
        foreach (var inst in _dissolveInstances)
            inst.SetFloat(ID_Dissolve, amount);
    }

    private void ApplySettings(Material mat)
    {
        if (noiseTexture != null) mat.SetTexture(ID_Noise, noiseTexture);
        mat.SetColor(ID_EdgeColor, edgeColor);
        mat.SetFloat(ID_EdgeWidth, edgeWidth);
        mat.SetFloat(ID_EdgeIntens, edgeIntensity);
    }

    private void StopActive()
    {
        if (_activeCoroutine != null)
        {
            StopCoroutine(_activeCoroutine);
            _activeCoroutine = null;
        }
    }

    // ─────────────────────────────────────────────
    // CLEANUP
    // ─────────────────────────────────────────────

    private void OnDestroy()
    {
        foreach (var inst in _dissolveInstances)
            if (inst != null) Destroy(inst);
    }
}