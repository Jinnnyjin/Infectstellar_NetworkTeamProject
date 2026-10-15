using Photon.Pun;
using UnityEngine;

/// <summary>플레이어의 로컬 상태 변경을 RPC로 전달하고 원격 상태를 적용합니다.</summary>
[DisallowMultipleComponent]
public sealed class PlayerNetworkRpc : MonoBehaviour
{
    private PhotonView photonView;
    private _PlayerNetworkSync networkSync;
    private FlashlightController flashlightController;
    private PlayerGrabber playerGrabber;
    private Health health;
    private PlayerAnimationController playerAnimationController;

    /// <summary>RPC 송수신에 필요한 같은 객체의 컴포넌트를 확보합니다.</summary>
    private void Awake()
    {
        photonView = GetComponent<PhotonView>();
        networkSync = GetComponent<_PlayerNetworkSync>();
        flashlightController = GetComponent<FlashlightController>();
        playerGrabber = GetComponent<PlayerGrabber>();
        health = GetComponent<Health>();
        playerAnimationController = GetComponentInChildren<PlayerAnimationController>();

        if (photonView == null || networkSync == null || flashlightController == null || playerGrabber == null || health == null || playerAnimationController == null)
        {
#if UNITY_EDITOR
            Debug.LogError("[PlayerNetworkRpc] PhotonView, NetworkSync, FlashlightController, PlayerGrabber, playerAnimationController 또는 Health가 없습니다.", this);
#endif
            enabled = false;
            return;
        }
    }

    /// <summary>손전등 상태 변경·아이템 잡기·피해 적용 알림을 구독합니다.</summary>
    private void OnEnable()
    {
        if (flashlightController != null)
        {
            flashlightController.D_FlashlightStateChanged += HandleFlashlightStateChanged;
        }

        if (playerGrabber != null)
        {
            playerGrabber.GrabRequested += HandleGrabRequested;
        }

        if (health != null)
        {
            health.D_DamageApplied += HandleDamageApplied;
        }

        if (playerAnimationController != null)
        {
            playerAnimationController.D_HandsUpStateChanged += HandleHandsUpStateChanged;
        }
    }

    /// <summary>손전등 상태 변경·아이템 잡기·피해 적용 알림의 구독을 해제합니다.</summary>
    private void OnDisable()
    {
        if (flashlightController != null)
        {
            flashlightController.D_FlashlightStateChanged -= HandleFlashlightStateChanged;
        }

        if (playerGrabber != null)
        {
            playerGrabber.GrabRequested -= HandleGrabRequested;
        }

        if (health != null)
        {
            health.D_DamageApplied -= HandleDamageApplied;
        }

        if (playerAnimationController != null)
        {
            playerAnimationController.D_HandsUpStateChanged -= HandleHandsUpStateChanged;
        }
    }

    /// <summary>로컬에서 적용한 손전등 상태를 다른 클라이언트에 전달합니다.</summary>
    private void HandleFlashlightStateChanged(bool isOn)
    {
        if (!isActiveAndEnabled || photonView == null || networkSync == null || !networkSync.IsLocalControlActive)
        {
            return;
        }

        photonView.RPC(nameof(RpcSetFlashlight), RpcTarget.Others, isOn);
    }

    /// <summary>해당 플레이어의 제어 주체가 보낸 손전등 상태를 적용합니다.</summary>
    [PunRPC]
    private void RpcSetFlashlight(bool isOn, PhotonMessageInfo info)
    {
        if (!isActiveAndEnabled || photonView == null || flashlightController == null)
        {
            return;
        }

        if (info.Sender == null || info.Sender.ActorNumber != photonView.ControllerActorNr)
        {
            return;
        }

        flashlightController.SetFlashlightEnabled(isOn);
    }

    /// <summary>로컬에서 집은 아이템의 네트워크 식별자를 다른 클라이언트에 전달합니다.</summary>
    private void HandleGrabRequested(bool isHandsUp)
    {
        if (!isActiveAndEnabled || photonView == null || networkSync == null || !networkSync.IsLocalControlActive || playerGrabber == null)
        {
            return;
        }

        // 기존 bool 인자는 손들기용이며, 집은 아이템은 Grabber에서 읽습니다.
        Item item = playerGrabber.HeldItem;
        if (item == null || item.CurrentState != Item.State.Grabbed)
        {
            return;
        }

        PhotonView itemView = item.GetComponent<PhotonView>();
        if (itemView == null || itemView.ViewID == 0)
        {
            return;
        }

        photonView.RPC(nameof(RpcGrabItem), RpcTarget.Others, itemView.ViewID);
    }

    /// <summary>원격 플레이어가 집은 아이템을 해당 소지자 번호로 적용합니다.</summary>
    [PunRPC]
    private void RpcGrabItem(int itemViewId, PhotonMessageInfo info)
    {
        if (!isActiveAndEnabled || photonView == null || info.Sender == null || info.Sender.ActorNumber != photonView.ControllerActorNr || itemViewId <= 0)
        {
            return;
        }

        PhotonView itemView = PhotonView.Find(itemViewId);
        if (itemView == null)
        {
            return;
        }

        Item item = itemView.GetComponent<Item>();
        if (item == null)
        {
            return;
        }

        item.ApplyGrab(info.Sender.ActorNumber);

        playerAnimationController.SetHandsUp(item.CurrentState == Item.State.Grabbed);
    }

    /// <summary>이 클라이언트에서 적용한 피해를 다른 클라이언트에 전달합니다.</summary>
    private void HandleDamageApplied(DamageInfo damageInfo)
    {
        if (!isActiveAndEnabled || !PhotonNetwork.InRoom || photonView == null || photonView.ViewID == 0)
        {
            return;
        }

        // 공격자가 피해 대상의 복제본에 적용한 피해도 보내므로 로컬 소유권을 요구하지 않습니다.
        photonView.RPC(nameof(RpcTakeDamage), RpcTarget.Others, damageInfo.Amount);
    }

    /// <summary>수신한 피해를 송신용 이벤트 없이 체력에 적용합니다.</summary>
    [PunRPC]
    private void RpcTakeDamage(float amount, PhotonMessageInfo info)
    {
        if (!isActiveAndEnabled || !PhotonNetwork.InRoom || photonView == null || health == null || info.Sender == null)
        {
            return;
        }

        // 발신자는 피해 대상의 제어 주체와 다를 수 있으며, 유효한 피해량 검사는 Health가 담당합니다.
        health.ApplyDamage(new DamageInfo(amount));
    }

    private void HandleHandsUpStateChanged(bool isHandsup)
    {
        if (!isActiveAndEnabled || photonView == null || networkSync == null || !networkSync.IsLocalControlActive)
        {
            return;
        }

        photonView.RPC(nameof(RpcSetHandsUp), RpcTarget.Others, isHandsup);
    }

    [PunRPC]
    private void RpcSetHandsUp(bool isHandsUp, PhotonMessageInfo info)
    {
        if (!isActiveAndEnabled || photonView == null || playerAnimationController == null || info.Sender == null)
        {
            return;
        }

        if (info.Sender.ActorNumber != photonView.ControllerActorNr)
        {
            return;
        }

        playerAnimationController.SetHandsUp(isHandsUp);
    }
}
