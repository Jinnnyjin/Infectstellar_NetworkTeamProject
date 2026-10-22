using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;


namespace ENZEUN.Runtime
{


    public class EmptySceneController : MonoBehaviour
    {
        public async void Start()
        {

#if UNITY_EDITOR
            Debug.Log("[EmptyScene] 입장.");
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

            SceneManager.LoadScene("TitleScene");
        }
    }
}