using UnityEngine;

/// <summary>본인 시점과 사망 중 관전 시점을 전환합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerNetworkSyncManager))]
public sealed class CinemachineCameraController : MonoBehaviour
{
    private static CinemachineCameraController instance;
    private PlayerNetworkSyncManager playerNetworkSyncManager;
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
        playerNetworkSyncManager.D_FocusedPlayerChanged += ShowNextThirdPersonView;

        // 구독 전에 결정된 대상이나 비활성 중 변경된 대상도 반영합니다.
        ShowNextThirdPersonView(playerNetworkSyncManager.FocusedPlayerSync);
    }

    private void OnDisable()
    {
        if (playerNetworkSyncManager == null)
        {
            return;
        }

        playerNetworkSyncManager.D_FocusedPlayerChanged -= ShowNextThirdPersonView;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
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

        if (localPlayer.PointOfView == null)
        {
            return;
        }

        SwitchView(localPlayer.PointOfView);

        if (localPlayer.PlayerRenderer.enabled)
        {
            localPlayer.PlayerRenderer.enabled = false;
        }
    }

    /// <summary>집중 대상에 맞춰 생존 중 일인칭 또는 사망 중 삼인칭을 적용합니다.</summary>
    public void ShowNextThirdPersonView(_PlayerNetworkSync nextPlayer)
    {
        if (nextPlayer == null)
        {
            if (currentViewObj != null)
            {
                currentViewObj.SetActive(false);
            }

            currentViewObj = null;
            return;
        }

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

        if (!nextPlayer.isActiveAndEnabled)
        {
            return;
        }

        if (nextPlayer.ThirdPersonView == null)
        {
            if (nextPlayer == localPlayer)
            {
                ShowLocalPointOfView();
            }

            return;
        }

        SwitchView(nextPlayer.ThirdPersonView);

        if (!localPlayer.PlayerRenderer.enabled)
        {
            localPlayer.PlayerRenderer.enabled = true;
        }
    }

    /// <summary>이전 카메라를 끄고 선택한 카메라를 켭니다.</summary>
    private void SwitchView(GameObject nextView)
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
        if (!currentViewObj.activeSelf)
        {
            currentViewObj.SetActive(true);
        }
    }
}
