using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using System;

public class DelayedEventActions : MonoBehaviour
{
    public List<DelayEventList> ListOfDelayedEvents;


    public void CallEventAtIndex(int index)
    {
        if (index < 0 || index >= ListOfDelayedEvents.Count) return;

        var entry = ListOfDelayedEvents[index];

        bool isAssessmentMode = SimulationManager.Instance != null && SimulationManager.Instance.simulationMode == SimulationMode.Assessment;

        // In guided mode always fire with original delay
        if (!isAssessmentMode)
        {
            entry.DelayedEvents.InvokeDelayedEvent();
            return;
        }

        // In assessment mode, behaviour depends on the AssessmentEventBehaviour setting for this event
        switch (entry.assessmentBehaviour)
        {
            case AssessmentEventBehaviour.FireWithDelay:
                entry.DelayedEvents.InvokeDelayedEvent();
                break;
            case AssessmentEventBehaviour.FireImmediately:
                entry.DelayedEvents.InvokeDelayedEventWithOverride(0);
                break;
            case AssessmentEventBehaviour.Skip:
                // Do nothing
                break;
        }
    }
}
[Serializable]
public struct DelayEventList
{
    public string EventName;
    public DelayedEvent DelayedEvents;

    [Tooltip("Guided mode always fires with delay. This controls assessment mode behaviour only.")]
    public AssessmentEventBehaviour assessmentBehaviour;
}

public enum AssessmentEventBehaviour
{
    FireWithDelay,      // fires as normal with original delay
    FireImmediately,    // fires with 0 delay
    Skip                // does not fire in assessment mode
}