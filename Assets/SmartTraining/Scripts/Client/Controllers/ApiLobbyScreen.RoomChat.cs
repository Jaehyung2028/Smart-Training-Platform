using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SmartTraining
{
    public sealed partial class ApiLobbyScreen
    {
        private readonly List<LobbyChatMessage> roomChatMessages = new List<LobbyChatMessage>();
        private string chatRoomId = "", roomChatBoundRoomId = "", chatAdmissionToken = "";
        private RoomNetworkClient chatNetwork, roomChatBoundNetwork;
        private RoomHubConnection roomChatConnection;
        // 구독 응답보다 그룹 메시지가 먼저 도착할 수 있어, 기준 번호를 받은 뒤 확인합니다.
        private readonly List<LobbyChatMessage> pendingRoomChatMessages = new List<LobbyChatMessage>();
        private long roomChatAfterSequence = -1;
        private int chatChannel;
        private float nextRoomChatAttempt;

        private void ClearRoomChat()
        {
            roomChatMessages.Clear();
            chatRoomId = "";
            chatAdmissionToken = "";
            chatNetwork = null;
            chatChannel = 0;
            roomChatBoundRoomId = "";
            roomChatConnection = null;
            roomChatBoundNetwork = null;
            pendingRoomChatMessages.Clear();
            roomChatAfterSequence = -1;
            nextRoomChatAttempt = 0;
        }


        private void SyncChatRoom()
        {
            var current = network?.IsConnected == true && network.LocalPlayer != null && network.LocalPlayer.UserId == api?.User?.id ? network : null;
            var room = current != null ? networkRoomId : "";
            var admission = current?.AdmissionToken ?? "";
            if (room == chatRoomId && current == chatNetwork && admission == chatAdmissionToken)
            {
                return;
            }

            roomChatMessages.Clear();
            chatRoomId = room;
            chatNetwork = current;
            chatAdmissionToken = admission;
            nextRoomChatAttempt = 0;
            pendingRoomChatMessages.Clear();
            roomChatAfterSequence = -1;
            if (chatChannel == 1)
            {
                chatInput = "";
                chatStatus = "";
            }

            chatChannel = 0;
        }


        private bool RoomChatIsBound() => chatRoomId.Length > 0 &&
            roomChatBoundRoomId == chatRoomId &&
            roomChatConnection == lobbyUpdates &&
            roomChatBoundNetwork == chatNetwork &&
            roomChatAfterSequence >= 0;

        private bool RoomChatNeedsBinding() => lobbyUpdates != null &&
            lobbyUpdates.Failure == null &&
            liveLobbyId.Length > 0 &&
            Time.realtimeSinceStartup >= nextRoomChatAttempt &&
            (roomChatBoundRoomId != chatRoomId ||
            (chatRoomId.Length > 0 &&
            !RoomChatIsBound()));

        private IEnumerator BindRoomChat()
        {
            SyncChatLobby();
            var connection = lobbyUpdates;
            var current = chatNetwork;
            var room = chatRoomId;
            if (connection == null)
            {
                yield break;
            }

            var admission = current?.AdmissionToken ?? "";
            roomChatAfterSequence = -1;
            pendingRoomChatMessages.Clear();
            var subscribe = connection.Invoke("SubscribeRoomChat", room, admission);
            while (!subscribe.IsCompleted && lobbyUpdates == connection)
            {
                connection.Tick();
                yield return null;
            }

            if (lobbyUpdates != connection ||
                current != chatNetwork ||
                room != chatRoomId ||
                admission != (current?.AdmissionToken ?? ""))
            {
                yield break;
            }

            if (!subscribe.IsCompleted || subscribe.IsCanceled || subscribe.IsFaulted || !subscribe.Result.success)
            {
                _ = subscribe.Exception;
                pendingRoomChatMessages.Clear();
                nextRoomChatAttempt = Time.realtimeSinceStartup + 3;
                yield break;
            }

            roomChatConnection = connection;
            roomChatBoundNetwork = current;
            roomChatBoundRoomId = room;
            CompleteRoomChatBinding(subscribe.Result.chatSequence);
        }


        private void CompleteRoomChatBinding(long sequence)
        {
            roomChatAfterSequence = sequence;
            foreach (var message in pendingRoomChatMessages)
            {
                AcceptRoomChat(message);
            }

            pendingRoomChatMessages.Clear();
        }


        // 그룹 메시지에 담긴 방 ID가 현재 접속한 방인지 확인합니다.
        private void ReceiveRoomChat(RoomHubConnection connection, string json, string roomId)
        {
            if (lobbyUpdates != connection || api != liveApi || api?.IsLoggedIn != true)
            {
                return;
            }

            SyncChatLobby();
            if (chatNetwork == null || roomId != chatRoomId)
            {
                return;
            }

            var message = TrainingJson.Deserialize<LobbyChatMessage>(json);
            if (message == null ||
                message.roomId != chatRoomId ||
                message.lobbyId != chatLobbyId ||
                string.IsNullOrEmpty(message.id))
            {
                return;
            }

            if (roomChatAfterSequence < 0)
            {
                pendingRoomChatMessages.Add(message);
                if (pendingRoomChatMessages.Count > 100)
                {
                    pendingRoomChatMessages.RemoveAt(0);
                }

                return;
            }

            AcceptRoomChat(message);
        }


        private void AcceptRoomChat(LobbyChatMessage message)
        {
            // 같은 방을 재입장해도 이전 구독 시점에 생성된 메시지는 표시하지 않습니다.
            if (message.sequence <= roomChatAfterSequence || roomChatMessages.Exists(item => item.id == message.id))
            {
                return;
            }

            roomChatMessages.Add(message);
            if (roomChatMessages.Count > 100)
            {
                roomChatMessages.RemoveAt(0);
            }
        }


        internal IReadOnlyList<LobbyChatMessage> UiContentRoomMessages => roomChatMessages;

        internal bool UiCanSendContentChat => UiRoomCanAct && UiCanChat && RoomChatIsBound() && UiLocalPlayer.RoomIsRunning;

        internal string UiContentChatStatus => UiChatConnected && RoomChatIsBound() ? chatStatus : "방 채팅 연결 중…";


        internal void UiSendContentChat(string text, System.Action<bool> completed)
        {
            if (!UiCanSendContentChat)
            {
                completed(false);
                return;
            }

            RunOperation(SendRoomChat(text, completed), "채팅을 전송하고 있습니다.");
        }


        private IEnumerator SendRoomChat()
        {
            var draft = chatInput;
            return SendRoomChat(draft, success =>
            {
                if (success && chatInput == draft)
                {
                    chatInput = "";
                }
            });
        }


        // 방 채널의 전송 경로를 대기실과 콘텐츠가 함께 사용합니다. 초안은 각 화면에서 소유합니다.
        private IEnumerator SendRoomChat(string draft, System.Action<bool> completed)
        {
            bool succeeded = false;
            try
            {
                SyncChatLobby();
                if (!RoomChatIsBound())
                {
                    chatStatus = "방 채팅 연결을 확인해주세요.";
                    yield break;
                }

                var text = draft.Trim();
                if (text.Length == 0)
                {
                    chatStatus = "메시지를 입력해주세요.";
                    yield break;
                }

                var connection = lobbyUpdates;
                var current = chatNetwork;
                var admission = current.AdmissionToken;
                var room = chatRoomId;
                var send = connection.Invoke("SendRoomChat", room, admission, text);
                while (!send.IsCompleted && lobbyUpdates == connection)
                {
                    connection.Tick();
                    yield return null;
                }

                if (lobbyUpdates != connection ||
                    current != chatNetwork ||
                    room != chatRoomId ||
                    admission != (current?.AdmissionToken ?? ""))
                {
                    yield break;
                }

                if (!send.IsCompleted || send.IsCanceled || send.IsFaulted)
                {
                    _ = send.Exception;
                    chatStatus = "전송 결과를 확인하지 못했습니다. 연결을 확인해주세요.";
                    yield break;
                }

                chatStatus = send.Result.message;
                succeeded = send.Result.success;
            }
            finally
            {
                completed?.Invoke(succeeded);
            }
        }
    }
}
