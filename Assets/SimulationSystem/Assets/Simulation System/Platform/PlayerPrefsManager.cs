using UnityEngine;

namespace Platform
{
    public class PlayerPrefsManager : MonoBehaviour
    {
        public static PlayerPrefsManager Instance;

        private void Awake() 
        {
            if(Instance == null)
                Instance = this;
            else
                Destroy(this.gameObject); 
            //APIHandler.Instance.OnLoginSuccess.AddListener(SetLogIn);
        }

        private void Start() {
        
        }

        public bool IsLoginRequired()
        {
            if(!PlayerPrefs.HasKey("LoggedIn"))
            {   
                //Enable login
                //PlayerPrefs.SetInt("LoggedIn", 1);
                print("needs login");
                return true;
            }
            else if(PlayerPrefs.HasKey("LoggedIn") && PlayerPrefs.GetInt("LoggedIn") == 0)
            {
                //Enable Login
                return true;
            }
            else
            {
                //Skip login
                return false;
            }
        }

        public void SetLogIn()
        {
            PlayerPrefs.SetInt("LoggedIn", 1);
        }

        public void SetAuthtoken(string token)
        {
            PlayerPrefs.SetString("AuthToken", token);
        }
        public string GetAuthtoken()
        {
            string token = "";
            /*if(PlayerPrefs.HasKey("AuthToken"))
                token = PlayerPrefs.GetString("AuthToken");*/
            if(LoginManager.Instance.recorderToken == null && PlayerPrefs.HasKey("AuthToken"))
                token = PlayerPrefs.GetString("AuthToken");
            else
                token = LoginManager.Instance.recorderToken;
            return token;
        }

        private void OnApplicationQuit() {
            PlayerPrefs.Save();
        }
    }
}