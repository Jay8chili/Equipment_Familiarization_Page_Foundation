// ════════════════════════════════════════════════════════════════════════════
//  PartStepData.cs
//  Sits alongside SimulationState on each generated state GameObject.
//  Carries all familiarization metadata for that step.
//  Populated automatically by the Parts Identification Wizard.
//  Read at runtime by PartsIdentificationManager.
// ════════════════════════════════════════════════════════════════════════════

using UnityEngine;

public enum PartStepType
{
    Station,    // Intro step that describes the whole station
    Part        // Step that describes an individual part
}

public class PartStepData : MonoBehaviour
{
    [Tooltip("Display name of this part or station. Shown in the UI panel title.")]
    public string displayName = string.Empty;

    [Tooltip("Whether this step is a Station intro or an individual Part description.")]
    public PartStepType stepType = PartStepType.Part;

    [Tooltip("Station group ID this step belongs to (e.g. S01). " +
             "All parts of a station share the same ID as their station intro.")]
    public string stationId = string.Empty;

    [Tooltip("The mesh or parent GameObject to highlight when this step is active.")]
    public GameObject highlightTarget;

    [Tooltip("The world-space UI panel to show when this step's prompt starts " +
             "and hide when the prompt audio completes. " +
             "Assigned automatically by the wizard from the UI Parent.")]
    public FamiliarizationUIPanel uiPanel;
}