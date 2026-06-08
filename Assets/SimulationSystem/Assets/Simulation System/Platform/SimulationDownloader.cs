using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.Rendering.Universal;

public class SimulationDownloader : MonoBehaviour
{
        public int lectureID;
        public string lectureName;
        public float downloadProgress;

        private string _downloadURL;
        private string _downloadEtag;
        private string _simulationPath;
        
        private string _currentDownloadPath;

        private ConcurrentBag<Task> _tasks;
        private CancellationTokenSource _cancellationTokenSource;
        private CancellationToken _cancellationToken;


        bool _isDownloaded = true;
        private string _persistentDataPath;

        public enum DownloadStatus
        {
            NotDownloaded,
            Downloading,
            Completed
        }

        public DownloadStatus downloadStatus;

        [HideInInspector] public UnityEvent onDownloadStart;
        [HideInInspector] public UnityEvent onDownloadComplete;


        private void Awake()
        {   
            _persistentDataPath = Application.persistentDataPath;
        }

        private void Start()
        {
            // onDownloadStart = new UnityEvent();
            // onDownloadComplete = new UnityEvent();
            onDownloadStart.AddListener(() => print("Download started"));


            _tasks = new ConcurrentBag<Task>();
        }

        /// <summary>
        /// Check if lecture is already downloaded, partially downloaded or not downloaded at all
        /// </summary>
        public async Task CheckDownloadStatus(SimulationDetails sDetails)
        {
            downloadProgress = 0;
            _isDownloaded = true;
            _simulationPath = Path.Combine(_persistentDataPath, "Simulations");
            Directory.CreateDirectory(_simulationPath);

#if UNITY_EDITOR
            _downloadURL = sDetails.windowsAssetURL;
            _downloadEtag = sDetails.windowsAssetEtag;
#elif UNITY_ANDROID
            _downloadURL = sDetails.androidAssetURL;
            _downloadEtag = sDetails.androidAssetEtag;
#endif
            _simulationPath = Path.Combine(_simulationPath, sDetails.id.ToString());
            CheckForFileIntegrity();

            if (_isDownloaded)
                downloadStatus = DownloadStatus.Completed;
            else
                downloadStatus = DownloadStatus.NotDownloaded;
        }

        private async void CheckForFileIntegrity()
        {
            if (string.IsNullOrEmpty(_downloadURL))
            {
                _isDownloaded = false;
            }
            if (!String.IsNullOrEmpty(_downloadEtag) && !(File.Exists(_simulationPath) && await MD5Generator.CompareMD5(_simulationPath, _downloadEtag)))
            {
                _isDownloaded = false;
            }
        }

        public async void BeginDownload()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _cancellationToken = _cancellationTokenSource.Token;
            Task t = DownloadSimulation(_cancellationToken);
            _tasks.Add(t);
            if (_cancellationTokenSource.IsCancellationRequested)
            {
                Debug.LogError("cancellation requested");
                _cancellationToken.ThrowIfCancellationRequested();
            }
            await t;
        }

        private async Task DownloadSimulation(CancellationToken token)
        {
            downloadProgress = 0f;
            downloadStatus = DownloadStatus.Downloading;
            Task t = DownloadItem(_downloadURL, _simulationPath, token);
                    await t;

            downloadStatus = DownloadStatus.Completed;
            Debug.Log("Download complerte");
            onDownloadComplete.Invoke();
        }

        private async Task DownloadItem(string url, string filePath, CancellationToken token)
        {
            print(url);
            _currentDownloadPath = url;
            UnityWebRequest uwr = UnityWebRequest.Get(url);
            DownloadHandlerFile dHandlerFile = new DownloadHandlerFile(filePath);
            dHandlerFile.removeFileOnAbort = true;
            uwr.downloadHandler = dHandlerFile;
            uwr.disposeDownloadHandlerOnDispose = true;
            uwr.SendWebRequest();

            while (!uwr.isDone)
            {
                await Task.Yield();
                downloadProgress = uwr.downloadProgress;
                if (_cancellationTokenSource.IsCancellationRequested)
                {
                    Debug.LogError("cancellation requested");
                    uwr.Dispose();
                    _cancellationToken.ThrowIfCancellationRequested();
                }
            }

            if (uwr.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(uwr.error);
                uwr.Dispose();
            }
            downloadProgress = 0;
        }


        public async void ClearFiles()
        {
            string dirPath = Path.Combine(_persistentDataPath, lectureID.ToString());
            if (downloadStatus != DownloadStatus.Downloading)
            {
                if (Directory.Exists(dirPath))
                    Directory.Delete(dirPath, true);
                downloadStatus = DownloadStatus.NotDownloaded;
            }
            else
            {
                _cancellationTokenSource.Cancel();
                try
                {
                    await Task.WhenAll(_tasks.ToArray());
                }
                catch (OperationCanceledException)
                {
                    Debug.Log("cancellation ");
                }
                finally
                {
                    _cancellationTokenSource.Dispose();
                }

                downloadStatus = DownloadStatus.NotDownloaded;
                downloadProgress = 0f;
                Directory.Delete(dirPath, true);
            }

            await Task.Delay(1000);
            // LectureDownloadManager.Instance.DownloadLecture(lectureID);
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
            if(responseCode == 0)
            {
                // errorMessage.gameObject.SetActive(true);
                // errorMessage.errorText.text = "Please check internet connection";
                return string.Empty;
            }
            
            // errorMessage.gameObject.SetActive(true);
            // errorMessage.errorText.text = "Protocol error";
            return string.Empty;
        }
}
