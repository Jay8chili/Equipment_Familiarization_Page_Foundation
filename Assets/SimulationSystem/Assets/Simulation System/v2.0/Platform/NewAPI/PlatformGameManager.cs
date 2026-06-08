
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using static Platform.GameManager;

public class PlatformGameManager : MonoBehaviour
{
    public static PlatformGameManager Instance;

    [SerializeField] private string orgName;
    [SerializeField] private string siteName;
    [SerializeField] private int orgId;

    [SerializeField] private int userId;
    [SerializeField] private string userName;

    [SerializeField] private int deviceId;

    [SerializeField] private bool isLoggedIn;

    [SerializeField] private UnityEvent onRestart, onReturn; 

    private HintVRControls _controls;

    private void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(this.gameObject);
        }

    }

    private void Start()
    {
        _controls = new HintVRControls();
        _controls.Enable();
        _controls.Player.MenuButton.performed += ctx =>
        {

            if (SceneManager.GetActiveScene().name != "MainScene")
            {
                SceneManager.LoadSceneAsync("MainScene");
            }
                
        };

        onReturn.AddListener(SwitchToHome);
        onRestart.AddListener(ReloadScene);
    }
    public void SetOrgDetails(string orgName, string siteName, int orgId,string deviceAuth)
    {
        this.orgName = orgName;
        this.siteName = siteName;
        this.orgId = orgId;
        NewAPIManager.Instance.SetDeviceAuth(deviceAuth,orgId);
    }

    public void SetUserDetails(string name, string auth,int userId)
    {
        this.userName = name;
        this.userId = userId;
        NewAPIManager.Instance.SetUserAuth(auth);
    }

    public string GetOrgName()
    {
        return this.orgName;
    }

    public string GetSiteName() 
    { 
        return this.siteName;
    }
    public int GetOrgId()
    {
        return this.orgId;
    }
    
    public string GetUserName()
    {
        return this.userName;
    }
    public int GetUserID()
    {
        return this.userId;
    }

    public void SetDeviceID(int id)
    {
        deviceId = id;
    }

    public int GetDeviceID()
    {
        return deviceId;
    }

    public void SetIsLoggedIn(bool loggedIn)
    {
        isLoggedIn = loggedIn;
    }

    public bool GetIsLoggedIn()
    {
        return isLoggedIn;
    }

    private void SwitchToHome()
    {
        SceneManager.LoadSceneAsync("MainScene");
    }

    private void ReloadScene()
    {
        SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().name);
    }

    public void TriggerOnReturn()
    {
        onReturn?.Invoke();
    }

    public void TriggerOnRestart()
    {
        onRestart?.Invoke(); 
    }
}
