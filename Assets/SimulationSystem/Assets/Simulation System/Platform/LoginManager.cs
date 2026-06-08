using System;
using System.Collections;
using SimpleJSON;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Platform
{
    public class LoginManager : MonoBehaviour
    {
        public static LoginManager Instance;
        
        // [SerializeField] private TextMeshProUGUI pinText;
        [SerializeField] private GameObject[] pinMaskers;

        [SerializeField] private GameObject loginPage;
        [SerializeField] private TextMeshProUGUI message;
        [SerializeField] private TextMeshProUGUI versionText;
        
        [SerializeField] private string testPin;
        [SerializeField] public string recorderToken;
        [SerializeField] private Button submitButton;
        public string _pin;


        public string SimulationID;
        public enum APIEnvironment
        {
            Production,
            Staging
        }
        public APIEnvironment apiEnvironment;
        
        public UnityEvent onLoginSuccess;
        public UnityEvent<string> onLoginError;
        public UnityEvent onNetworkError;

        private HintVRControls _controls;

        private void Awake()
        {
            if(Instance == null)
                Instance = this;
            else
                Destroy(this.gameObject);
            
            APICollection.SetEnvironment(apiEnvironment.ToString());
            onLoginSuccess = new UnityEvent();


        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.M))
                LoginWithPin(testPin);


        }

        private void Start()
        {
            versionText.text = 'v' + Application.version;
        
            if(GameManager.Instance.loggedIn)
                RemoveLoginPinUI();
        
            onLoginSuccess.AddListener(() =>
            {
                // AnalyticsEventManager.Instance.LoginAnalytics(pinText.text, "t");
                // APIManager.Instance._authorizationToken = PlayerPrefsManager.Instance.GetAuthtoken();
                RemoveLoginPinUI();
                UIManager.instance.OpenContentPanel();
            });
            onLoginError.AddListener(arg0 =>
            {
                // AnalyticsEventManager.Instance.LoginAnalytics(pinText.text, "f");
                DisplayError(arg0);
            });
        
            onNetworkError.AddListener(() => DisplayError("Check Internet"));
            
            _controls = new HintVRControls();
            _controls.Enable();
            _controls.Testing.K.performed += ctx =>
            {
                _pin = testPin;
                // pinText.text = testPin;
                Submit();
            };

            
        }
        private void OnApplicationQuit()
        {
            SceneHandler.Instance.onSimulationScene.RemoveAllListeners();
        }
        /// <summary>
        /// Check if response is valid and handle error accordingly
        /// </summary>
        /// <param name="responseCode">API Response Code</param>
        /// <param name="responseMessage">API Response Message</param>
        /// <returns></returns>
        private void CheckResponse(long responseCode, string responseMessage)
        {
            if (responseCode == 200)
            {
                LoginResponse(responseCode, responseMessage);
                onLoginSuccess?.Invoke();
                Debug.Log("loggedin");
            }
            if(responseCode == 0)
            {
                onLoginError.Invoke("Please check internet connection");
                StartCoroutine(DisplayError("Please check internet connection"));
            }
            StartCoroutine(DisplayError(responseMessage));
            submitButton.interactable = true;
        }
        
        public void LoginWithPin(string pin)
        {
            submitButton.interactable = false;
            StartCoroutine(APIHelper.GetWebRequest(APICollection.PinLogin(pin), CheckResponse));
        }
        
        private void LoginResponse(long responseCode, string responseMessage)
        {
            if (responseCode == 200)
            {
                var json = JSON.Parse(responseMessage);
                recorderToken = json["auth_token"].Value;
                Debug.Log("response Message " + recorderToken);
                GameManager.Instance.role = json["type"];
                GameManager.Instance.userID = json["id"];
                GameManager.Instance.loggedIn = true;
                PlayerPrefsManager.Instance.SetAuthtoken(recorderToken);
            }   
            else
            {
                Debug.LogError(responseMessage);
                onLoginError?.Invoke(responseMessage);
            }
            
        }
    
        private void RemoveLoginPinUI()
        {
            // pinText.text = "";
            _pin = string.Empty;
            loginPage.SetActive(false);
        }

        public void EnterNum(int num)
        {
            if (_pin.Length < 6)
            {
                pinMaskers[_pin.Length].SetActive(true);
                _pin += num.ToString();
            }
        }

        public void BackSpace()
        {
            if (_pin.Length > 0)
            {
                _pin = _pin.Remove(_pin.Length-1, 1);
                pinMaskers[_pin.Length].SetActive(false);   
            }
            // if(_pin.Length == 0)
            //     errorText.text = "";
        }

        public void Submit()
        {
            if (_pin.Length == 6)
            {
                LoginWithPin(_pin);
                // errorText.text = "";
            }
            else
                StartCoroutine(DisplayError("Incomplete Pin"));
        }
    

        private IEnumerator DisplayError(string error)
        {
            foreach (var masker in pinMaskers)
            {
                masker.SetActive(false);   
            }

            _pin = string.Empty;
            message.text = error; //error;
            yield return new WaitForSeconds(2f);
            message.text = "Enter Pin";
        }


     
    }
}