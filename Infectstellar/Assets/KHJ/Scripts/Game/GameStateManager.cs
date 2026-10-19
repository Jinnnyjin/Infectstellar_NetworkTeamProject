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

            // 초기화 로직



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
        }




        #endregion
    }
}