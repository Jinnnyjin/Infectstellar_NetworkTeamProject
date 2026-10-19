using UnityEngine;
using MoreMountains.Tools;
using Sirenix.OdinInspector;


namespace ENZEUN.Runtime
{
    public class GameManager : MonoBehaviour
    {
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

        }


        //============================================================================


        #region FSM Settings

        public enum GameStateFSM
        {
            None,               // 아무상태 X
            ReadyForStart,      // 게임 시작 준비 중
            OnGameStart,        // 게임 시작 타이밍
            GameStarted,        // 게임이 시작된 후
            OnGameLoopEnd,      // 게임 루프 종료
            OnFailed,           // 모두가 죽었을 때 (실패)
            ShowResult,         // 결과 보여주기
            RegisterRanking,    // 랭킹 등록

        }

        private MMStateMachine<GameStateFSM> gameStateFSM;

        [ShowInInspector, ReadOnly]
        public GameStateFSM currentGameState
        => gameStateFSM?.CurrentState ?? GameStateFSM.None;


        #endregion

        [Button]
        public void ChangeGameState(GameStateFSM gameState)
        {
            gameStateFSM.ChangeState(gameState);
        }
    }

}