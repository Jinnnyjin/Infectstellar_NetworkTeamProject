using System;
using Photon.Realtime;
using TMPro;
using UnityEngine;


namespace ENZEUN.Runtime
{

    public class RoomContent : MonoBehaviour
    {
        public bool isSelected = false;

        [SerializeField] private UnityEngine.UI.Image focusImage;
        [SerializeField] private TextMeshProUGUI roomNameText;
        [SerializeField] private TextMeshProUGUI roomPlayerCountText;

        public RoomInfo roomInfo;

        public Action<RoomContent> OnSelected;

        public int refreshCount = 0;

        public void RefreshRoomContent()
        {
            roomNameText.SetText($"{roomInfo.Name}");
            roomPlayerCountText.SetText($"{roomInfo.PlayerCount}/{roomInfo.MaxPlayers}");
        }

        public void SelectThisRoom()
        {
            isSelected = true;
            OnSelected?.Invoke(this);
            FocusThisRoom();
        }

        public void UnselectThisRoom()
        {
            isSelected = false;
            FocusThisRoom();
        }

        private void FocusThisRoom()
        {
            if (focusImage == null)
                return;

            focusImage.enabled = isSelected;
        }

        public void DestroyThisRoom()
        {
            Destroy(gameObject);
        }
    }
}