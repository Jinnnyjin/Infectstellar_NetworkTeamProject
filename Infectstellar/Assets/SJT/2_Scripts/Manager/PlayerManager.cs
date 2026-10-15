using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    private static PlayerManager instance;
    public static PlayerManager Instance => instance;

    private Dictionary<int, _PlayerNetworkSync> playerNetworkSyncDic;
    private List<int> actorNumberList;
    private _PlayerNetworkSync localPlayerSync;

    /// <summary>목록에 플레이어가 추가된 뒤 알립니다.</summary>
    public event Action<_PlayerNetworkSync> D_PlayerRegistered;

    /// <summary>목록에서 플레이어가 제거된 뒤 알립니다.</summary>
    public event Action<_PlayerNetworkSync> D_PlayerUnregistered;

    public _PlayerNetworkSync LocalPlayerSync => localPlayerSync;

    public List<int> ActorNumberList => actorNumberList;

    private void Awake()
    {
        playerNetworkSyncDic = new();
        actorNumberList = new();

        if (instance != null && instance != this)
        {
#if UNITY_EDITOR
            Debug.LogError("[PlayerManager] 이미 등록된 관리자가 있습니다.", this);
#endif
            enabled = false;
            return;
        }

        instance = this;
    }

    public void Register(_PlayerNetworkSync playerNetworkSync)
    {
        if (instance != this || !isActiveAndEnabled
            || playerNetworkSync == null || !playerNetworkSync.isActiveAndEnabled
            || playerNetworkSync.ActorNumber <= 0
            || playerNetworkSync.gameObject.scene != gameObject.scene)
        {
            return;
        }

        int actorNumber = playerNetworkSync.ActorNumber;

        if (playerNetworkSyncDic.ContainsKey(actorNumber))
        {
#if UNITY_EDITOR
            Debug.LogWarning($"actorNumber {actorNumber} 키가 이미 등록되어 있습니다.");
#endif
            return;
        }

        playerNetworkSyncDic.Add(actorNumber, playerNetworkSync);
        actorNumberList.Add(actorNumber);

        if (playerNetworkSync.IsLocalControlActive)
        {
            localPlayerSync = playerNetworkSync;
        }

        D_PlayerRegistered?.Invoke(playerNetworkSync);
    }

    public void UnRegister(int actorNumber)
    {
        if (!playerNetworkSyncDic.TryGetValue(actorNumber, out _PlayerNetworkSync player))
        {
            return;
        }

        playerNetworkSyncDic.Remove(actorNumber);
        actorNumberList.Remove(actorNumber);

        if (localPlayerSync == player)
        {
            localPlayerSync = null;
        }

        D_PlayerUnregistered?.Invoke(player);
    }

    public _PlayerNetworkSync GetPlayer(int actorNumber)
    {
        if (!playerNetworkSyncDic.TryGetValue(actorNumber, out _PlayerNetworkSync player))
        {
#if UNITY_EDITOR
            Debug.LogWarning($"[GetPlayer] actorNumber {actorNumber} 키가 등록되어 있지 않습니다.");
#endif
            return null;
        }

        return player;
    }

    /// <summary>씬 종료 시 자신이 소유한 참조와 구독을 정리합니다.</summary>
    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }

        playerNetworkSyncDic.Clear();
        actorNumberList.Clear();
        localPlayerSync = null;

        D_PlayerRegistered = null;
        D_PlayerUnregistered = null;
    }
}
