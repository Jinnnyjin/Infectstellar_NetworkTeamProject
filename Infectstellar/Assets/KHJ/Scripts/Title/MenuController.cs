using UnityEngine;
using MoreMountains.Feedbacks;
using Sirenix.OdinInspector;
using MoreMountains.Tools;

namespace ENZEUN.Runtime
{

    public class MenuController : MonoBehaviour
    {

        [SerializeField] private MMF_Player gameStartBtnFb;
        [SerializeField] private MMF_Player mainMenuFb;


        public enum MenuState
        {
            Title,
            MainMenu,
            Options,
            PlayOnline,
            CreateRoom,
            PlayOffline,
            GameStart,
        }

        [ShowInInspector]
        MMStateMachine<MenuState> menuFSM;

        public void OnGameStartBtnClicked()
        {
            mainMenuFb.PlayFeedbacks();
        }

        public void OnMainMenuBtnClicked()
        {
            mainMenuFb.PlayFeedbacks();
        }
    }

}