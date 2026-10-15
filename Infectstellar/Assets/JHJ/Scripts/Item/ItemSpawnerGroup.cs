using UnityEngine;

/// <summary>
/// 하위 스포너 전체 실행 창구. 게임 매니저는 이 그룹의 SpawnAll()만 호출
/// 스포너는 이 오브젝트 아래에 둠 (꺼 두면 제외)
/// 방장 검사는 각 스포너의 Spawn에서 처리
/// </summary>
public class ItemSpawnerGroup : MonoBehaviour
{
    [ContextMenu("SpawnItem_All")]
    public void SpawnAll()
    {
        ItemSpawner[] spawners = GetComponentsInChildren<ItemSpawner>();

        if (spawners.Length == 0)
        {
            Debug.LogWarning("[ItemSpawnerGroup] 하위에 활성화된 ItemSpawner가 없음", this);
            return;
        }

        foreach (ItemSpawner spawner in spawners)
        {
            spawner.Spawn();
        }    
    }
}
