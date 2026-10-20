using System;
using System.Diagnostics;
using ENZEUN.Runtime;
using Photon.Pun;
using Sirenix.OdinInspector;

public class PlayerDataObserver : MonoBehaviourPun
{

    public float hp { get; private set; } // 아직 안쓰는 필드
    public bool isDead => hp <= 0;
    public bool isInfected { get; private set; }


    public Action OnPlayerDie;
    public Action OnPlayerInfected;




    [Button]
    public void InitializePlayerDataObserver()
    {
        GameManager.Instance.RegisterPlayerData(photonView.OwnerActorNr, this);
    }

    /// <summary>
    /// 플레이어의 체력이 바뀔 때마다 호출해주세요 // 아직 안쓰고 있으니 상의 후 호출 추가해주세요
    /// </summary>
    /// <param name="hp"></param>
    public void SetPlayerCurrentHp(int hp)
    {
        this.hp = hp;
    }

    /// <summary>
    /// 게임 매니저에서 랜덤으로 플레이어를 골라서 감염시킴.
    /// 당첨된 플레이어에게서 실행되는 메서드임. 이 뒤에 추가 로직 붙이면 됩니다.
    /// </summary>
    /// <param name="infected"></param>
    public void SetPlayerInfectedState(bool infected = true)
    {
        isInfected = infected;
        OnPlayerInfected?.Invoke();
    }

    /// <summary>
    /// 플레이어가 죽을 때 호출해주세요.
    /// [GM 에서 이벤트 콜백을 받기위한 메서드. GM 에서 OnPlayerDie 를 구독하고 있음.]
    /// </summary>
    public void PlayerDied()
    {
        OnPlayerDie?.Invoke();
    }

}
