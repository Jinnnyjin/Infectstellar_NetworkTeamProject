using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 게임매니저에서 초기값도 준다고 생각하고 구현, 추후 수정될 수 있음 [임시]
/// </summary>
public class BaseCampInfoView : MonoBehaviour
{
    [SerializeField] private TMP_Text goldText;
    [SerializeField] private PlayerSlotView[] playerSlots;
    [SerializeField] private TMP_Text noneInfectedText;
    [SerializeField] private TMP_Text infectedText;

    private void Awake()
    {
        if(goldText == null || noneInfectedText == null || infectedText == null)
        {
            Debug.LogError($"[BaseCampInfoVeiw] 참조 미연결: {name}", this);
            return;
        }

        if (playerSlots == null || playerSlots.Length == 0)
        {
            Debug.LogError($"[BaseCampInfoView] 플레이어 슬롯 미연결: {name}", this);
            return;
        }

        for (int i = 0; i < playerSlots.Length; i++)
        {
            if (playerSlots[i] == null)
                Debug.LogError($"[BaseCampInfoView] 플레이어 슬롯 {i}번 비어 있음: {name}", this);
        }
    }

    public void ShowGold(int amount)
    {
        goldText.text = amount.ToString("N0") + " G";
    }

    public void ShowInfected(bool infected)
    {
        infectedText.gameObject.SetActive(infected);
        noneInfectedText.gameObject.SetActive(!infected);
    }

    public void ShowPlayers(IReadOnlyList<PlayerStatusInfo> list)
    {
        if(list  == null || list.Count == 0)
        {
            Debug.LogError($"[BaseCampInfoVeiw] 플레이어 리스트 확인 불가: {name}", this);
            return;
        }

        if(list.Count > playerSlots.Length)
        {
            Debug.LogError($"[BaseCampInfoVeiw] 플레이어 수가 {playerSlots.Length}명이 넘음. 플레이어 수 {list.Count}명", this);
            return;
        }

        for (int i = 0; i < playerSlots.Length; i++)
        {
            if (i < list.Count)
                playerSlots[i].SetInfo(list[i]);

            else
                playerSlots[i].Clear();
        }
    }

    [ContextMenu("Test/Show Sample")]
    private void TestShowSample()
    {
        ShowGold(12345);
        ShowInfected(true);
        ShowPlayers(new List<PlayerStatusInfo>
    {
        new PlayerStatusInfo("KHJ", true),
        new PlayerStatusInfo("SJT", false),
        new PlayerStatusInfo("JHJ", true),
    });
    }

    [ContextMenu("Test/Show Full")]
    private void TestShowFull()
    {
        ShowGold(0);
        ShowInfected(false);
        ShowPlayers(new List<PlayerStatusInfo>
    {
        new PlayerStatusInfo("KHJ", true),
        new PlayerStatusInfo("SJT", true),
        new PlayerStatusInfo("JHJ", true),
        new PlayerStatusInfo("TEST123", true),
    });
    }
}
