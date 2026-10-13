using UnityEngine;
using Sirenix.OdinInspector;
using MoreMountains.Tools;
using Cysharp.Threading.Tasks;


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
            PlayOnline,
            PlayOffline,
            Ranking,
            Options,
            CreateRoom,
            JoinRoom,
        }

        [ShowInInspector, ReadOnly] public MenuState CurrentState => menuFSM?.CurrentState ?? MenuState.Initailize;

        private MMStateMachine<MenuState> menuFSM;

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
            //더미
            menuFSM.ChangeState(MenuState.PlayOnline);
        }

        public void OnPlayOfflineBtnClicked()
        {
            //더미
            menuFSM.ChangeState(MenuState.PlayOffline);
        }

        public void OnRankingBtnClicked()
        {
            menuFSM.ChangeState(MenuState.Ranking);
        }

        public void OnOptionsBtnClicked()
        {
            menuFSM.ChangeState(MenuState.Options);
        }

        public void OnCreateRoomClicked()
        {

        }

        public void OnJoinRoomClicked()
        {

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