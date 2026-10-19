using Unity.AI.Navigation;
using UnityEngine;

/// <summary>베이크 차단과 플레이어 보호가 함께 사용하는 몬스터 진입 금지 범위입니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshModifierVolume))]
public sealed class MonsterRestrictedZone : MonoBehaviour
{
    private NavMeshModifierVolume volume;
    private Transform cachedTransform;

    /// <summary>같은 객체의 영역 데이터를 한 번 확보합니다.</summary>
    private void Awake()
    {
        volume = GetComponent<NavMeshModifierVolume>();
        cachedTransform = transform;
    }

    /// <summary>플레이어 루트 위치가 보호 범위 안인지 확인합니다.</summary>
    public bool Contains(Vector3 position)
    {
        if (!isActiveAndEnabled || !volume.isActiveAndEnabled)
        {
            return false;
        }

        Vector3 local = cachedTransform.InverseTransformPoint(position) - volume.center;
        Vector3 half = volume.size * 0.5f;
        return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
    }
}
