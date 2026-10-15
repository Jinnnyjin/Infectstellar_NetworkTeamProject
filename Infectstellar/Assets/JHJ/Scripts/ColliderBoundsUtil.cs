using UnityEngine;
/// <summary>
/// 콜라이더 범위 계산 유틸(순수 계산용)
/// </summary>
public static class ColliderBoundsUtil
{
    /// <summary>
    /// 루트를 똑바로 세웠을 때(회전 0) 루트 기준 콜라이더 바닥면 가운데 지점 (루트 로컬 위치)
    /// 피벗이 모델마다 달라 높이뿐 아니라 가로 중심도 보정. 소켓·스폰 배치에 사용
    /// </summary>
    public static bool TryGetUprightBottomCenter(Transform root, Collider[] colliders, out Vector3 bottomCenter)
    {
        bottomCenter = Vector3.zero;

        // 무한대로 초기화
        Vector3 min = Vector3.positiveInfinity;
        Vector3 max = Vector3.negativeInfinity;

        bool found = false;

        foreach (Collider col in colliders)
        {
            if (!col.enabled) continue;

            if (!TryGetLocalBox(col, out Bounds localBounds))
                return false;

            Transform colliderTransform = col.transform;
            Vector3 center = localBounds.center;
            Vector3 extents = localBounds.extents;      // size의 절반

            // 로컬 상자 꼭짓점 8개 (축마다 -절반 / +절반)
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        Vector3 localCorner = center + new Vector3(sx * extents.x, sy * extents.y, sz * extents.z);

                        // 콜라이더 기준 → 월드 → 루트 기준
                        // 루트 회전이 되돌려져 놓인 자세와 무관하게 "똑바로 선 기준"이 됨
                        Vector3 worldCorner = colliderTransform.TransformPoint(localCorner);
                        Vector3 rootCorner = root.InverseTransformPoint(worldCorner);

                        min = Vector3.Min(min, rootCorner);
                        max = Vector3.Max(max, rootCorner);
                    }
                }
            }

            found = true;

        }

        if (!found)
            return false;


        bottomCenter = new Vector3((min.x + max.x) * 0.5f, min.y, (min.z + max.z) * 0.5f);
        return true;
    }

    /// <summary>
    /// 콜라이더 자기 좌표계 기준 상자.
    /// 현재는 mesh만 지원, 지원하지 않는 종류는 false
    /// </summary>
    private static bool TryGetLocalBox(Collider col, out Bounds localBounds )
    {
        localBounds = default;

        if(col is MeshCollider meshCollider && meshCollider.sharedMesh != null)
        {
            localBounds = meshCollider.sharedMesh.bounds;
            return true;
        }

        // 메쉬만
        // 추후 Box, Sphere, Capsule 등 필요해지면 추가
        return false;
    }
}
