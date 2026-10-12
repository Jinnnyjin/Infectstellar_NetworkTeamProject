using System;
using UnityEngine;
using Photon.Pun;

/// <summary>
/// 아이템 네트워크 담당 : ShootRPC 송수신
/// </summary>
public class ItemNetwork : MonoBehaviourPun
{
    private Action<Vector3, Quaternion, Vector3, int> onShootReceived;

    public void Init(Action<Vector3, Quaternion, Vector3, int> OnShootReceived)
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

    [PunRPC]
    private void RpcShoot(Vector3 startPos, Quaternion startRot, Vector3 force, PhotonMessageInfo info)
    {
        Debug.Log($"[ItemNetwork] shoot 수신: 보낸 사람 actorNumber {info.Sender.ActorNumber}, / 내가 보낸사람(판정 컴퓨터)?: {info.Sender.IsLocal}" ,this);

        if (onShootReceived == null)
        {
            Debug.LogError("[ItemNetwork] Init 안 됨, Shoot 수신 처리 불가", this);
            return;
        }

        onShootReceived.Invoke(startPos, startRot, force, info.Sender.ActorNumber);
    }
}
