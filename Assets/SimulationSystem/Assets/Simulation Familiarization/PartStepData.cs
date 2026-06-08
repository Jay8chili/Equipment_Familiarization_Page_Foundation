// ════════════════════════════════════════════════════════════════════════════
//  PartStepData.cs
// ════════════════════════════════════════════════════════════════════════════

using UnityEngine;
using UnityEngine.Video;

public enum PartStepType
{
    Station,
    Part
}

public class PartStepData : MonoBehaviour
{
    [Tooltip("Display name of this part or station. Shown in the UI panel title.")]
    public string displayName = string.Empty;

    [Tooltip("Whether this step is a Station intro or an individual Part description.")]
    public PartStepType stepType = PartStepType.Part;

    [Tooltip("Station group ID this step belongs to (e.g. S01).")]
    public string stationId = string.Empty;

    [Tooltip("The mesh or parent GameObject to highlight when this step is active.")]
    public GameObject highlightTarget;

    [Tooltip("The world-space UI panel. Assigned automatically by the wizard.")]
    public FamiliarizationUIPanel uiPanel;

    [Tooltip("The video panel child of the UI prefab. Assigned automatically by the wizard.")]
    public FamiliarizationVideoPanel videoPanel;

    [Tooltip("Video clip to play for this step. Leave empty if no video.")]
    public VideoClip videoClip;
}