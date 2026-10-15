using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;

/// <summary>본인 시점과 사망 후 다른 플레이어의 관전 시점을 전환합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerManager))]
public sealed class CinemachineCameraController : MonoBehaviour
{
    private static CinemachineCameraController instance;
    public static CinemachineCameraController Instance => instance;
    private Dictionary<int, _PlayerNetworkSync> playerNetworkSyncDic;
    private _PlayerNetworkSync localPlayerSync;

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
    }

    public void UnRegister(int actorNumber)
    {
        if (!playerNetworkSyncDic.TryGetValue(actorNumber, out _PlayerNetworkSync player))
        {
            return;
        }

        playerNetworkSyncDic.Remove(actorNumber);

        if (localPlayerSync == player)
        {
            localPlayerSync = null;
        }
    }

    public void ShowPointOfView()
    {
    }
}