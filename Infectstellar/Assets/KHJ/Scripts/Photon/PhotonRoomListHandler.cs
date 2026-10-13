using System.Collections.Generic;
using Photon.Realtime;
using UnityEngine;
using Sirenix.OdinInspector;
using UnityEngine.UI;
using TMPro;


namespace ENZEUN.Runtime
{

    public class PhotonRoomListHandler : MonoBehaviour
    {
        [SerializeField] private RectTransform roomListTransform;
        [SerializeField] private Button joinRoomBtn;
        [SerializeField] private Button CreateRoomBtn;
        [SerializeField] private Button QuickMatchBtn;
        [SerializeField] private TMP_InputField roomnameEntered;

        private PhotonLinkManager photonInstance;

        private List<RoomContent> roomContents = new();
        [SerializeField] private RoomContent roomContentPrefab;

        [ReadOnly]
        public RoomContent currentSeletedRoom;

        private bool isInitialized = false;

        private int refreshCount = 0;

        public void Initialize()
        {
            if (isInitialized)
                return;

            if (PhotonLinkManager.Instance == null)
                return;

            photonInstance = PhotonLinkManager.Instance;
            photonInstance.OnRoomListUpdated += RefreshRoomList;
            isInitialized = true;

            RefreshBtns();
        }

        private void OnEnable()
        {
            joinRoomBtn?.onClick.AddListener(JoinRoomBtnClicked);
            QuickMatchBtn?.onClick.AddListener(QuickMatchBtnClicked);
            CreateRoomBtn?.onClick.AddListener(CreateRoomBtnClicked);
            roomnameEntered?.onValueChanged.AddListener(OnRoomnameTextChanged);
        }

        private void OnDisable()
        {
            if (!isInitialized)
                return;

            if (photonInstance != null)
            {
                photonInstance.OnRoomListUpdated -= RefreshRoomList;
                isInitialized = false;
            }

            joinRoomBtn?.onClick.RemoveListener(JoinRoomBtnClicked);
            QuickMatchBtn?.onClick.RemoveListener(QuickMatchBtnClicked);
            CreateRoomBtn?.onClick.RemoveListener(CreateRoomBtnClicked);
            roomnameEntered?.onValueChanged.RemoveListener(OnRoomnameTextChanged);
        }

        private void RefreshRoomList(List<RoomInfo> rooms)
        {
            refreshCount++;
            // 넘겨받은 모든 roomInfo 에 대하여
            foreach (var room in rooms)
            {
                bool foundThisRoom = false;

                // 중복된 이름이 있는지 확인하고
                foreach (var rc in roomContents)
                {
                    // 중복된 이름이 있으면, 정보 갱신
                    if (rc.roomInfo.Name == room.Name)
                    {
                        rc.roomInfo = room;

                        rc.refreshCount = refreshCount;

                        rc.RefreshRoomContent();

                        foundThisRoom = true;

                        break;
                    }
                }

                // 방을 찾았고, 정보갱신을 했다면, 다음 방으로
                if (foundThisRoom)
                {
                    continue;
                }
                // 방을 못 찾았다면, Instaciate
                else
                {
                    InstancitateRoom(room);
                }
            }

            List<RoomContent> roomsForDelete = new();

            for (int i = roomContents.Count - 1; i >= 0; i--)
            {
                if (roomContents[i].refreshCount != refreshCount)
                {
                    roomsForDelete.Add(roomContents[i]);

                    roomContents.RemoveAt(i);
                }
            }

            for (int i = roomsForDelete.Count - 1; i >= 0; i--)
            {
                if (currentSeletedRoom == roomsForDelete[i])
                {
                    currentSeletedRoom = null;
                }
                roomsForDelete[i].OnSelected -= ChangeSelectedRoom;
                roomsForDelete[i].DestroyThisRoom();
            }

            RefreshBtns();
        }

        private void InstancitateRoom(RoomInfo roomInfo)
        {
            RoomContent roomContent = Instantiate(roomContentPrefab, roomListTransform);

            roomContent.roomInfo = roomInfo;

            roomContent.refreshCount = refreshCount;

            roomContent.OnSelected += ChangeSelectedRoom;

            roomContent.RefreshRoomContent();

            AddToRoomList(roomContent);
        }

        private void AddToRoomList(RoomContent roomContent)
        {
            roomContents.Add(roomContent);
        }

        private void ChangeSelectedRoom(RoomContent roomContent)
        {
            if (currentSeletedRoom != null)
            {
                if (currentSeletedRoom == roomContent)
                {
                    return;
                }
                currentSeletedRoom.UnselectThisRoom();
            }
            currentSeletedRoom = roomContent;

            RefreshBtns();
        }

        public void RefreshBtns()
        {
            bool roomSelected = currentSeletedRoom != null;
            bool hasRoom = roomContents.Count > 0;


            joinRoomBtn.interactable = roomSelected;
            QuickMatchBtn.interactable = hasRoom;
        }

        private void JoinRoomBtnClicked()
        {
            PhotonLinkManager.Instance.JoinRoom(currentSeletedRoom.roomInfo.Name);
        }
        private void QuickMatchBtnClicked()
        {
            PhotonLinkManager.Instance.JoinRandomRoom();
        }
        private void CreateRoomBtnClicked()
        {
            PhotonLinkManager.Instance.CreateRoom(roomnameEntered.text);
        }

        private void OnRoomnameTextChanged(string text)
        {
            bool isroomnameEntered = !string.IsNullOrWhiteSpace(roomnameEntered.text);
            CreateRoomBtn.interactable = isroomnameEntered;
        }
    }

}