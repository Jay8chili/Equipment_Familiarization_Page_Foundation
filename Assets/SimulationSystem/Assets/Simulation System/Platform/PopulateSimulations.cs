using System;
using System.Collections;
using SimpleJSON;
using UnityEngine;

namespace Platform
{
    public class PopulateSimulations : MonoBehaviour
    {
        
        /// <summary>
        /// Simulation UI Card
        /// </summary>
        [SerializeField] private GameObject simulationPrefab;

        /// <summary>
        /// Container for simulation UI cards
        /// </summary>
        [SerializeField] private Transform simulationContainer;

        [SerializeField] private GameObject waitingMessage;
        
        // /// <summary>
        // /// Error messages on API failure
        // /// </summary>
        // public ErrorMessage errorMessage;
        // /// <summary>
        // /// Loader to be shown when waiting for API response
        // /// </summary>
        // public LoaderHandler loader;
        

        private void Start()
        {
        }

        private void OnDisable()
        {
            
        }

        private void Update()
        {
            // if(Input.GetKeyDown(KeyCode.Alpha1))
            //     EnterSimulation(7, "sm_1_2");
            // if(Input.GetKeyDown(KeyCode.Alpha2))
            //     EnterSimulation(8, "sm_1_8");
            // if(Input.GetKeyDown(KeyCode.Alpha3))
            //     EnterSimulation(9, "sm_1_3");
            // if(Input.GetKeyDown(KeyCode.Alpha4))
            //     EnterSimulation(10, "sm_1_1");
            // if(Input.GetKeyDown(KeyCode.Alpha5))
            //     EnterSimulation(11, "sm_1_4");
            // if(Input.GetKeyDown(KeyCode.Alpha6))
            //     EnterSimulation(12, "sm_1_5");
            // if(Input.GetKeyDown(KeyCode.Alpha7))
            //     EnterSimulation(13, "sm_1_6");
            // if(Input.GetKeyDown(KeyCode.Alpha8))
            //     EnterSimulation(14, "sm_1_7");
            // if(Input.GetKeyDown(KeyCode.Alpha9))
            //     EnterSimulation(15, "sm_1_9");
        }

        /// <summary>
        /// Fetch simulation by id
        /// </summary>
        /// <param name="id"></param>
        public void FetchSimulations(int id)
        {
            StartCoroutine(FetchSimulationCoroutine(id));
        }

        /// <summary>
        /// Fetches the list of simulations in the module selected
        /// </summary>
        /// <returns></returns>
        private IEnumerator FetchSimulationCoroutine(int id)
        {
            // loader.Show();
            bool received = false;
            string simulationDetail = "";
            StartCoroutine(APIHelper.GetWebRequest(APICollection.GetSimulations(id), (responseCode, responseMessage) =>
            {
                received = true;
                simulationDetail = CheckResponse(responseCode, responseMessage);
            }));
            yield return new WaitUntil(() => received);
            // loader.Hide();
            if (simulationDetail != String.Empty)
                yield return StartCoroutine(PopulateSimulationList(simulationDetail));
        }


        /// <summary>
        /// Populate simulations in the content panel
        /// </summary>
        /// <param name="details">List of simulations</param>
        /// <returns></returns>
        private IEnumerator PopulateSimulationList(string details)
        {
            ClearModules();
            var simulations = JSONNode.Parse(details);
            int simulationTotal = simulations["simulations"].Count;
            Debug.Log(simulations);
            Debug.Log($"simulation total {simulationTotal}");
            yield return null;
            if (simulations.IsNull || simulationTotal == 0)
            {
                // errorMessage.gameObject.SetActive(true);
                //         errorMessage.errorText.text = "No courses attached. \nPlease attach courses and restart the application.";
                Debug.LogError("No simulations found");
            }

            SimulationDetails simulationDetails = new SimulationDetails();
            // Instantiate simulation Items and set title
            for (int i = 0; i < simulationTotal; i++)
            {
                // if (i == 1 || i == 2)
                {
                    var sDetail = simulations["simulations"][i];

                    simulationDetails.name = sDetail["name"];
                    simulationDetails.id = sDetail["id"];
                    simulationDetails.index = i;
                    simulationDetails.code = sDetail["code"];
                    simulationDetails.thumbnailURL = sDetail["thumbnail"]["url"];
                    simulationDetails.thumbnailEtag = sDetail["thumbnail"]["etag"];
                    simulationDetails.androidAssetURL = sDetail["assets"][0]["android"]["url"];
                    simulationDetails.androidAssetEtag = sDetail["assets"][0]["android"]["etag"];
                    simulationDetails.windowsAssetURL = sDetail["assets"][0]["windows"]["url"];
                    simulationDetails.windowsAssetEtag = sDetail["assets"][0]["windows"]["etag"];
                    
                    GameObject g = Instantiate(simulationPrefab, simulationContainer);
                    
                    SimulationDownloadManager.Instance.InitializeLectureDownload(simulationDetails.id, simulationDetails.name);

                    Card cItem = g.GetComponent<Card>();
                    cItem.EnableLoader();
                    cItem.SetCard(simulationDetails.id, simulationDetails.name, i+1, simulationDetails.code);
                    // cItem.image.sprite = simulationSprites[i - 1];
                    cItem.OnClickSendCode.AddListener(EnterSimulation);
                    
                    g.GetComponent<SimulationDownloadProgress>().SetSimulationDetails(simulationDetails);
                    g.GetComponent<SimulationDownloadProgress>().AssignDownloader();
                }   

            }
            // Assign Course Item Thumbnail
            // for(int i = 0; i < simulationTotal; i++)
            // {
            //     string thumbnail_url = simulations["simulations"][i]["thumbnail"]["url"];
            //     if(string.IsNullOrEmpty(thumbnail_url) == false)                                  
            //     {
            //         bool errorReceived = false;
            //         bool textureReceived = false;
            //         Texture2D tex = null;   
            //         StartCoroutine(APIHelper.GetTexture(thumbnail_url, thumbnail => {if(thumbnail == null) errorReceived = true;
            //                                                                                                 else {textureReceived = true; tex = thumbnail;}}, true));
            //         
            //         yield return new WaitUntil(() => textureReceived || errorReceived);
            //         
            //         if(textureReceived)
            //         {
            //             Sprite sprite = UtilityScript.TextureToSprite(tex);
            //             simulationContainer.GetChild(i).GetComponent<Card>().SetThumbnail(sprite);
            //             
            //             textureReceived = false;
            //         }
            //         else 
            //         {
            //             // have default texture
            //             errorReceived = false;
            //         }
            //     }
            // }


            // yield return new WaitForSeconds(0.5f);
            // for(int i = 0; i < simualationTotal; i++)
            // {
            //     var sDetail = simulations["simulations"][i];
            //     int simulationID = sDetail["id"];
            //     simulationContainer.GetChild(i).GetComponent<SimulationDownloadProgress>().SetSimulationDetails(simulationDetails);
            //     simulationContainer.GetChild(i).GetComponent<SimulationDownloadProgress>().AssignDownloader();
            //     
            // }
        }
        

        /// <summary>
        /// Enter a simulation when clicked on
        /// </summary>
        /// <param name="id"></param>
        public void EnterSimulation(int id, string code)
        {
            if(SimulationDownloadManager.Instance.GetLectureDownloader(id).downloadStatus == SimulationDownloader.DownloadStatus.NotDownloaded)
                SimulationDownloadManager.Instance.DownloadSimulation(id);

            else
            {
                waitingMessage.SetActive(true);
                GameManager.Instance.simulationID = id;
                GameManager.Instance.simulationCode = code;
                // APIManager.Instance.simulationID = id.ToString();
                // SceneHandler.Instance.ChangeScene(code);
                SimulationAssetLoader.Instance.LoadSimulationScene(id, code);
            }
        }

        /// <summary>
        /// Clears the list of simulations
        /// </summary>
        public void ClearModules()
        {
            foreach (Transform simObj in simulationContainer)
            {
                Destroy(simObj.gameObject);
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
