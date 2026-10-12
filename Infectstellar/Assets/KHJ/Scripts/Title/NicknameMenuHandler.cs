
using System;
using Cysharp.Threading.Tasks;
using ENZEUN.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class NicknameMenuHandler : MonoBehaviour
{
    [SerializeField] private TMP_InputField nicknameInput;
    [SerializeField] private Button confirmBtn;
    [SerializeField] private Button cancelBtn;
    [SerializeField] private TMP_Text userNickname;


    public Action OnNicknameSaveSuccessed;

    public bool isMenuOnScreen = false;

    public void RefreshThisMenu()
    {
        isMenuOnScreen = true;
        nicknameInput.text = string.Empty;
    }

    public void Update()
    {
        if (!isMenuOnScreen)
            return;

        if (string.IsNullOrEmpty(nicknameInput.text))
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
        confirmBtn.interactable = false;

        TrySaveNickname().Forget();
    }

    public async UniTask TrySaveNickname()
    {
        if (await FirebaseLinkManager.Instance.SaveUserNicknameAsync(nicknameInput.text))
        {
            isMenuOnScreen = false;

            userNickname.SetText(FirebaseLinkManager.CurrentPlayerData.Nickname);

            OnNicknameSaveSuccessed?.Invoke();
        }

        else
        {
#if UNITY_EDITOR
            Debug.Log("닉네임 변경에 문제가 생겼습니다. 다시 시도하세요");
#endif

            confirmBtn.interactable = true;
        }
    }

}
