using System;
using System.Collections;
using UnityEngine;

public class ContaminationTrigger : MonoBehaviour
{
    public static event Action<Collider> OnHandContactDetected;

    /*[HideInInspector]*/ public float contactThreshold = 1f;

    private int _handsInside = 0;
    private Coroutine _thresholdRoutine;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Hand")) return;

        _handsInside++;

        if (_thresholdRoutine == null)
            _thresholdRoutine = StartCoroutine(ThresholdRoutine());
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Hand")) return;

        _handsInside = Mathf.Max(0, _handsInside - 1);

        if (_handsInside == 0)
            ResetTimer();
    }

    private IEnumerator ThresholdRoutine()
    {
        yield return new WaitForSeconds(contactThreshold);
        OnHandContactDetected?.Invoke(GetComponent<Collider>());
        Debug.Log("[ContaminationTrigger]" + "Hand contact detected");
        _thresholdRoutine = null;
    }

    private void ResetTimer()
    {
        if (_thresholdRoutine != null)
        {
            StopCoroutine(_thresholdRoutine);
            _thresholdRoutine = null;
        }
        _handsInside = 0;
    }
}