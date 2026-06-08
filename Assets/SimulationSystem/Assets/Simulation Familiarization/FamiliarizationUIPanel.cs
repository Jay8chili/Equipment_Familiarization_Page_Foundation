// ════════════════════════════════════════════════════════════════════════════
//  FamiliarizationUIPanel.cs
//
//  Attach to each world-space UI panel GameObject that lives under the
//  UI Parent holder in the scene.
//
//  Holds references to the two TMP text fields (name label + description).
//  Exposes Show() and Hide() which are called by PartsIdentificationManager.
//
//  SETUP
//      1. Create your world-space Canvas panel prefab with two TMP_Text
//         children — one for the part/station name, one for the description.
//      2. Add this component to the panel root.
//      3. Wire nameLabel and descriptionLabel in the Inspector.
//      4. The wizard auto-assigns this panel to its matching PartStepData.uiPanel.
// ════════════════════════════════════════════════════════════════════════════

using TMPro;
using UnityEngine;

public class FamiliarizationUIPanel : MonoBehaviour
{
    [Header("Text References")]
    [Tooltip("TMP label that displays the part or station name.")]
    public TMP_Text nameLabel;

    [Tooltip("TMP label that displays the description (prompt text).")]
    public TMP_Text descriptionLabel;

    // ─────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Start hidden — PartsIdentificationManager controls visibility
        gameObject.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────────────
    // PUBLIC API
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Populates both text fields and shows the panel.
    /// Called by PartsIdentificationManager when the step's prompt starts.
    /// </summary>
    public void Show(string name, string description)
    {
        if (nameLabel != null)
            nameLabel.text = name;

        if (descriptionLabel != null)
            descriptionLabel.text = description;

        gameObject.SetActive(true);
    }

    /// <summary>
    /// Hides the panel.
    /// Called by PartsIdentificationManager when AssistantManager.PromptCompleted fires.
    /// </summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
