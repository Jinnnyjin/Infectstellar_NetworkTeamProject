using UnityEngine;
/// <summary>
/// 콜라이더 범위 계산 유틸(순수 계산용)
/// </summary>
public static class ColliderBoundsUtil
{
    /// <summary>
    /// 켜진 콜라이더들의 범위를 하나로 (복잡한 모양은 단순 콜라이더 여러개로 조합)
    /// 켜진 콜라이더가 하나도 없으면 false
    /// </summary>
    public static bool TryGetCombinedBounds(Collider[] colliders, out Bounds bounds)
    {
        bounds = default;
        bool found = false;

        foreach(Collider col in colliders)
        {
            // 꺼져있는 콜라이더 제외(범위가 비어있음)
            if (!col.enabled) continue;

            if(!found)
            {
                bounds = col.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(col.bounds);
            }
        }

        return found;
    }

    /// <summary>
    /// 루트(아이템 베이스)를 똑바로 세웠을때 (회전0) 루트에서 콜라이더 전체 바닥까지 거리 구함
    /// 플레이어 머리 위 소켓 맞추는 곳에 사용
    /// </summary>
    public static bool TryGetUprightBottomOffset(Transform root, Collider[] colliders, out float offset)
    {
        offset = 0f;

        // 놓인 자세와 무관하게 재기위해 잠시 세움
        Quaternion originalRotation = root.rotation;
        root.rotation = Quaternion.identity;

        // Transform을 바꾼 직후 콜라이더 범위 바로 갱신안될 수도 있기 때문에 갱신
        Physics.SyncTransforms();

        bool found = TryGetCombinedBounds(colliders, out Bounds bounds);
        if(found)
        {
            // 루트 높이 - 실제 바닥 높이 (피벗 위치가 모델마다 달라 높이의 절반X)
            offset = root.position.y - bounds.min.y;
        }

        root.rotation = originalRotation;

        return found;
    }
}
