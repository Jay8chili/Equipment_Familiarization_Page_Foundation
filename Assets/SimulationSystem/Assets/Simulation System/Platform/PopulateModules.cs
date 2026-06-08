using System;
using System.Collections;
using System.Collections.Generic;
using SimpleJSON;
using UnityEngine;

namespace Platform
{

    public class PopulateModules : MonoBehaviour
    {
        /// <summary>
        /// Course UI Card
        /// </summary>
        public GameObject modulePrefab;

        /// <summary>
        /// Container for course UI cards
        /// </summary>
        public Transform moduleContainer;

        /// <summary>
        /// Temporary list of sprites
        /// </summary>
        public List<Sprite> spriteList;

        private HintVRControls _controls;

        private void Start()
        {
            if (GameManager.Instance.loggedIn)
                StartCoroutine(FetchModules());
            else
                LoginManager.Instance.onLoginSuccess.AddListener(() => StartCoroutine(FetchModules()));

            _controls = new HintVRControls();
            _controls.Testing.Alpha1.performed += ctx => EnterModule(2);
            _controls.Enable();
        }

        private void Update()
        {
            if(Input.GetKeyDown(KeyCode.N))
                EnterModule(Debugger.Instance.ModuleId);
        }

        private void OnDisable()
        {
            if (_controls != null)
                _controls.Disable();
        }


        /// <summary>
        /// Fetches the list of modules assigned to the user
        /// </summary>
        /// <returns></returns>
        private IEnumerator FetchModules()
        {
            // loader.Show();
            bool received = false;
            string courseDetails = "";
            StartCoroutine(APIHelper.GetWebRequest(APICollection.GetModules(), (responseCode, responseMessage) =>
            {
                
                received = true;
                courseDetails = CheckResponse(responseCode, responseMessage);
            }));
            yield return new WaitUntil(() => received);
            // loader.Hide();
            if (courseDetails != String.Empty)
                yield return StartCoroutine(PopulateModuleList(courseDetails));
        }


        /// <summary>
        /// Populate modules in the content panel
        /// </summary>
        /// <param name="details">List of courses</param>
        /// <returns></returns>
        private IEnumerator PopulateModuleList(string details)
        {
            ClearModules();
            var modules = JSONNode.Parse(details);
            int moduleTotal = modules["details"].Count;

            yield return null;
            if (modules.IsNull || moduleTotal == 0)
            {
                // errorMessage.gameObject.SetActive(true);
                //         errorMessage.errorText.text = "No courses attached. \nPlease attach courses and restart the application.";
                Debug.LogError("No modules found");
            }

            // Instantiate Course Items and set title
            for (int i = 0; i < modules["details"].Count; i++)
            {
                var mDetail = modules["details"][i];
                int moduleID = mDetail["id"];
                string name = mDetail["name"];
                Debug.Log(name);
                
                GameObject g = Instantiate(modulePrefab, moduleContainer);

                Card cItem = g.GetComponent<Card>();
                cItem.EnableLoader();
                cItem.SetCard(moduleID, name, i+1);
                cItem.OnClick.AddListener(EnterModule);
            }
            // Assign Course Item Thumbnail
            for(int i = 0; i < modules["details"][i].Count; i++)
            {
                // string thumbnail_url = courses["courses"][i]["thumbnail"]["url"];
                // if(string.IsNullOrEmpty(thumbnail_url) == false)                                  
                // {
                //     bool errorReceived = false;
                //     bool textureReceived = false;
                //     Texture2D tex = null;   
                //     StartCoroutine(APIHelper.GetTexture(thumbnail_url, thumbnail => {if(thumbnail == null) errorReceived = true;
                //                                                                                             else {textureReceived = true; tex = thumbnail;}}, true));
                //     
                //     yield return new WaitUntil(() => textureReceived || errorReceived);
                //     
                //     if(textureReceived)
                //     {
                //         Sprite sprite = UtilityScript.TextureToSprite(tex);
                //         moduleContainer.GetChild(i).GetComponent<Card>().SetThumbnail(sprite);
                //         
                //         textureReceived = false;
                //     }
                //     else 
                //     {
                //         // have default texture
                //         errorReceived = false;
                //     }
                // }
                
                // temporary
                moduleContainer.GetChild(i).GetComponent<Card>().SetThumbnail(spriteList[i]);
            }
        }

        /// <summary>
        /// Enter a module when clicked on
        /// </summary>
        /// <param name="id"></param>
        public void EnterModule(int id)
        {
            UIManager.instance.CloseModulePanel();
            UIManager.instance.OpenSimulationPanel(id);
        }

        /// <summary>
        /// Clears the list of modules
        /// </summary>
        public void ClearModules()
        {
            foreach (Transform moduleObj in moduleContainer)
            {
                Destroy(moduleObj.gameObject);
            }
        }

        /// <summary>
        /// Check if response is valid and handle error accordingly
        /// </summary>
        /// <param name="responseCode">API Response Code</param>
        /// <param name="responseMessage">API Response Message</param>
        /// <returns></returns>
        private string CheckResponse(long responseCode, string responseMessage)
        {
            if (responseCode == 200)
            {
                return responseMessage;
            }

            if (responseCode == 0)
            {
                // errorMessage.gameObject.SetActive(true);
                // errorMessage.errorText.text = "Please check internet connection";
                Debug.LogError(responseMessage);
                return string.Empty;
            }

            // errorMessage.gameObject.SetActive(true);
            // errorMessage.errorText.text = "Protocol error";
            Debug.LogError(responseMessage);
            return string.Empty;
        }
    }
}

