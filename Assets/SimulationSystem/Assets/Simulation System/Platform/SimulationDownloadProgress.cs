using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Display Lecture Progress on UI
/// </summary>
public class SimulationDownloadProgress : MonoBehaviour
{
    [ReadOnly(true)] public int simulationID;
    public Image progressBar;
    public TextMeshProUGUI statusText;
    public GameObject downloadButton;
    public GameObject loaderImage;
    private SimulationDownloader _sDownloader;
    private bool _downloading;
    private SimulationDetails _simulationDetails;
    
    private void Start()
    {
    }

    public void SetSimulationDetails(SimulationDetails sDetails)
    {
        simulationID = sDetails.id;
        _simulationDetails = sDetails;
    }
        
    /// <summary>
    /// Assign lecture downloader to the lecture progress bar
    /// </summary>
    public async void AssignDownloader()
    {
        downloadButton.SetActive(true);
        _sDownloader = SimulationDownloadManager.Instance.GetLectureDownloader(simulationID);
        if(_sDownloader.downloadStatus == SimulationDownloader.DownloadStatus.Downloading)
            ShowProgressBar();
        else
        {
            statusText.text = "Syncing";
            loaderImage.SetActive(true);
            downloadButton.GetComponent<Button>().interactable = false;
            await SimulationDownloadManager.Instance.GetLectureDownloader(simulationID).CheckDownloadStatus(_simulationDetails);
            downloadButton.GetComponent<Button>().interactable = true;
            Debug.Log(_sDownloader.downloadStatus);
            if (_sDownloader.downloadStatus == SimulationDownloader.DownloadStatus.Completed)
            {
                statusText.text = "LAUNCH";
                loaderImage.SetActive(false);
            }
            else if (_sDownloader.downloadStatus == SimulationDownloader.DownloadStatus.NotDownloaded)
            {
                statusText.text = "DOWNLOAD";
                loaderImage.SetActive(false);
            }
        }
        _sDownloader.onDownloadStart.AddListener(ShowProgressBar);
        _sDownloader.onDownloadComplete.AddListener(HideProgressBar);
    }
    public void ShowProgressBar()
    {
        progressBar.gameObject.SetActive(true);
        statusText.text = "DOWNLOADING";
        loaderImage.SetActive(true);
        _downloading = true;
        downloadButton.GetComponent<Button>().interactable = false;
    }
    public void HideProgressBar()
    {
        Debug.Log("HideProgress");
        loaderImage.SetActive(false);
        statusText.text = "LAUNCH";
        progressBar.fillAmount = 0;
        progressBar.gameObject.SetActive(false);
        _downloading = false;
        downloadButton.GetComponent<Button>().interactable = true;
    }
    
    public void DisableDownloadButton()
    {
        downloadButton.SetActive(false);
    }
    
    private void Update()
    {
        if(_downloading)
            SetProgress();
    }
    private void SetProgress()
    {
        progressBar.fillAmount = _sDownloader.downloadProgress;
    }
    
    /// <summary>
    /// Reset download by clearing files and resetting progress bar
    /// </summary>
    public void ResetDownload()
    {
        SimulationDownloadManager.Instance.GetLectureDownloader(simulationID).ClearFiles();
        HideProgressBar();
    }
}
