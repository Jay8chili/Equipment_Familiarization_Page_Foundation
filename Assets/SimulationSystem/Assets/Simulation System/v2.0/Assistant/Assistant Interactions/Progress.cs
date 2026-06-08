using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Progress : MonoBehaviour
{
    public enum DisplayMode { Percentage, StepIndex }

    [Tooltip("Percentage = shows '75%'. StepIndex = shows 'Step 3'.")]
    [SerializeField] private DisplayMode displayMode = DisplayMode.Percentage;

    [SerializeField] private TMP_Text progressText;

    private Slider _progressBar;
    private float _lastProgress;
    private int _lastStepIndex;

    private void Awake()
    {
        _progressBar = GetComponent<Slider>();
    }

    /// <summary>Updates slider and text, caches values for end-of-animation refresh.</summary>
    public void UpdateValues(float progress, int stepIndex)
    {
        _lastProgress = progress;
        _lastStepIndex = stepIndex;
        if (_progressBar != null)
            _progressBar.value = progress;
        UpdateText(progress, stepIndex);
    }

    /// <summary>Sets slider value directly (used during animated updates).</summary>
    public void SetSliderValue(float value)
    {
        if (_progressBar != null)
            _progressBar.value = value;
    }

    /// <summary>Snaps slider to final value and refreshes text with cached values.</summary>
    public void SnapToFinal(float value)
    {
        if (_progressBar != null)
            _progressBar.value = value;
        UpdateText(_lastProgress, _lastStepIndex);
    }

    /// <summary>Returns the current slider value (animation start point).</summary>
    public float GetSliderValue() => _progressBar != null ? _progressBar.value : 0f;

    private void UpdateText(float progress, int stepIndex)
    {
        if (progressText == null) return;
        progressText.text = displayMode == DisplayMode.Percentage
            ? $"{progress * 100f:F0}%"
            : $"Step {stepIndex}";
    }
}