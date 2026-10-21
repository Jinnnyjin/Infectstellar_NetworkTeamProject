using UnityEngine;
using MoreMountains.Tools;
using static ENZEUN.Runtime.GameManager;

namespace ENZEUN.Runtime
{
    public class GameStateManager : MonoBehaviour, MMEventListener<MMStateChangeEvent<GameStateFSM>>
    {

        private bool isInitialized = false;


        /// <summary>
        /// 초기 세팅. GameManager 에서 호출.
        /// </summary>
        public void InitializeGameStateManager()
        {
            if (isInitialized)
                return;



            isInitialized = true;
        }


        public void OnEnable()
        {
            this.MMEventStartListening<MMStateChangeEvent<GameManager.GameStateFSM>>();
        }

        public void OnDisable()
        {
            this.MMEventStopListening<MMStateChangeEvent<GameManager.GameStateFSM>>();
            isInitialized = false;
        }


        #region FSM Event Callback


        public void OnMMEvent(MMStateChangeEvent<GameStateFSM> eventType)
        {


            Debug.Log($"GameStateManager: OnMMEvent: NewState : {eventType.NewState} / PrevState : {eventType.PreviousState}");

            // 현재 상태 진입 로직
            switch (eventType.NewState)
            {

                case GameStateFSM.None:
                    break;

                case GameStateFSM.ReadyForStart:
                    Instance.ReadyForGame();
                    break;

                case GameStateFSM.OnGameStart:
                    Instance.TriggerGameStart();
                    break;

                case GameStateFSM.GameStarted:
                    break;

                case GameStateFSM.Escaped:
                    break;

                case GameStateFSM.OnFailed:
                    break;

                case GameStateFSM.ShowResult:
                    break;

                case GameStateFSM.RegisterRanking:
                    break;

            }

            // 이전 상태 종료 로직
            // switch (eventType.PreviousState)
            // {
            //     case GameStateFSM.None:
            //         break;

            //     case GameStateFSM.ReadyForStart:
            //         break;

            //     case GameStateFSM.OnGameStart:
            //         break;

            //     case GameStateFSM.GameStarted:
            //         break;

            //     case GameStateFSM.Escaped:
            //         break;

            //     case GameStateFSM.OnFailed:
            //         break;

            //     case GameStateFSM.ShowResult:
            //         break;

            //     case GameStateFSM.RegisterRanking:
            //         break;
            // }

        }




        #endregion



    }
}