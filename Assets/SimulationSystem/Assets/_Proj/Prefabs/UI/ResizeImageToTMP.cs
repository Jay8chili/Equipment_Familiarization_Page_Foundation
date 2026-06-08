using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ResizeImageToTMP : MonoBehaviour
{
    public TextMeshProUGUI tmp;
    public RectTransform imageRect;
    public float padding = 0.02f; // world units

    public ResizeImageMode resizeImageMode;
    void Start()
    {
        //Resize();
        ChangeResizeImageMode();
    }

    private void ResizeWidth()
    {
        tmp.ForceMeshUpdate();
        float width = tmp.preferredWidth;

        imageRect.sizeDelta = new Vector2(width + padding, imageRect.sizeDelta.y);
    }

    private void ResizeHeight()
    {
        tmp.ForceMeshUpdate();
        float height = tmp.preferredHeight;
        imageRect.sizeDelta = new Vector2(imageRect.sizeDelta.x, height + padding);
    }

    public void ChangeResizeImageMode()
    {
        switch (resizeImageMode)
        {
            case ResizeImageMode.Width:

                ResizeWidth();
                break;
            case ResizeImageMode.Height:

                ResizeHeight();
                break;
            default:
                break;
        }
    }
}

public enum ResizeImageMode
{
    Width,
    Height,
}
