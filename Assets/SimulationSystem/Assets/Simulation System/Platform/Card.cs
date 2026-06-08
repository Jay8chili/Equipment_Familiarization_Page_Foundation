using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Platform
{
    public class Card : MonoBehaviour
    {
        public int id;
        public string code;
        public TextMeshProUGUI index;
        public Image image;
        public TextMeshProUGUI title;
        public Button button;
        public Image loaderImage;
        private bool _enableLoader;
    
        [HideInInspector] public UnityEvent<int> OnClick;
        [HideInInspector] public UnityEvent<int, string> OnClickSendCode;

        private void Start()
        {
        }

        private void OnEnable()
        {
            if (_enableLoader)
            {
                button.interactable = true;
                // loaderImage.gameObject.SetActive(false);    
            }
        }

        public void EnableLoader()
        {
            _enableLoader = true;
        }

        public void SetCard(int id, string text, int index, string code)
        {
            this.id = id;
            title.text = text;
            this.code = code;
            this.index.text = index.ToString();
        }
        
        public void SetCard(int id, string text, int index)
        {
            this.id = id;
            title.text = text;
            this.code = code;
            this.index.text = index.ToString();
        }

        public void SetThumbnail(Sprite sprite)
        {
            image.sprite = sprite;
        }

        public void SubmitClick()
        {
            button.interactable = false;
            // loaderImage.gameObject.SetActive(true);
            OnClick.Invoke(id);
        }

        public void SubmitClickWithCode()
        {
            button.interactable = false;
            OnClickSendCode.Invoke(id, code);
        }
    }
}