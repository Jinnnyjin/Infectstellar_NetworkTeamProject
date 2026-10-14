using UnityEngine;

/// <summary>
/// 아이템 스폰 위치(아이템이 놓일 바닥 지점, 바닥에 붙여서 배치)
/// 스폰 포인트 부모오브젝트 아래에(끄면 스폰 대상에서 제외)
/// </summary>
public class ItemSpawnPoint : MonoBehaviour
{
    private const float SIZE = 1f;
    private const float THICKNESS = 0.1f;
    private const float LINE_LENGTH = 1f;
    private static readonly Color gizmosColor = Color.blue;

    private void OnDrawGizmos()
    {
        Gizmos.color = gizmosColor;

        Gizmos.DrawWireCube(transform.position, new Vector3(SIZE, THICKNESS, SIZE));
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * LINE_LENGTH);
    }
}
