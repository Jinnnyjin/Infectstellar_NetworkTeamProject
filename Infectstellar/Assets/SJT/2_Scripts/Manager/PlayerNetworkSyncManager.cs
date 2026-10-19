using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerNetworkSyncManager : MonoBehaviour
{
    private static PlayerNetworkSyncManager instance;
    public static PlayerNetworkSyncManager Instance => instance;

    private Dictionary<int, _PlayerNetworkSync> playerNetworkSyncDic;
    private _PlayerNetworkSync localPlayerSync;
    private _PlayerNetworkSync focusedPlayerSync;

    public event Action<_PlayerNetworkSync> D_Registered;
    public event Action<_PlayerNetworkSync> D_UnRegistered;
    public event Action<_PlayerNetworkSync> D_FocusedPlayerChanged;

    public _PlayerNetworkSync LocalPlayerSync => localPlayerSync;

    /// <summary>현재 집중 중인 플레이어를 반환합니다.</summary>
    public _PlayerNetworkSync FocusedPlayerSync => focusedPlayerSync;

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

        if (localPlayerSync == null)
        {
            return;
        }

        if (playerNetworkSync == localPlayerSync)
        {
            if (localPlayerSync.IsAlive)
            {
                SetFocusedPlayer(localPlayerSync);
            }
            else
            {
                ChangeToNextFocusedPlayer();
            }

            return;
        }

        // 이미 다른 플레이어를 관전 중이면 현재 대상을 유지합니다.
        if (!localPlayerSync.IsAlive && (focusedPlayerSync == null || focusedPlayerSync == localPlayerSync))
        {
            ChangeToNextFocusedPlayer();
        }
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

        bool wasFocusedPlayer = focusedPlayerSync == player;
        playerNetworkSyncDic.Remove(actorNumber);

        if (localPlayerSync == player)
        {
            localPlayerSync = null;
        }

        if (localPlayerSync == null)
        {
            SetFocusedPlayer(null);
        }
        else if (wasFocusedPlayer)
        {
            // 제거된 대상의 ActorNumber 다음부터 탐색하도록 참조를 먼저 지우지 않습니다.
            ChangeToNextFocusedPlayer();
        }

        D_UnRegistered?.Invoke(player);
    }

    /// <summary>집중 대상을 변경하고 실제로 달라졌을 때만 알립니다.</summary>
    public void SetFocusedPlayer(_PlayerNetworkSync player)
    {
        if (focusedPlayerSync == player)
        {
            return;
        }

        focusedPlayerSync = player;
        D_FocusedPlayerChanged?.Invoke(player);
    }

    /// <summary>생존 중에는 본인을, 사망 중에는 다음 관전 대상을 선택합니다.</summary>
    public void ChangeToNextFocusedPlayer()
    {
        if (localPlayerSync == null || !localPlayerSync.IsLocalControlActive)
        {
            return;
        }

        if (localPlayerSync.IsAlive)
        {
            SetFocusedPlayer(localPlayerSync);
            return;
        }

        int currentActorNumber = focusedPlayerSync != null ? focusedPlayerSync.ActorNumber : 0;
        _PlayerNetworkSync nextPlayer = GetNextSpectateTarget(currentActorNumber);

        SetFocusedPlayer(nextPlayer != null ? nextPlayer : localPlayerSync);
    }

    /// <summary>본인을 포함해 ActorNumber 순서로 다음 관전 대상을 반환합니다.</summary>
    public _PlayerNetworkSync GetNextSpectateTarget(int currentActorNumber)
    {
        _PlayerNetworkSync firstPlayer = null;
        _PlayerNetworkSync nextPlayer = null;

        foreach (KeyValuePair<int, _PlayerNetworkSync> entry in playerNetworkSyncDic)
        {
            _PlayerNetworkSync player = entry.Value;
            if (player == null || !player.isActiveAndEnabled || player.ThirdPersonView == null)
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

    /// <summary>ActorNumber에 해당하는 등록된 플레이어가 있으면 반환합니다.</summary>
    public bool TryGetPlayer(int actorNumber, out _PlayerNetworkSync player)
    {
        return playerNetworkSyncDic.TryGetValue(actorNumber, out player) && player != null;
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
        focusedPlayerSync = null;
        D_Registered = null;
        D_UnRegistered = null;
        D_FocusedPlayerChanged = null;
    }
}
