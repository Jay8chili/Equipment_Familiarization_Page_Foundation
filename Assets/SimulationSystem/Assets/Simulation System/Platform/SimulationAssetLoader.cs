using System;
using System.Collections;
using System.Collections.Generic;
using Platform;
using SimulationSystem.V0._1.Utility.Miscellanous;
using UnityEngine;
using UnityEngine.SceneManagement;
using SimulationSystem.V02.Extensions;
public class SimulationAssetLoader : MonoBehaviour
{
    public static SimulationAssetLoader Instance;

    private void Awake()
    {
        if(Instance == null)
            Instance = this;
        else
        {
            Destroy(this.gameObject);
        }
        DontDestroyOnLoad(this.gameObject);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha3))
            LoadSimulationScene(19, "sm_1_9");
    }

    public void LoadSimulationScene(int simulationID, string simulationCode)
    {
        ScreenFade.instance.fadeTime = 4.0f;
        ScreenFade.instance.FadeOut();
        StartCoroutine(LoadSceneCoroutine(simulationID, simulationCode));
    }
    
    private IEnumerator LoadSceneCoroutine(int simulationID, string simulationCode)
    {
        AssetBundle.UnloadAllAssetBundles(true);
        // Path to the asset bundle
        string bundlePath = Application.persistentDataPath + "/Simulations/" + simulationID;

        // Load asset bundle
        var bundleRequest = AssetBundle.LoadFromFileAsync(bundlePath);
        yield return bundleRequest;

        if (bundleRequest.assetBundle == null)
        {
            Debug.LogError("Failed to load AssetBundle!");
            yield break;
        }

        // Load scene asynchronously from asset bundle
        string sceneName = simulationCode;
        AsyncOperation sceneLoadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);

        // Wait until the scene is fully loaded
        while (!sceneLoadOperation.isDone)
        {
            float progress = Mathf.Clamp01(sceneLoadOperation.progress / 0.9f); // Clamp progress to [0, 1]
            Debug.Log("Loading progress: " + (progress * 100f).ToString("0") + "%");
            yield return null;
        }
        
        GameManager.Instance.SwitchToSimulation(sceneName);
        
        Debug.Log("Scene loaded successfully from AssetBundle!");

        // Unload the asset bundle to release resources
        // bundleRequest.assetBundle.Unload(false);
    }
}
