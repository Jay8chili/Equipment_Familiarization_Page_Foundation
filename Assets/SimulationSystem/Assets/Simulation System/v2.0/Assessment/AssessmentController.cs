using System.Collections.Generic;
using UnityEngine;

public class AssessmentController : MonoBehaviour
{
    [Tooltip("Per-interaction assessment config.")]
    public List<InteractionAssessmentConfig> interactionConfigs = new();
}

[System.Serializable]
public class InteractionAssessmentConfig
{
    public Interactions interaction;
    public float maxScore = 10f;
    public float hintPenalty = 5f;

    // Only shown in Inspector when interaction is DetectInteraction — managed by editor script
    public float wrongDetectPenalty = 5f;
    public List<DetectInteraction> wrongDetects = new();
    public List<GameObject> WOTD = new();

    // Only shown in Inspector when interaction is GrabInteraction — managed by editor script
    public float wrongGrabPenalty = 5f;
    public List<GrabInteraction> wrongGrabs = new();
}