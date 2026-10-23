using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SmartTraining.Editor
{
    public static class RoomJoinCancellationChecks
    {
        private const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
        private static object Get(ApiLobbyScreen screen, string name) => typeof(ApiLobbyScreen).GetField(name, Flags).GetValue(screen);
        private static void Set(ApiLobbyScreen screen, string name, object value) => typeof(ApiLobbyScreen).GetField(name, Flags).SetValue(screen, value);
        private static void Cancel(ApiLobbyScreen screen) => typeof(ApiLobbyScreen).GetMethod("RequestCancelRoomJoin", Flags).Invoke(screen, null);
        private static void Start(ApiLobbyScreen screen, string method, params object[] args)
        {
            var operation = (IEnumerator)typeof(ApiLobbyScreen).GetMethod(method, Flags).Invoke(screen, args);
            typeof(ApiLobbyScreen).GetMethod("Run", Flags).Invoke(screen, new object[] { operation });
        }
        private static bool Idle(ApiLobbyScreen screen) => !(bool)Get(screen, "busy");
        private static IEnumerator Wait(Func<bool> predicate, string name)
        {
            double until = Time.realtimeSinceStartupAsDouble + 20;
            while (!predicate())
            {
                if (Time.realtimeSinceStartupAsDouble > until)
                    throw new InvalidOperationException(name);
                yield return null;
            }
        }
        private static void Check(bool ok, string name)
        {
            if (!ok)
                throw new InvalidOperationException(name);
            Debug.Log("ROOM PASS: " + name);
        }
        private static void Clean(ApiLobbyScreen screen, TrainingApiClient user, string lobby, string name)
        {
            Check(user.IsLoggedIn && user.User.currentLobbyId == lobby && user.User.currentRoomId == "" && user.User.pendingRoomId == "" && Get(screen, "network") == null,
                name + " clears reservation and connection while preserving login and lobby");
            Check((string)Get(screen, "roomJoinProgress") == "" && !(bool)Get(screen, "roomJoinCanCancel") && !(bool)Get(screen, "roomJoinCancelRequested") &&
                (string)Get(screen, "reconnectRoomId") == "" && ((string)Get(screen, "status")).Contains("취소"), name + " restores idle view with explicit cancellation message");
        }
        public static IEnumerator Run(string url)
        {
            var anchor = new TrainingApiClient(url);
            var user = new TrainingApiClient(url);
            var anchorNetwork = new RoomNetworkClient();
            ApiLobbyScreen screen = null;
            Action<string> fail = message => throw new InvalidOperationException(message);
            try
            {
                string suffix = Guid.NewGuid().ToString("N").Substring(0, 6);
                yield return anchor.Login("jc-a-" + suffix, () => { }, fail);
                yield return user.Login("jc-u-" + suffix, () => { }, fail);
                string lobby = null, room = null;
                yield return CatalogLobbyTestSupport.Select(anchor, id => lobby = id, fail);
                yield return anchor.JoinLobby(lobby, () => { }, fail);
                yield return user.JoinLobby(lobby, () => { }, fail);
                yield return anchor.CreateRoom(lobby, "cancel-room", lobby, 2, id => room = id, fail);
                JoinRoomResponse admission = null;
                yield return anchor.JoinRoom(lobby, room, value => admission = value, fail);
                yield return anchorNetwork.Connect(anchor.User.id, admission, () => { }, fail, url);
                yield return Wait(() => anchorNetwork.LocalPlayer != null, "anchor player missing");
                screen = new GameObject("room-join-cancellation-check").AddComponent<ApiLobbyScreen>();
                Set(screen, "testPauseLobbyUpdates", true);
                Set(screen, "api", user);
                Set(screen, "serverUrl", url);
                Set(screen, "selectedLobbyId", lobby);

                Start(screen, "JoinRoom", room, null);
                Cancel(screen);
                Cancel(screen);
                Check((bool)Get(screen, "roomJoinCancelRequested"), "repeat cancel clicks record one cancellation request");
                yield return Wait(() => Idle(screen), "immediate join cancellation stuck");
                Clean(screen, user, lobby, "immediate cancellation");

                Start(screen, "JoinRoom", room, null);
                yield return Wait(() => ((string)Get(screen, "roomJoinProgress")).StartsWith("2/4"), "Netcode connect stage not observed");
                Cancel(screen);
                yield return Wait(() => Idle(screen), "transport-stage cancellation stuck");
                Clean(screen, user, lobby, "transport-stage cancellation");
                yield return Wait(() => anchorNetwork.Players.Length == 1, "cancelled transport remains in roster");

                Set(screen, "testPauseRoomJoinAfterConnection", true);
                Start(screen, "JoinRoom", room, null);
                yield return Wait(() => ((RoomNetworkClient)Get(screen, "network"))?.IsConnected == true &&
                    anchorNetwork.Players.Any(player => player.UserId == user.User.id), "confirmed cancellation window missing");
                yield return user.GetMe(_ => { }, fail);
                Check(user.User.currentRoomId == room, "late cancellation fixture is already confirmed by ASP.NET and Netcode");
                Cancel(screen);
                yield return Wait(() => Idle(screen), "confirmed join cancellation stuck");
                Clean(screen, user, lobby, "confirmed connection cancellation");
                yield return Wait(() => anchorNetwork.Players.Length == 1, "confirmed cancelled user remains in Netcode roster");
                yield return anchor.GetRoom(lobby, room, value => Check(value.connectedUsers == 1 && value.reservedUsers == 0 && value.availableSlots == 1,
                    "confirmed cancellation frees slot and preserves existing participant"), fail);

                Set(screen, "testPauseRoomJoinAfterConnection", false);
                Start(screen, "JoinRoom", room, null);
                yield return Wait(() => Idle(screen), "retry after cancellation stuck");
                Check(((RoomNetworkClient)Get(screen, "network"))?.IsConnected == true && user.User.currentRoomId == room &&
                    anchorNetwork.Players.Count(player => player.UserId == user.User.id) == 1, "retry after cancellations joins once with the same user identity");
                Cancel(screen);
                Check(!(bool)Get(screen, "roomJoinCancelRequested") && user.User.currentRoomId == room, "cancel command after completed admission does not remove established participant");

                Start(screen, "LeaveRoom", "test leave");
                yield return Wait(() => Idle(screen), "reconnect fixture leave stuck");
                Set(screen, "reconnectRoomId", room);
                Set(screen, "reconnectLobbyId", lobby);
                Start(screen, "ReconnectRoom");
                yield return Wait(() => (bool)Get(screen, "roomJoinCanCancel"), "reconnect join stage not observed");
                Cancel(screen);
                yield return Wait(() => Idle(screen), "reconnect cancellation stuck");
                Clean(screen, user, lobby, "reconnect cancellation");
                yield return user.Logout(() => { }, fail);
                anchorNetwork.Dispose();
                yield return anchor.Logout(() => { }, fail);
            }
            finally
            {
                if (screen != null)
                {
                    ((RoomNetworkClient)Get(screen, "network"))?.Dispose();
                    Set(screen, "network", null);
                    Set(screen, "api", null);
                    screen.enabled = false;
                    UnityEngine.Object.DestroyImmediate(screen.gameObject);
                }
                anchorNetwork.Dispose();
                user.LogoutOnShutdown();
                user.Dispose();
                anchor.LogoutOnShutdown();
                anchor.Dispose();
            }
        }
    }
}
