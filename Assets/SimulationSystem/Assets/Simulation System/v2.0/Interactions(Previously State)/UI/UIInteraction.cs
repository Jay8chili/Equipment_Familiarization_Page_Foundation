using SimulationSystem.V02.Assistant;
using System.Collections;
using System.Threading;
using TMPro;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

public class UIInteraction : Interactions
{
    [SerializeField] public CustomButton button;
    [SerializeField] public GameObject uiPanel;
    [SerializeField] public TMPro.TMP_Text uiText;
    [SerializeField] public string content;

    // ─────────────────────────────────────────────
    // BOT-HANDLED MODE
    // True when none of the three scene-UI fields are assigned.
    // AssistantManager shows the bot's own panel/button and calls
    // OnInteractionComplete when the player dismisses it.
    // ─────────────────────────────────────────────

    public bool IsBotHandled => uiPanel == null && button == null && uiText == null;
    public string Content => uiText != null ? uiText.text : content;

    // ─────────────────────────────────────────────
    // AWAKE
    // ─────────────────────────────────────────────

    public override void Awake()
    {
        base.Awake();

        if (button == null) button = GetComponent<CustomButton>();

        // uiText is NOT auto-assigned — must be set explicitly in the Inspector.
        // If left null (along with uiPanel and button), IsBotHandled = true.

        if (uiText != null && !string.IsNullOrEmpty(content))
            uiText.text = content;

        if (button != null)
        {
            button.gameObject.SetActive(true);
            button.holdTime = Time;
        }

        if (uiPanel != null && uiText != null)
        {
            /*var panelRect = uiPanel.GetComponent<RectTransform>();
            var textRect = uiText.GetComponent<RectTransform>();

            textRect.SetParent(panelRect, worldPositionStays: false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = Vector2.zero;
            textRect.offsetMin = new Vector2(10, 10);
            textRect.offsetMax = new Vector2(-10, -10);

            uiText.enableAutoSizing = true;
            uiText.fontSizeMin = 10f;
            uiText.fontSizeMax = Mathf.Min(panelRect.rect.width, panelRect.rect.height);
            uiText.alignment = TextAlignmentOptions.Center;*/
        }
    }

    // ─────────────────────────────────────────────
    // START
    // ─────────────────────────────────────────────

    public override void Start()
    {
        base.Start();

        if (button != null)
        {
            button.OnStartHolding.AddListener(OnInteractionStart);
            button.OnSuspendHolding.AddListener(OnInteractionSuspend);
            button.OnButtonClicked.AddListener(OnInteractionComplete);
        }
    }

    // ─────────────────────────────────────────────
    // START INTERACTION
    // Called once by SimulationState.RunSequence when it is this interaction's turn.
    // ─────────────────────────────────────────────

    public override void StartInteraction()
    {
        base.StartInteraction();

        if (IsBotHandled)
        {
            // Pass OnInteractionComplete as the callback so the bot button click
            // flips IsCompleted = true, which lets the RunSequence coroutine advance.
            AssistantManager.Instance?.TriggerBotUI(content, OnInteractionComplete);
        }
        else
        {
            if (uiText != null) uiText.gameObject.transform.localScale = Vector3.one;
            if (uiPanel != null) 
            {
                CancellationTokenSource cts = new CancellationTokenSource();
                _ = uiPanel.GetComponent<CanvasGroup>().DoFade(1f, 1f, Ease.InSine, cts.Token
                    ,onComplete: () =>
                {
                    // Enable colliders only once fully scaled up — prevents accidental
                    // button triggers while the panel is still animating in.
                    uiPanel?.GetComponent<PanelButtonController>()?.SetCollidersEnabled(true);
                });

                //uiPanel.SetActive(true); 
            }
        }
    }

    // ─────────────────────────────────────────────
    // INTERACTION COMPLETE
    // ─────────────────────────────────────────────

    public override void OnInteractionComplete()
    {
        base.OnInteractionComplete();

        // In bot-handled mode the callback wired by TriggerBotUI already called
        // HideBotUI before invoking this — this is a safety call only.
        if (IsBotHandled)
        {
            AssistantManager.Instance?.HideBotUI();
        }
        else
        {
            if (uiPanel != null)
            {
                uiPanel?.GetComponent<PanelButtonController>()?.SetCollidersEnabled(false);

                CancellationTokenSource cts = new CancellationTokenSource();
                _ = uiPanel.GetComponent<CanvasGroup>().DoFade(0f, 0.1f, Ease.OutSine, cts.Token);

                //uiPanel.SetActive(flase); 
            }

            if (button != null)
            {
                button.OnButtonClicked.RemoveListener(OnInteractionComplete);
                button.OnStartHolding.RemoveListener(OnInteractionStart);
                button.OnSuspendHolding.RemoveListener(OnInteractionSuspend);
            }
        }
    }

    // ─────────────────────────────────────────────
    // STOP INTERACTION (forced exit)
    // ─────────────────────────────────────────────

    public override void StopInteraction()
    {
        base.StopInteraction();

        if (IsBotHandled)
            AssistantManager.Instance?.HideBotUI();
        else
            if (uiPanel != null)
            {
                uiPanel?.GetComponent<PanelButtonController>()?.SetCollidersEnabled(false);

                CancellationTokenSource cts = new CancellationTokenSource();
                _ = uiPanel.GetComponent<CanvasGroup>().DoFade(0f, 0.1f, Ease.OutSine, cts.Token);

                //uiPanel.SetActive(flase); 
            }
    }

    // ─────────────────────────────────────────────
    // REMAINING OVERRIDES
    // ─────────────────────────────────────────────

    public override void OnInteractionStart() => base.OnInteractionStart();
    public override void OnInteractionSuspend() => base.OnInteractionSuspend();
    public override void OnInteractionUpdate() => base.OnInteractionUpdate();

    protected override IEnumerator ExecuteInteraction() { yield return null; }
}