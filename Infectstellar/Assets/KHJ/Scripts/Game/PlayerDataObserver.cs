using System;
using Cysharp.Threading.Tasks;
using ENZEUN.Runtime;
using Photon.Pun;
using Photon.Realtime;
using Sirenix.OdinInspector;
using UnityEngine;

public class PlayerDataObserver : MonoBehaviourPun
{
    public Player ownerPlayer;
    public float hp { get; private set; } = 100f;
    public bool isDead => hp <= 0;
    public bool isInfected { get; private set; }

    /// <summary>
    /// 플레이어가 죽었을 때 발동 / bool : 감염자인지
    /// </summary>
    public Action OnPlayerDie;
    public Action OnPlayerInfected;
    public Action OnPlayerHpChanged;


    public async void Start()
    {
#if UNITY_EDITOR
        Debug.Log("[PlayerDataObserver] : 플레이어가 생성되었습니다. 초기화를 시작합니다.");
#endif
        while (photonView != null && photonView.Owner != null)
        {
#if UNITY_EDITOR
            Debug.Log("[PlayerDataObserver] : Photon 초기화 기다리는 중...");
#endif
            await UniTask.Yield();
        }

        InitializePlayerDataObserver();

#if UNITY_EDITOR
        Debug.Log("[PlayerDataObserver] : 초기화가 완료되었습니다. 2 초 뒤 커스텀프로퍼티를 업데이트 합니다.");
#endif

        await UniTask.Delay(TimeSpan.FromSeconds(2));

        GameManager.Instance.SetGameReadyState(true);
    }

    [Button]
    public void InitializePlayerDataObserver()
    {
        ownerPlayer = photonView.Owner;

        GameManager.Instance.RegisterPlayerData(ownerPlayer.ActorNumber, this);

    }

    /// <summary>
    /// 플레이어의 체력이 바뀔 때마다 호출해주세요 // 아직 안쓰고 있으니 상의 후 호출 추가해주세요
    /// </summary>
    /// <param name="hp"></param>
    public void SetPlayerCurrentHp(float hp)
    {
#if UNITY_EDITOR
        Debug.Log($"[PlayerDataObserver] : 플레이어({ownerPlayer.NickName})의 체력이 변경되었습니다. 현재 체력 : [{hp}]");
#endif
        this.hp = hp;

        OnPlayerHpChanged?.Invoke();
    }

    /// <summary>
    /// 게임 매니저에서 랜덤으로 플레이어를 골라서 감염시킴.
    /// 당첨된 플레이어에게서 실행되는 메서드임. 이 뒤에 추가 로직 붙이면 됩니다.
    /// </summary>
    /// <param name="infected"></param>
    public void SetPlayerInfectedState(bool infected = true)
    {
        isInfected = infected;

        if (ownerPlayer.IsLocal)
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
