
using MoreMountains.Feedbacks;
using TMPro;
using UnityEngine;


namespace ENZEUN.Runtime
{

    public class ErrorPanelManager : MonoBehaviour
    {
        public static ErrorPanelManager Instance;

        /// <summary>
        /// Instance 는 하나만 존재하도록 (싱글턴)
        /// </summary>
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        [SerializeField] TextMeshProUGUI errorText;
        [SerializeField] MMF_Player fb;

        public void ShowError(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;
            if (errorText == null)
                return;

            fb.PlayFeedbacks();
            errorText.SetText("Error : " + message);
        }
    }

}