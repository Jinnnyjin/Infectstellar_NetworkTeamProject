using UnityEngine;
using MoreMountains.Tools;
using Sirenix.OdinInspector;
using Cysharp.Threading.Tasks;
using Photon.Pun;
using System.Collections.Generic;
using System;
using Photon.Realtime;


namespace ENZEUN.Runtime
{
    public class GameManager : MonoBehaviourPun
    {

        //========== 게임 진행 중 필요한 필드 들 =============================
        [ShowInInspector]
        public int currentGold { get; private set; } // 총 골드
        [ShowInInspector]
        public bool isInfected { get; private set; } // 감염자 여부
        [ShowInInspector]
        public Dictionary<int, PlayerDataObserver> playerDataDict { get; private set; } = new();
        [ShowInInspector]
        public Player infectedPlayer { get; private set; } // 감염된 플레이어 레퍼런스


        //========== 런타임 이벤트 들 =============================

        public Action OnPlayerInfected;
        public Action OnInfectedStateChanged;
        public Action OnGoldChanged;



        //===================================================================

        public static GameManager Instance;
        private GameStateManager gameStateManager;

        /// <summary>
        /// Instance 는 하나만 존재하도록 (싱글턴)
        /// </summary>
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                //DontDestroyOnLoad(gameObject); // 게임 맵을 옮기면 없어지도록 설정.
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            gameStateFSM = new MMStateMachine<GameStateFSM>(this.gameObject, true);
        }


        public void OnEnable()
        {

        }

        public void OnDisable()
        {
            foreach (var data in playerDataDict.Values)
            {
                data.OnPlayerDie -= CheckAllPlayerDied;
            }
        }

        private void Start()
        {
            // 게임 StateManager 를 찾아서 초기화.
            gameStateManager = FindFirstObjectByType<GameStateManager>();

            if (gameStateManager == null)
            {
                Debug.Log("GameStateManager 가 존재하지 않습니다. 초기화에 실패했습니다.");
            }
            else
            {
                gameStateManager.InitializeGameStateManager();
            }

            if (PhotonNetwork.IsConnected)
            {
                if (PhotonNetwork.IsMasterClient)
                {
                    ChangeGameState(GameStateFSM.ReadyForStart);
                }
                else
                {
                    if (PhotonNetwork.OfflineMode)
                    {
                        ChangeGameState(GameStateFSM.ReadyForStart);
                    }
                }
            }
        }


        //============================================================================

        public void RegisterPlayerData(int ActorNr, PlayerDataObserver playerDataObserver)
        {
            if (playerDataDict.ContainsKey(ActorNr))
            {
                return;
            }
            else
            {
                playerDataDict.Add(ActorNr, playerDataObserver);
                playerDataObserver.OnPlayerDie -= CheckAllPlayerDied;
                playerDataObserver.OnPlayerDie += CheckAllPlayerDied;
            }
        }


        public void CheckAllPlayerDied()
        {
            if (!PhotonNetwork.IsMasterClient)
                return;

            bool allDead = true;

            bool hasInfectedPlayer = false;

            foreach (var data in playerDataDict.Values)
            {
                // 한 명이라도 안죽었으면 false;
                if (!data.isDead)
                    allDead = false;
                // 감염된 플레이어가 살아있으면, hasInfectedPlayer 참
                if (data.isInfected && !data.isDead)
                {
                    hasInfectedPlayer = true;
                }
            }

            // 현재 상태와 동기화 시킴
            if (isInfected != hasInfectedPlayer)
            {
                photonView.RPC(nameof(RPC_InfectedStateChanged), RpcTarget.All, hasInfectedPlayer);
            }

            if (allDead)
            {
                ChangeGameState(GameStateFSM.OnFailed);
            }
        }


        //============================================================================


        #region FSM Settings

        public enum GameStateFSM
        {
            None,               // 초기상태
            ReadyForStart,      // 게임 시작 준비 중
            OnGameStart,        // 게임 시작 타이밍
            GameStarted,        // 게임이 시작된 후
            Escaped,            // 탈출 성공
            OnFailed,           // 모두가 죽었을 때 (실패)
            ShowResult,         // 결과 보여주기
            RegisterRanking,    // 랭킹 등록

        }

        private MMStateMachine<GameStateFSM> gameStateFSM;

        [ShowInInspector, ReadOnly]
        public GameStateFSM currentGameState
        => gameStateFSM?.CurrentState ?? GameStateFSM.None;


        #endregion




        #region GameState Logic


        [Button]
        public void ChangeGameState(GameStateFSM gameState)
        {
            if (!PhotonNetwork.IsMasterClient)
                return;

            photonView.RPC(nameof(RPC_ChangeGameState), RpcTarget.All, gameState);
        }

        [PunRPC]
        public void RPC_ChangeGameState(GameStateFSM gameState)
        {
            gameStateFSM.ChangeState(gameState);
        }

        #endregion



        #region Gold Logic

        // 1. 일반 플레이어가 마스터에게 골드 지급을 요청하는 진입점
        [Button]
        public void RequestAddGold(int amount)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                // 마스터라면 직접 처리
                ExecuteAddGold(amount);
            }
            else
            {
                // 클라이언트라면 마스터에게 RPC 요청
                photonView.RPC(nameof(RPC_RequestAddGold), RpcTarget.MasterClient, amount);
            }
        }

        // 2. 마스터만 받는 RPC (요청 수신)
        [PunRPC]
        private void RPC_RequestAddGold(int amount)
        {
            // 보안을 위해 마스터 클라이언트에서 한 번 더 검증
            if (!PhotonNetwork.IsMasterClient) return;

            // 실제 골드 계산 및 전체 동기화 실행
            ExecuteAddGold(amount);
        }

        // 3. 실제 계산 및 모든 클라이언트(혹은 자신)에게 데이터 동기화
        private void ExecuteAddGold(int amount)
        {
            currentGold += amount;

            // 모든 클라이언트에게 최종 골드 갱신하라고 전파
            photonView.RPC(nameof(RPC_SyncGold), RpcTarget.All, currentGold);
        }


        // 4. 모든 클라이언트의 골드 값을 동기화하는 RPC
        [PunRPC]
        public void RPC_SyncGold(int syncedGold)
        {
            this.currentGold = syncedGold;
            OnGoldChanged?.Invoke();
        }

        #endregion



        #region Infect Logic

        // 분 단위
        private int minInfectionTime = 5;
        private int InfectionInterval = 1;
        private float IntectionChance = 20f;


        // 0. 각자 컴퓨터에서 감염 조건(시간) 초기화 후 실행.
        private async UniTaskVoid InfectTimer()
        {
            var token = this.GetCancellationTokenOnDestroy();

            await UniTask.Delay(TimeSpan.FromMinutes(minInfectionTime));

            TryInfectPlayer();

            while (!token.IsCancellationRequested || !isInfected)
            {
                try
                {
                    await UniTask.Delay(TimeSpan.FromMinutes(InfectionInterval));
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                TryInfectPlayer();
            }
        }


        // 1. Master 에서 감염 발생
        [Button]
        private void TryInfectPlayer()
        {
            if (!PhotonNetwork.IsMasterClient)
            {
#if UNITY_EDITOR
                Debug.Log("마스터 클라이언트가 아닙니다.");
#endif
                return;
            }

            if (PhotonNetwork.CountOfPlayersInRooms <= 1)
            {
#if UNITY_EDITOR
                Debug.Log("감염시키기에는 플레이어가 너무 적습니다.");
#endif
                return;
            }

            if (infectedPlayer != null)
            {
#if UNITY_EDITOR
                Debug.Log("이미 감염된 플레이어가 존재합니다.");
#endif
                return;
            }

            float randF = UnityEngine.Random.Range(0f, 100f);

            // 1. 감염 확률 실패 시 즉시 탈출
            if (randF > IntectionChance)
            {
#if UNITY_EDITOR
                Debug.Log("감염확률을 뚫지 못했습니다.");
#endif
                return;
            }

            // 2. 살아있는 플레이어들의 ActorNumber만 추리기
            List<int> aliveActorNumbers = new List<int>();

            foreach (var pair in playerDataDict)
            {
                // pair.Value가 PlayerDataObserver라고 가정
                if (pair.Value != null && pair.Value.isDead == false)
                {
                    aliveActorNumbers.Add(pair.Key);
                }
            }

            // 3. 살아있는 유저가 아예 없으면 감염 실패
            if (aliveActorNumbers.Count <= 0)
            {
#if UNITY_EDITOR
                Debug.Log("감염시키기엔 살아있는 플레이어가 없습니다.");
#endif
                return;
            }

            // 4. 살아있는 유저들 중 무작위로 1명 선택
            int randomIndex = UnityEngine.Random.Range(0, aliveActorNumbers.Count);
            int selectedActorNr = aliveActorNumbers[randomIndex];

#if UNITY_EDITOR
            Debug.Log($"감염 대상 선정 완료 : ActorNr - [{selectedActorNr}]");
#endif

            // 5. RPC 전파
            photonView.RPC(nameof(RPC_InfectPlayer), RpcTarget.All, selectedActorNr);

#if UNITY_EDITOR
            Debug.Log("대상이 감염되었습니다.");
#endif

            return;
        }


        // 2. RPC
        [PunRPC]
        private void RPC_InfectPlayer(int actorNr)
        {
            isInfected = true;

            if (playerDataDict.TryGetValue(actorNr, out PlayerDataObserver dataObserver))
            {
                infectedPlayer = dataObserver.ownerPlayer;
                dataObserver.SetPlayerInfectedState(true);
                OnPlayerInfected?.Invoke();
            }
        }

        [PunRPC]
        private void RPC_InfectedStateChanged(bool hasInfectedPlayer)
        {
            isInfected = hasInfectedPlayer;
            OnInfectedStateChanged?.Invoke();
        }


        #endregion

    }

}