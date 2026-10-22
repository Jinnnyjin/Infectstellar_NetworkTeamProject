using TMPro;
using UnityEngine;
using ENZEUN.Runtime;

public class ResultView : MonoBehaviour
{
    [SerializeField] private TMP_Text earnedGoldText;
    [SerializeField] private TMP_Text killBonusText;
    [SerializeField] private TMP_Text infectedWinBonusText;
    [SerializeField] private TMP_Text deadPenaltyText;
    [SerializeField] private TMP_Text resultGoldText;

    public void Show(GameResultData data)
    {
        earnedGoldText.text = data.earnedGold;
        killBonusText.text = data.killInfectedBonus;
        infectedWinBonusText.text = data.infectedWinBonus;
        deadPenaltyText.text = data.DeadPenalty;
        resultGoldText.text = data.resultGold;
        gameObject.SetActive(true);
    }
}
