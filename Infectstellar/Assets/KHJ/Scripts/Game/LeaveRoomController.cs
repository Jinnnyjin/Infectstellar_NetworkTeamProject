using UnityEngine;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;
using System;
using Photon.Pun;

namespace ENZEUN.Runtime
{
    public class LeaveRoomController : MonoBehaviour
    {
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        public async void Start()
        {
#if UNITY_EDITOR
            Debug.Log("[LeaveRoom] 입장.");
#endif

            int count = 0;

            while (PhotonLinkManager.Instance == null)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(1));

                count++;

                if (count >= 30)
                {
                    Application.Quit();
                    return;
                }
            }

            PhotonLinkManager.Instance.ResetPlayerCustomProperty();

            PhotonNetwork.Disconnect();

            SceneManager.LoadScene("TitleScene");
        }
    }
}
