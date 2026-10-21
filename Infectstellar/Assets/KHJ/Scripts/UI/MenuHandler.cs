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
        [SerializeField] private MMF_Player connectPhotonFb;
        [SerializeField] private MMF_Player roomSearchMenuFb;
        [SerializeField] private TextMeshProUGUI userNickname;
        [SerializeField] private MMF_Player roomMenuFb;
        [SerializeField] private MMF_Player rankingMenuFb;
        [SerializeField] private TextMeshProUGUI rankingText;



        public void OnEnable()
        {
            this.MMEventStartListening<MMStateChangeEvent<MenuController.MenuState>>();
        }

        public void OnDisable()
        {
            this.MMEventStopListening<MMStateChangeEvent<MenuController.MenuState>>();

            if (FirebaseLinkManager.Instance != null)
            {
                FirebaseLinkManager.Instance.OnNicknameLoadSucceeded -= ChangeNicknameText;
                FirebaseLinkManager.Instance.OnNicknameSaveSucceeded -= ChangeNicknameText;
                FirebaseLinkManager.Instance.OnRankingListUpdated -= UpdateRankingBoard;
            }

        }

        public async UniTaskVoid InitialSettings()
        {
            if (FirebaseLinkManager.Instance == null)
            {
                ErrorPanelManager.Instance?.ShowError("FirebaseLinkManager has not Initialized. Please check it out");
#if UNITY_EDITOR
                Debug.Log("FirebaseLinkManager 가 초기화되지 않은 문제 발생.");
#endif

                menuController.ChangeToTitleMenu();
                return;
            }
            FirebaseLinkManager firebaseLinkManager = FirebaseLinkManager.Instance;

            firebaseLinkManager.OnNicknameLoadSucceeded += ChangeNicknameText;
            firebaseLinkManager.OnNicknameSaveSucceeded += ChangeNicknameText;
            firebaseLinkManager.OnRankingListUpdated += UpdateRankingBoard;

            // 여기서 로딩 연출함. 추가적으로 로직이 필요하면 사이에 넣으면 됨.
            await firebaseLinkManager.ConnectToFirebaseAsync();

            //loadingProgressBar.UpdateBar(50f, 0f, 100f);

            loadingProgressBar.UpdateBar(100f, 0f, 100f);

            await UniTask.Delay(1000);

            menuController.ChangeToMainMenu();
        }

        public void ChangeNicknameText()
        {
            userNickname.SetText($"{FirebaseLinkManager.CurrentPlayerData.Nickname}");
        }

        public void OnMMEvent(MMStateChangeEvent<MenuController.MenuState> eventType)
        {

#if UNITY_EDITOR
            Debug.Log($"MenuHandler: OnMMEvent: NewState : {eventType.NewState} / PrevState : {eventType.PreviousState}");
#endif

            // 현재 상태 진입 로직
            switch (eventType.NewState)
            {
                case MenuController.MenuState.Title:
                    gameStartBtnFb.PlayFeedbacksTopToBottom();
                    break;

                case MenuController.MenuState.ConnectToFirebase:
                    LoadingPannelFb.PlayFeedbacksTopToBottom();
                    InitialSettings().Forget();
                    break;

                case MenuController.MenuState.MainMenu:
                    if (!FirebaseLinkManager.playerHasNickname)
                    {
                        menuController.ChangeToNicknameMenu();
                        break;
                    }
                    mainMenuFb.PlayFeedbacksTopToBottom();
                    break;

                case MenuController.MenuState.SetNickname:
                    nicknameMenuHandler.RefreshThisMenu();
                    nicknameMenuFb.PlayFeedbacksTopToBottom();
                    break;

                case MenuController.MenuState.ConnectToPhotonServer:
                    connectPhotonFb.PlayFeedbacksTopToBottom();
                    break;

                case MenuController.MenuState.InLobby:
                    roomSearchMenuFb.PlayFeedbacksTopToBottom();
                    break;

                case MenuController.MenuState.InRoom:
                    roomMenuFb.PlayFeedbacksTopToBottom();
                    break;

                case MenuController.MenuState.Ranking:
                    RefreshRankingBoard();
                    rankingText.SetText("Loading...");
                    rankingMenuFb.PlayFeedbacksTopToBottom();
                    break;
            }

            // 이전 상태 종료 로직
            switch (eventType.PreviousState)
            {
                case MenuController.MenuState.Title:
                    gameStartBtnFb.PlayFeedbacksBottomToTop();
                    break;

                case MenuController.MenuState.ConnectToFirebase:
                    LoadingPannelFb.PlayFeedbacksBottomToTop();
                    break;

                case MenuController.MenuState.MainMenu:
                    mainMenuFb.PlayFeedbacksBottomToTop();
                    break;

                case MenuController.MenuState.SetNickname:
                    nicknameMenuFb.PlayFeedbacksBottomToTop();
                    break;

                case MenuController.MenuState.ConnectToPhotonServer:
                    connectPhotonFb.PlayFeedbacksBottomToTop();
                    break;

                case MenuController.MenuState.InLobby:
                    roomSearchMenuFb.PlayFeedbacksBottomToTop();
                    break;

                case MenuController.MenuState.InRoom:
                    roomMenuFb.PlayFeedbacksBottomToTop();
                    break;

                case MenuController.MenuState.Ranking:
                    rankingMenuFb.PlayFeedbacksBottomToTop();
                    break;
            }

        }

        public void RefreshRankingBoard()
        {
            FirebaseLinkManager.Instance.GetTopRankingsAsync().Forget();
        }
        public void UpdateRankingBoard()
        {

            string final = string.Empty;

            int count = 1;

            foreach (var data in FirebaseLinkManager.Instance.rankingList)
            {
                final = final + $"{count}. Players : [{data.nicknames}]\n\tEarned Gold : [{data.earnedGold}]\n";
                count++;
            }

            rankingText.text = final;
        }
    }
}