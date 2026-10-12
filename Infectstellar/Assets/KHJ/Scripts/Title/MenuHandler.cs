using UnityEngine;
using MoreMountains.Tools;
using MoreMountains.Feedbacks;

namespace ENZEUN.Runtime
{
    public class MenuHandler : MonoBehaviour, MMEventListener<MMStateChangeEvent<MenuController.MenuState>>
    {
        [SerializeField] private MMF_Player gameStartBtnFb;
        [SerializeField] private MMF_Player mainMenuFb;
        [SerializeField] private MMF_Player nicknameMenuFb;
        [SerializeField] private NicknameMenuHandler nicknameMenuHandler;




        public void OnEnable()
        {
            this.MMEventStartListening<MMStateChangeEvent<MenuController.MenuState>>();
        }

        public void OnDisable()
        {
            this.MMEventStopListening<MMStateChangeEvent<MenuController.MenuState>>();
        }



        public void OnMMEvent(MMStateChangeEvent<MenuController.MenuState> eventType)
        {

#if UNITY_EDITOR
            Debug.Log($"MenuHandler: OnMMEvent: NewState : {eventType.NewState} / PrevState : {eventType.PreviousState}");
#endif

            switch (eventType.PreviousState)
            {
                case MenuController.MenuState.Title:
                    gameStartBtnFb.SetDirection(MMFeedbacks.Directions.BottomToTop);
                    gameStartBtnFb.PlayFeedbacks();
                    break;

                case MenuController.MenuState.MainMenu:
                    mainMenuFb.SetDirection(MMFeedbacks.Directions.BottomToTop);
                    mainMenuFb.PlayFeedbacks();
                    break;

                case MenuController.MenuState.SetNickname:
                    nicknameMenuFb.SetDirection(MMFeedbacks.Directions.BottomToTop);
                    nicknameMenuFb.PlayFeedbacks();
                    break;

            }

            switch (eventType.NewState)
            {
                case MenuController.MenuState.Waiting:
                    break;

                case MenuController.MenuState.Title:
                    gameStartBtnFb.SetDirection(MMFeedbacks.Directions.TopToBottom);
                    gameStartBtnFb.PlayFeedbacks();
                    break;

                case MenuController.MenuState.MainMenu:
                    mainMenuFb.SetDirection(MMFeedbacks.Directions.TopToBottom);
                    mainMenuFb.PlayFeedbacks();
                    break;

                case MenuController.MenuState.SetNickname:
                    nicknameMenuHandler.RefreshThisMenu();
                    nicknameMenuFb.SetDirection(MMFeedbacks.Directions.TopToBottom);
                    nicknameMenuFb.PlayFeedbacks();
                    break;

            }
        }
    }
}