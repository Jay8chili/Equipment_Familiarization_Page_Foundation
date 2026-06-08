using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ProgressBarManager: MonoBehaviour
{
    public static ProgressBarManager Instance;
    [SerializeField] private CanvasGroup progressUI;
    [SerializeField] private TextMeshProUGUI title;
    [SerializeField] private TextMeshProUGUI status;
    [SerializeField] private Slider progressSlider;

    [SerializeField] private Color successColor, failColor;

    private Color activeColor;

    private bool isProgressUsed;
    private void Awake()
    {
        Instance = this;
    }
    public void UpdateProgressBar(float progress,string title=null, string status = "Downloading")
    {
        if(progressUI.alpha != 1)
        {
            progressUI.DOFade(1, 0.5f);
        }
        if (!string.IsNullOrEmpty(title))
        {
            isProgressUsed = true;
            this.title.text = title;
        }
        if (this.status.text != status)
        {
            this.status.text = status;
        }
        progressSlider.value = progress;
    }

    public void CloseProgress(string status = "Success",float delay=3f)
    {
        string statusText;
        if(status == "Success")
        {
            activeColor = successColor;
            statusText = "Completed";
        }
        else
        {
            activeColor = failColor;
            statusText = "Failed";
        }
        StartCoroutine(CloseProgressRoutine(activeColor, statusText,delay));

    }

    private IEnumerator CloseProgressRoutine(Color color, string msg,float delay)
    {
        isProgressUsed = false;
        this.status.text = msg;
        this.status.color = color;
        yield return new WaitForSeconds(delay);
        progressUI.DOFade(0, 0.5f);
    }

    public bool IsProgressBarAvailable()
    {
        return isProgressUsed;
    }
}
