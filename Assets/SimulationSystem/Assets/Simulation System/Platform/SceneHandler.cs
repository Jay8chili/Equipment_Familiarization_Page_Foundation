using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace Platform
{
    public class SceneHandler : MonoBehaviour
    {
        public static SceneHandler Instance;
        // public RenderTexture RenderTexCam;
        public UnityEvent onMainScene;
        public UnityEvent onSimulationScene;
        private bool _sceneChanged;
        private bool _isSceneChanging;

    
        private void Awake() 
        {
            if(Instance == null)
                Instance = this;
            else
                Destroy(this.gameObject);
            DontDestroyOnLoad(this);
            onSimulationScene = new UnityEvent();
            onMainScene = new UnityEvent();
        
        }

        public void Start()
        {
            SceneManager.sceneLoaded += ChangedActiveScene;
        }

        public IEnumerator ReloadScene(float time)
        {
            yield return new WaitForSeconds(time);
            SceneManager.LoadScene(SceneManager.GetActiveScene().name, LoadSceneMode.Single);
        }

        private void ChangedActiveScene(Scene current, LoadSceneMode mode)
        {
            _sceneChanged = true;
            string currentName = current.name;
            if (currentName != "MainScene")
            {
                onSimulationScene?.Invoke();
            }

            if (currentName == "MainScene")
            {
                onMainScene?.Invoke();
                // GameManager.Instance.SetOVRRig(true);
            }
        }

        private void Update() 
        {   
        }

        public void ChangeScene(string scene)
        {   
            if(_isSceneChanging == false)
                StartCoroutine(FadeSceneChange(scene));
        }

        public void SetSceneID(string id)
        {
            ChangeScene(string.Concat("Simulation_", id));
        }

        private IEnumerator FadeSceneChange(string scene)
        {
            _isSceneChanging = true;
            //OVRScreenFade.instance.fadeTime = 1f;
            //OVRScreenFade.instance.FadeOut();
            yield return new WaitForSeconds(1f);
            SceneManager.LoadSceneAsync(scene);
            yield return new WaitUntil(() => _sceneChanged == true);
            yield return new WaitForSeconds(1f);
            //OVRScreenFade.instance.FadeIn();
            Debug.Log("New scene loaded - " + scene);
            // if(scene == "HomeScene") GameObject.Find("LoginCanvas").SetActive(false);
            // sceneChanged = false;
            //
            // if(fade)
            // {
            //     fade.fadeTime = 0.5f;
            //     fade.FadeIn();
            // }

            _isSceneChanging = false;
        }
    }
}