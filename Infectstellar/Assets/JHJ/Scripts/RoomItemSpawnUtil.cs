using UnityEngine;
using Photon.Pun;

/// <summary>
/// 아이템 프리팹을 바닥 지점에 맞춰 방 소속으로 생성
/// 스포너, 상점이 사용
/// 방장 검사는 호출쪽에서
/// </summary>
public static class RoomItemSpawnUtil
{
    private const string ITEM_RESOURCE_PATH = "Items/";

    /// <summary>
    /// 바닥 지점에 바닥 중심이 닿도록 하는 루트 위치 계산
    /// </summary>
    public static bool TryGetRootPositionOnFloor(Item prefab, Vector3 floorPoint, Quaternion rotation, out Vector3 rootPos)
    {
        rootPos = default;

        if (prefab == null)
            return false;

        Collider[] colliders = ItemPhysics.GetColliders(prefab.transform);
        if (!ColliderBoundsUtil.TryGetUprightBottomCenter(prefab.transform, colliders, out Vector3 bottomCenter))
        {
            Debug.LogWarning($"[ItemSpawner] 바닥 중심 계산 실패 / 프리팹 이름: {prefab.name}");
            return false;
        }

        rootPos = floorPoint - rotation * bottomCenter;
        return true;
    }

    public static GameObject InstantiateRoomItem(Item prefab, Vector3 rootPos, Quaternion rotation)
    {
        return PhotonNetwork.InstantiateRoomObject(ITEM_RESOURCE_PATH + prefab.name, rootPos, rotation);
    }
}
