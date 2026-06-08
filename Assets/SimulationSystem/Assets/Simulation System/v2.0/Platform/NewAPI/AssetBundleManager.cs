using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AssetBundleManager : MonoBehaviour
{
    public static AssetBundleManager Instance;

    BundleVersions bundleVersionData=new();

    private AssetBundle loadedBundle;

    string filePath; 
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            filePath = Path.Combine(Application.persistentDataPath, "bundle_versions.json");
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(this.gameObject);
        }

    }
    private void Start()
    {
        if (File.Exists(filePath))
        {
            string versionData = File.ReadAllText(filePath);
            if (!string.IsNullOrEmpty(versionData))
            {
                bundleVersionData = JsonUtility.FromJson<BundleVersions>(versionData);
                bool isFileChanges = false;
                foreach(Bundle bundle in bundleVersionData.bundles)
                {
                    if (File.Exists(bundle.path))
                    {
                        continue;
                    }
                    else
                    {
                        bundleVersionData.bundles.Remove(bundle);
                        isFileChanges = true;
                    }
                }
                if (isFileChanges) 
                {
                    string json = JsonUtility.ToJson(bundleVersionData);
                    File.WriteAllText(filePath, json);
                }

            }
            else
            {
                bundleVersionData= new BundleVersions();
                string json = JsonUtility.ToJson(bundleVersionData);
                File.WriteAllText(filePath, json);
            }
        }
    }

    public string DownloadValidator(string bundle,string uuid)
    {
        Debug.LogError(bundle+"========"+uuid +" === "+ bundleVersionData.bundles.Count);
        if (bundleVersionData.bundles.Count == 0)
        {
            return null;
        }
        string path=null;
        foreach(Bundle ab in bundleVersionData.bundles)
        {
            Debug.LogError(ab.name);
            if(ab.name == bundle && ab.uuid == uuid)
            {
                path = ab.path;
            }
            else if(ab.name == bundle && string.IsNullOrEmpty(uuid))
            {
                path=ab.path;
            }

        }
        return path;
    }

    public void UpdateBundleVersion(string name,string uuid, string path)
    {
        bool isExisting = false; 
        foreach(Bundle b  in bundleVersionData.bundles)
        {
            if(b.name == name)
            {
                b.uuid = uuid;
                b.path = path;
                isExisting = true;
            }
        }
        if (!isExisting)
        {
            Bundle assetVersion = new()
            {
                uuid = uuid,
                path = path,
                name = name
            };

            bundleVersionData.bundles.Add(assetVersion);
        }

        string json = JsonUtility.ToJson(bundleVersionData);

        File.WriteAllText(filePath, json);
    }

    public void DownloadAssetBundle(string url,string uuid, DataContainer container)
    {
        Debug.LogError("Download Initiated");
        StartCoroutine(NewAPIManager.Instance.AssetBundleDownloader(url, uuid, (filename,uuid,path,status) =>
        {
            if(status == "Success")
            {
                UpdateBundleVersion(filename, uuid, path);
                container.UpdateButton("Launch");
                container.UpdateLocalPath(path);

            }
            else
            {
                container.UpdateButton("Download");
            }
        }));
    }
    
    public void LoadAssetBundle(string path, string key)
    {
        StartCoroutine(LoadAssetBundleRoutine(path, key));
    }

    private IEnumerator LoadAssetBundleRoutine(string path, string key)
    {
        // Unload previous bundle
        if (loadedBundle != null)
        {
            loadedBundle.Unload(true);
            loadedBundle = null;
        }

        var bundleRequest = AssetBundle.LoadFromFileAsync(path);
        yield return bundleRequest;

        loadedBundle = bundleRequest.assetBundle;

        if (loadedBundle == null)
        {
            Debug.LogError("Failed to load AssetBundle");
            yield break;
        }

        Debug.LogError(key + " === " + path);

        AsyncOperation sceneLoadOperation = SceneManager.LoadSceneAsync(key, LoadSceneMode.Single);
        sceneLoadOperation.allowSceneActivation = false;

        while (!sceneLoadOperation.isDone)
        {
            if (ProgressBarManager.Instance)
            {
                ProgressBarManager.Instance.UpdateProgressBar(sceneLoadOperation.progress);
            }

            if (sceneLoadOperation.progress >= 0.9f)
            {
                sceneLoadOperation.allowSceneActivation = true;
            }

            yield return null;
        }

        if (ProgressBarManager.Instance)
        {
            ProgressBarManager.Instance.CloseProgress();
        }
    }
}

[Serializable]
public class Bundle
{
    public string name;
    public string path;
    public string uuid;
}
[Serializable]
public class BundleVersions
{
    public List<Bundle> bundles=new();
}
