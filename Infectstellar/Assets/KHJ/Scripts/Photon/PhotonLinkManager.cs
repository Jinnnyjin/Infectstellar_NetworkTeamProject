using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using System;
using TMPro;


namespace ENZEUN.Runtime
{
    public class PhotonLinkManager : MonoBehaviourPunCallbacks
    {
        public static PhotonLinkManager Instance;

        public static int MaxPlayer = 4;

        [SerializeField] private PhotonRoomListHandler photonRoomListHandler;

        /// <summary>
        /// Instance 는 하나만 존재하도록 (싱글턴)
        /// </summary>
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }


        // 방 목록 UI나 상태 관리를 위한 리스트
        public List<RoomInfo> CachedRoomList { get; private set; } = new List<RoomInfo>();

        // 방 목록 갱신 시 호출할 수 있는 이벤트 (UI 연동용)
        public Action<List<RoomInfo>> OnRoomListUpdated;
        public Action OnJoinedLobbyCallback;

        public void ConnectToPhotonServer()
        {
            if (!PhotonNetwork.IsConnected)
            {
                PhotonNetwork.ConnectUsingSettings();
            }
        }

        public override void OnConnectedToMaster()
        {
            Debug.Log("Connected to Master Server");
            PhotonNetwork.JoinLobby();
        }

        public override void OnJoinedLobby()
        {
            Debug.Log("Joined Lobby");
            photonRoomListHandler.Initialize();
            OnJoinedLobbyCallback?.Invoke();
        }


        /// <summary>
        /// 1. 특정 이름으로 방 만들기 (방이 이미 있으면 에러 발생 가능)
        /// </summary>
        public void CreateRoom(string roomName)
        {
            if (!PhotonNetwork.IsConnectedAndReady) return;

            RoomOptions roomOptions = new RoomOptions { MaxPlayers = (byte)MaxPlayer };
            PhotonNetwork.CreateRoom(roomName, roomOptions);
        }

        /// <summary>
        /// 2. 특정 이름의 방에 들어가기 (없으면 입장 실패)
        /// </summary>
        public void JoinRoom(string roomName)
        {
            if (!PhotonNetwork.IsConnectedAndReady) return;

            PhotonNetwork.JoinRoom(roomName);
        }

        /// <summary>
        /// 3. 대기 중인 방이 있으면 들어가고, 없으면 새로 만들기 (빠른 매칭)
        /// </summary>
        public void JoinOrCreateRandomRoom()
        {
            if (!PhotonNetwork.IsConnectedAndReady) return;

            RoomOptions roomOptions = new RoomOptions { MaxPlayers = (byte)MaxPlayer };
            PhotonNetwork.JoinRandomOrCreateRoom(expectedCustomRoomProperties: null, expectedMaxPlayers: 0, roomOptions: roomOptions);
        }

        /// <summary>
        /// 4. 랜덤 방에 들어가기
        /// </summary>
        public void JoinRandomRoom()
        {
            if (!PhotonNetwork.IsConnectedAndReady) return;

            PhotonNetwork.JoinRandomRoom();
        }


        #region Photon Callbacks

        public override void OnJoinedRoom()
        {

#if UNITY_EDITOR
            Debug.Log($"방 입장 성공! 방 이름: {PhotonNetwork.CurrentRoom.Name} (현재 인원: {PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers})");
#endif
        }

        public override void OnCreateRoomFailed(short returnCode, string message)
        {
            if (ErrorPanelManager.Instance != null)
            {
                ErrorPanelManager.Instance.ShowError($"방 생성 실패: {message}");
            }
#if UNITY_EDITOR
            Debug.LogError($"방 생성 실패: {message} (코드: {returnCode})");
#endif
        }

        public override void OnJoinRoomFailed(short returnCode, string message)
        {
            if (ErrorPanelManager.Instance != null)
            {
                ErrorPanelManager.Instance.ShowError($"방 입장 실패: {message}");
            }
#if UNITY_EDITOR
            Debug.LogError($"방 입장 실패: {message} (코드: {returnCode})");
#endif
        }

        public override void OnJoinRandomFailed(short returnCode, string message)
        {
            if (ErrorPanelManager.Instance != null)
            {
                ErrorPanelManager.Instance.ShowError("입장할 수 있는 랜덤 방이 없습니다.");
            }
#if UNITY_EDITOR
            Debug.Log("입장할 수 있는 랜덤 방이 없습니다.");
            //CreateRoom("Room_" + UnityEngine.Random.Range(1000, 9999));
#endif
        }

        // 로비에 있을 때 방 목록이 변경될 때마다 자동 호출됨 (방 찾기 목록용)
        public override void OnRoomListUpdate(List<RoomInfo> roomList)
        {
            foreach (var room in roomList)
            {
                // 방이 삭제되었거나 꽉 찬 경우 목록에서 제거/갱신
                if (room.RemovedFromList)
                {
                    CachedRoomList.RemoveAll(x => x.Name == room.Name);
                }
                else
                {
                    // 이미 있는 방이면 갱신, 없으면 추가
                    int index = CachedRoomList.FindIndex(x => x.Name == room.Name);
                    if (index != -1)
                    {
                        CachedRoomList[index] = room;
                    }
                    else
                    {
                        CachedRoomList.Add(room);
                    }
                }
            }

            // UI 스크립트에서 방 목록 갱신을 감지할 수 있도록 이벤트 발행
            OnRoomListUpdated?.Invoke(CachedRoomList);
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            Debug.Log($"서버 연결 끊김: {cause}");
        }

        #endregion
    }
}