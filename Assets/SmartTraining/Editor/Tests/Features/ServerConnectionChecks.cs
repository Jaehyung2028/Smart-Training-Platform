using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SmartTraining.Editor
{
    public static class ServerConnectionChecks
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

        private static object Get(ApiLobbyScreen screen, string field) => typeof(ApiLobbyScreen).GetField(field, Flags).GetValue(screen);

        private static void Set(ApiLobbyScreen screen, string field, object value) => typeof(ApiLobbyScreen).GetField(field, Flags).SetValue(screen, value);

        private static string State(ApiLobbyScreen screen) => Get(screen, "serverConnectionState").ToString();

        private static string StatusText(ApiLobbyScreen screen) => (string)typeof(ApiLobbyScreen).GetMethod("ServerConnectionStatusText", Flags).Invoke(screen, null);

        private static int ChatCount(ApiLobbyScreen screen, string field) => ((List<LobbyChatMessage>)Get(screen, field)).Count;

        private static void Run(ApiLobbyScreen screen, string method)
        {
            var routine = (IEnumerator)typeof(ApiLobbyScreen).GetMethod(method, Flags).Invoke(screen, null);
            typeof(ApiLobbyScreen).GetMethod("Run", Flags).Invoke(screen, new object[] { routine });
        }

        private static bool Idle(ApiLobbyScreen screen, TrainingApiClient api) => !(bool)Get(screen, "busy") && typeof(TrainingApiClient).GetField("activeRequest", Flags).GetValue(api) == null;

        private static IEnumerator Wait(Func<bool> condition, string name)
        {
            double end = Time.realtimeSinceStartupAsDouble + 20;
            while (!condition())
            {
                if (Time.realtimeSinceStartupAsDouble > end)
                {
                    throw new Exception(name);
                }

                yield return null;
            }
        }

        private static void Check(bool ok, string name)
        {
            if (!ok)
            {
                throw new InvalidOperationException(name);
            }

            Debug.Log("ROOM PASS: " + name);
        }

        public static IEnumerator Run(string url, string occupiedNickname)
        {
            Action<string> fail = error => throw new InvalidOperationException(error);
            var user = new TrainingApiClient(url);
            var revoker = new TrainingApiClient(url);
            var network = new RoomNetworkClient();
            ApiLobbyScreen screen = null;
            string nickname = "scu-" + Guid.NewGuid().ToString("N").Substring(0, 6);
            try
            {
                yield return user.Login(nickname, () =>
                {
                }, fail);
                string lobby = null, room = null;
                yield return CatalogLobbyTestSupport.Select(user, id => lobby = id, fail);
                yield return user.JoinLobby(lobby, () =>
                {
                }, fail);
                yield return user.CreateRoom(lobby, "connection-room", lobby, 1, id => room = id, fail);
                JoinRoomResponse admission = null;
                yield return user.JoinRoom(lobby, room, value => admission = value, fail);
                yield return network.Connect(user.User.id, admission, () =>
                {
                }, fail);
                yield return user.GetMe(_ =>
                {
                }, fail);
                screen = new GameObject("connection-status-test").AddComponent<ApiLobbyScreen>();
                Set(screen, "api", user);
                Set(screen, "serverUrl", url);
                Set(screen, "nickname", nickname);
                Set(screen, "network", network);
                Set(screen, "networkRoomId", room);
                Set(screen, "networkLobbyId", lobby);
                Set(screen, "selectedLobbyId", lobby);
                yield return Wait(
                    () => State(screen) == "Connected" && (bool)typeof(ApiLobbyScreen).GetMethod("RoomChatIsBound", Flags).Invoke(screen, null) && Idle(screen, user),
                    "connection banner did not reach connected state"
                );
                Check(
                    StatusText(screen).Contains("연결됨") && user.IsLoggedIn && network.IsConnected,
                    "connection banner reports authenticated notification connection alongside Netcode"
                );
                while (!Idle(screen, user))
                {
                    yield return null;
                }

                Set(screen, "chatInput", "keep lobby display");
                Run(screen, "SendLobbyChat");
                yield return Wait(
                    () => ChatCount(screen, "chatMessages") == 1 && Idle(screen, user),
                    "lobby chat did not populate connection test"
                );
                double end = Time.realtimeSinceStartupAsDouble + 1.1;
                while (Time.realtimeSinceStartupAsDouble < end)
                {
                    yield return null;
                }

                while (!Idle(screen, user))
                {
                    yield return null;
                }

                Set(screen, "chatInput", "keep room display");
                Run(screen, "SendRoomChat");
                yield return Wait(
                    () => ChatCount(screen, "roomChatMessages") == 1 && Idle(screen, user),
                    "room chat did not populate connection test"
                );
                var previous = (RoomHubConnection)Get(screen, "lobbyUpdates");
                previous.Dispose();
                yield return Wait(() => State(screen) == "RetryWaiting", "transport loss did not enter retry state");
                Check(
                    StatusText(screen).Contains("재시도") && (int)Get(screen, "serverReconnectAttempts") == 1,
                    "notification transport failure displays retry countdown and attempt count"
                );
                Check(
                    user.IsLoggedIn && network.IsConnected && ChatCount(screen, "chatMessages") == 1 && ChatCount(screen, "roomChatMessages") == 1,
                    "transient notification loss preserves login Netcode and chat display"
                );
                while (!Idle(screen, user))
                {
                    yield return null;
                }

                Run(screen, "RetryLiveConnection");
                yield return Wait(
                    () => State(screen) == "Connected" && Get(screen, "lobbyUpdates") != previous && (bool)typeof(ApiLobbyScreen).GetMethod("RoomChatIsBound", Flags).Invoke(screen, null) && Idle(screen, user),
                    "manual reconnect did not restore room subscription"
                );
                Check(
                    (int)Get(screen, "serverReconnectAttempts") == 0 && network.IsConnected && ((LobbyParticipant[])Get(screen, "lobbyParticipants")).Length == 1,
                    "manual reconnect restores current snapshot and resets retry count"
                );
                // Revoke the same server session from an independent API client. The UI
                // client still has its cached token, just as after an in-memory server restart.
                foreach (var field in new[]
                {
                    "accessToken",
                    "expiresAt"
                }

                )
                {
                    typeof(TrainingApiClient).GetField(field, Flags).SetValue(revoker, typeof(TrainingApiClient).GetField(field, Flags).GetValue(user));
                }

                Set(screen, "editRoomName", "old-editor-name");
                Set(screen, "editRoomCapacity", "99");
                Set(screen, "editRoomPassword", "old-input");
                Set(screen, "nextLiveAttempt", Time.realtimeSinceStartup + 60);
                yield return revoker.Logout(() =>
                {
                }, fail);
                yield return Wait(
                    () => State(screen) == "SessionEnded" && !user.IsLoggedIn && Get(screen, "network") == null,
                    "invalidated auth session did not clear old network state"
                );
                Check(StatusText(screen).Contains("다시 로그인"), "auth session rejection displays explicit login guidance");
                Check(
                    ((LobbyParticipant[])Get(screen, "lobbyParticipants")).Length == 0 && ((RoomSummary[])Get(screen, "rooms")).Length == 0 && ChatCount(screen, "chatMessages") == 0 && ChatCount(screen, "roomChatMessages") == 0 && (string)Get(screen, "selectedLobbyId") == "",
                    "auth session rejection clears old room participant and chat identities"
                );
                Check(
                    (string)Get(screen, "networkRoomId") == "" && (string)Get(screen, "reconnectRoomId") == "" && Get(screen, "lobbyUpdates") == null,
                    "auth session rejection removes stale room reconnect target and notification connection"
                );
                Check(
                    (string)Get(screen, "editRoomName") == "" && (string)Get(screen, "editRoomCapacity") == "" && (string)Get(screen, "editRoomPassword") == "",
                    "auth session rejection clears old room editor and password inputs"
                );
                while (!Idle(screen, user))
                {
                    yield return null;
                }

                Set(screen, "nickname", occupiedNickname);
                Run(screen, "Login");
                yield return Wait(
                    () => Idle(screen, user) && !user.IsLoggedIn && ((string)Get(screen, "status")).Contains("닉네임"),
                    "login rejection detail was hidden after session ended"
                );
                Check(
                    StatusText(screen).Contains("다시 로그인"),
                    "session ended banner preserves nickname rejection details during new login attempt"
                );
                Set(screen, "nickname", nickname);
                while (!Idle(screen, user))
                {
                    yield return null;
                }

                Run(screen, "Login");
                yield return Wait(
                    () => user.IsLoggedIn && State(screen) == "Connected" && Idle(screen, user),
                    "fresh login did not restore notification connection"
                );
                Check(
                    user.User.currentRoomId == "" && user.User.currentLobbyId == "" && StatusText(screen).Contains("연결됨"),
                    "fresh login restores global notifications without reusing old membership"
                );
                while (!Idle(screen, user))
                {
                    yield return null;
                }

                Run(screen, "Logout");
                yield return Wait(
                    () => !user.IsLoggedIn && State(screen) == "LoginRequired" && Idle(screen, user),
                    "explicit logout did not reset connection banner"
                );
                Check(
                    !StatusText(screen).Contains("세션 종료"),
                    "explicit logout resets connection banner without reporting authentication failure"
                );
                while (!Idle(screen, user))
                {
                    yield return null;
                }

                Set(screen, "testPauseLobbyUpdates", true);
                yield return user.Login(nickname, () =>
                {
                }, fail);
                yield return CatalogLobbyTestSupport.Select(user, id => lobby = id, fail);
                yield return user.JoinLobby(lobby, () =>
                {
                }, fail);
                Set(screen, "selectedLobbyId", lobby);
                string progressRoom = null;
                yield return user.CreateRoom(lobby, "progress-room", lobby, 1, id => progressRoom = id, fail);
                var join = (IEnumerator)typeof(ApiLobbyScreen).GetMethod("JoinRoom", Flags).Invoke(screen, new object[] { progressRoom, null });
                typeof(ApiLobbyScreen).GetMethod("Run", Flags).Invoke(screen, new object[] { join });
                Check(
                    ((string)Get(screen, "roomJoinProgress")).StartsWith("1/4"),
                    "room join immediately shows reservation progress"
                );
                yield return Wait(() => Idle(screen, user), "room join progress did not finish");
                var joinedNetwork = (RoomNetworkClient)Get(screen, "network");
                Check(
                    joinedNetwork?.IsConnected == true && joinedNetwork.LocalPlayer != null && user.User.currentRoomId == progressRoom && (string)Get(screen, "roomJoinProgress") == "",
                    "room join progress clears only after server confirmation and player readiness"
                );
                var leave = (IEnumerator)typeof(ApiLobbyScreen).GetMethod("LeaveRoom", Flags).Invoke(screen, new object[] { "test leave" });
                typeof(ApiLobbyScreen).GetMethod("Run", Flags).Invoke(screen, new object[] { leave });
                yield return Wait(() => Idle(screen, user), "progress test leave failed");
                join = (IEnumerator)typeof(ApiLobbyScreen).GetMethod("JoinRoom", Flags).Invoke(screen, new object[] { "missing-room", null });
                typeof(ApiLobbyScreen).GetMethod("Run", Flags).Invoke(screen, new object[] { join });
                yield return Wait(() => Idle(screen, user), "failed room join progress did not finish");
                Check(
                    (string)Get(screen, "roomJoinProgress") == "" && Get(screen, "network") == null && user.User.pendingRoomId == "",
                    "failed reservation clears progress and permits another action"
                );
            }
            finally
            {
                if (screen != null)
                {
                    ((RoomHubConnection)Get(screen, "lobbyUpdates"))?.Dispose();
                    ((RoomNetworkClient)Get(screen, "network"))?.Dispose();
                    Set(screen, "network", null);
                    Set(screen, "api", null);
                    screen.enabled = false;
                    UnityEngine.Object.DestroyImmediate(screen.gameObject);
                }

                network.Dispose();
                revoker.Dispose();
                user.LogoutOnShutdown();
                user.Dispose();
            }
        }
    }
}
