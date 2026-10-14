using System.Collections.Generic;
using Photon.Pun;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>플레이어 등록·시작 슬롯·내 카메라를 관리하고 30Hz 송신을 설정합니다.</summary>
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public sealed class _PlayerNetworkManager : MonoBehaviour
{
    private static _PlayerNetworkManager instance; // 현재 씬의 관리자
    [SerializeField] private CinemachineBrain brain; // Main Camera의 카메라 전환 컴포넌트
    private readonly List<_PlayerNetworkSync> players = new List<_PlayerNetworkSync>(PlayerNetworkConstants.c_spawnSlotCount); // 등록된 복제본
    private _PlayerNetworkSync localPlayer; // 내가 조작할 플레이어
    private GameObject localCamera; // 현재 활성화된 내 카메라

    public static _PlayerNetworkManager Instance => instance;

    private void Awake()
    {
        if ((instance != null && instance != this) || brain == null)
        {
#if UNITY_EDITOR
            Debug.LogError("[_PlayerNetworkManager] 중복 관리자 또는 CinemachineBrain 참조 누락입니다.", this);
#endif
            enabled = false;
            return;
        }
        instance = this;
        PhotonNetwork.SendRate = PlayerNetworkConstants.c_highSendRate;
        PhotonNetwork.SerializationRate = PlayerNetworkConstants.c_highSendRate;
    }

    private void OnEnable()
    {
        if (instance != this)
        {
            enabled = false;
        }
    }

    private void OnDisable()
    {
        if (instance == this)
        {
            localPlayer?.SuspendLocalControl();
            SwitchCamera(null);
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    #region 플레이어 등록·슬롯·카메라

    /// <summary>슬롯·플레이어 중복을 차단하고 로컬 카메라를 한 개만 활성화합니다.</summary>
    public bool Register(_PlayerNetworkSync player) // 생성 초기화를 마친 등록 대상
    {
        if (instance != this || player == null || brain == null || !isActiveAndEnabled)
        {
            return false;
        }
        // 동일 객체의 재등록은 초기화를 반복하지 않는다. 다른 객체의 Actor/슬롯 중복은 차단한다.
        if (players.Contains(player))
        {
            return true;
        }
        foreach (_PlayerNetworkSync existing in players) // Actor와 시작 슬롯이 겹치는지 비교할 기존 복제본
        {
            if (existing != null && (existing.ActorNumber == player.ActorNumber || existing.SpawnSlot == player.SpawnSlot))
            {
#if UNITY_EDITOR
                Debug.LogError("[_PlayerNetworkManager] 중복 플레이어 또는 시작 슬롯입니다.", player);
#endif
                return false;
            }
        }
        players.Add(player);
        if (player.IsLocalControlActive)
        {
            if (localPlayer != null && localPlayer != player)
            {
                localPlayer.SuspendLocalControl();
            }
            localPlayer = player;
            SwitchCamera(player.PlayerCamera);
        }
        return true;
    }

    /// <summary>파괴된 플레이어를 목록과 로컬 카메라에서 제거합니다.</summary>
    public void Unregister(_PlayerNetworkSync player) // 파괴되어 등록을 해제할 대상
    {
        players.Remove(player);
        if (localPlayer == player)
        {
            player.SuspendLocalControl();
            localPlayer = null;
            SwitchCamera(null);
        }
    }

    /// <summary>ActorNumber에 해당하는 생성 콜백이 등록됐는지 확인합니다.</summary>
    public bool HasActor(int actorNumber) // 방 안에서 플레이어를 구분하는 번호
    {
        foreach (_PlayerNetworkSync player in players) // 생성 등록 여부를 조회할 복제본
        {
            if (player != null && player.ActorNumber == actorNumber)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>등록 목록에서 첫 빈 시작 슬롯을 선택합니다.</summary>
    public int FindFreeSlot()
    {
        for (int slot = 0; slot < PlayerNetworkConstants.c_spawnSlotCount; slot++) // 비어 있는지 검사할 시작 위치 번호
        {
            if (!IsSlotOccupied(slot))
            {
                return slot;
            }
        }
        return PlayerNetworkConstants.c_unassignedSpawnSlot;
    }

    /// <summary>지정한 시작 슬롯을 사용하는 유효 복제본이 있는지 확인합니다.</summary>
    private bool IsSlotOccupied(int slot) // 검사할 시작 위치 번호
    {
        foreach (_PlayerNetworkSync player in players) // 슬롯을 사용 중인지 확인할 복제본
        {
            if (player != null && player.SpawnSlot == slot)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>이전 카메라를 먼저 끈 뒤 새 카메라를 켜고 Brain의 블렌드 기록을 초기화합니다.</summary>
    private void SwitchCamera(GameObject nextCamera) // 새로 켤 내 플레이어 카메라; null이면 기존 카메라만 해제
    {
        if (localCamera != null)
        {
            localCamera.SetActive(false);
        }
        localCamera = nextCamera;
        if (localCamera != null)
        {
            localCamera.SetActive(true);
        }
        if (brain != null)
        {
            brain.ResetState();
        }
    }

    #endregion

}
