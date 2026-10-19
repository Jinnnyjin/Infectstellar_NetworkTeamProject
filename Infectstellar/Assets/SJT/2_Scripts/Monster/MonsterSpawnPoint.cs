using Photon.Pun;
using UnityEngine;

/// <summary>생성할 몬스터와 출현 위치·방향을 지정합니다.</summary>
[DisallowMultipleComponent]
public sealed class MonsterSpawnPoint : MonoBehaviour
{
    private const string c_prefabFolder = "PhotonPrefabs/Monsters/";
    [SerializeField, Tooltip("Resources/PhotonPrefabs/Monsters 바로 아래의 네트워크 몬스터 프리팹")] private GameObject monsterPrefab;

    /// <summary>Resources 경로와 필수 네트워크 구성을 검사합니다.</summary>
    internal bool TryGetPrefabPath(out string prefabPath)
    {
        prefabPath = monsterPrefab != null ? c_prefabFolder + monsterPrefab.name : null;
        if (monsterPrefab == null || !monsterPrefab.activeSelf || Resources.Load<GameObject>(prefabPath) != monsterPrefab ||
            !monsterPrefab.TryGetComponent(out PhotonView view) || !view.enabled || view.Synchronization == ViewSynchronization.Off ||
            !monsterPrefab.TryGetComponent(out MonsterController controller) ||
            !monsterPrefab.TryGetComponent(out MonsterNetworkSync networkSync) || !controller.enabled || !networkSync.enabled ||
            view.ObservedComponents == null || !view.ObservedComponents.Contains(networkSync))
        {
#if UNITY_EDITOR
            Debug.LogError("[MonsterSpawnPoint] Resources/Monsters의 활성 프리팹과 PhotonView·MonsterController·MonsterNetworkSync 연결을 확인하세요.", this);
#endif
            return false;
        }
        return true;
    }

#if UNITY_EDITOR
    /// <summary>출현 위치와 수평 정면 방향을 표시합니다.</summary>
    private void OnDrawGizmos()
    {
        Transform point = transform;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(point.position, 0.3f);
        Gizmos.DrawLine(point.position, point.position + Quaternion.Euler(0f, point.eulerAngles.y, 0f) * Vector3.forward);
    }
#endif
}
