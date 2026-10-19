using Photon.Pun;
using System;
using UnityEngine;

/// <summary>
/// 아이템 네트워크 담당 : Shoot·Drop·Stop RPC 송수신, 닿은 대상 actorNumber 제공
/// </summary>
public class ItemNetwork : MonoBehaviourPun
{
    private Action<Vector3, Quaternion, Vector3, int, bool> onShootReceived;
    private Action<Vector3, Quaternion> onStopReceived;
    private Action<Vector3, Quaternion, bool> onDropReceived;
    private Func<bool> tryMarkRemoved;

    public void Init(Action<Vector3, Quaternion, Vector3, int, bool> onShootReceived, Action<Vector3, Quaternion> onStopReceived, Action<Vector3, Quaternion, bool> onDropReceived, Func<bool> tryMarkRemoved)
    {
        this.onShootReceived = onShootReceived;
        this.onStopReceived = onStopReceived;
        this.onDropReceived = onDropReceived;
        this.tryMarkRemoved = tryMarkRemoved;
    }

    // =============================================================
    // Stop
    public void SendStop(Vector3 pos, Quaternion rot)
    {
        if (!PhotonNetwork.InRoom)
        {
            Debug.LogWarning($"현재 방에 들어가있지 않음");
            return;
        }

        photonView.RPC(nameof(RpcStop), RpcTarget.Others, pos, rot);
    }

    [PunRPC]
    private void RpcStop(Vector3 pos, Quaternion rot, PhotonMessageInfo info)
    {
        Debug.Log($"[ItemNetwork] Stop 수신: 보낸 사람 actorNumber {info.Sender.ActorNumber}",this);

        if (onStopReceived == null)
        {
            Debug.LogError("[ItemNetwork] Init 안 됨, Shoot 수신 처리 불가", this);
            return;
        }

        onStopReceived.Invoke(pos, rot);
    }

    // =============================================================
    // Shoot
    public void SendShoot(Vector3 startPos, Quaternion startRot, Vector3 force)
    {
        if(!PhotonNetwork.InRoom)
        {
            Debug.LogWarning($"현재 방에 들어가있지 않음");
            return;
        }

        photonView.RPC(nameof(RpcShoot), RpcTarget.All,startPos, startRot, force);
    }

    [PunRPC]
    private void RpcShoot(Vector3 startPos, Quaternion startRot, Vector3 force, PhotonMessageInfo info)
    {
        Debug.Log($"[ItemNetwork] shoot 수신: 보낸 사람 actorNumber {info.Sender.ActorNumber}, / 내가 보낸사람(판정 컴퓨터)?: {info.Sender.IsLocal}" ,this);

        if (onShootReceived == null)
        {
            Debug.LogError("[ItemNetwork] Init 안 됨, Shoot 수신 처리 불가", this);
            return;
        }

        onShootReceived.Invoke(startPos, startRot, force, info.Sender.ActorNumber, info.Sender.IsLocal);
    }

    // =============================================================
    // Drop
    public void SendDrop(Vector3 startPos, Quaternion startRot)
    {
        if (!PhotonNetwork.InRoom)
        {
            Debug.LogWarning($"현재 방에 들어가있지 않음");
            return;
        }

        photonView.RPC(nameof(RpcDrop), RpcTarget.All, startPos, startRot);
    }

    [PunRPC]
    private void RpcDrop(Vector3 startPos, Quaternion startRot, PhotonMessageInfo info)
    {
        Debug.Log($"[ItemNetwork] Drop 수신: 보낸 사람 actorNumber {info.Sender.ActorNumber}, / 내가 보낸사람(판정 컴퓨터)?: {info.Sender.IsLocal}", this);

        if (onDropReceived == null)
        {
            Debug.LogError("[ItemNetwork] Init 안 됨, Drop 수신 처리 불가", this);
            return;
        }

        onDropReceived(startPos, startRot, info.Sender.IsLocal);
    }


    // =============================================================
    // Consume
    public void SendConsume()
    {
        if (!PhotonNetwork.InRoom)
        {
            Debug.LogWarning($"현재 방에 들어가있지 않음");
            return;
        }

        photonView.RPC(nameof(RpcConsume), RpcTarget.MasterClient);
    }

    [PunRPC]
    private void RpcConsume(PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning("[ItemNetwork] 방장이 아님, 삭제 요청 무시", this);
            return;
        }

        Debug.Log($"[ItemNetwork] Consume 수신: 보낸 사람 actorNumber {info.Sender.ActorNumber}, / 내가 보낸사람(판정 컴퓨터)?: {info.Sender.IsLocal}", this);

        if (tryMarkRemoved == null)
        {
            Debug.LogError("[ItemNetwork] Init 안 됨, Consume 수신 처리 불가", this);
            return;
        }

        if(!tryMarkRemoved())
        {
            Debug.Log($"[ItemNetwork] 이미 제거 처리 중, 삭제 요청 무시", this);
            return;
        }

        PhotonNetwork.Destroy(gameObject);
    }

    public int GetOwnerActorNumber(Collider collider)
    {
        // 본인 photonview와 헷갈림 방지 target photonView 
        PhotonView targetView = collider.GetComponentInParent<PhotonView>();

        // 벽, 바닥 등..
        if (targetView == null)
            return -1;

        return targetView.OwnerActorNr;
    }

    /// <summary>
    /// 이 컴퓨터의 Actor 번호 (방 밖 = -1)
    /// </summary>
    public int GetLocalActorNumber()
    {
        return PhotonNetwork.InRoom ? PhotonNetwork.LocalPlayer.ActorNumber : -1;
    }

}
