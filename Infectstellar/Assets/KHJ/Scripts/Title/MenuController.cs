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
            Waiting,
            Title,
            MainMenu,
            SetNickname,
            Options,
            Ranking,
            PlayOnline,
            CreateRoom,
            PlayOffline,
            GameStart,
        }

        [ShowInInspector]
        public MMStateMachine<MenuState> menuFSM;

        private void Awake()
        {
            menuFSM = new MMStateMachine<MenuState>(this.gameObject, true);
        }

        private void OnEnable()
        {
            menuFSM.ChangeState(MenuState.Waiting);

            Wait();
        }

        public async void Wait()
        {
            await UniTask.Delay(2000);
            menuFSM.ChangeState(MenuState.Title);
        }


        public void OnGameStartBtnClicked()
        {
            menuFSM.ChangeState(MenuState.MainMenu);
        }

        public void OnSetNicknameBtnClicked()
        {
            menuFSM.ChangeState(MenuState.SetNickname);
        }

        public void OnPlayOnlineBtnClicked()
        {
            menuFSM.ChangeState(MenuState.Title);
        }

        public void OnPlayOfflineBtnClicked()
        {
            menuFSM.ChangeState(MenuState.Title);
        }

        public void OnRankingBtnClicked()
        {
            menuFSM.ChangeState(MenuState.Ranking);
        }

        public void OnOptionsBtnClicked()
        {
            menuFSM.ChangeState(MenuState.Options);
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