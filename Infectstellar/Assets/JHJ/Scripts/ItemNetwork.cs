using System;
using UnityEngine;
using Photon.Pun;

/// <summary>
/// 아이템 네트워크 담당 : ShootRPC 송수신, 닿은 대상 actorNumber 제공
/// </summary>
public class ItemNetwork : MonoBehaviourPun
{
    private Action<Vector3, Quaternion, Vector3, int, bool> onShootReceived;

    public void Init(Action<Vector3, Quaternion, Vector3, int, bool> OnShootReceived)
    {
        this.onShootReceived = OnShootReceived;   
    }

    public void SendShoot(Vector3 startPos, Quaternion startRot, Vector3 force)
    {
        if(!PhotonNetwork.InRoom)
        {
            Debug.LogWarning($"현재 방에 들어가있지 않음");
            return;
        }

        photonView.RPC(nameof(RpcShoot), RpcTarget.All,startPos, startRot, force);
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
}
