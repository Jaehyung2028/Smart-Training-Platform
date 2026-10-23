using System;
using System.Collections;
using UnityEngine;

namespace SmartTraining
{
    public sealed partial class ApiLobbyScreen
    {
        private RoomHubConnection lobbyUpdates;
        private string liveLobbyId = "";
        private TrainingApiClient liveApi;
        private bool liveConnecting, roomsDirty;
        private float nextLiveAttempt;
#if UNITY_EDITOR
        // Test driver shares this screen's API client while exercising unrelated controls.
        private bool testPauseLobbyUpdates;
#endif

        private void DisposeLobbyUpdates()
        {
            lobbyUpdates?.Dispose();
            lobbyUpdates = null;
            liveApi = null;
            liveLobbyId = "";
            liveConnecting = false;
            roomsDirty = false;
            roomChatConnection = null;
            roomChatBoundRoomId = "";
        }
        private void UpdateLobbyRooms()
        {
            SyncParticipantsContext();
            SyncChatLobby();
#if UNITY_EDITOR
            if (testPauseLobbyUpdates) { DisposeLobbyUpdates(); return; }
#endif
            ObserveConnectionSession();
            string target = api?.IsLoggedIn == true ? api.User.currentLobbyId ?? "" : "";
            if (lobbyUpdates != null && (liveApi != api || liveLobbyId != target))
            {
                DisposeLobbyUpdates();
                nextLiveAttempt = 0;
            }
            if (api?.IsLoggedIn != true)
            {
                if (connectionSessionObserved)
                    EndServerSession();
                else
                    DisposeLobbyUpdates();
                return;
            }
            lobbyUpdates?.Tick();
            if (lobbyUpdates?.Failure != null)
            {
                ScheduleLiveRetry("서버 알림 연결이 끊겼습니다.");
            }
            if (!busy && !liveConnecting && RoomChatNeedsBinding())
            {
                Run(BindRoomChat());
                return;
            }
            if (!busy && !liveConnecting && lobbyUpdates == null && Time.realtimeSinceStartup >= nextLiveAttempt)
                Run(ConnectLobbyRooms(target));
            else if (!busy && roomsDirty && !liveConnecting)
                Run(RefreshLiveRooms(target));
        }
        private IEnumerator ConnectLobbyRooms(string lobby)
        {
            ObserveConnectionSession();
            liveConnecting = true;
            serverConnectionState = ServerConnectionState.Connecting;
            bool valid = false;
            string error = null;
            yield return api.GetMe(_ => valid = true, message => error = message);
            if (valid && lobby.Length > 0 && api.User.currentLobbyId != lobby)
            {
                ForgetReconnect();
                DisposeRoomConnection();
                selectedLobbyId = "";
                ClearLobbyData();
                yield return LoadState();
                status = "로비 이용이 종료되어 로비 목록으로 돌아왔습니다.";
                ScheduleLiveRetry(status);
                yield break;
            }
            if (!valid)
            {
                ScheduleLiveRetry(error ?? "현재 로비 정보가 변경되었습니다. 다시 연결합니다.");
                yield break;
            }
            SyncChatLobby();
            liveApi = api;
            liveLobbyId = lobby;
            liveConnecting = true;
            var connection = lobbyUpdates = new RoomHubConnection();
            connection.LobbyChat += json => ReceiveLobbyChat(connection, json);
            connection.RoomChat += (json, roomId) => ReceiveRoomChat(connection, json, roomId);
            connection.RoomsChanged += id => { if (id == liveLobbyId) roomsDirty = true; };
            connection.SubscriptionEnded += _ => connection.Dispose();
            var connect = api.ConnectLobbyUpdates(connection);
            double deadline = Time.realtimeSinceStartupAsDouble + 12;
            while (!connect.IsCompleted && lobbyUpdates == connection && Time.realtimeSinceStartupAsDouble < deadline)
            {
                connection.Tick();
                yield return null;
            }
            if (lobbyUpdates != connection)
                yield break;
            if (!connect.IsCompleted || connect.IsFaulted || connect.IsCanceled)
            {
                _ = connect.Exception;
                ScheduleLiveRetry("서버 알림 연결을 열지 못했습니다.");
                yield break;
            }
            var subscribe = connection.Invoke("Subscribe", lobby);
            while (!subscribe.IsCompleted && lobbyUpdates == connection)
            {
                connection.Tick();
                yield return null;
            }
            if (lobbyUpdates != connection)
                yield break;
            if (subscribe.IsFaulted || subscribe.IsCanceled || !subscribe.Result.success)
            {
                ScheduleLiveRetry("현재 로비의 알림을 등록하지 못했습니다.");
                yield break;
            }
            liveConnecting = false;
            roomsDirty = true;
            serverConnectionState = ServerConnectionState.Connected;
            serverReconnectAttempts = 0;
        }
        private IEnumerator RefreshLiveRooms(string lobby)
        {
            roomsDirty = false;
            bool loaded = false;
            yield return api.GetLobbies(items => { lobbies = items; loaded = true; }, ScheduleLiveRetry);
            if (!loaded || string.IsNullOrEmpty(lobby))
                yield break;
            yield return LoadLobbyParticipants(lobby);
            if (api != liveApi || liveLobbyId != lobby)
                yield break;
            yield return api.GetRooms(lobby, items =>
            {
                if (api != liveApi || selectedLobbyId != lobby)
                    return;
                rooms = items;
                selectedRoom = Array.Find(rooms, item => item.id == selectedRoom?.id);
            }, ScheduleLiveRetry);
        }
    }
}
