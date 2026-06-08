using UnityEngine;

public interface IAssessment 
{
    public float NegativeMarks { get; set; }
    public AssessmentStatus AssessmentResultStatus { get; set; }
    public string ErrorMessage { get; }
}
