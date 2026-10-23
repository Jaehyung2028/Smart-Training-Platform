using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace SmartTraining.Editor
{
    public static class LobbyParticipantsChecks
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static object Get(ApiLobbyScreen screen, string field) => typeof(ApiLobbyScreen).GetField(field, Flags).GetValue(screen);
        private static void Set(ApiLobbyScreen screen, string field, object value) => typeof(ApiLobbyScreen).GetField(field, Flags).SetValue(screen, value);
        private static LobbyParticipant[] Rows(ApiLobbyScreen screen) => (LobbyParticipant[])Get(screen, "lobbyParticipants");
        private static LobbyParticipant Row(ApiLobbyScreen screen, string id) => Array.Find(Rows(screen), item => item.userId == id);
        private static void Check(bool ok, string name)
        {
            if (!ok)
                throw new InvalidOperationException(name);
            Debug.Log("ROOM PASS: " + name);
        }
        private static IEnumerator Wait(Func<bool> condition, string name)
        {
            double end = Time.realtimeSinceStartupAsDouble + 18;
            while (!condition())
            {
                if (Time.realtimeSinceStartupAsDouble > end)
                    throw new Exception(name);
                yield return null;
            }
        }
        public static IEnumerator Run(TrainingApiClient first, TrainingApiClient second, string lobby, string room)
        {
            Action<string> fail = error => throw new InvalidOperationException(error);
            var observer = new TrainingApiClient(first.BaseUrl);
            var visitor = new TrainingApiClient(first.BaseUrl);
            var network = new RoomNetworkClient();
            ApiLobbyScreen screen = null;
            try
            {
                yield return observer.Login("lpw-" + Guid.NewGuid().ToString("N").Substring(0, 6), () => { }, fail);
                yield return observer.JoinLobby(lobby, () => { }, fail);
                yield return observer.GetMe(_ => { }, fail);
                screen = new GameObject("participants-test").AddComponent<ApiLobbyScreen>();
                Set(screen, "api", observer);
                Set(screen, "selectedLobbyId", lobby);
                yield return Wait(() => Row(screen, first.User.id)?.connectionState == "connected" && Row(screen, second.User.id)?.connectionState == "connected" && Row(screen, observer.User.id)?.connectionState == "lobby", "initial automatic participant list timed out");
                Check(Rows(screen).Length == 3 && Row(screen, first.User.id).roomId == room && Row(screen, first.User.id).isRoomMaster, "participants automatically show lobby waiting and connected master");
                yield return visitor.Login("lpv-" + Guid.NewGuid().ToString("N").Substring(0, 6), () => { }, fail);
                yield return visitor.JoinLobby(lobby, () => { }, fail);
                yield return Wait(() => Row(screen, visitor.User.id)?.connectionState == "lobby", "new participant did not appear");
                Check(Rows(screen).Length == 4 && Row(screen, visitor.User.id).nickname == visitor.User.name, "live participant list adds new nickname without manual refresh");
                JoinRoomResponse admission = null;
                yield return visitor.JoinRoom(lobby, room, value => admission = value, fail);
                yield return Wait(() => Row(screen, visitor.User.id)?.connectionState == "reserved", "reservation state did not appear");
                Check(Row(screen, visitor.User.id).roomId == room && !Row(screen, visitor.User.id).isRoomMaster, "participant list distinguishes room reservation from confirmed connection");
                yield return network.Connect(visitor.User.id, admission, () => { }, fail);
                yield return Wait(() => Row(screen, visitor.User.id)?.connectionState == "connected", "confirmed participant state did not appear");
                Check(Row(screen, visitor.User.id).roomName == "network-room" && !Row(screen, visitor.User.id).isRoomMaster, "actual UDP confirmation changes participant state and room name");
                string foreignLobby = null;
                yield return CatalogLobbyTestSupport.Select(visitor, id => foreignLobby = id, fail, lobby);
                Set(screen, "selectedLobbyId", foreignLobby);
                network.Dispose();
                yield return Wait(() => Row(screen, visitor.User.id)?.connectionState == "lobby", "disconnect did not restore lobby state");
                Check((string)Get(screen, "participantsLobbyId") == lobby && Rows(screen).Length == 4, "browsing another lobby keeps current-lobby participant list and updates disconnect");
                var departedId = visitor.User.id;
                yield return visitor.Logout(() => { }, fail);
                yield return Wait(() => Row(screen, departedId) == null, "logout did not remove participant");
                Check(Rows(screen).Length == 3, "logout removes departed participant automatically");
                yield return first.TransferRoomMaster(lobby, room, second.User.id, () => { }, fail);
                yield return Wait(() => Row(screen, second.User.id)?.isRoomMaster == true && Row(screen, first.User.id)?.isRoomMaster == false, "participant role update timed out");
                Check(Row(screen, second.User.id).connectionState == "connected", "live participant list follows room master handover");
                yield return second.TransferRoomMaster(lobby, room, first.User.id, () => { }, fail);
                yield return Wait(() => Row(screen, first.User.id)?.isRoomMaster == true, "master restore timed out");
                var oldConnection = (RoomHubConnection)Get(screen, "lobbyUpdates");
                oldConnection.Dispose();
                yield return Wait(() => Get(screen, "lobbyUpdates") != null && Get(screen, "lobbyUpdates") != oldConnection &&
                    !(bool)Get(screen, "liveConnecting") && !(bool)Get(screen, "busy") && (bool)Get(screen, "participantsLoaded"), "participant reconnect timed out");
                Check(Rows(screen).Length == 3 && Row(screen, first.User.id).isRoomMaster, "SignalR reconnect restores participant snapshot automatically");
                // Take the screen's operation slot in the same driver step that observes it idle.
                while ((bool)Get(screen, "busy") || typeof(TrainingApiClient).GetField("activeRequest", Flags).GetValue(observer) != null)
                    yield return null;
                Set(screen, "busy", true);
                yield return observer.Logout(() => { }, fail);
                yield return Wait(() => Rows(screen).Length == 0 && (string)Get(screen, "participantsLobbyId") == "" && Get(screen, "lobbyUpdates") == null, "logout view cleanup timed out");
                Check(!(bool)Get(screen, "participantsLoaded"), "logout clears participant identities and old subscription");
            }
            finally
            {
                if (screen != null)
                {
                    ((RoomHubConnection)Get(screen, "lobbyUpdates"))?.Dispose();
                    Set(screen, "api", null);
                    screen.enabled = false;
                    UnityEngine.Object.DestroyImmediate(screen.gameObject);
                }
                network.Dispose();
                visitor.LogoutOnShutdown();
                visitor.Dispose();
                observer.LogoutOnShutdown();
                observer.Dispose();
            }
        }
    }
}
