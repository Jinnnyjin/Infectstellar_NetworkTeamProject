using UnityEngine;
using Sirenix.OdinInspector;
using MoreMountains.Tools;


namespace ENZEUN.Runtime
{
    public class MenuController : MonoBehaviour
    {
        public enum MenuState
        {
            Initailize,
            Title,
            ConnectToFirebase,
            SetNickname,
            MainMenu,
            ConnectToPhotonServer,
            InLobby,
            PlayOffline,
            Ranking,
            Options,
            InRoom,
        }

        [ShowInInspector, ReadOnly] public MenuState CurrentState => menuFSM?.CurrentState ?? MenuState.Initailize;

        private MMStateMachine<MenuState> menuFSM;

        private bool subscribedJoinLobby = false;

        private void Awake()
        {
            menuFSM = new MMStateMachine<MenuState>(this.gameObject, true);
        }

        private void OnEnable()
        {
            menuFSM.ChangeState(MenuState.Initailize);
        }

        private void OnDisable()
        {
            if (PhotonLinkManager.Instance != null)
            {
                if (subscribedJoinLobby)
                {
                    PhotonLinkManager.Instance.OnJoinedLobbyCallback -= ChangeToRoomSearchMenu;
                    subscribedJoinLobby = false;
                }
            }
        }

        private void Start()
        {
            ChangeToTitleMenu();
        }

        public void ChangeToTitleMenu()
        {
            menuFSM.ChangeState(MenuState.Title);
        }

        public void ChangeToNicknameMenu()
        {
            menuFSM.ChangeState(MenuState.SetNickname);
        }

        public void ChangeToMainMenu()
        {
            menuFSM.ChangeState(MenuState.MainMenu);
        }

        public void ChangeToRoomSearchMenu()
        {
            menuFSM.ChangeState(MenuState.InLobby);

        }

        public void ChangeToInRoomMenu()
        {
            menuFSM.ChangeState(MenuState.InRoom);

        }


        /// <summary>
        /// Title 상태에서 GameStart 버튼 클릭 시
        /// </summary>
        public void OnGameStartBtnClicked()
        {
            menuFSM.ChangeState(MenuState.ConnectToFirebase);
        }

        public void OnSetNicknameBtnClicked()
        {
            menuFSM.ChangeState(MenuState.SetNickname);
        }

        public void OnPlayOnlineBtnClicked()
        {
            if (PhotonLinkManager.Instance != null)
            {
                PhotonLinkManager.Instance.ConnectToPhotonServer();

                if (!subscribedJoinLobby)
                {
                    PhotonLinkManager.Instance.OnJoinedLobbyCallback += ChangeToRoomSearchMenu;
                    subscribedJoinLobby = true;
                }
            }
            else
            {
                Debug.Log("서버에 연결할 수 없습니다");
            }
            menuFSM.ChangeState(MenuState.ConnectToPhotonServer);
        }

        public void OnPlayOfflineBtnClicked()
        {
            ErrorPanelManager.Instance?.ShowError("아직 메뉴가 완성되지 않았습니다");
            //menuFSM.ChangeState(MenuState.PlayOffline);
        }

        public void OnRankingBtnClicked()
        {
            ErrorPanelManager.Instance?.ShowError("아직 메뉴가 완성되지 않았습니다");
            //menuFSM.ChangeState(MenuState.Ranking);
        }

        public void OnOptionsBtnClicked()
        {
            ErrorPanelManager.Instance?.ShowError("아직 메뉴가 완성되지 않았습니다");
            //menuFSM.ChangeState(MenuState.Options);
        }

        public void OnQuitBtnClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif

            Application.Quit();
        }
    }
}