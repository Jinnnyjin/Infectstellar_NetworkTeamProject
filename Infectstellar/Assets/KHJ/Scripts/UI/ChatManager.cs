using UnityEngine;
using TMPro;
using Photon.Pun;
using System.Collections.Generic;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;

namespace ENZEUN.Runtime
{
    public class ChatManager : MonoBehaviourPunCallbacks
    {
        [SerializeField] private TMP_InputField chatInputField;
        [SerializeField] private TextMeshProUGUI chatLogText; // 채팅 기록을 보여줄 UI

        private const int MaxChatLines = 15; // 최대 채팅 줄 수


        private void Start()
        {
            InitializeChat();
        }

        public void InitializeChat()
        {
            if (chatInputField != null)
            {
                // 기존 리스너 중복 등록 방지
                chatInputField.onSubmit.RemoveListener(SubmitChat);
                chatInputField.onSubmit.AddListener(SubmitChat);
            }
        }

        private async void SubmitChat(string chat)
        {
            // 빈 칸이거나 공백만 있는 경우 전송 안 함
            if (string.IsNullOrWhiteSpace(chat)) return;

            // 방에 입장해 있는 상태에서만 전송
            if (PhotonNetwork.InRoom)
            {
                // 1. 내 닉네임 가져오기 (포톤 닉네임)
                string senderName = PhotonNetwork.NickName;

                // 2. RPC를 통해 방 안의 모든 유저(나 포함)에게 채팅 전송
                photonView.RPC(nameof(RPC_ReceiveChat), RpcTarget.All, senderName, chat);
            }

            // 3. 입력창 비우기 및 포커스 유지 (연속 채팅용)
            chatInputField.text = "";

            await UniTask.DelayFrame(3);

            chatInputField.ActivateInputField();

            chatInputField.Select();
        }

        /// <summary>
        /// 채팅 수신 메서드
        /// </summary>
        [PunRPC]
        private void RPC_ReceiveChat(string senderName, string message, PhotonMessageInfo info)
        {
            Debug.Log($"[채팅] {senderName}: {message}");

            // UI 텍스트에 채팅 로그 추가 (chatLogText가 연결되어 있을 때만)
            if (chatLogText != null)
            {
                AddChatMessage(senderName, message);
            }
        }

        private void AddChatMessage(string senderName, string message)
        {
            string newLog = $"<b>{senderName}</b>: {message}";

            // 기존 텍스트를 줄바꿈 기준으로 쪼개기
            var lines = new List<string>(chatLogText.text.Split('\n'));

            // 빈 줄 제거 등 정리 후 새 메시지 추가
            lines.Add(newLog);

            // 최대 줄 수를 넘어가면 위에서부터 삭제
            if (lines.Count > MaxChatLines)
            {
                lines.RemoveRange(0, lines.Count - MaxChatLines);
            }

            // 다시 줄바꿈으로 합치기
            chatLogText.text = string.Join("\n", lines);
        }

        public override void OnDisable()
        {
            // 메모리 누수 방지를 위한 리스너 해제
            if (chatInputField != null)
            {
                chatInputField.onSubmit.RemoveListener(SubmitChat);
            }
        }
    }
}