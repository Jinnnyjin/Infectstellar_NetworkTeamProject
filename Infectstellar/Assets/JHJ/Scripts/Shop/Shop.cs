using Photon.Pun;
using UnityEngine;

/// <summary>
/// 베이스 캠프 상점 / 카탈로그의 아이템을 생성 지점에 방 소속으로 생성
/// 구매판정 및 생성은 방장만
/// </summary>
public class Shop : MonoBehaviourPun
{
    [SerializeField] private ShopCatalogSO catalog;
    [SerializeField] private Transform spawnPointRoot;
    [SerializeField] private LayerMask itemLayerMask;
    [SerializeField, Tooltip("지점이 비었는지 검사하는 상자의 절반 크기")] private Vector3 spotCheckHalfSize = new Vector3(0.3f, 0.3f, 0.3f);

    

    /// <summary>
    /// 목록 중 index번 아이템 구매 후 빈 지점에 생성
    /// 순서 : 검증 -> 빈자리 -> 골드 차감 -> 생성
    /// </summary>
    private bool TryPurchase(int index)
    {
        if (!PhotonNetwork.InRoom)
        {
            Debug.LogWarning("[Shop] 구매 불가: 방에 들어와 있지 않음", this);
            return false;
        }

        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning("[Shop] 구매 불가: 방장이 아님", this);
            return false;
        }

        if (catalog == null || spawnPointRoot == null)
        {
            Debug.LogError($"[Shop] catalog 또는 spawnPoint가 비어있음", this);
            return false;
        }

        if (!catalog.TryGetEntry(index, out ShopEntry entry))
        {
            Debug.LogWarning($"[Shop] catalog에서 잘못된 인덱스({index}번) 호출", this);
            return false;
        }

        if(entry.Item == null)
        {
            Debug.LogError($"[Shop] {index}번 아이템 비어있음", this);
            return false;
        }

        Quaternion rot = Quaternion.identity;
        if(!TryFindFreeRootPosition(entry.Item, rot, out Vector3 pos))
        {
            Debug.LogWarning($"[Shop] 빈자리 없음", this);
            return false;
        }

        // [임시] 골드 차감: 항상 성공. 게임 매니저 골드 차감 연결 시 교체 예정
        //        빈 자리 확보 뒤, 생성 앞 (자리가 없으면 골드를 쓰지 않음)

        RoomItemSpawnUtil.InstantiateRoomItem(entry.Item, pos, rot);
        Debug.Log($"[Shop] 구매 완료, {entry.Item.name} {entry.BuyPrice}");
        return true;
    }


    /// <summary>
    /// 아이템 없는 생성 지점 찾아서 루트위치 돌려줌
    /// </summary>
    private bool TryFindFreeRootPosition(Item prefab, Quaternion rot, out Vector3 rootPos)
    {
        rootPos = default;

        ItemSpawnPoint[] points = spawnPointRoot.GetComponentsInChildren<ItemSpawnPoint>();
        if(points.Length == 0)
        {
            Debug.LogError($"[Shop] 아이템 스폰 포인트 0개", this);
            return false;
        }

        foreach(ItemSpawnPoint point in points)
        {
            Vector3 floor = point.transform.position;
            Vector3 center = floor + Vector3.up * spotCheckHalfSize.y;

            // 자리없으면 다음 지점
            if (Physics.CheckBox(center, spotCheckHalfSize, Quaternion.identity, itemLayerMask, QueryTriggerInteraction.Ignore))
                continue;

            // 빈 지점: 루트 위치 계산
            if (!RoomItemSpawnUtil.TryGetRootPositionOnFloor(prefab, floor, rot, out rootPos))
            {
                Debug.LogError($"[Shop] 바닥 중심 계산 실패: {prefab.name}", this);
                return false;
            }

            return true;
        }

        // 전부 차있을때
        return false;
    }


    public void RequestPurchase(int index)
    {
        if (!PhotonNetwork.InRoom)
        {
            Debug.LogWarning("[Shop] 구매 요청 불가: 방에 들어와 있지 않음", this);
            return;
        }

        photonView.RPC(nameof(RpcRequestPurchase), RpcTarget.MasterClient, index);
        Debug.Log($"[Shop] 구매 요청 보냄, {index}번", this);
    }

    [PunRPC]
    private void RpcRequestPurchase(int index, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient)
        {
            Debug.LogWarning($"[Shop] 구매 요청 수신했으나 방장이 아님 (요청: {info.Sender.NickName}, {index}번)", this);
            return;
        }

        bool isSuccess = TryPurchase(index);
        Debug.Log($"[Shop] 구매 요청한 사람 : {info.Sender.NickName} 구매물품 {index}번, 성공여부 {isSuccess}", this);

        // 실패 시 요청한 사람에게 알림? / 골드 부족, 빈자리없음?

    }

    [ContextMenu("Test/Purchase Index 0")]
    private void TestPurchaseFirst()
    {
        RequestPurchase(0);
    }

    [ContextMenu("Test/Purchase Invalid Index")]
    private void TestPurchaseInvalid()
    {
        RequestPurchase(-1);
    }
}
