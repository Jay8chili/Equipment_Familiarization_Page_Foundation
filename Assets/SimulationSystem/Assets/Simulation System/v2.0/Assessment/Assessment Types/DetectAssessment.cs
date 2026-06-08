using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(AssessmentController))]
public class DetectAssessment : MonoBehaviour, IAssessment
{
    #region Variables
    [field: SerializeField] public float NegativeMarks { get; set; }
    [field: SerializeField] public AssessmentStatus AssessmentResultStatus { get; set; }

    [Space(5f)]
    [Header("Overrides")]
    public string thisAssessmentText;
    public string ErrorMessage { get; set; }

    [Header("Wrong detects.")]
    [Tooltip("//The object detected will be the same as the step's state grabable//")]
    public List<DetectInteraction> WrongDetects = new ();
    #endregion

    #region Unity Methods
    private void Awake()
    {
        if (thisAssessmentText != "")
        {
            ErrorMessage = thisAssessmentText;
        }
        else ErrorMessage = "Wrong object was detected";

        // Set the wrong detects type to wrong
        foreach (DetectInteraction dI in WrongDetects)
        {
            dI.ChangeDetectType(DetectInteraction.DetectType.Wrong);
        }
    }

    private void OnDestroy()
    {
        RemoveListeners();
    }

    #endregion

    #region Helper Methods
    // This method is used to add listeners to the interaction events, to toggle the wrong detects in the scene, if the player can detect or not.
    public void AddListeners()
    {
        if (SimulationManager.Instance.simulationMode == SimulationMode.Assessment)
        {
            GetComponent<Interactions>().OnInteractionStartedEvent.AddListener(() => ToggleWrongdetects(true));
            GetComponent<Interactions>().OnInteractionCompletedEvent.AddListener(() => ToggleWrongdetects(false));
        }
    }

    // This method is used to remove the listeners added to the interaction events, when the assessment is completed or failed.
    public void RemoveListeners()
    {
        if (SimulationManager.Instance.simulationMode == SimulationMode.Assessment)
        {
            GetComponent<Interactions>().OnInteractionStartedEvent.RemoveListener(() => ToggleWrongdetects(true));
            GetComponent<Interactions>().OnInteractionCompletedEvent.RemoveListener(() => ToggleWrongdetects(false));
        }
    }

    // This method is used to toggle the wrong detects in the scene, if the player can detect or not.
    private void ToggleWrongdetects(bool enableWrongDetects)
    {
        if (!enableWrongDetects)
        {
            foreach (var GO in WrongDetects)
            {
                GO.gameObject.SetActive(false);
            }
        }
        else
        {
            foreach (var GO in WrongDetects)
            {
                GO.gameObject.SetActive(true);
            }
        }
    }
    #endregion
}
