using UnityEngine;
using MoreMountains.Tools;
using MoreMountains.Feedbacks;
using Cysharp.Threading.Tasks;
using TMPro;

namespace ENZEUN.Runtime
{
    public class MenuHandler : MonoBehaviour, MMEventListener<MMStateChangeEvent<MenuController.MenuState>>
    {
        [SerializeField] private MenuController menuController;
        [SerializeField] private MMF_Player LoadingPannelFb;
        [SerializeField] private MMProgressBar loadingProgressBar;
        [SerializeField] private MMF_Player gameStartBtnFb;
        [SerializeField] private MMF_Player mainMenuFb;
        [SerializeField] private MMF_Player nicknameMenuFb;
        [SerializeField] private NicknameMenuHandler nicknameMenuHandler;
        [SerializeField] private TMP_Text userNickname;


        public void OnEnable()
        {
            this.MMEventStartListening<MMStateChangeEvent<MenuController.MenuState>>();
        }

        public void OnDisable()
        {
            this.MMEventStopListening<MMStateChangeEvent<MenuController.MenuState>>();
        }

        public async UniTaskVoid InitialSettings()
        {
            FirebaseLinkManager firebaseLinkManager = FirebaseLinkManager.Instance;

            await firebaseLinkManager.ConnectToFirebaseAsync();

            // 여기서 로딩 연출함. 추가적으로 로직이 필요하면 사이에 넣으면 됨.
            loadingProgressBar.UpdateBar(100f, 0f, 100f);
            await UniTask.Delay(1000);

            menuController.ChangeToMainMenu();
        }


        public void OnMMEvent(MMStateChangeEvent<MenuController.MenuState> eventType)
        {

#if UNITY_EDITOR
            Debug.Log($"MenuHandler: OnMMEvent: NewState : {eventType.NewState} / PrevState : {eventType.PreviousState}");
#endif

            switch (eventType.NewState)
            {
                case MenuController.MenuState.Title:
                    gameStartBtnFb.SetDirection(MMFeedbacks.Directions.TopToBottom);
                    gameStartBtnFb.PlayFeedbacks();
                    break;

                case MenuController.MenuState.ConnectToFirebase:
                    LoadingPannelFb.SetDirection(MMFeedbacks.Directions.TopToBottom);
                    LoadingPannelFb.PlayFeedbacks();
                    InitialSettings().Forget();
                    break;

                case MenuController.MenuState.MainMenu:
                    if (!FirebaseLinkManager.playerHasNickname)
                    {
                        menuController.ChangeToNicknameMenu();
                        break;
                    }
                    mainMenuFb.SetDirection(MMFeedbacks.Directions.TopToBottom);
                    mainMenuFb.PlayFeedbacks();
                    break;

                case MenuController.MenuState.SetNickname:
                    nicknameMenuHandler.RefreshThisMenu();
                    nicknameMenuFb.SetDirection(MMFeedbacks.Directions.TopToBottom);
                    nicknameMenuFb.PlayFeedbacks();
                    break;

            }

            switch (eventType.PreviousState)
            {
                case MenuController.MenuState.Title:
                    gameStartBtnFb.SetDirection(MMFeedbacks.Directions.BottomToTop);
                    gameStartBtnFb.PlayFeedbacks();
                    break;

                case MenuController.MenuState.ConnectToFirebase:
                    LoadingPannelFb.SetDirection(MMFeedbacks.Directions.BottomToTop);
                    LoadingPannelFb.PlayFeedbacks();
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

        }
    }
}