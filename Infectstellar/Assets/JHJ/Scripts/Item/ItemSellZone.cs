using ENZEUN.Runtime;
using Photon.Pun;
using System;
using UnityEngine;
using MoreMountains.Feedbacks;

/// <summary>
/// 판매 구역. 들어온 아이템을 Master가 판매 처리
/// BoxCollider: Is Trigger, Exclude Layers는 Item 외 전부
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class ItemSellZone : MonoBehaviourPun
{
    [SerializeField] private MMF_Player sellFb;

    // [임시] 현재는 금액만 넘김, 필요한 정보 있으면 추가 예정
    // 판매될 때 금액만 전달합니다. 게임 매니저에서 구독하는 방식으로 이루어져 있습니다.
    // Master에서만 발생합니다.
    public static event Action<int> ItemSold;

    private void OnTriggerEnter(Collider other)
    {
        if(!PhotonNetwork.IsMasterClient)
            return;

        Rigidbody rb = other.attachedRigidbody;
        if (rb == null)
            return;

        if (!rb.TryGetComponent(out Item item))
            return;

        if (!item.TryMarkRemoved())
            return;

        int price = item.ItemData.Price;
        PhotonNetwork.Destroy(item.gameObject);

        if (GameManager.Instance!=null)
        GameManager.Instance?.RequestAddGold(price);
        ItemSold?.Invoke(price);
        Debug.Log($"[ItemSellZone] {item.name} {price} 판매완료");
        photonView.RPC(nameof(RpcPlaySellFeedback), RpcTarget.All);
    }

    [PunRPC]
    private void RpcPlaySellFeedback()
    {
        if (sellFb == null)
            return;

        sellFb.PlayFeedbacks();
    }

    private void OnDrawGizmos()
    {
        if (!TryGetComponent(out BoxCollider box))
            return;

        Gizmos.color = Color.green;

        Matrix4x4 originalMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;

        Gizmos.DrawWireCube(box.center, box.size);

        Gizmos.matrix = originalMatrix;
    }
}
