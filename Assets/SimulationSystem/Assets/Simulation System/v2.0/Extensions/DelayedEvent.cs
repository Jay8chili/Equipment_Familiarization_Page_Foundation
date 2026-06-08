using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;


[Serializable]
public class DelayedEvent
{
    public UnityEvent delayedEvent;
    public float waitTimeInSeconds;
    
    public async void InvokeDelayedEvent()
    {
        await Task.Delay((int)waitTimeInSeconds * 1000);
        delayedEvent.Invoke();
    }

    /// <summary>
    /// Invokes the event with an overridden delay.
    /// Used by DelayedEventActions to fire immediately (delay = 0) in assessment mode.
    /// </summary>
    public async void InvokeDelayedEventWithOverride(float overrideDelay)
    {
        int ms = Mathf.Max(0, (int)(overrideDelay * 1000));
        await Task.Delay(ms);
        delayedEvent.Invoke();
    }
}
