using System.Collections;
using UnityEngine;

public class IdleInteraction : Interactions
{
    public bool resetTimer;

    [Tooltip("If true, this interaction will not complete on its own — " +
             "call Advance() from an external script or UnityEvent to move to the next step.")]
    public bool waitForManualAdvance = false;

    // Called by a UnityEvent, button, or any external script to complete
    // this interaction when waitForManualAdvance is true.
    public void Advance()
    {
        if (!waitForManualAdvance) return;
        OnInteractionComplete();
    }

    public override void StartInteraction()
    {
        IsStarted = true;
        base.StartInteraction();
        OnInteractionStart();
    }

    public override void OnInteractionStart()
    {
        base.OnInteractionStart();
        StartCoroutine(ExecuteInteraction());
    }

    public override void OnInteractionUpdate()
    {
        base.OnInteractionUpdate();
    }

    public override void OnInteractionSuspend()
    {
        base.OnInteractionSuspend();
        StopTimer(resetTimer);
    }

    public override void OnInteractionComplete()
    {
        base.OnInteractionComplete();
        StopCoroutine(ExecuteInteraction());
    }

    protected override IEnumerator ExecuteInteraction()
    {
        // When waiting for manual advance, skip the timer entirely and
        // just loop until Advance() triggers OnInteractionComplete.
        if (waitForManualAdvance)
        {
            while (!IsCompleted)
            {
                OnInteractionUpdate();
                yield return null;
            }
            yield break;
        }

        StartTimer();

        if (canInteract)
        {
            if (IsCompleted)
            {
                OnInteractionComplete();
            }

            while (!IsCompleted)
            {
                OnInteractionUpdate();
                yield return null;
            }
        }
    }
}