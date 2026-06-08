using JetBrains.Annotations;
using SimulationSystem.V0._1.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR;

public class SessionManager : MonoBehaviour
{
    public static SessionManager Instance;

    [SerializeField] private int activeSimulationID;
    [SerializeField] private int activeSessionID;
    [SerializeField] private UIAnimationHandler errorHandlerUI;

    private NewAPICollections collections;
    SessionStepUpdateRequest updateRequest = new();
    float elapsedTime = 0;

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
        collections = NewAPIManager.Instance.GetAPICollections();
    }
    private void Update()
    {
        elapsedTime += Time.deltaTime;
    }

   public void ResetElapsedTime()
    {
        elapsedTime = 0;
    }

    public int GetElapsedTime()
    {
        return ((int)elapsedTime);
    }
    public void SetActiveSimulationID(int activeSimulationID)
    {
        this.activeSimulationID = activeSimulationID;   
    }

    int GetBattery(XRNode node)
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(node);

        if (device.isValid &&
            device.TryGetFeatureValue(CommonUsages.batteryLevel, out float battery))
        {
            float percent = battery * 100f;
            Debug.Log($"{node} Controller Battery: {percent:F0}%");
            return ((int)percent);
        }
        return 0;
    }

    public async void CreateSession(string simType, List<Step> steps,float maxScore,Action execution)
    {
        var activeroutine = StartCoroutine(DelayScaleup());
        SessionRequest request = new SessionRequest()
        {
            user = PlatformGameManager.Instance.GetUserID(),
            simulation_id = activeSimulationID,
            device_id = PlatformGameManager.Instance.GetDeviceID(),
            type = simType,
            device_log= new((int)SystemInfo.batteryLevel, GetBattery(XRNode.LeftHand), GetBattery(XRNode.RightHand)),
            steps = steps,
            max_score = (int)maxScore
        };
        
        string requestJson = JsonUtility.ToJson(request);
        StartCoroutine(NewAPIManager.Instance.PostWebRequest(collections.GetSessionStart(), requestJson, 
            (res) => {
                SessionLogResponse response = JsonUtility.FromJson<SessionLogResponse>(res);
                if (response != null) 
                {
                    StopCoroutine(activeroutine);
                    activeSessionID = response.session_id;
                    SimulationManager.Instance.AssignStepSessionID(response.steps);
                    updateRequest.session_id = response.session_id;
                    ResetElapsedTime();
                    execution.Invoke();
                }
                else
                {
                    StartCoroutine(ScaleupAndDown());
                }
            }));
    }

    public void UpdateSession(StepUpdate updateData)
    {
        ResetElapsedTime();
        updateRequest.steps.Add(updateData);
        string requestJson = JsonUtility.ToJson(updateRequest);
        StartCoroutine(NewAPIManager.Instance.PostWebRequest(collections.GetSessionUpdate(), requestJson,
            (res) =>
            {
                Debug.Log("Steps Updated");
                updateRequest.steps.Clear();
            }));
    }

    public void EndSession()
    {
        SessionEnd request = new SessionEnd()
        {
            session_id = activeSessionID,
        };
        string requestJson = JsonUtility.ToJson(request); 
        StartCoroutine(NewAPIManager.Instance.PostWebRequest(collections.GetSessionEnd(), requestJson,
            (res) =>
            {
                Debug.Log("Steps Ended");
                updateRequest.steps.Clear();
            }));
    }

    IEnumerator DelayScaleup()
    {
        yield return new WaitForSeconds(2f);
        SimulationManager.Instance.setModeUI.SetActive(true);
    }

    IEnumerator ScaleupAndDown(float delayTime=2f)
    {
        errorHandlerUI.ScaleUp();
        yield return new WaitForSeconds(delayTime);
        errorHandlerUI.ScaleDown();
    }
}




[Serializable]
public class SessionRequest
{
    public int user;
    public int simulation_id;
    public int device_id;
    public string type;
    public DeviceLog device_log;
    public List<Step> steps=new();
    public int max_score;
}

[Serializable]
public class DeviceLog
{
    public int device;
    public int left_controller;
    public int right_controller;

    public DeviceLog(int device, int left_controller, int right_controller)
    {
        this.device = device;
        this.left_controller = left_controller;
        this.right_controller = right_controller;
    }
}

[Serializable]
public class Step
{
    public string step_name;
    public string step_type;
    public int log_id;

    public Step(string step_name, string step_type, int log_id=0)
    {
        this.step_name = step_name; 
        this.step_type = step_type; 
        if(log_id != 0)
        {
            this.log_id = log_id;
        }
    }
}

[Serializable]
public class SessionLogResponse
{
    public int session_id;
    public string message;
    public List<Step> steps=new();
}

[Serializable]
public class SessionStepUpdateRequest
{
    public int session_id;
    public List<StepUpdate> steps=new();
}

[Serializable]
public class StepUpdate
{
    public int log_id;
    public int is_completed;   // 0 or 1
    public string response;
    public int time_spent;
    public string error_message;
    public int score;

    public StepUpdate(int log_id,string response,int time_spent,int score,string error_message="")
    {
        this.log_id=log_id;
        this.is_completed = 1;
        this.response=response;
        this.time_spent=time_spent;
        this.score=score;
        this.error_message=error_message;

    }
}

[Serializable]
public class SessionEnd
{
    public int session_id;
}
