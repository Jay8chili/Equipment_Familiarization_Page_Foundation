
using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class APIManager : MonoBehaviour
{
    public static APIManager Instance { get; private set; }

    private enum ENV
    {
        production,
        staging,
        dev
    }

    [SerializeField] private ENV env;

    private APICollections collections;

    // Tokens are stored here.
    private string deviceAuthToken = string.Empty;
    private string userAuthToken = string.Empty;
    private string orgId = string.Empty;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            //DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public APICollections GetAPICollections()
    {
        // Use the enum string value directly
        collections ??= new APICollections(env.ToString());
        return collections;
    }

    public void SetDeviceAuth(string deviceAuthCode, int orgId)
    {
        this.deviceAuthToken=deviceAuthCode;
        this.orgId = orgId.ToString();
    }
    public void SetUserAuth(string code)
    {
        this.userAuthToken=code;
    }
    public IEnumerator GetWebRequest(string url, bool is_device_auth, Action<string> callback, bool is_auth_required = true,string platform="")
    {
        using UnityWebRequest uwr = UnityWebRequest.Get(url);
        if (is_auth_required)
        {
            string token = is_device_auth ? deviceAuthToken : userAuthToken;
            if (is_device_auth)
            {
                uwr.SetRequestHeader("device_code", token);
            }
            else
            {
                uwr.SetRequestHeader("Authorization", "Bearer " + token);
            }

            if (!string.IsNullOrEmpty(platform))
            {
                uwr.SetRequestHeader("platform", platform);
            }
            uwr.SetRequestHeader("org_id", orgId);
        }


        yield return uwr.SendWebRequest();

        ProcessResponse(uwr, url, callback);
    }

    public IEnumerator PostWebRequest(string url, string jsonString, Action<string> callback, bool is_auth_required = true)
    {
        Debug.LogError(jsonString);
        // Use 'using' to ensure UnityWebRequest is disposed correctly
        using UnityWebRequest uwr = new(url, UnityWebRequest.kHttpVerbPOST);
        byte[] jsonToSend = Encoding.UTF8.GetBytes(jsonString);
        uwr.uploadHandler = new UploadHandlerRaw(jsonToSend);
        uwr.downloadHandler = new DownloadHandlerBuffer();

        uwr.SetRequestHeader("Content-Type", "application/json");

        if (is_auth_required)
        {
            uwr.SetRequestHeader("Authorization", "Bearer " + userAuthToken);
            uwr.SetRequestHeader("org_id", orgId);
        }

        yield return uwr.SendWebRequest();

        ProcessResponse(uwr, url, callback);
    }

    private void ProcessResponse(UnityWebRequest uwr, string url, Action<string> callback)
    {
        if (uwr.result == UnityWebRequest.Result.ConnectionError || uwr.result == UnityWebRequest.Result.ProtocolError)
        {
            MessageHandleManager.Instance.ShowMessage(MessageHandleManager.OperationStatus.failure, "Check Your Internet Connection");
            Debug.LogError($"URL: {url} | Error: {uwr.error} | Code: {uwr.responseCode}");
        }

        string responseString = uwr.downloadHandler.text;
        long responseCode = uwr.responseCode;

        if (uwr.result == UnityWebRequest.Result.Success)
        {
            Debug.Log($"Success: {responseString}");
        }
        else
        {
            MessageHandleManager.Instance.ShowMessage(MessageHandleManager.OperationStatus.failure, "Response Error");
        }

        if (CheckResponseCode(url, responseCode))
        {
            callback?.Invoke(responseString);
        }
    }

    public bool CheckResponseCode(string url, long code)
    {
        if (code == 200) return true;


        if (code >= 400 && code < 500)
        {
            MessageHandleManager.Instance.ShowMessage(MessageHandleManager.OperationStatus.failure, "Auth/Client Error");
            Debug.LogWarning($"Auth/Client Error on {url}: {code}");
        }
        else if (code >= 500 && code < 600)
        {
            MessageHandleManager.Instance.ShowMessage(MessageHandleManager.OperationStatus.failure, "Server Error");
            Debug.LogError($"Server Error on {url}: {code}");
        }
        else
        {
            MessageHandleManager.Instance.ShowMessage(MessageHandleManager.OperationStatus.failure, "Unknown Error");
            Debug.LogError($"Unknown Error on {url}: {code}");
        }
        return false;
    }

    //Texture Downloader
    public IEnumerator DownloadTexture(string url,Image img,Sprite failOverSprite)
    {
        using UnityWebRequest request = UnityWebRequestTexture.GetTexture(url);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Image Download Failed: " + request.error);
            img.sprite = failOverSprite;
        }
        else
        {
            // Get downloaded texture
            Texture2D texture = DownloadHandlerTexture.GetContent(request);

            // Convert Texture2D → Sprite
            Sprite sprite = ConvertToSprite(texture);
            img.sprite = sprite;

        }
    }

    Sprite ConvertToSprite(Texture2D texture)
    {
        return Sprite.Create(
            texture,
            new Rect(0, 0, texture.width, texture.height),
            new Vector2(0.5f, 0.5f) // Pivot center
        );
    }

  

    private string GetFileNameFromURL(string url)
    {
        Uri uri = new(url);

        // Get last segment of path
        string fileName =
            Path.GetFileName(uri.LocalPath);

        return fileName;
    }
}