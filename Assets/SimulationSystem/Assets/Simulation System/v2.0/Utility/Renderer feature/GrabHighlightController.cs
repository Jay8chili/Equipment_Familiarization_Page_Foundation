using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GrabHighlightController : MonoBehaviour
{
    [Header("Appearance")]
    public Color RimColor = new Color(0f, 0.85f, 1f, 1f);
    [Range(0f, 1f)]
    public float MaxAmount = 1.0f;

    [Header("Transition")]
    [Range(0.05f, 1f)] public float FadeInDuration = 0.25f;
    [Range(0.05f, 1f)] public float FadeOutDuration = 0.45f;

    private const string LAYER_NAME = "Grabbable";

    private readonly List<GrabHighlightRegistry.RendererEntry> _entries
        = new List<GrabHighlightRegistry.RendererEntry>();

    private Coroutine _coroutine;
    private float _currentAmount = 0f;
    private int _originalLayer;          // layer before highlight
    private int _grabbableLayer = -1;

    private void Awake()
    {
        // Store the original layer so we can restore it on HideHighlight
        _originalLayer = gameObject.layer;

        // Resolve the Grabbable layer index
        _grabbableLayer = LayerMask.NameToLayer(LAYER_NAME);
        if (_grabbableLayer == -1)
        {
#if UNITY_EDITOR
            _grabbableLayer = TryCreateLayer(LAYER_NAME);
            if (_grabbableLayer == -1)
            {
                Debug.LogError($"[GrabHighlight] Could not create layer '{LAYER_NAME}'. All 32 slots are full.");
                return;
            }
            Debug.Log($"[GrabHighlight] Layer '{LAYER_NAME}' created at index {_grabbableLayer}.");
#else
            Debug.LogError($"[GrabHighlight] Layer '{LAYER_NAME}' missing. Add it in Project Settings.");
            return;
#endif
        }

        // Register renderers but do NOT set the layer yet.
        // Layer is set only when ShowHighlight() is called.
        var renderers = GetComponentsInChildren<Renderer>(includeInactive: true);

        if (renderers.Length == 0)
        {
            Debug.LogError($"[GrabHighlight] '{gameObject.name}' has no Renderer components in its hierarchy.", this);
            return;
        }

        foreach (var r in renderers)
        {
            var entry = GrabHighlightRegistry.Register(r, RimColor);
            _entries.Add(entry);
        }

        Debug.Log($"[GrabHighlight] '{gameObject.name}' registered {_entries.Count} renderer(s).");
    }

    private void OnDestroy()
    {
        foreach (var e in _entries)
            GrabHighlightRegistry.Unregister(e.Renderer);
    }

    // ── Public API ─────────────────────────────────────────────────────────

    public void ShowHighlight()
    {
        if (_entries.Count == 0)
        {
            Debug.LogError($"[GrabHighlight] ShowHighlight called but no renderers registered on '{gameObject.name}'.", this);
            return;
        }

        // Switch to Grabbable layer so the RendererList picks this object up
        SetLayerRecursive(gameObject, _grabbableLayer);

        Fade(MaxAmount, FadeInDuration);
    }

    public void HideHighlight()
    {
        Fade(0f, FadeOutDuration);
    }

    public void SetAmountImmediate(float amount)
    {
        if (_coroutine != null) StopCoroutine(_coroutine);
        _currentAmount = Mathf.Clamp01(amount);
        Push(_currentAmount);
    }

    // ── Internal ───────────────────────────────────────────────────────────

    private void Fade(float target, float duration)
    {
        if (_coroutine != null) StopCoroutine(_coroutine);
        _coroutine = StartCoroutine(FadeRoutine(target, duration));
    }

    private IEnumerator FadeRoutine(float target, float duration)
    {
        float start = _currentAmount;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            _currentAmount = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t));
            Push(_currentAmount);
            yield return null;
        }
        _currentAmount = target;
        Push(_currentAmount);

        // Once fully hidden restore the original layer
        if (_currentAmount <= 0f)
            SetLayerRecursive(gameObject, _originalLayer);

        _coroutine = null;
    }

    private void Push(float amount)
    {
        foreach (var e in _entries)
        {
            e.Amount = amount;
            e.RimColor = RimColor;
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static void SetLayerRecursive(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursive(child.gameObject, layer);
    }

#if UNITY_EDITOR
    private static int TryCreateLayer(string name)
    {
        var tagManager = new UnityEditor.SerializedObject(
            UnityEditor.AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layersProp = tagManager.FindProperty("layers");
        for (int i = 8; i < layersProp.arraySize; i++)
        {
            var layerProp = layersProp.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(layerProp.stringValue))
            {
                layerProp.stringValue = name;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                return i;
            }
        }
        return -1;
    }
#endif
}