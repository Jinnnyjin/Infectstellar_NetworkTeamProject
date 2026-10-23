using UnityEngine;
using MoreMountains.Tools;
using Sirenix.OdinInspector;
using Cysharp.Threading.Tasks;
using Photon.Pun;
using System.Collections.Generic;
using System;
using Photon.Realtime;
using ExitGames.Client.Photon;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;


namespace ENZEUN.Runtime
{
    public class GameManager : MonoBehaviourPunCallbacks
    {

        //========== 게임 진행 중 필요한 필드 들 =============================
        [ShowInInspector, ReadOnly]
        public int currentGold { get; private set; } // 총 골드
        [ShowInInspector, ReadOnly]
        public int resultGold { get; private set; } // 총 골드
        [ShowInInspector, ReadOnly]
        public bool isInfected { get; private set; } // 감염자 여부
        [ShowInInspector, ReadOnly]
        public Dictionary<int, PlayerDataObserver> playerDataDict { get; private set; } = new();
        [ShowInInspector, ReadOnly]
        public Player infectedPlayer { get; private set; } // 감염된 플레이어 레퍼런스
        [ShowInInspector, ReadOnly]
        public bool isGameStarted { get; private set; } = false; // 게임이 정상적으로 시작되었는지
        [ShowInInspector, ReadOnly]
        public GameEndType gameEndType = GameEndType.Escaped;


        //========== 게임 종료 Enum =============================

        public enum GameEndType
        {
            Failed,     // 모두 사망
            Escaped,    // 탈출 성공
            InfectedWin // 감염자만 생존
        }


        //========== 런타임 이벤트 들 =============================

        public Action OnPlayerInfected;
        public Action OnInfectedStateChanged;
        public Action OnGoldChanged;
        public Action OnFailed;
        public Action OnEscaped;



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


        public override void OnEnable()
        {
            base.OnEnable();
        }

        public override void OnDisable()
        {
            base.OnDisable();

            foreach (var data in playerDataDict.Values)
            {
                data.OnPlayerDie -= CheckAllPlayerDied;
                data.OnPlayerRevive -= PlayerRevived;
            }
        }

        private void Start()
        {
            // 게임 StateManager 를 찾아서 초기화.
            gameStateManager = FindFirstObjectByType<GameStateManager>();

            if (gameStateManager == null)
            {
                Debug.LogError("[GameManager] : GameStateManager 가 존재하지 않습니다. 초기화에 실패했습니다.");
            }


            if (PhotonNetwork.IsConnected)
            {
                if (PhotonNetwork.IsMasterClient)
                {
                    WaitAndTriggerReadyGame().Forget();
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

        private async UniTask WaitAndTriggerReadyGame()
        {
#if UNITY_EDITOR
            Debug.Log("[GameManager] : 게임을 초기화 합니다. 2 초 뒤 [Ready For Start] 단계로 넘어갑니다");
#endif

            await UniTask.Delay(TimeSpan.FromSeconds(2));

            ChangeGameState(GameStateFSM.ReadyForStart);
        }

        //=================== Ready For Start ===============================================

        #region Ready For Start


        public void ReadyForGame()
        {
#if UNITY_EDITOR
            Debug.Log("[GameManager] : [Ready For Start] 단계 입니다. 게임 매니저 초기화를 시작합니다");
#endif
            gameStateManager.InitializeGameStateManager();

            FindFirstObjectByType<PhotonPlayerSpawnerForTest>().SpawnPlayer();

            WaitForStart().Forget();
        }

        public async UniTask WaitForStart()
        {
#if UNITY_EDITOR
            Debug.Log("[GameManager] : 게임 매니저 초기화가 완료되었습니다. PlayerDataObserver 초기화를 기다립니다.");
#endif
            //await UniTask.Delay(TimeSpan.FromSeconds(3));

            //SetGameReadyState(true);

            CheckAllPlayerGameReady();
        }

        public void SetGameReadyState(bool isReady)
        {
            Hashtable props = new Hashtable() { { PhotonLinkManager.gameReady_Customproperty, isReady } };

            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        }


        private void CheckAllPlayerGameReady()
        {
            if (!PhotonNetwork.IsMasterClient)
                return;

            if (isGameStarted)
                return;

            bool allReady = true;

            Player[] players = PhotonNetwork.PlayerList;

            foreach (var p in players)
            {
                if (!allReady)
                {
                    break;
                }

                if (p.CustomProperties.TryGetValue(PhotonLinkManager.gameReady_Customproperty, out object obj))
                {
                    if (!(bool)obj)
                    {
                        allReady = false;
                    }

                }
                else
                {
                    allReady = false;
                }
            }

            if (!allReady)
            {
#if UNITY_EDITOR
                Debug.Log("[GameManager] : 아직 모든 플레이어가 준비되지 않았습니다.");
#endif
                return;
            }

#if UNITY_EDITOR
            Debug.Log("[GameManager] : 플레이어가 모두 준비되었습니다.");
#endif
            WaitAndTriggerGameStart().Forget();
        }

        private async UniTask WaitAndTriggerGameStart()
        {
#if UNITY_EDITOR
            Debug.Log("[GameManager] : 1초 뒤 [On Game Start] 단계로 넘어갑니다.");
#endif
            await UniTask.Delay(TimeSpan.FromSeconds(1));

            ChangeGameState(GameStateFSM.OnGameStart);
        }


        #endregion

        //=================== On Game Start ===============================================
        #region On Game Start

        public void TriggerGameStart()
        {
            if (isGameStarted)
                return;

#if UNITY_EDITOR
            Debug.Log("[GameManager] : [On Game Start] 단계 입니다. 초기화를 시작합니다.");
#endif

            //============== 초기화 중 =============================================================

            HUDController hUDController = FindFirstObjectByType<HUDController>();

            if (hUDController == null)
            {
#if UNITY_EDITOR
                Debug.LogError("[GameManager] : HUD 를 초기화할 수 없습니다. [HUD Is Null]");
#endif
                return;
            }

            hUDController.InitialSetting();

#if UNITY_EDITOR
            Debug.Log("[GameManager] : HUD 초기화가 완료되었습니다.");
#endif


            hUDController.ChangeHUD_LoadingBar(20f); // 로딩바 업데이트

            //============== 초기화 중 =============================================================
            BaseCampInfoView baseCampInfo = FindFirstObjectByType<BaseCampInfoView>();


            if (baseCampInfo == null)
            {
#if UNITY_EDITOR
                Debug.LogError("[GameManager] : BaseCampInfo 를 초기화할 수 없습니다. [BaseCampInfo Is Null]");
#endif
                return;
            }

            baseCampInfo.InitializeBaseCampInfo();

#if UNITY_EDITOR
            Debug.Log("[GameManager] : BaseCampInfo 초기화가 완료되었습니다.");
#endif


            hUDController.ChangeHUD_LoadingBar(40f); // 로딩바 업데이트


            //============== 초기화 중 =============================================================

            ItemSpawnerGroup itemSpawner = FindFirstObjectByType<ItemSpawnerGroup>();

            if (itemSpawner == null)
            {
#if UNITY_EDITOR
                Debug.LogError("[GameManager] : ItemSpawner 를 초기화할 수 없습니다. [ItemSpawner Is Null]");
#endif
                return;
            }

            itemSpawner.SpawnAll();

#if UNITY_EDITOR
            Debug.Log("[GameManager] : Item 스폰이 완료되었습니다.");
#endif

            hUDController.ChangeHUD_LoadingBar(60f); // 로딩바 업데이트


            //============== 초기화 중 =============================================================

            MonsterSpawner monster = FindFirstObjectByType<MonsterSpawner>();

            if (monster == null)
            {
#if UNITY_EDITOR
                Debug.LogError("[GameManager] : MonsterSpawner 를 초기화할 수 없습니다. [MonsterSpawner Is Null]");
#endif
                return;
            }

            monster.SpawnAll();

#if UNITY_EDITOR
            Debug.Log("[GameManager] : Monster 스폰이 완료되었습니다.");
#endif

            hUDController.ChangeHUD_LoadingBar(100f); // 로딩바 업데이트


            //============== 초기화 중 =============================================================




            isGameStarted = true;

            InfectTimer().Forget();

            hUDController.FadeInDarkImage(false); // 로딩화면 지우기

            ChangeGameState(GameStateFSM.GameStarted);
        }

        #endregion

        //=================== Game Started ===============================================

        #region Game Started




        #endregion


        //=================== Escaped ===============================================


        #region Escaped




        #endregion


        //=================== OnFailed ===============================================

        #region OnFailed




        #endregion


        //=================== ShowResult ===============================================


        #region ShowResult




        #endregion


        //=================== Register Ranking ===============================================

        #region Register Ranking
        [Button, BoxGroup("디버깅")]
        public void RegisterRanking()
        {
            if (!PhotonNetwork.IsMasterClient)
            {
#if UNITY_EDITOR
                Debug.Log("[GameManager] : 마스터만 랭킹을 등록할 수 있습니다.");
#endif
                return;
            }

            if (currentGold <= 1000)
            {
#if UNITY_EDITOR
                Debug.Log("[GameManager] : 점수가 너무 낮아 점수 등록을 스킵합니다.");
#endif
                return;
            }

            if (FirebaseLinkManager.Auth == null)
            {
#if UNITY_EDITOR
                Debug.Log("[GameManager] : Firebase 에 연결이 되어있지않아 랭킹등록이 불가능합니다.");
#endif
                return;
            }

            RankingData data = MakeRankingData();

            if (data == null)
            {
#if UNITY_EDITOR
                Debug.Log("[GameManager] : 랭킹등록이 불가능합니다.");
#endif
                return;
            }

            FirebaseLinkManager.Instance.SaveRankingScoreAsync(data).Forget();

        }


        private RankingData MakeRankingData()
        {
            if (!PhotonNetwork.InRoom)
            {
#if UNITY_EDITOR
                Debug.Log("[GameManager] : 랭킹 데이터 생성이 불가능합니다. - 포톤네트워크 InRoom 이 False 입니다.");
#endif
                return null;
            }

            RankingData data = new RankingData(MakeNickNameData(), resultGold);

            return data;
        }

        private string MakeNickNameData()
        {
            string nickNames = string.Empty;

            switch (gameEndType)
            {
                case GameEndType.Failed:
#if UNITY_EDITOR
                    Debug.Log("[GameManager] : 게임에서 패배하였기 때문에 랭킹 등록이 불가능합니다.");
#endif
                    break;

                case GameEndType.Escaped:
                    foreach (var playerData in playerDataDict.Values)
                    {
                        // 감염된 플레이어가 죽었으면, 이름을 등록 안함
                        if (playerData.isInfected && playerData.isDead)
                        {
                            continue;
                        }

                        nickNames = nickNames + playerData.ownerPlayer.NickName + ",";
                    }
                    break;

                case GameEndType.InfectedWin:
                    if (infectedPlayer == null)
                    {
#if UNITY_EDITOR
                        Debug.Log("[GameManager] : 감염자가 이겼는데 감염된 플레이어가 없습니다.");
#endif
                        break;
                    }
                    nickNames = infectedPlayer.NickName;
                    break;
            }
            // 마지막에 붙은 쉼표(,) 제거하기 (글자가 비어있지 않은 경우에만)

            if (!string.IsNullOrEmpty(nickNames))
            {
                nickNames = nickNames.TrimEnd(',');
            }

            return nickNames;
        }

        public GameResultData GetResultStringAndChangeGold()
        {
            int originGold = currentGold;
            int killInfectedBonusGold = 0;
            int infectedWinBonus = 0;
            int DeadPenaltyGold = 0;
            int LastGold = currentGold;

            switch (gameEndType)
            {
                case GameEndType.Failed:
#if UNITY_EDITOR
                    Debug.Log("[GameManager] : 게임에서 패배하였기에 골드정산을 스킵합니다.");
#endif

                    break;

                // 생존자 팀 승리 경우
                case GameEndType.Escaped:

                    int deadCount = 0;
                    foreach (var playerData in playerDataDict.Values)
                    {
                        // 감염된 플레이어가 죽었으면, 보너스 30%
                        if (playerData.isInfected && playerData.isDead)
                        {
                            killInfectedBonusGold = (int)Mathf.Round(originGold * 0.4f);
                            continue;
                        }
                        // 플레이어가 사망하면 -20%
                        else if (!playerData.isInfected && playerData.isDead)
                        {
                            deadCount++;
                            continue;
                        }
                    }

                    DeadPenaltyGold = -(((int)Mathf.Round(originGold * 0.2f)) * deadCount);

                    LastGold = originGold + killInfectedBonusGold + DeadPenaltyGold;

                    break;

                // 감염자 혼자 살아남은 경우
                case GameEndType.InfectedWin:
                    // 감염된 플레이어가 이겼으면, 보너스 50%
                    infectedWinBonus = (int)Mathf.Round(originGold * 0.5f); DeadPenaltyGold = 0;
                    LastGold = originGold + infectedWinBonus;

                    break;
            }

            resultGold = LastGold;

            GameResultData gameResult = new()
            {
                earnedGold = originGold.ToString(),
                killInfectedBonus = killInfectedBonusGold.ToString(),
                infectedWinBonus = infectedWinBonus.ToString(),
                DeadPenalty = DeadPenaltyGold.ToString(),
                resultGold = LastGold.ToString()
            };

            return gameResult;
        }


        #endregion

        //============================================================================
        //============================================================================

        #region GamePlayMethods

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
                playerDataObserver.OnPlayerRevive = PlayerRevived;

                playerDataObserver.OnPlayerDie += CheckAllPlayerDied;
                playerDataObserver.OnPlayerRevive += PlayerRevived;
            }
        }

        public void PlayerRevived()
        {
            // 부활했을때, 감염 체크 하고 동기화하도록 수정
            CheckAllPlayerDied();
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
                gameEndType = GameEndType.Failed;

                OnFailed?.Invoke();

                ChangeGameState(GameStateFSM.OnFailed);
            }
        }

        public void CheckGameResult()
        {
            int countOfAlivedPlayers = 0;
            bool infectedPlayerAlive = false;

            foreach (var data in playerDataDict.Values)
            {
                if (!data.isDead)
                {
                    countOfAlivedPlayers++;
                    if (isInfected)
                    {
                        infectedPlayerAlive = true;
                    }
                }
            }


            if (countOfAlivedPlayers == 1)
            {
                if (infectedPlayerAlive)
                {
                    gameEndType = GameEndType.InfectedWin;
                }
            }
            else
            {
                gameEndType = GameEndType.Escaped;
            }

            OnEscaped?.Invoke();

            ChangeGameState(GameStateFSM.Escaped);
        }


        public async UniTask GoToTitleAfter3Sec()
        {
            await UniTask.Delay(TimeSpan.FromSeconds(3));

            // #if UNITY_EDITOR
            //             UnityEditor.EditorApplication.isPlaying = false;
            // #endif

            //Application.Quit();

            //SceneManager.LoadScene("TitleScene");

            SceneManager.LoadScene("LeaveRoom");
        }



        #endregion


        //===================== Photon Callbacks ==============================================

        #region Photon Callbacks

        public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
        {
            base.OnPlayerPropertiesUpdate(targetPlayer, changedProps);

            // "GameReady" 키가 변경 목록에 포함되어 있는지 확인
            if (changedProps.ContainsKey(PhotonLinkManager.gameReady_Customproperty))
            {
                bool newReady = (bool)changedProps[PhotonLinkManager.gameReady_Customproperty];
#if UNITY_EDITOR
                Debug.Log($"[GameManager] : {targetPlayer.NickName}의 GameReady 상태가 변경됨: {newReady}");
#endif
                CheckAllPlayerGameReady();
            }
        }


        #endregion
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

        //============================================================================



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

        //============================================================================


        #region Infect Logic

        // 분 단위
        private int minInfectionTime = 3; // 최초 감염 발생 시간
        private int InfectionInterval = 1; // 감염 발생 Term
        private float IntectionChance = 20f; // 감염 확률 


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
                Debug.Log("[GameManager] : 마스터 클라이언트가 아닙니다.");
#endif
                return;
            }

            if (PhotonNetwork.CurrentRoom.PlayerCount <= 1)
            {
#if UNITY_EDITOR
                Debug.Log("[GameManager] : 감염시키기에는 플레이어가 너무 적습니다.");
#endif
                return;
            }

            if (infectedPlayer != null)
            {
#if UNITY_EDITOR
                Debug.Log("[GameManager] : 이미 감염된 플레이어가 존재합니다.");
#endif
                return;
            }

            float randF = UnityEngine.Random.Range(0f, 100f);

            // 1. 감염 확률 실패 시 즉시 탈출
            if (randF > IntectionChance)
            {
#if UNITY_EDITOR
                Debug.Log("[GameManager] : 감염확률을 뚫지 못했습니다.");
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
                Debug.Log("[GameManager] : 감염시키기엔 살아있는 플레이어가 없습니다.");
#endif
                return;
            }

            // 4. 살아있는 유저들 중 무작위로 1명 선택
            int randomIndex = UnityEngine.Random.Range(0, aliveActorNumbers.Count);
            int selectedActorNr = aliveActorNumbers[randomIndex];

#if UNITY_EDITOR
            Debug.Log($"[GameManager] : 감염 대상 선정 완료 : ActorNr - [{selectedActorNr}]");
#endif

            // 5. RPC 전파
            photonView.RPC(nameof(RPC_InfectPlayer), RpcTarget.All, selectedActorNr);

#if UNITY_EDITOR
            Debug.Log("[GameManager] : 대상이 감염되었습니다.");
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


        //============================================================================

        #region Esacpe Button

        public Action OnEscapeButtonClicked;

        // 1. 탈출 요청
        public void RequestEscape()
        {
            if (PhotonNetwork.IsMasterClient)
            {
                ExecuteEscape();
            }
            else
            {
                photonView.RPC(nameof(RPC_RequestEscapeOnMaster), RpcTarget.MasterClient);
            }
        }

        [PunRPC]
        private void RPC_RequestEscapeOnMaster()
        {
            if (!PhotonNetwork.IsMasterClient) return;

            ExecuteEscape();
        }

        private void ExecuteEscape()
        {
            photonView.RPC(nameof(RPC_ExecuteEscape), RpcTarget.All);
        }

        [PunRPC]
        private void RPC_ExecuteEscape()
        {
            CheckGameResult();

            OnEscapeButtonClicked?.Invoke();
        }


        #endregion

        //============================================================================

        #region Door Button

        public bool doorIsOpen = false;

        public Action<bool> OnDoorButtonClicked;

        // 1. 문 열기 요청
        public void RequestToggleDoor()
        {
            if (PhotonNetwork.IsMasterClient)
            {
                ToggleDoor();
            }
            else
            {
                photonView.RPC(nameof(RPC_RequestToggleDoor), RpcTarget.MasterClient);
            }
        }

        [PunRPC]
        private void RPC_RequestToggleDoor()
        {
            if (!PhotonNetwork.IsMasterClient) return;

            ToggleDoor();
        }

        private void ToggleDoor()
        {
            doorIsOpen = !doorIsOpen;

            photonView.RPC(nameof(RPC_ToggleDoor), RpcTarget.All, doorIsOpen);
        }

        [PunRPC]
        private void RPC_ToggleDoor(bool isOpen)
        {
            doorIsOpen = isOpen;

            OnDoorButtonClicked?.Invoke(doorIsOpen);
        }



        #endregion

    }

}