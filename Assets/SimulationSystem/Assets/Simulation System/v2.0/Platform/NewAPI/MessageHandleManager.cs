using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MessageHandleManager : MonoBehaviour
{
    public static MessageHandleManager Instance;
    [Header("Color Management")]
    [SerializeField] private Color defaultColor;
    [SerializeField] private Color sucessColor;
    [SerializeField] private Color errorColor;
    [Space(5)]

    [SerializeField] private CanvasGroup messageUI;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TextMeshProUGUI messageText;


    Color selectedColor;
    Coroutine activeRoutine;

    public enum OperationStatus
    {
        hint,
        success,
        failure,
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return; 
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void ShowMessage(OperationStatus status,string msg,float delay=3)
    {
        if (status == OperationStatus.success)
        {
            selectedColor = sucessColor;
            messageText.color = Color.black;
        }
        else if (status == OperationStatus.failure)
        {
            selectedColor = errorColor;
            messageText.color = Color.white;
        }
        else
        {
            selectedColor=defaultColor;
            messageText.color = Color.black;
        }
        if (activeRoutine != null) 
        { 
            StopCoroutine(activeRoutine);
        }
        activeRoutine = StartCoroutine(ShowMessageRoutine(selectedColor,msg,delay));
    }

    private IEnumerator ShowMessageRoutine(Color color, string msg, float delay)
    {
        yield return null;
        backgroundImage.color = color;
        messageText.text = msg;
        messageUI.DOFade(1, 0.5f);
        yield return new WaitForSeconds(delay);
        messageUI.DOFade(0, 0.5f);
    }
}
