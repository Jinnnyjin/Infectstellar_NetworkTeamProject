
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;


namespace ENZEUN.Runtime
{

    public class NicknameMenuHandler : MonoBehaviour
    {
        [SerializeField] private TMP_InputField nicknameInput;
        [SerializeField] private Button confirmBtn;
        [SerializeField] private Button cancelBtn;


        private bool isSubscribed = false;
        private bool isMenuOnScreen = false;

        public UnityEvent OnMenuClosed;

        private void OnDisable()
        {
            if (FirebaseLinkManager.Instance != null)
            {
                if (isSubscribed)
                {
                    FirebaseLinkManager.Instance.OnNicknameSaveSucceeded -= OnNicknameSaved;
                    isSubscribed = false;
                }
            }
        }


        public void InitializeNicknameMenu()
        {
            if (!isSubscribed)
            {
                if (FirebaseLinkManager.Instance != null)
                {
                    isSubscribed = true;
                    FirebaseLinkManager.Instance.OnNicknameSaveSucceeded += OnNicknameSaved;
                }
                else
                {
                    Debug.Log("아직 FirebaseLinkManager 가 초기화되지 않았습니다.");
                    return;
                }
            }
        }

        public void RefreshThisMenu()
        {
            InitializeNicknameMenu();

            cancelBtn.interactable = !string.IsNullOrWhiteSpace(FirebaseLinkManager.CurrentPlayerData.Nickname);

            isMenuOnScreen = true;

            nicknameInput.text = string.Empty;
        }

        public void Update()
        {
            if (!isMenuOnScreen)
                return;

            if (string.IsNullOrWhiteSpace(nicknameInput.text))
            {
                if (confirmBtn.interactable == true)
                {
                    confirmBtn.interactable = false;
                }
            }
            else
            {
                if (confirmBtn.interactable == false)
                {
                    confirmBtn.interactable = true;
                }
            }
        }

        public void OnConfirmed()
        {
            isMenuOnScreen = false;
            confirmBtn.interactable = false;
            TrySaveNickname().Forget();
        }

        public async UniTask TrySaveNickname()
        {
            if (await FirebaseLinkManager.Instance.SaveUserNicknameAsync(nicknameInput.text))
            {
#if UNITY_EDITOR
                Debug.Log("닉네임 변경에 성공했습니다.");
#endif
            }

            else
            {
                if (ErrorPanelManager.Instance != null)
                {
                    ErrorPanelManager.Instance.ShowError("There was a problem changing your username. Please try again.");
                }
#if UNITY_EDITOR
                Debug.Log("닉네임 변경에 문제가 생겼습니다. 다시 시도하세요");
#endif
                RefreshThisMenu();
            }
        }

        private void OnNicknameSaved()
        {
            CloseMenu();
        }



        public void CloseMenu()
        {
            isMenuOnScreen = false;
            OnMenuClosed?.Invoke();
        }
    }
}