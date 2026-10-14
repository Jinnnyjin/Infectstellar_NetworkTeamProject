// [임시] PlayerScene_Item 멀티플레이 테스트용: 네트워크로 생성된 플레이어의 내 것/남의 것 처리
// TempPlayerNetworkSpawner의 프리팹 풀이 생성 시 자동으로 붙임 (프리팹 수정 X), 통합 시 삭제 예정
using Photon.Pun;
using UnityEngine;

/// <summary>
/// [임시] 내 플레이어만 조작(PlayerController)을 켜고, 남의 플레이어는 PhotonTransformView로만 움직임
/// 아이템 적중 확인용 ITempDamageable (판정은 던진 사람 컴퓨터에서만 일어나므로 로그도 그 창에만 찍힘)
/// </summary>
[RequireComponent(typeof(PhotonView))]
public class TempNetworkPlayer : MonoBehaviourPun, ITempDamageable // [임시]
{
    private void Awake()
    {
        int actorNumber = photonView.OwnerActorNr;
        gameObject.name = $"Player_Actor{actorNumber}{(photonView.IsMine ? " (나)" : "")}";
    }

    private void Start()
    {
        // [임시] 생성 시 꺼둔 조작을 내 플레이어만 다시 켬 (OnEnable에서 입력·커서 잠금 시작)
        if (!photonView.IsMine)
            return;

        PlayerController controller = GetComponent<PlayerController>();
        if (controller == null)
        {
            Debug.LogError($"[TempNetworkPlayer] {gameObject.name}: PlayerController가 없음", this);
            return;
        }

        controller.enabled = true;
    }

    public void TakeDamage(TempDamageInfo info) // [임시]
    {
        Debug.Log($"[TempNetworkPlayer] 피격 판정: 주인 Actor {photonView.OwnerActorNr}, 공격자 {info.AttackerActorNumber}, 데미지 {info.Damage} / 판정한 창 Actor {PhotonNetwork.LocalPlayer.ActorNumber}", this);
    }

    private void OnGUI()
    {
        if (!photonView.IsMine)
            return;

        GUI.Label(new Rect(10f, Screen.height - 30f, 300f, 20f), $"나: Actor {photonView.OwnerActorNr}");
    }
}
