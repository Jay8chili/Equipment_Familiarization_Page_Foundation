using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SocialPlatforms.Impl;

public class ProgressDataContainer : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI totalAttempts;
    [SerializeField] private TextMeshProUGUI guidedAttempts;
    [SerializeField] private TextMeshProUGUI assessmentAttempts;
    [SerializeField] private TextMeshProUGUI score;
    [SerializeField] private TextMeshProUGUI timeSpent;
    [SerializeField] private TextMeshProUGUI simName;

    public void SetData(int totalAttempts,int guidedAttempts,int assessmentAttempts, int score, int timeSpent, string simName=null)
    {
        this.totalAttempts.text= totalAttempts.ToString();
        this.guidedAttempts.text= guidedAttempts.ToString();
        this.assessmentAttempts.text= assessmentAttempts.ToString();
        this.score.text= score.ToString();
        this.timeSpent.text= (timeSpent/60).ToString();
        if (!string.IsNullOrEmpty(simName))
        {
            this.simName.text = simName;
        }
    }

}
