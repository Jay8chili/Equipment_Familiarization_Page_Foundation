using Platform;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class bypassScript : MonoBehaviour
{
    [Header("Enter module ids to hide")]
    public List<int> ModuleIDToEnable;
    public bool ISForModule;

    [Header("Enter Simulation ids to hide specific to modules")]
    public List<ListOfModules> IdToEnable; 

    
    private void OnEnable()
    {
        if (LoginManager.Instance.apiEnvironment == LoginManager.APIEnvironment.Production)
        {
            StartCoroutine(SetButtonFalse());
        }
    }

    IEnumerator SetButtonFalse()
    {
        yield return new WaitForSeconds(0.2f);
        
        if (!ISForModule)
        {
            
            foreach(var module in IdToEnable)
            {
                //if(module.ModuleId == GameManager.Instance.moduleID)
                //{
                //    if (module.SimulationId.Contains(GetComponent<Platform.Card>().id))
                //    {
                //        this.gameObject.SetActive(false);
                //        GetComponent<Platform.Card>().button.interactable = false;
                //        GetComponent<SimulationDownloadProgress>().DisableDownloadButton();
                //    }
                //    yield break;
                //}
            }
        }

        else
        {
            if (ModuleIDToEnable.Contains(GetComponent<Platform.Card>().id))
            {
                //donothing;
                this.gameObject.SetActive(false);

            }
        }
       
    }

}



[Serializable]
public struct ListOfModules
{
    public int ModuleId;
    public List<int> SimulationId;
}
