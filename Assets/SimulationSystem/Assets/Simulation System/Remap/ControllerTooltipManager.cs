using System.Collections;
using UnityEngine;
using SimulationSystem.V02.Assistant;
public class ControllerTooltipController : MonoBehaviour
{
    [Header("Left Visual Swap")]
    [SerializeField] private GameObject leftHandModel;
    [SerializeField] private GameObject leftControllerModel;

    [Header("Right Visual Swap")]
    [SerializeField] private GameObject rightHandModel;
    [SerializeField] private GameObject rightControllerModel;

    [Header("Tooltip Prefabs")]
    [SerializeField] private GameObject leftTooltipPrefab;
    [SerializeField] private GameObject rightTooltipPrefab;

    [Header("Animation")]
    [SerializeField] private float animDuration = 0.25f;
    [SerializeField] private Vector3 outwardOffset = new Vector3(0.05f, 0.02f, 0f);
    [SerializeField]
    private AnimationCurve animCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private bool tooltipsVisible;

    [ContextMenu("Show Tooltips")]
    public void ShowTooltips()
    {
        if (tooltipsVisible) return;
        tooltipsVisible = true;

        if (leftHandModel != null) leftHandModel.SetActive(false);
        if (leftControllerModel != null) leftControllerModel.SetActive(true);
        if (rightHandModel != null) rightHandModel.SetActive(false);
        if (rightControllerModel != null) rightControllerModel.SetActive(true);

        if (leftTooltipPrefab != null && leftControllerModel != null)
        {
            StartCoroutine(AnimateOut(leftTooltipPrefab.transform));

        }

        if (rightTooltipPrefab != null && rightControllerModel != null)
        {
            StartCoroutine(AnimateOut(rightTooltipPrefab.transform));
        }
    }

    [ContextMenu("Hide Tooltips")]
    public void HideTooltips()
    {
        if (!tooltipsVisible) return;
        tooltipsVisible = false;

        StopAllCoroutines();


        if (leftControllerModel != null) leftControllerModel.SetActive(false);
        if (rightControllerModel != null) rightControllerModel.SetActive(false);
        if (leftHandModel != null) leftHandModel.SetActive(true);
        if (rightHandModel != null) rightHandModel.SetActive(true);
    }

    private IEnumerator AnimateOut(Transform ui)
    {
        Vector3 startLocal = Vector3.zero;
        Vector3 endLocal = outwardOffset;
        Vector3 endScale = ui.localScale == Vector3.zero ? Vector3.one : ui.localScale;

        ui.localPosition = startLocal;
        ui.localScale = Vector3.zero;

        float elapsed = 0f;
        while (elapsed < animDuration)
        {
            elapsed += Time.deltaTime;
            float k = animCurve.Evaluate(elapsed / animDuration);
            ui.localPosition = Vector3.Lerp(startLocal, endLocal, k);
            ui.localScale = Vector3.Lerp(Vector3.zero, endScale, k);
            yield return null;
        }

        ui.localPosition = endLocal;
        ui.localScale = endScale;
    }
}