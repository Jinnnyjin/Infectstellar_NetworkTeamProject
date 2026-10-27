using Photon.Pun;
using UnityEngine;
using MoreMountains.Feedbacks;

[RequireComponent(typeof(PhotonView))]
public class ShopProduct : MonoBehaviourPun, IInteractObject
{
    [SerializeField] private ShopProductSO product;
    [SerializeField, Tooltip("비어있으면 씬에서 찾음 (프리팹은 씬 오브젝트를 참조할 수 없음)")] private Shop shop;
    [SerializeField] private MMF_Player purchaseFb;

    private string promptText;

    public string PromptText => promptText;
    public string Description => InteractPromptSO.Instance.ShopProductDescription;
    public bool CanInteract => product != null && product.Item != null && shop != null;


    private void Awake()
    {
        promptText = InteractPromptSO.Instance.CannotPurchase;

        if (shop == null)
            shop = FindFirstObjectByType<Shop>();

        if (shop == null)
            Debug.LogError($"[ShopProduct] 씬에 Shop 없음: {name}", this);

        if (product == null || product.Item == null || product.Item.ItemData == null)
        {
            Debug.LogError($"[ShopProduct] 상품 정보 비어있음: {name}", this);
            return;
        }

        promptText = InteractPromptSO.Instance.GetPurchaseText(product.Item.ItemData.ItemName, product.BuyPrice);
    }


    public void Interact()
    {
        if (!PhotonNetwork.InRoom)
        {
            Debug.LogWarning("[ShopProduct] 구매 요청 불가: 방에 들어와 있지 않음", this);
            return;
        }

        photonView.RPC(nameof(RpcRequestPurchase), RpcTarget.MasterClient);
        Debug.Log($"[ShopProduct] 구매 요청 보냄, {product.name}", this);
    }

    [PunRPC]
    private void RpcRequestPurchase(PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning($"[ShopProduct] 구매 요청 수신했으나 방장이 아님 (요청: {info.Sender.NickName})", this);
            return;
        }

        if (!CanInteract)
        {
            Debug.LogError($"[ShopProduct] 구매 처리 불가: 상품 또는 Shop 비어있음", this);
            return;
        }

        bool isSuccess = shop.TryPurchase(product.Item, product.BuyPrice);
        
        if(isSuccess)
            photonView.RPC(nameof(RpcPlayPurchaseFeedback), RpcTarget.All);
        
        Debug.Log($"[ShopProduct] 구매 요청한 사람 : {info.Sender.NickName} 구매물품 {product.name}, 성공여부 {isSuccess}", this);

        // 실패 시 요청한 사람에게 알림? / 골드 부족, 빈자리없음?
    }

    [PunRPC]
    private void RpcPlayPurchaseFeedback()
    {
        if (purchaseFb == null)
            return;

        purchaseFb.PlayFeedbacks();
    }
}
