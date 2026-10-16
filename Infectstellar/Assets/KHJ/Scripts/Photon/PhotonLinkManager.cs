using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using System;
using ExitGames.Client.Photon;
using Unity.VisualScripting;


namespace ENZEUN.Runtime
{
    public class PhotonLinkManager : MonoBehaviourPunCallbacks
    {
        public static PhotonLinkManager Instance;


        /// <summary>
        /// 포톤 Player Custom Property 의 대기실 준비 상태
        /// </summary>
        public static string isReady_Customproperty = "IsReady";
        /// <summary>
        /// 포톤 Player Custom Property 의 캐릭터 색깔
        /// </summary>
        public static string myColor_Customproperty = "MyColor";
        /// <summary>
        /// 포톤 Player Custom Property 의 다음 씬으로의 로딩이 완료되었는지
        /// </summary>
        public static string sceneLoading_Customproperty = "SceneLoaded";


        public static int MaxPlayer = 4;

        private PhotonRoomListHandler photonRoomListHandler;

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
        public Action OnJoinedRoomCallback;
        public Action<Player> OnMasterChangedCallback;

        // Custom Property 속성 변경 시 발행할 이벤트들 (원하는 키별로 정의)
        public Action<Player, bool> OnReadyStateChanged; // 플레이어, 바뀐 준비 상태
        public Action<Player, int> OnMyColorChanged; // 플레이어, 바뀐 색 상태


        public Action OnAllPlayerReadyToNextScene;


        public void ConnectToPhotonServer()
        {
            if (!PhotonNetwork.IsConnected)
            {
                PhotonNetwork.ConnectUsingSettings();
            }
        }

        public override void OnConnectedToMaster()
        {
#if UNITY_EDITOR
            Debug.Log("Connected to Master Server");
#endif
            PhotonNetwork.NickName = FirebaseLinkManager.CurrentPlayerData.Nickname;
#if UNITY_EDITOR
            Debug.Log("NickName Has Uploaded to Photon Server");
#endif
            PhotonNetwork.JoinLobby();
        }

        public override void OnJoinedLobby()
        {
#if UNITY_EDITOR
            Debug.Log("Joined Lobby");
#endif
            photonRoomListHandler = FindFirstObjectByType<PhotonRoomListHandler>();
            photonRoomListHandler?.Initialize();
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

        /// <summary>
        /// 게임 시작 하면 Room 을 닫는 로직
        /// </summary>
        public void HideRoomWhenGameStarted()
        {
            if (PhotonNetwork.IsMasterClient)
            {
                // 더 이상 목록에 뜨지 않도록 숨김 처리
                PhotonNetwork.CurrentRoom.IsVisible = false;

                // 새로운 플레이어의 입장을 막기
                PhotonNetwork.CurrentRoom.IsOpen = false;
            }
        }


        private void CheckAllPlayerSceneLoaded()
        {
            // 마스터만 가능하도록 설정
            if (!PhotonNetwork.LocalPlayer.IsMasterClient)
            {
                return;
            }

            Player[] players = PhotonNetwork.PlayerList;

            bool isAllReady = true;

            foreach (var p in players)
            {
                if (p.CustomProperties.TryGetValue(sceneLoading_Customproperty, out object obj))
                {
                    if (!(bool)obj)
                    {
                        isAllReady = false;
                    }
                }
                else
                {
                    isAllReady = false;
                }
            }

            if (!isAllReady)
            {
                return;
            }

            photonView.RPC(nameof(LoadSceneRPC), RpcTarget.All);
        }

        //=================================================================================================
        #region Photon Callbacks

        public override void OnJoinedRoom()
        {
            OnJoinedRoomCallback?.Invoke();

#if UNITY_EDITOR
            Debug.Log($"방 입장 성공! 방 이름: {PhotonNetwork.CurrentRoom.Name} (현재 인원: {PhotonNetwork.CurrentRoom.PlayerCount}/{PhotonNetwork.CurrentRoom.MaxPlayers})");
#endif
        }

        public override void OnCreateRoomFailed(short returnCode, string message)
        {
            if (ErrorPanelManager.Instance != null)
            {
                ErrorPanelManager.Instance.ShowError($"Failed to create a room: {message}");
            }
#if UNITY_EDITOR
            Debug.LogError($"방 생성 실패: {message} (코드: {returnCode})");
#endif
        }

        public override void OnJoinRoomFailed(short returnCode, string message)
        {
            if (ErrorPanelManager.Instance != null)
            {
                ErrorPanelManager.Instance.ShowError($"Failed to enter the room: {message}");
            }
#if UNITY_EDITOR
            Debug.LogError($"방 입장 실패: {message} (코드: {returnCode})");
#endif
        }

        public override void OnJoinRandomFailed(short returnCode, string message)
        {
            if (ErrorPanelManager.Instance != null)
            {
                ErrorPanelManager.Instance.ShowError("There are no random rooms available to join..");
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

        public override void OnMasterClientSwitched(Player newMasterClient)
        {
            base.OnMasterClientSwitched(newMasterClient);

            OnMasterChangedCallback?.Invoke(newMasterClient);
        }

        public override void OnDisconnected(DisconnectCause cause)
        {
            Debug.Log($"서버 연결 끊김: {cause}");
        }

        #endregion

        public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
        {
            base.OnPlayerPropertiesUpdate(targetPlayer, changedProps);

            // "IsReady" 키가 변경 목록에 포함되어 있는지 확인
            if (changedProps.ContainsKey(isReady_Customproperty))
            {
                bool newReady = (bool)changedProps[isReady_Customproperty];
#if UNITY_EDITOR
                Debug.Log($"[포톤링크매니저] {targetPlayer.NickName}의 준비 상태가 변경됨: {newReady}");
#endif
                // 준비 상태 변경 이벤트 발행
                OnReadyStateChanged?.Invoke(targetPlayer, newReady);
            }

            // "MyColor" 키가 변경 목록에 포함되어 있는지 확인
            if (changedProps.ContainsKey(myColor_Customproperty))
            {

                int MyColorIndex = (int)changedProps[myColor_Customproperty];

#if UNITY_EDITOR
                Debug.Log($"[포톤링크매니저] {targetPlayer.NickName}의 플레이어 색이 변경됨: {MyColorIndex}");
#endif

                // 준비 상태 변경 이벤트 발행
                OnMyColorChanged?.Invoke(targetPlayer, MyColorIndex);
            }

            // "SceneLoaded" 키가 변경 목록에 포함되어 있는지 확인
            if (changedProps.ContainsKey(sceneLoading_Customproperty))
            {
                bool sceneLoadingState = (bool)changedProps[sceneLoading_Customproperty];
#if UNITY_EDITOR
                Debug.Log($"[포톤링크매니저] {targetPlayer.NickName}의 로딩 씬 상태가 변경됨: {sceneLoadingState}");
#endif

                // 준비 상태 변경 이벤트 발행
                CheckAllPlayerSceneLoaded();
            }

        }

        #region Player Custom Property Edit Methods

        /// <summary>
        /// 내 준비 상태 (대기실 -> 실제 게임 필드)
        /// </summary>
        public void SetMyReadyState(bool isReady)
        {
            Hashtable props = new Hashtable() { { isReady_Customproperty, isReady } };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        }

        /// <summary>
        /// 내 캐릭터 색
        /// </summary>
        public void SetMyColorState(int index)
        {
            Hashtable props = new Hashtable() { { myColor_Customproperty, index } };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        }

        /// <summary>
        /// 씬 로딩이 완료되었는지
        /// </summary>
        public void SetLoadSceneState(bool loaded)
        {
            Hashtable props = new Hashtable() { { sceneLoading_Customproperty, loaded } };
            PhotonNetwork.LocalPlayer.SetCustomProperties(props);
        }

        #endregion

        #region Room Custom Property Edit Methods

        public void SetRoomMapType(string mapName)
        {
            // ExitGames.Client.Photon.Hashtable 사용
            Hashtable props = new Hashtable() { { "GameStarted", mapName } };

            // 현재 방(Room)에 커스텀 속성 설정
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
        }

        #endregion


        #region Photon RPC Method
        [PunRPC]
        private void LoadSceneRPC()
        {
#if UNITY_EDITOR
            Debug.LogError($"모든 플레이어가 준비되었습니다.");
#endif
            OnAllPlayerReadyToNextScene?.Invoke();
        }

        #endregion


        #region PlayerColor 플레이어의 색상을 쉽게 가져오도록 설계

        // 색상 정의 
        public enum PlayerColor
        {
            White = 0,
            Red = 1,
            Orange = 2,
            Yellow = 3,
            Green = 4,
            Blue = 5,
            Indigo = 6,
            Violet = 7,
        }

        // 딕셔너리로 enum과 실제 Unity Color 매핑
        private static readonly Dictionary<PlayerColor, Color> PlayerColorMap = new Dictionary<PlayerColor, Color>()
        {
            { PlayerColor.White,   Color.white },
            { PlayerColor.Red,     Color.red },
            { PlayerColor.Orange,  Color.orange },
            { PlayerColor.Yellow,  Color.yellow },
            { PlayerColor.Green,   Color.green },
            { PlayerColor.Blue,    Color.blue },
            { PlayerColor.Indigo,  Color.indigo },
            { PlayerColor.Violet,  Color.violet },
        };

        /// <summary>
        /// 외부에서 색상을 가져다 쓸 수 있는 메서드
        /// </summary>
        /// <returns>Color 값</returns>
        public static Color GetCurrentColor()
        {
            if (!PhotonNetwork.IsConnected)
                return Color.white;

            if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(myColor_Customproperty, out object obj))
            {
                int index = (int)obj;

                if (PlayerColorMap.TryGetValue((PlayerColor)index, out Color color))
                {
                    return color;
                }
            }

            // 예외 처리 (기본값 흰색 반환)
            return Color.white;
        }

        #endregion
    }
}