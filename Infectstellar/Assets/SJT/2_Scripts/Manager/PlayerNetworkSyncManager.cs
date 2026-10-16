using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerNetworkSyncManager : MonoBehaviour
{
    private static PlayerNetworkSyncManager instance;
    public static PlayerNetworkSyncManager Instance => instance;

    private Dictionary<int, _PlayerNetworkSync> playerNetworkSyncDic;
    private _PlayerNetworkSync localPlayerSync;

    public event Action<_PlayerNetworkSync> D_Registered;
    public event Action<_PlayerNetworkSync> D_UnRegistered;

    public _PlayerNetworkSync LocalPlayerSync => localPlayerSync;

    /// <summary>현재 등록된 플레이어 수를 반환합니다.</summary>
    public int UserCount => playerNetworkSyncDic.Count;

    private void Awake()
    {
        playerNetworkSyncDic = new();

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

        if (playerNetworkSync.IsLocalControlActive)
        {
            localPlayerSync = playerNetworkSync;
        }

        D_Registered?.Invoke(playerNetworkSync);
    }

    /// <summary>등록된 본인을 목록에서 제거하고 퇴장을 알립니다.</summary>
    public void UnRegister(_PlayerNetworkSync playerNetworkSync)
    {
        if (playerNetworkSync == null)
        {
            return;
        }

        int actorNumber = playerNetworkSync.ActorNumber;
        if (!playerNetworkSyncDic.TryGetValue(actorNumber, out _PlayerNetworkSync player) || player != playerNetworkSync)
        {
            return;
        }

        playerNetworkSyncDic.Remove(actorNumber);

        if (localPlayerSync == player)
        {
            localPlayerSync = null;
        }

        D_UnRegistered?.Invoke(player);
    }

    /// <summary>본인을 제외하고 ActorNumber 순서로 다음 관전 대상을 반환합니다.</summary>
    public _PlayerNetworkSync GetNextSpectateTarget(int currentActorNumber)
    {
        _PlayerNetworkSync firstPlayer = null;
        _PlayerNetworkSync nextPlayer = null;

        foreach (KeyValuePair<int, _PlayerNetworkSync> entry in playerNetworkSyncDic)
        {
            _PlayerNetworkSync player = entry.Value;
            if (player == null || !player.isActiveAndEnabled || player == localPlayerSync || player.ThirdPersonView == null)
            {
                continue;
            }

            if (firstPlayer == null || entry.Key < firstPlayer.ActorNumber)
            {
                firstPlayer = player;
            }

            if (entry.Key > currentActorNumber && (nextPlayer == null || entry.Key < nextPlayer.ActorNumber))
            {
                nextPlayer = player;
            }
        }

        return nextPlayer != null ? nextPlayer : firstPlayer;
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
        localPlayerSync = null;
        D_Registered = null;
        D_UnRegistered = null;
    }
}
