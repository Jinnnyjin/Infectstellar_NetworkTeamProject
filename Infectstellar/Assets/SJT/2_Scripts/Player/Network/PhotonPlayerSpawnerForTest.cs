using Photon.Pun;
using UnityEngine;

/// <summary>룸 입장 시 본인의 번호에 대응하는 위치에 Player를 생성합니다.</summary>
[DisallowMultipleComponent]
public sealed class PhotonPlayerSpawnerForTest : MonoBehaviourPunCallbacks
{
    private const string c_prefabPath = "PhotonPrefabs/Player";

    [SerializeField] private Transform playerSpawnPoints;
    //[] private GameObject 

    private void Awake()
    {
        SpawnPlayer(playerSpawnPoints);
    }

    /// <summary>룸에 입장하면 본인의 Player를 생성합니다.</summary>
    // public override void OnJoinedRoom()
    // {
    //     SpawnPlayer(playerSpawnPoints);
    // }

    /// <summary>전달받은 부모의 자식 포인트 중 본인의 번호에 해당하는 위치를 사용합니다.</summary>
    private void SpawnPlayer(Transform spawnPoints)
    {
        if (!PhotonNetwork.InRoom || spawnPoints == null)
        {
            return;
        }

        int spawnIndex = PhotonNetwork.LocalPlayer.ActorNumber - 1;

        if (spawnIndex < 0 || spawnIndex >= spawnPoints.childCount)
        {
#if UNITY_EDITOR
            Debug.LogError("[PhotonPlayerSpawnerForTest] ActorNumber에 대응하는 스폰 포인트가 없습니다.", this);
#endif
            return;
        }

        Transform spawnPoint = spawnPoints.GetChild(spawnIndex);

        PhotonNetwork.Instantiate(
            c_prefabPath,
            spawnPoint.position,
            spawnPoint.rotation);
    }
}