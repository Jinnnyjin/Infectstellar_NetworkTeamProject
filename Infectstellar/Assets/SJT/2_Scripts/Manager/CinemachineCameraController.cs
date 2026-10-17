using UnityEngine;

/// <summary>본인 시점과 사망 중 관전 시점을 전환합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerNetworkSyncManager))]
public sealed class CinemachineCameraController : MonoBehaviour
{
    private static CinemachineCameraController instance;
    private PlayerNetworkSyncManager playerNetworkSyncManager;
    private int currentActorNumber;
    private GameObject currentViewObj;

    /// <summary>현재 씬의 카메라 관리자를 반환합니다.</summary>
    public static CinemachineCameraController Instance => instance;

    private void Awake()
    {
        playerNetworkSyncManager = GetComponent<PlayerNetworkSyncManager>();
        if (instance != null && instance != this)
        {
            enabled = false;
            return;
        }

        instance = this;
    }

    private void OnEnable()
    {
        playerNetworkSyncManager.D_Registered += HandleRegistered;
        playerNetworkSyncManager.D_UnRegistered += HandleUnRegistered;

        // 관리자 활성화 전에 본인이 등록된 경우에도 반영합니다.
        _PlayerNetworkSync localPlayer = playerNetworkSyncManager.LocalPlayerSync;
        if (localPlayer != null)
        {
            HandleRegistered(localPlayer);
        }
    }

    private void OnDisable()
    {
        if (playerNetworkSyncManager == null)
        {
            return;
        }

        playerNetworkSyncManager.D_Registered -= HandleRegistered;
        playerNetworkSyncManager.D_UnRegistered -= HandleUnRegistered;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    /// <summary>본인 등록 시 초기 시점을 설정하고, 대상이 없던 관전을 시작합니다.</summary>
    private void HandleRegistered(_PlayerNetworkSync player)
    {
        _PlayerNetworkSync localPlayer = playerNetworkSyncManager.LocalPlayerSync;
        if (localPlayer == null)
        {
            return;
        }

        if (player == localPlayer)
        {
            if (localPlayer.IsAlive)
            {
                ShowLocalPointOfView();
            }
            else
            {
                ShowNextThirdPersonView();
            }

            return;
        }

        // 이미 다른 사람을 관전 중이면 신규 참가자가 와도 시점을 유지합니다.
        if (!localPlayer.IsAlive && (currentViewObj == null || currentActorNumber == localPlayer.ActorNumber))
        {
            ShowNextThirdPersonView();
        }
    }

    /// <summary>본인이나 현재 관전 대상이 퇴장하면 시점을 정리합니다.</summary>
    private void HandleUnRegistered(_PlayerNetworkSync player)
    {
        if (playerNetworkSyncManager.LocalPlayerSync == null)
        {
            if (currentViewObj != null)
            {
                currentViewObj.SetActive(false);
            }

            currentViewObj = null;
            currentActorNumber = 0;
            return;
        }

        if (player.ActorNumber == currentActorNumber)
        {
            ShowNextThirdPersonView();
        }
    }

    /// <summary>본인 시점으로 복귀합니다.</summary>
    public void ShowLocalPointOfView()
    {
        _PlayerNetworkSync localPlayer = playerNetworkSyncManager.LocalPlayerSync;
        if (localPlayer == null || !localPlayer.IsLocalControlActive)
        {
            return;
        }

        SwitchView(localPlayer.PointOfView, localPlayer.ActorNumber);

        if (localPlayer.MineModel.activeSelf)
        {
            localPlayer.MineModel.SetActive(false);
        }
    }

    /// <summary>사망 중 본인을 포함한 지정 플레이어의 삼인칭 시점으로 전환합니다.</summary>
    public void ShowThirdPersonView(int actorNumber)
    {
        _PlayerNetworkSync localPlayer = playerNetworkSyncManager.LocalPlayerSync;
        if (localPlayer == null || !localPlayer.IsLocalControlActive || localPlayer.IsAlive)
        {
            return;
        }

        _PlayerNetworkSync player = playerNetworkSyncManager.GetPlayer(actorNumber);
        if (player == null || !player.isActiveAndEnabled)
        {
            return;
        }

        SwitchView(player.ThirdPersonView, actorNumber);

        if (!localPlayer.MineModel.activeSelf)
        {
            localPlayer.MineModel.SetActive(true);
        }
    }

    /// <summary>다음 관전 대상을 선택하고, 대상이 없으면 본인 시점으로 복귀합니다.</summary>
    public void ShowNextThirdPersonView()
    {
        _PlayerNetworkSync localPlayer = playerNetworkSyncManager.LocalPlayerSync;
        if (localPlayer == null || !localPlayer.IsLocalControlActive)
        {
            return;
        }

        if (localPlayer.IsAlive)
        {
            ShowLocalPointOfView();
            return;
        }

        _PlayerNetworkSync nextPlayer = playerNetworkSyncManager.GetNextSpectateTarget(currentActorNumber);
        if (nextPlayer == null)
        {
            ShowLocalPointOfView();
            return;
        }

        ShowThirdPersonView(nextPlayer.ActorNumber);
    }

    /// <summary>이전 카메라를 끄고 선택한 카메라를 켭니다.</summary>
    private void SwitchView(GameObject nextView, int actorNumber)
    {
        if (nextView == null)
        {
            return;
        }

        if (currentViewObj != null && currentViewObj != nextView)
        {
            currentViewObj.SetActive(false);
        }

        currentViewObj = nextView;
        currentActorNumber = actorNumber;
        currentViewObj.SetActive(true);
    }
}
