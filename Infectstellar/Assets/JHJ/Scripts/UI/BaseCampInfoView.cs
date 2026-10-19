using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Photon.Realtime;
using ENZEUN.Runtime;
using Photon.Pun;

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
        if (goldText == null || noneInfectedText == null || infectedText == null)
            Debug.LogError($"[BaseCampInfoView] 참조 미연결: {name}", this);

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
        if (list == null || list.Count == 0)
        {
            Debug.LogError($"[BaseCampInfoView] 플레이어 리스트 확인 불가: {name}", this);
            return;
        }

        if (list.Count > playerSlots.Length)
        {
            Debug.LogError($"[BaseCampInfoView] 플레이어 수가 {playerSlots.Length}명이 넘음. 플레이어 수 {list.Count}명", this);
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

    // ── Photon Player → 화면용 정보 변환 (생존 여부는 게임 매니저가 줌) 추후 연결
    private static PlayerStatusInfo ToStatusInfo(Player player, bool isAlive)
    {
        return new PlayerStatusInfo(player.NickName, isAlive, GetPlayerColor(player));
    }

    private static Color GetPlayerColor(Player player)
    {
        if (player.CustomProperties.TryGetValue(PhotonLinkManager.myColor_Customproperty, out object value) && value is int)
        {
            return PhotonLinkManager.GetIndexedColor((int)value);
        }

        Debug.LogWarning($"[BaseCampInfoView] {player.NickName}의 색 정보 없음, 흰색으로 표시");
        return Color.white;
    }

    // 테스트 코드

    [ContextMenu("Test/Show Room Players")]
    private void TestShowRoomPlayers()
    {
        if (!PhotonNetwork.InRoom)
        {
            Debug.LogWarning("[BaseCampInfoView] 방 안에서만 테스트 가능", this);
            return;
        }

        List<PlayerStatusInfo> list = new List<PlayerStatusInfo>();
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            list.Add(ToStatusInfo(player, true));
        }
        ShowPlayers(list);
    }
}