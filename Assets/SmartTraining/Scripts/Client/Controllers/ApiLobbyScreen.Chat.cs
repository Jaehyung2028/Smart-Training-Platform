using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SmartTraining
{
    [Serializable]
    public sealed class LobbyChatMessage
    {
        public string id, lobbyId, roomId, userId, nickname, text, sentAt;
        public long sequence;
    }

    public sealed partial class ApiLobbyScreen
    {
        private readonly List<LobbyChatMessage> chatMessages = new List<LobbyChatMessage>();
        private string chatLobbyId = "", chatInput = "", chatStatus = "";

        private void SyncChatLobby()
        {
            var current = api?.IsLoggedIn == true ? api.User.currentLobbyId ?? "" : "";
            if (current != chatLobbyId)
            {
                ClearChat();
                chatLobbyId = current;
            }
            SyncChatRoom();
        }
        private void ClearChat()
        {
            chatMessages.Clear();
            chatLobbyId = "";
            chatInput = "";
            chatStatus = "";
            ClearRoomChat();
        }
        private void ReceiveLobbyChat(RoomHubConnection connection, string json)
        {
            if (lobbyUpdates != connection || api != liveApi || api?.IsLoggedIn != true)
                return;
            SyncChatLobby();
            var message = JsonUtility.FromJson<LobbyChatMessage>(json);
            if (message == null || message.lobbyId != chatLobbyId || string.IsNullOrEmpty(message.id) ||
                chatMessages.Exists(item => item.id == message.id))
                return;
            chatMessages.Add(message);
            if (chatMessages.Count > 100)
                chatMessages.RemoveAt(0);
        }
        private IEnumerator SendLobbyChat()
        {
            SyncChatLobby();
            var connection = lobbyUpdates;
            var lobby = chatLobbyId;
            var text = chatInput.Trim();
            if (text.Length == 0)
            {
                chatStatus = "메시지를 입력해주세요.";
                yield break;
            }
            if (connection == null || liveConnecting || liveLobbyId != lobby || lobby != api?.User?.currentLobbyId)
            {
                chatStatus = "로비 연결을 확인해주세요.";
                yield break;
            }
            var send = connection.Invoke("SendChat", text);
            while (!send.IsCompleted && lobbyUpdates == connection)
            {
                connection.Tick();
                yield return null;
            }
            if (lobbyUpdates != connection || send.IsCanceled || send.IsFaulted)
            {
                _ = send.Exception;
                chatStatus = "전송 결과를 확인하지 못했습니다. 연결을 확인해주세요.";
                yield break;
            }
            chatStatus = send.Result.message;
            if (send.Result.success)
                chatInput = "";
        }
    }
}
