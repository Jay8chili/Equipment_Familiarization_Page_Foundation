using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimulationDownloadManager : MonoBehaviour
{
     public static SimulationDownloadManager Instance;
        /// <summary>
        /// Prefab to create instances of independent simulation downloader
        /// </summary>
        public GameObject simulationDownloaderPrefab;
        
        private Dictionary<int, SimulationDownloader> simulationDownloaderList;

        private void Start()
        {
            if (Instance != null)
                Destroy(this.gameObject);
            else
                Instance = this;
            DontDestroyOnLoad(this.gameObject);
            simulationDownloaderList = new Dictionary<int, SimulationDownloader>();
        }
        
        /// <summary>
        /// Checks if the simulation content download is complete
        /// </summary>
        /// <param name="lectureID">Simulation ID</param>
        /// <returns>Returns true, if simulation content is downloaded and ready</returns>
        public bool IsSimulationReady(int lectureID)
        {
            if (simulationDownloaderList[lectureID].downloadStatus == SimulationDownloader.DownloadStatus.Completed)
                return true;
            return false;
        }

        /// <summary>
        /// Checks if the simulation content is being downloaded
        /// </summary>
        /// <param name="simulationID">Simulation ID</param>
        /// <returns>Returns true, if simulation content is downloading</returns>
        public bool IsSimulationDownloading(int simulationID)
        {
            if (simulationDownloaderList[simulationID].downloadStatus == SimulationDownloader.DownloadStatus.Downloading)
                return true;
            return false;
        }

        /// <summary>
        /// Instantiates an instance of the simulation downloader for a particular lecture
        /// </summary>
        /// <param name="simulationID"></param>
        /// <param name="simulationName"></param>
        public void InitializeLectureDownload(int simulationID, string simulationName)
        {
            if (simulationDownloaderList.ContainsKey(simulationID))
                return;
            GameObject sDownloader = Instantiate(simulationDownloaderPrefab, transform);
            sDownloader.GetComponent<SimulationDownloader>().lectureID = simulationID;
            sDownloader.GetComponent<SimulationDownloader>().lectureName = simulationName;
            simulationDownloaderList.Add(simulationID, sDownloader.GetComponent<SimulationDownloader>());
        }

        /// <summary>
        /// Get Lecture downloader for a particular lecturer
        /// </summary>
        /// <param name="lectureID"></param>
        /// <returns></returns>
        public SimulationDownloader GetLectureDownloader(int lectureID)
        {
            return simulationDownloaderList[lectureID];
        }

        /// <summary>
        /// Starts downloading simulation assetbundle
        /// </summary>
        /// <param name="simulationID"></param>
        public void DownloadSimulation(int simulationID)
        {
            print("download " + simulationID);
            simulationDownloaderList[simulationID].onDownloadStart?.Invoke();
            simulationDownloaderList[simulationID].BeginDownload();
        }
}
