using TMPro;
using UnityEngine;

public class FamiliarizationUIPanel : MonoBehaviour
{
    [Header("Text References")]
    public TMP_Text nameLabel;
    public TMP_Text descriptionLabel;

    [Header("Visibility Control")]
    [Tooltip("Untick to hide the description panel (name + text) for this step.")]
    public bool showDescriptionUI = true;

    [Tooltip("Untick to hide the video panel for this step.")]
    public bool showVideoUI = true;

    [Tooltip("The description panel child GO to show/hide. " +
             "Must be assigned for showDescriptionUI to work.")]
    public GameObject descriptionPanelGO;

    [Tooltip("The video panel child GO to show/hide. " +
             "Must be assigned for showVideoUI to work.")]
    public GameObject videoPanelGO;

    private void Awake()
    {
        gameObject.SetActive(false);
    }

    public void Show(string name, string description)
    {
        gameObject.SetActive(true);

        // ── Description panel ─────────────────────────────────────────────
        if (descriptionPanelGO != null)
            descriptionPanelGO.SetActive(showDescriptionUI);

        if (showDescriptionUI)
        {
            if (nameLabel != null) nameLabel.text = name;
            if (descriptionLabel != null) descriptionLabel.text = description;
        }

        // ── Video panel ───────────────────────────────────────────────────
        // showVideoUI = false hides the panel GO immediately.
        // If true, FamiliarizationVideoPanel.Play controls it.
        if (videoPanelGO != null && !showVideoUI)
            videoPanelGO.SetActive(false);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}