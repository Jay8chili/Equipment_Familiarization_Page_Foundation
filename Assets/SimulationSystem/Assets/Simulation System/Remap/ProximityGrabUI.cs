using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Attach this to a world-space Canvas prefab. GrabInteraction spawns it when
/// a hand enters proximity and drives it each frame.
///
/// PREFAB STRUCTURE (example):
/// ────────────────────────────
///   ProximityGrabCanvas  (Canvas — World Space, this component)
///     ├── Background     (Image — optional backing)
///     ├── FillImage      (Image — type = Filled, radial 360)
///     └── Label          (TextMeshProUGUI)
///
/// GrabInteraction calls SetProgress(0..1) every frame during the proximity
/// timer. When progress reaches 1 the grab fires and the UI is hidden.
///
/// The canvas auto-faces the main camera every frame (billboard).
///
/// POP ANIMATION
/// ─────────────
/// PopIn  → enables GO, scales from 0 → targetScale with OutBack (bouncy overshoot).
/// PopOut → scales from current → 0 with InBack (tucks in), then disables GO.
/// </summary>
public class ProximityGrabUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Radial fill image (Image type = Filled, Fill Method = Radial 360).")]
    public Image fillImage;

    public bool LookAtCamera = true;
    [Tooltip("Text label shown on the indicator.")]
    public TextMeshProUGUI label;
    public Transform CameraTransform;
    private Transform _cameraTransform;
    private Coroutine _popCoroutine;

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Initialize the UI. Called once right after instantiation.
    /// </summary>
    /// 
    
   
    public void Initialize(string text)
    {
        if (label != null)
            label.text = text;

        if (fillImage != null)
            fillImage.fillAmount = 0f;

        if(LookAtCamera)
        {
        _cameraTransform = Camera.main != null ? Camera.main.transform : null;
        }
        gameObject.SetActive(true);
    }

    /// <summary>
    /// Set the radial fill progress (0 = empty, 1 = full).
    /// </summary>
    public void SetProgress(float progress01)
    {
        if (fillImage != null)
            fillImage.fillAmount = Mathf.Clamp01(progress01);
    }

    /// <summary>
    /// Update the label text at runtime.
    /// </summary>
    public void SetText(string text)
    {
        if (label != null)
            label.text = text;
    }

  
    /// <summary>
    /// Hide and destroy the UI.
    /// </summary>
    public void Dismiss()
    {
        Destroy(gameObject);
    }

    // ── Pop Animation API ────────────────────────────────────────────────────

    /// <summary>
    /// Enable the GameObject, reset fill to 0, then scale from 0 → targetScale
    /// with a bouncy OutBack ease. Calls onComplete when the pop finishes.
    /// </summary>
 

   

    // ── Billboard ────────────────────────────────────────────────────────────

    private void LateUpdate()
    {
       if(LookAtCamera)
       {
        if (_cameraTransform == null)
        {
            _cameraTransform = Camera.main != null ? Camera.main.transform : null;
            if (_cameraTransform == null) return;
        }

        // Face the camera — flip forward so text reads correctly.
       transform.LookAt(_cameraTransform);
    }}
}
