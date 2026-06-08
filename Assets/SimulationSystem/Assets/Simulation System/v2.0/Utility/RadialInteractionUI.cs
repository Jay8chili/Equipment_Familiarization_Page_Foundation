using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class RadialInteractionUI : MonoBehaviour
{
    [Tooltip("The Image with Fill Method = Radial 360.")]
    public Image fillImage;

    private Transform _cam;

    private CanvasGroup _canvasGroup;
    private CanvasGroup CanvasGroup
    {
        get
        {
            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();
            return _canvasGroup;
        }
    }


    private void OnEnable()
    {
        _cam = Camera.main?.transform;

        SetProgress(0f);
    }
    private void Awake()
    {
        _cam = Camera.main?.transform;
        Hide();     // hidden until Show() is called
    }

    /// <summary>progress 0..1</summary>
    public void SetProgress(float progress)
    {
        if (fillImage != null)
            fillImage.fillAmount = Mathf.Clamp01(progress);
    }

    public void Show()
    {
        CanvasGroup.alpha = 1f;
        SetProgress(0f);
        //Debug.Log($"[RadialUI] Show — frame {Time.frameCount}");
    }

    public void Hide()
    {

        SetProgress(0f);
        CanvasGroup.alpha = 0f;
        //Debug.Log($"[RadialUI] Hide — frame {UnityEngine.Time.frameCount}\n{new System.Diagnostics.StackTrace(true)}");
    }
}