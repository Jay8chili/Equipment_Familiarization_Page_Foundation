using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class Debugger : MonoBehaviour
{
    [Header("*USE M TO LOGIN USING PIN AND USE N TO SELECT THE SIMULATION MODULE*")]


    public GameObject Content;
    [Header("*MODULE ID IN PRODUCTION STARTS WITH 1 AND STAGING STARTS WITH 2 AND SO ON*")]
    public int ModuleId;

    [TooltipAttribute("Starts with 0, Dont Put Simulation ID or Code, Just the int of Simulation you need to access")]
    public int AccessSimulation;

    [TooltipAttribute("Use this Key To Download Or Play the Simulation In the app // Use Anything Other Than M and N ")]
    public KeyCode AccessSimulationWithThisKey;

    [HideInInspector]
    public List<Button> PlayButtons;


    public static Debugger Instance;

    private bool GotAllButtons;
    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        if (Input.GetKeyDown(AccessSimulationWithThisKey))
        {
            AccessSimulationWithInt();
        }

        if(!GotAllButtons && Content.transform.childCount>0)
        {
            GetAllButtons();
        }
    }
    public void GetAllButtons()
    {
            GotAllButtons = true;
        PlayButtons = new List<Button>();

        for (int i = 0; i < Content.transform.childCount; i++)
        {
            PlayButtons.Add(Content.transform.GetChild(i).transform.GetChild(Content.transform.GetChild(i).transform.childCount-1).GetComponent<Button>());
        }
    }

    public void AccessSimulationWithInt()
    {
        if ((AccessSimulation > PlayButtons.Count - 1))
        {
            return;
        }

        else
        {
            PlayButtons[AccessSimulation].onClick.Invoke();
        }
    }
}
