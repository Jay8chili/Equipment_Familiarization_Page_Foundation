using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LeaderBoardCard : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI rank;
    [SerializeField] private TextMeshProUGUI userName;
    [SerializeField] private TextMeshProUGUI score;
    [SerializeField] private Image background;
    [SerializeField] private Color userCardColor;

    public void SetRankDetails(string userName, int rank, int score,bool isPlayer=false)
    {
        this.rank.text=rank.ToString();
        this.userName.text=userName;
        this.score.text=score.ToString();

        if (isPlayer)
        {
            this.background.color=userCardColor;
        }
    }
}
