using UnityEngine;
using TMPro;

public class PromptInteraction : MonoBehaviour
{
    [Header("Prompt UI")]
    [SerializeField] private GameObject promptPanel;
    [SerializeField] private TMP_Text promptText;

    /// <summary>
    /// Show the prompt panel with <paramref name="message"/>.
    /// </summary>
    public void SetPrompt(string message)
    {
        if (promptPanel == null || promptText == null) return;

        // Activate the panel first so RectTransform sizes are valid
        promptPanel.SetActive(true);

        var panelRect = promptPanel.GetComponent<RectTransform>();
        var textRect = promptText.GetComponent<RectTransform>();

        promptText.text = message;

        textRect.SetParent(panelRect, worldPositionStays: false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = Vector2.zero;
        textRect.offsetMin = new Vector2(10, 10);
        textRect.offsetMax = new Vector2(-10, -10);

        promptText.enableAutoSizing = true;
        promptText.fontSizeMin = 10f;
        promptText.fontSizeMax = Mathf.Min(panelRect.rect.width, panelRect.rect.height);
        promptText.alignment = TextAlignmentOptions.Center;
    }

    public void HidePrompt()
    {
        if (promptPanel != null) promptPanel.SetActive(false);
    }
}