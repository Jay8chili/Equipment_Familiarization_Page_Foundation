using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class KeyboardManager : MonoBehaviour
{
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private List<Button> buttons = new List<Button>();
    [SerializeField] private Button backSpace;
    // Start is called before the first frame update
    void Start()
    {
        foreach (var button in buttons) 
        {
            button.onClick.AddListener(() => {
                inputField.text += button.name;
            });
        }

        backSpace.onClick.AddListener(() =>
        {
            string text = inputField.text;

            if (!string.IsNullOrEmpty(text))
            {
                inputField.text = text.Substring(0, text.Length - 1);
            }
        });
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
