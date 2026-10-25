using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SmartTraining.Editor
{
    public static class LobbyChatChecks
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

        private static object Get(ApiLobbyScreen screen, string field) => typeof(ApiLobbyScreen).GetField(field, Flags).GetValue(screen);

        private static void Set(ApiLobbyScreen screen, string field, object value) => typeof(ApiLobbyScreen).GetField(field, Flags).SetValue(screen, value);

        private static IEnumerator Call(ApiLobbyScreen screen, string method, params object[] args) => (IEnumerator)typeof(ApiLobbyScreen).GetMethod(method, Flags).Invoke(screen, args);

        private static void Check(bool ok, string name)
        {
            if (!ok)
            {
                throw new InvalidOperationException(name);
            }

            Debug.Log("ROOM PASS: " + name);
        }

        private static List<LobbyChatMessage> Messages(ApiLobbyScreen screen) => (List<LobbyChatMessage>)Get(screen, "chatMessages");

        private static RoomHubConnection Connection(ApiLobbyScreen screen) => (RoomHubConnection)Get(screen, "lobbyUpdates");

        private static ApiLobbyScreen Screen(TrainingApiClient api, string lobby)
        {
            var screen = new GameObject("chat-test").AddComponent<ApiLobbyScreen>();
            screen.enabled = false; // The test drives the same button coroutines and main-thread ticks explicitly.
            Set(screen, "api", api);
            Set(screen, "selectedLobbyId", lobby);
            typeof(ApiLobbyScreen).GetMethod("SyncChatLobby", Flags).Invoke(screen, null);
            return screen;
        }

        private static IEnumerator Pump(ApiLobbyScreen a, ApiLobbyScreen b, RoomHubConnection foreign, double seconds)
        {
            double end = Time.realtimeSinceStartupAsDouble + seconds;
            while (Time.realtimeSinceStartupAsDouble < end)
            {
                Connection(a)?.Tick();
                Connection(b)?.Tick();
                foreign.Tick();
                yield return null;
            }
        }

        public static IEnumerator Run(TrainingApiClient first, TrainingApiClient second, string lobby)
        {
            Action<string> fail = error => throw new InvalidOperationException(error);
            var stranger = new TrainingApiClient(first.BaseUrl);
            var foreign = new RoomHubConnection();
            ApiLobbyScreen a = null, b = null;
            try
            {
                yield return stranger.Login("chat-" + Guid.NewGuid().ToString("N").Substring(0, 6), () =>
                {
                }, fail);
                string foreignLobby = null;
                yield return CatalogLobbyTestSupport.Select(stranger, id => foreignLobby = id, fail, lobby);
                yield return stranger.JoinLobby(foreignLobby, () =>
                {
                }, fail);
                a = Screen(first, lobby);
                b = Screen(second, lobby);
                yield return Call(a, "ConnectLobbyRooms", lobby);
                yield return Call(b, "ConnectLobbyRooms", lobby);
                Check(
                    Connection(a) != null && !(bool)Get(a, "liveConnecting") && Connection(b) != null && !(bool)Get(b, "liveConnecting"),
                    "chat screens establish authenticated SignalR subscriptions"
                );
                var connect = stranger.ConnectLobbyUpdates(foreign);
                double deadline = Time.realtimeSinceStartupAsDouble + 15;
                while (!connect.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline)
                {
                    foreign.Tick();
                    yield return null;
                }

                Check(connect.IsCompletedSuccessfully, "chat foreign lobby connects separately");
                var subscribe = foreign.Invoke("Subscribe", foreignLobby);
                while (!subscribe.IsCompleted)
                {
                    foreign.Tick();
                    yield return null;
                }

                Check(subscribe.IsCompletedSuccessfully, "chat foreign lobby subscribes");
                int foreignMessages = 0;
                foreign.LobbyChat += _ => foreignMessages++;
                Set(a, "chatInput", "안녕하세요 🙂");
                yield return Call(a, "SendLobbyChat");
                yield return Pump(a, b, foreign, .3);
                Check(
                    Messages(a).Count == 1 && Messages(b).Count == 1,
                    "chat button delivers once to sender and same-lobby participant"
                );
                Check(
                    Messages(b)[0].nickname == first.User.name && Messages(b)[0].userId == first.User.id && Messages(b)[0].text == "안녕하세요 🙂" && DateTimeOffset.TryParse(Messages(b)[0].sentAt, out _),
                    "chat wire preserves unicode and server identity with timestamp"
                );
                Check(foreignMessages == 0, "chat never broadcasts to another lobby");
                Check((string)Get(a, "chatInput") == "", "successful chat clears input");
                Set(a, "chatInput", "too fast");
                yield return Call(a, "SendLobbyChat");
                Check(
                    (string)Get(a, "chatInput") == "too fast" && ((string)Get(a, "chatStatus")).Contains("1초"),
                    "chat button retains rejected input and displays rate-limit reason"
                );
                yield return Pump(a, b, foreign, 1.1);
                Set(a, "selectedLobbyId", foreignLobby);
                Set(a, "chatInput", "still my lobby");
                yield return Call(a, "SendLobbyChat");
                yield return Pump(a, b, foreign, .3);
                Check(Messages(b).Count == 2 && foreignMessages == 0, "browsing another lobby preserves current-lobby chat");
                // Token refresh invalidates both sending and receiving on the old subscription.
                typeof(TrainingApiClient).GetField("expiresAt", Flags).SetValue(second, DateTimeOffset.UtcNow);
                yield return second.GetMe(_ =>
                {
                }, fail);
                yield return Pump(a, b, foreign, 1.1);
                Set(a, "chatInput", "after rotation");
                yield return Call(a, "SendLobbyChat");
                yield return Pump(a, b, foreign, .3);
                Check(Messages(b).Count == 2, "rotated token stops old connection receiving chat");
                typeof(ApiLobbyScreen).GetMethod("DisposeLobbyUpdates", Flags).Invoke(b, null);
                yield return Call(b, "ConnectLobbyRooms", lobby);
                Set(b, "chatInput", "reconnected");
                yield return Call(b, "SendLobbyChat");
                yield return Pump(a, b, foreign, .3);
                Check(
                    Messages(a).Exists(item => item.text == "reconnected"),
                    "fresh subscription restores chat after token refresh"
                );
                yield return second.LeaveLobby(lobby, () =>
                {
                }, fail);
                yield return second.GetMe(_ =>
                {
                }, fail);
                Set(b, "testPauseLobbyUpdates", true);
                typeof(ApiLobbyScreen).GetMethod("UpdateLobbyRooms", Flags).Invoke(b, null);
                Check(
                    Messages(b).Count == 0 && (string)Get(b, "chatInput") == "" && Connection(b) == null,
                    "lobby departure clears chat and disposes previous connection"
                );
                yield return second.JoinLobby(lobby, () =>
                {
                }, fail);
                // ClearView is the same path used after successful logout.
                typeof(ApiLobbyScreen).GetMethod("ClearView", Flags).Invoke(a, null);
                Check(Messages(a).Count == 0 && Connection(a) == null, "logout view cleanup clears chat and connection");
            }
            finally
            {
                foreach (var screen in new[]
                {
                    a,
                    b
                }

                )
                {
                    if (screen != null)
                    {
                        Connection(screen)?.Dispose();
                        Set(screen, "api", null);
                        UnityEngine.Object.DestroyImmediate(screen.gameObject);
                    }
                }

                foreign.Dispose();
                stranger.LogoutOnShutdown();
                stranger.Dispose();
            }
        }
    }
}
