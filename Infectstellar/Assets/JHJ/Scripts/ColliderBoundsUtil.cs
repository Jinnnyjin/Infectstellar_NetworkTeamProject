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
    /// 루트를 똑바로 세웠을 때(회전 0) 루트 기준 콜라이더 바닥면 가운데 지점 (루트 로컬 위치)
    /// 피벗이 모델마다 달라 높이뿐 아니라 가로 중심도 보정. 소켓·스폰 배치에 사용
    /// </summary>
    public static bool TryGetUprightBottomCenter(Transform root, Collider[] colliders, out Vector3 bottomCenter)
    {
        bottomCenter = Vector3.zero;

        // 놓인 자세와 무관하게 재기위해 잠시 세움
        Quaternion originalRotation = root.rotation;
        root.rotation = Quaternion.identity;

        // Transform을 바꾼 직후 콜라이더 범위 바로 갱신안될 수도 있기 때문에 갱신
        Physics.SyncTransforms();

        bool found = TryGetCombinedBounds(colliders, out Bounds bounds);
        if(found)
        {
            // 바닥면 가운데 - 루트 위치 (회전 0, 스케일 1이라 월드 차이 = 루트 로컬 위치)
            // x, z: 범위 중심 / y: 범위 최저점 (루트보다 아래면 음수)
            bottomCenter = new(bounds.center.x - root.position.x, bounds.min.y - root.position.y, bounds.center.z - root.position.z);
        }

        root.rotation = originalRotation;

        return found;
    }
}
