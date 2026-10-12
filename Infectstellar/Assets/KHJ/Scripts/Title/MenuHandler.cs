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



        public void Start()
        {
            InitialSettings().Forget();
        }

        public void OnEnable()
        {
            this.MMEventStartListening<MMStateChangeEvent<MenuController.MenuState>>();
            nicknameMenuHandler.OnNicknameSaveSuccessed += menuController.ChangeToMainMenu;
        }

        public void OnDisable()
        {
            this.MMEventStopListening<MMStateChangeEvent<MenuController.MenuState>>();
            nicknameMenuHandler.OnNicknameSaveSuccessed -= menuController.ChangeToMainMenu;
        }

        public async UniTaskVoid InitialSettings()
        {
            FirebaseLinkManager firebaseLinkManager = FirebaseLinkManager.Instance;

            LoadingPannelFb.PlayFeedbacks();

            await UniTask.Delay(1000);

            await firebaseLinkManager.ConnectToFirebaseAsync();

            if (FirebaseLinkManager.playerHasNickname)
            {
                userNickname.SetText(FirebaseLinkManager.CurrentPlayerData.Nickname);
            }

            loadingProgressBar.UpdateBar(50f, 0f, 100f);

            await UniTask.Delay(1000);

            loadingProgressBar.UpdateBar(100f, 0f, 100f);

            await UniTask.Delay(1000);

            menuController.ChangeToTitleMenu();

        }




        public void OnMMEvent(MMStateChangeEvent<MenuController.MenuState> eventType)
        {

#if UNITY_EDITOR
            Debug.Log($"MenuHandler: OnMMEvent: NewState : {eventType.NewState} / PrevState : {eventType.PreviousState}");
#endif

            switch (eventType.PreviousState)
            {
                case MenuController.MenuState.Waiting:
                    LoadingPannelFb.SetDirection(MMFeedbacks.Directions.BottomToTop);
                    LoadingPannelFb.PlayFeedbacks();
                    break;

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
                    Debug.Log("asdasdsadsad");
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
        }
    }
}