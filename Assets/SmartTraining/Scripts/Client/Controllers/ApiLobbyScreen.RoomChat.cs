using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SmartTraining
{
    public sealed partial class ApiLobbyScreen
    {
        private readonly List<LobbyChatMessage> roomChatMessages = new List<LobbyChatMessage>();
        private string chatRoomId = "", roomChatBoundRoomId = "", roomChatBindingId = "";
        private RoomNetworkClient chatNetwork, roomChatBoundNetwork;
        private RoomHubConnection roomChatConnection;
        private int chatChannel;
        private float nextRoomChatAttempt;

        private void ClearRoomChat()
        {
            roomChatMessages.Clear(); chatRoomId = ""; chatNetwork = null; chatChannel = 0;
            roomChatBoundRoomId = ""; roomChatBindingId = ""; roomChatConnection = null; roomChatBoundNetwork = null;
            nextRoomChatAttempt = 0;
        }
        private void SyncChatRoom()
        {
            var current = network?.IsConnected == true && network.LocalPlayer != null && network.LocalPlayer.UserId == api?.User?.id ? network : null;
            var room = current != null ? networkRoomId : "";
            if (room == chatRoomId && current == chatNetwork) return;
            roomChatMessages.Clear(); chatRoomId = room; chatNetwork = current;
            roomChatBindingId = ""; nextRoomChatAttempt = 0;
            if (chatChannel == 1) { chatInput = ""; chatStatus = ""; }
            chatChannel = 0;
        }
        private bool RoomChatIsBound() => chatRoomId.Length > 0 && roomChatBoundRoomId == chatRoomId &&
            roomChatConnection == lobbyUpdates && roomChatBoundNetwork == chatNetwork && roomChatBindingId.Length > 0;
        private bool RoomChatNeedsBinding() => lobbyUpdates != null && lobbyUpdates.Failure == null && liveLobbyId.Length > 0 &&
            Time.realtimeSinceStartup >= nextRoomChatAttempt && (roomChatBoundRoomId != chatRoomId ||
                (chatRoomId.Length > 0 && !RoomChatIsBound()));

        private IEnumerator BindRoomChat()
        {
            SyncChatLobby();
            var connection = lobbyUpdates; var current = chatNetwork; var room = chatRoomId;
            if (connection == null) yield break;
            var binding = room.Length > 0 ? Guid.NewGuid().ToString("N") : "";
            roomChatBindingId = binding;
            var subscribe = connection.Invoke("SubscribeRoomChat", room, current?.AdmissionToken ?? "", binding);
            while (!subscribe.IsCompleted && lobbyUpdates == connection) { connection.Tick(); yield return null; }
            if (lobbyUpdates != connection || current != chatNetwork || room != chatRoomId) yield break;
            if (!subscribe.IsCompleted || subscribe.IsCanceled || subscribe.IsFaulted || !subscribe.Result.success)
            { _ = subscribe.Exception; roomChatBindingId = ""; nextRoomChatAttempt = Time.realtimeSinceStartup + 3; yield break; }
            roomChatConnection = connection; roomChatBoundNetwork = current; roomChatBoundRoomId = room;
        }
        private void ReceiveRoomChat(RoomHubConnection connection, string json, string binding)
        {
            if (lobbyUpdates != connection || api != liveApi || api?.IsLoggedIn != true) return;
            SyncChatLobby();
            if (chatNetwork == null || binding != roomChatBindingId || binding.Length == 0) return;
            var message = JsonUtility.FromJson<LobbyChatMessage>(json);
            if (message == null || message.roomId != chatRoomId || message.lobbyId != chatLobbyId || string.IsNullOrEmpty(message.id) ||
                roomChatMessages.Exists(item => item.id == message.id)) return;
            roomChatMessages.Add(message);
            if (roomChatMessages.Count > 100) roomChatMessages.RemoveAt(0);
        }
        private IEnumerator SendRoomChat()
        {
            SyncChatLobby();
            if (!RoomChatIsBound()) { chatStatus = "방 채팅 연결을 확인해주세요."; yield break; }
            var text = chatInput.Trim();
            if (text.Length == 0) { chatStatus = "메시지를 입력해주세요."; yield break; }
            var connection = lobbyUpdates; var current = chatNetwork; var binding = roomChatBindingId;
            var send = connection.Invoke("SendRoomChat", chatRoomId, current.AdmissionToken, binding, text);
            while (!send.IsCompleted && lobbyUpdates == connection) { connection.Tick(); yield return null; }
            if (lobbyUpdates != connection || current != chatNetwork || binding != roomChatBindingId) yield break;
            if (!send.IsCompleted || send.IsCanceled || send.IsFaulted)
            { _ = send.Exception; chatStatus = "전송 결과를 확인하지 못했습니다. 연결을 확인해주세요."; yield break; }
            chatStatus = send.Result.message;
            if (send.Result.success) chatInput = "";
        }
    }
}
