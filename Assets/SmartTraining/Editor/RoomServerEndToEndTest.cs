using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SmartTraining.Editor
{
    public static class RoomServerEndToEndTest
    {
        private static readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();
        private static TrainingApiClient first, second, visitor;
        private static RoomNetworkClient client, other, visitingClient;
        private static bool optionsEnabled;
        private static EnterPlayModeOptions options;
        private static double deadline;
        private static string runtimeError;
        private static void Log(string message, string trace, LogType kind)
        { if (kind == LogType.Error || kind == LogType.Exception) runtimeError = message; }
        public static void Run()
        {
            optionsEnabled = EditorSettings.enterPlayModeOptionsEnabled; options = EditorSettings.enterPlayModeOptions;
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/SmartTraining/Scenes/ApiLobby.unity");
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            deadline = EditorApplication.timeSinceStartup + 180;
            runtimeError = null; Application.logMessageReceived += Log;
            routines.Push(Checks()); EditorApplication.update += Tick;
        }
        private static void Check(bool value, string name)
        { if (!value) throw new InvalidOperationException(name); Debug.Log("ROOM PASS: " + name); }
        private static IEnumerator Checks()
        {
            EditorApplication.EnterPlaymode();
            yield return new WaitUntil(() => EditorApplication.isPlaying);
            var url = Environment.GetEnvironmentVariable("SMART_TRAINING_TEST_URL");
            first = new TrainingApiClient(url); second = new TrainingApiClient(url);
            Action<string> fail = message => throw new InvalidOperationException(message);
            yield return first.Login("room-a-" + Guid.NewGuid().ToString("N").Substring(0, 6), () => { }, fail);
            yield return second.Login("room-b-" + Guid.NewGuid().ToString("N").Substring(0, 6), () => { }, fail);
            string lobby = null, room = null;
            yield return first.CreateLobby("rt-" + Guid.NewGuid().ToString("N").Substring(0, 6), id => lobby = id, fail);
            yield return first.JoinLobby(lobby, () => { }, fail);
            yield return second.JoinLobby(lobby, () => { }, fail);
            yield return first.CreateRoom(lobby, "network-room", "sample-training", 3, id => room = id, fail);
            JoinRoomResponse reservation = null;
            yield return first.JoinRoom(lobby, room, value => reservation = value, fail);
            Check(reservation != null && !string.IsNullOrEmpty(reservation.admissionToken), "ASP.NET launches executable and receives RoomHub registration");
            yield return first.GetRoom(lobby, room, value => Check(value.reservedUsers == 1 && value.connectedUsers == 0, "reservation tracked before UDP connection"), fail);
            var bad = new JoinRoomResponse { room = reservation.room, expiresAtUtc = reservation.expiresAtUtc, admissionToken = "wrong-admission-token" };
            other = new RoomNetworkClient(); bool denied = false;
            yield return other.Connect(first.User.id, bad, () => throw new Exception("Invalid admission accepted"), _ => denied = true);
            Check(denied, "real room server rejects invalid admission token"); other.Dispose(); other = null;
            client = new RoomNetworkClient();
            yield return client.Connect(first.User.id, reservation, () => { }, fail);
            yield return WaitMembership(first, room, true);
            Check(client.IsConnected, "Netcode client connects to separate dedicated process");
            yield return WaitPlayers(client, 1);
            Check(client.LocalPlayer.UserId == first.User.id && client.LocalPlayer.UserName == first.User.name,
                "player identity comes from ASP.NET confirmation");
            yield return first.GetRoom(lobby, room, value => Check(value.connectedUsers == 1 && value.reservedUsers == 0, "ConfirmConnection updates connected count"), fail);
            JoinRoomResponse another = null;
            yield return second.JoinRoom(lobby, room, value => another = value, fail);
            yield return RejectTransfer(first, lobby, room, second.User.id, "reservation cannot receive master role before UDP confirmation");
            yield return RejectKick(first, lobby, room, second.User.id, "reservation cannot be kicked before UDP confirmation");
            other = new RoomNetworkClient();
            yield return other.Connect(second.User.id, another, () => { }, fail);
            yield return WaitMembership(second, room, true);
            yield return WaitPlayers(client, 2); yield return WaitPlayers(other, 2);
            yield return WaitMaster(client, first.User.id); yield return WaitMaster(other, first.User.id);
            Check(client.LocalPlayer.IsRoomMaster && !other.LocalPlayer.IsRoomMaster, "creator is master and second participant is not master");
            yield return RejectTransfer(second, lobby, room, first.User.id, "ordinary participant cannot transfer room master");
            // Exercise the same coroutine used by the in-room transfer button.
            var transferScreen = UnityEngine.Object.FindFirstObjectByType<ApiLobbyScreen>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(ApiLobbyScreen).GetField("api", flags).SetValue(transferScreen, first);
            typeof(ApiLobbyScreen).GetField("network", flags).SetValue(transferScreen, client);
            typeof(ApiLobbyScreen).GetField("networkLobbyId", flags).SetValue(transferScreen, lobby);
            typeof(ApiLobbyScreen).GetField("networkRoomId", flags).SetValue(transferScreen, room);
            yield return (IEnumerator)typeof(ApiLobbyScreen).GetMethod("TransferRoomMaster", flags)
                .Invoke(transferScreen, new object[] { second.User.id, second.User.name });
            typeof(ApiLobbyScreen).GetField("network", flags).SetValue(transferScreen, null);
            typeof(ApiLobbyScreen).GetField("api", flags).SetValue(transferScreen, null);
            typeof(ApiLobbyScreen).GetField("networkLobbyId", flags).SetValue(transferScreen, "");
            typeof(ApiLobbyScreen).GetField("networkRoomId", flags).SetValue(transferScreen, "");
            yield return WaitMaster(client, second.User.id); yield return WaitMaster(other, second.User.id);
            Check(!client.LocalPlayer.IsRoomMaster && other.LocalPlayer.IsRoomMaster && client.IsConnected && other.IsConnected,
                "manual transfer synchronizes both clients without reconnecting");
            yield return first.GetRoom(lobby, room, value => Check(value.masterUserId == second.User.id && value.connectedUsers == 2,
                "manual transfer preserves connected count and matches ASP.NET authority"), fail);
            yield return RejectTransfer(first, lobby, room, first.User.id, "former master cannot take authority back");
            yield return RejectKick(first, lobby, room, second.User.id, "former master immediately loses kick permission");
            yield return second.TransferRoomMaster(lobby, room, first.User.id, () => { }, fail);
            yield return WaitMaster(client, first.User.id); yield return WaitMaster(other, first.User.id);
            Check(client.LocalPlayer.OwnerClientId != other.LocalPlayer.OwnerClientId &&
                Vector3.Distance(client.LocalPlayer.ServerPosition, other.LocalPlayer.ServerPosition) > 1,
                "participants own separate players at separate spawn positions");
            yield return first.GetRoom(lobby, room, value => Check(value.connectedUsers == 2 && value.availableSlots == 1, "two clients use one dedicated room process"), fail);
            yield return EntryLockChecks(lobby, room);
            yield return MovementChecks();
            yield return KickChecks(lobby, room);
            yield return ReconnectChecks(lobby, room);
            yield return first.SetRoomEntryLock(lobby, room, true, () => { }, fail);
            yield return WaitEntryLock(client, true); yield return WaitEntryLock(other, true);

            client.Dispose(); client = null;
            yield return WaitMembership(first, room, false);
            yield return WaitPlayers(other, 1);
            yield return WaitMaster(other, second.User.id);
            Check(other.LocalPlayer.RoomEntryLocked, "automatic master handover preserves entry lock");
            yield return second.SetRoomEntryLock(lobby, room, false, () => { }, fail);
            yield return WaitEntryLock(other, false);
            yield return second.GetRoom(lobby, room, value => Check(value.masterUserId == second.User.id,
                "UDP disconnect transfers master consistently in ASP.NET and Unity"), fail);
            Check(other.Players.Length == 1 && other.Players[0].UserId == second.User.id, "disconnect despawns departing player on other client");

            yield return first.GetRoom(lobby, room, value => Check(value.connectedUsers == 1 && value.reservedUsers == 0, "UDP disconnect updates membership through RoomHub"), fail);
            yield return first.GetMe(user => Check(user.currentLobbyId == lobby && string.IsNullOrEmpty(user.pendingRoomId), "network disconnect preserves lobby and clears reservation"), fail);
            yield return Move(other, Vector2.up, 0.6f); yield return Move(other, Vector2.zero, 0.4f);
            var existingPosition = other.LocalPlayer.ServerPosition;
            yield return first.JoinRoom(lobby, room, value => reservation = value, fail);
            client = new RoomNetworkClient();
            yield return client.Connect(first.User.id, reservation, () => { }, fail);
            yield return WaitMembership(first, room, true);
            yield return WaitPlayers(client, 2);
            yield return WaitMaster(client, second.User.id);
            Check(!client.LocalPlayer.IsRoomMaster && other.LocalPlayer.IsRoomMaster, "rejoining former master does not take authority back");
            Check(client.LocalPlayer.UserId == first.User.id, "rejoining creates a fresh owned player");

            var existingPlayer = Array.Find(client.Players, player => player.UserId == second.User.id);
            Check(existingPlayer != null && Vector3.Distance(existingPlayer.ServerPosition, existingPosition) < 0.15f,
                "late joining client receives existing player's current position");
            var screen = UnityEngine.Object.FindFirstObjectByType<ApiLobbyScreen>();
            typeof(ApiLobbyScreen).GetField("api", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(screen, first);
            typeof(ApiLobbyScreen).GetField("network", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(screen, client);
            // Logout while still connected must send DisconnectUser to the dedicated server.
            yield return second.Logout(() => { }, fail);
            double until = Time.realtimeSinceStartupAsDouble + 5;
            while (other.IsConnected && Time.realtimeSinceStartupAsDouble < until) yield return null;
            Check(!other.IsConnected, "logout disconnects Netcode client via server notification");
            yield return WaitPlayers(client, 1);
            yield return WaitMaster(client, first.User.id);
            yield return first.GetRoom(lobby, room, value => Check(value.masterUserId == first.User.id,
                "master logout transfers authority without ending dedicated server"), fail);
            other.Dispose(); other = null;
            client.Dispose(); client = null;
            yield return WaitMembership(first, room, false);
            yield return first.Logout(() => { }, fail);
            yield return NetcodeLoopbackChecks.Run();
            Check(runtimeError == null, "player and room UI lifecycle has no runtime error");
            EditorApplication.ExitPlaymode();
            yield return new WaitUntil(() => !EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode);
        }
        private static IEnumerator WaitPlayers(RoomNetworkClient connection, int count)
        {
            double until = Time.realtimeSinceStartupAsDouble + 10;
            while ((connection.LocalPlayer == null || connection.Players.Length != count) && Time.realtimeSinceStartupAsDouble < until) yield return null;
            Check(connection.LocalPlayer != null && connection.Players.Length == count, "player spawn/despawn replication count " + count);
        }
        private static IEnumerator Pause(float seconds)
        {
            double until = Time.realtimeSinceStartupAsDouble + seconds;
            while (Time.realtimeSinceStartupAsDouble < until) yield return null;
        }
        private static IEnumerator WaitMaster(RoomNetworkClient connection, string userId)
        {
            double until = Time.realtimeSinceStartupAsDouble + 5;
            while ((connection.LocalPlayer == null || Array.Exists(connection.Players, player => player.RoomMasterUserId != userId)) &&
                Time.realtimeSinceStartupAsDouble < until) yield return null;
            Check(connection.LocalPlayer != null && connection.Players.Length > 0 &&
                Array.TrueForAll(connection.Players, player => player.RoomMasterUserId == userId) &&
                Array.FindAll(connection.Players, player => player.IsRoomMaster).Length == 1,
                "exactly one authoritative room master is replicated to every player");
        }
        private static IEnumerator RejectKick(TrainingApiClient api, string lobby, string room, string target, string name)
        {
            bool denied = false;
            yield return api.KickRoomUser(lobby, room, target,
                () => throw new InvalidOperationException("Unauthorized kick succeeded"), _ => denied = true);
            Check(denied && api.IsLoggedIn, name);
        }
        private static IEnumerator ReconnectChecks(string lobby, string room)
        {
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var screen = UnityEngine.Object.FindFirstObjectByType<ApiLobbyScreen>();
            BindScreen(screen, first, client, lobby, room);
            ulong previousOwner = client.LocalPlayer.OwnerClientId;
            var manager = (Unity.Netcode.NetworkManager)typeof(RoomNetworkClient).GetField("manager", flags).GetValue(client);
            var transport = (Unity.Netcode.Transports.UTP.UnityTransport)manager.NetworkConfig.NetworkTransport;
            Check(transport.DisconnectTimeoutMS == 10000 && transport.HeartbeatTimeoutMS == 1000,
                "client uses bounded heartbeat and disconnect timeout");
            transport.DisconnectLocalClient();
            double until = Time.realtimeSinceStartupAsDouble + 20;
            while (string.IsNullOrEmpty((string)typeof(ApiLobbyScreen).GetField("reconnectRoomId", flags).GetValue(screen)) ||
                (bool)typeof(ApiLobbyScreen).GetField("busy", flags).GetValue(screen))
            { if (Time.realtimeSinceStartupAsDouble > until) throw new Exception("Disconnect UI cleanup timed out"); yield return null; }
            Check(first.IsLoggedIn && first.User.currentLobbyId == lobby && string.IsNullOrEmpty(first.User.currentRoomId) &&
                string.IsNullOrEmpty(first.User.pendingRoomId), "unexpected disconnect UI clears membership and retains login and lobby");
            yield return WaitPlayers(other, 1); yield return WaitMaster(other, second.User.id);
            yield return second.SetRoomEntryLock(lobby, room, true, () => { }, message => throw new Exception(message));
            yield return (IEnumerator)typeof(ApiLobbyScreen).GetMethod("ReconnectRoom", flags).Invoke(screen, null);
            Check(typeof(ApiLobbyScreen).GetField("network", flags).GetValue(screen) == null &&
                ((string)typeof(ApiLobbyScreen).GetField("status", flags).GetValue(screen)).Contains("잠겨"),
                "reconnect respects current room lock without creating reservation");
            yield return second.SetRoomEntryLock(lobby, room, false, () => { }, message => throw new Exception(message));
            yield return (IEnumerator)typeof(ApiLobbyScreen).GetMethod("ReconnectRoom", flags).Invoke(screen, null);
            client = (RoomNetworkClient)typeof(ApiLobbyScreen).GetField("network", flags).GetValue(screen);
            Check(client != null && client.IsConnected && client.LocalPlayer.UserId == first.User.id &&
                client.LocalPlayer.OwnerClientId != previousOwner, "reconnect button creates fresh admission and owned player");
            Check(string.IsNullOrEmpty((string)typeof(ApiLobbyScreen).GetField("reconnectRoomId", flags).GetValue(screen)),
                "successful reconnect clears retry target");
            yield return WaitPlayers(other, 2); yield return WaitMaster(client, second.User.id);
            Check(!client.LocalPlayer.IsRoomMaster, "reconnect preserves automatically transferred master authority");
            BindScreen(screen, null, null, "", "");
            yield return second.TransferRoomMaster(lobby, room, first.User.id, () => { }, message => throw new Exception(message));
            yield return WaitMaster(client, first.User.id); yield return WaitMaster(other, first.User.id);
        }

        private static void BindScreen(ApiLobbyScreen screen, TrainingApiClient api, RoomNetworkClient connection, string lobby, string room)
        {
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(ApiLobbyScreen).GetField("api", flags).SetValue(screen, api);
            typeof(ApiLobbyScreen).GetField("network", flags).SetValue(screen, connection);
            typeof(ApiLobbyScreen).GetField("networkLobbyId", flags).SetValue(screen, lobby);
            typeof(ApiLobbyScreen).GetField("networkRoomId", flags).SetValue(screen, room);
        }
        private static IEnumerator KickChecks(string lobby, string room)
        {
            Action<string> fail = message => throw new InvalidOperationException(message);
            yield return RejectKick(second, lobby, room, first.User.id, "ordinary participant cannot kick master");
            yield return RejectKick(first, lobby, room, first.User.id, "master cannot kick self through API");
            var ownerId = client.LocalPlayer.OwnerClientId;
            var oldOtherId = other.LocalPlayer.OwnerClientId;
            var screen = UnityEngine.Object.FindFirstObjectByType<ApiLobbyScreen>();
            var targetRoot = new GameObject("KickTargetScreen");
            var targetScreen = targetRoot.AddComponent<ApiLobbyScreen>();
            BindScreen(screen, first, client, lobby, room);
            BindScreen(targetScreen, second, other, lobby, room);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            yield return (IEnumerator)typeof(ApiLobbyScreen).GetMethod("KickRoomUser", flags)
                .Invoke(screen, new object[] { second.User.id, second.User.name });
            double until = Time.realtimeSinceStartupAsDouble + 10;
            while ((bool)typeof(ApiLobbyScreen).GetField("busy", flags).GetValue(targetScreen) ||
                typeof(ApiLobbyScreen).GetField("network", flags).GetValue(targetScreen) != null)
            {
                if (Time.realtimeSinceStartupAsDouble > until) throw new TimeoutException("Kick target UI cleanup timed out");
                yield return null;
            }
            Check(!other.IsConnected && second.IsLoggedIn && second.User.currentLobbyId == lobby &&
                string.IsNullOrEmpty(second.User.currentRoomId), "kick returns target UI to lobby while preserving login");
            Check(((string)typeof(ApiLobbyScreen).GetField("status", flags).GetValue(targetScreen)).Contains("강퇴"),
                "target sees actual server-supplied kick reason");
            Check(string.IsNullOrEmpty((string)typeof(ApiLobbyScreen).GetField("reconnectRoomId", flags).GetValue(targetScreen)),
                "kick does not offer connection recovery target");
            yield return WaitPlayers(client, 1); yield return WaitMaster(client, first.User.id);
            yield return first.GetRoom(lobby, room, value => Check(value.connectedUsers == 1 && value.reservedUsers == 0 && value.availableSlots == 2,
                "confirmed UDP kick frees exactly one room slot"), fail);
            Check(client.IsConnected && client.LocalPlayer.OwnerClientId == ownerId &&
                ((string)typeof(ApiLobbyScreen).GetField("status", flags).GetValue(screen)).Contains("강퇴했습니다"),
                "kick button confirms departure while master connection survives");
            BindScreen(targetScreen, null, null, "", ""); UnityEngine.Object.Destroy(targetRoot);
            BindScreen(screen, null, null, "", "");
            other.Dispose(); other = null;
            JoinRoomResponse admission = null;
            yield return second.JoinRoom(lobby, room, value => admission = value, fail);
            other = new RoomNetworkClient();
            yield return other.Connect(second.User.id, admission, () => { }, fail);
            yield return WaitMembership(second, room, true);
            yield return WaitPlayers(client, 2); yield return WaitPlayers(other, 2); yield return WaitMaster(other, first.User.id);
            Check(other.LocalPlayer.OwnerClientId != oldOtherId && client.LocalPlayer.OwnerClientId == ownerId &&
                !other.LocalPlayer.IsRoomMaster, "kicked user rejoins with fresh connection while master and room remain intact");
        }

        private static IEnumerator RejectTransfer(TrainingApiClient api, string lobby, string room, string target, string name)
        {
            bool denied = false;
            yield return api.TransferRoomMaster(lobby, room, target,
                () => throw new InvalidOperationException("Unauthorized transfer succeeded"), _ => denied = true);
            Check(denied && api.IsLoggedIn, name);
        }
        private static IEnumerator WaitEntryLock(RoomNetworkClient connection, bool locked)
        {
            double until = Time.realtimeSinceStartupAsDouble + 5;
            while ((connection.LocalPlayer == null || Array.Exists(connection.Players, player => player.RoomEntryLocked != locked)) &&
                Time.realtimeSinceStartupAsDouble < until) yield return null;
            Check(connection.LocalPlayer != null && connection.IsConnected && connection.Players.Length > 0 &&
                Array.TrueForAll(connection.Players, player => player.RoomEntryLocked == locked),
                "entry lock state replicates to all existing participants: " + locked);
        }
        private static IEnumerator EntryLockChecks(string lobby, string room)
        {
            Action<string> fail = message => throw new InvalidOperationException(message);
            visitor = new TrainingApiClient(first.BaseUrl);
            yield return visitor.Login("room-c-" + Guid.NewGuid().ToString("N").Substring(0, 6), () => { }, fail);
            yield return visitor.JoinLobby(lobby, () => { }, fail);
            JoinRoomResponse pending = null;
            yield return visitor.JoinRoom(lobby, room, value => pending = value, fail);
            bool denied = false;
            yield return second.SetRoomEntryLock(lobby, room, true, () => throw new Exception("Non-master locked room"), _ => denied = true);
            Check(denied, "ordinary participant cannot lock entry");
            var screen = UnityEngine.Object.FindFirstObjectByType<ApiLobbyScreen>();
            BindScreen(screen, first, client, lobby, room);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            yield return (IEnumerator)typeof(ApiLobbyScreen).GetMethod("SetRoomEntryLock", flags).Invoke(screen, new object[] { true });
            BindScreen(screen, null, null, "", "");
            yield return WaitEntryLock(client, true); yield return WaitEntryLock(other, true);
            yield return visitor.GetMe(user => Check(string.IsNullOrEmpty(user.pendingRoomId) && user.currentLobbyId == lobby,
                "lock button cancels pending admission and preserves lobby"), fail);
            yield return first.GetRoom(lobby, room, value => Check(value.isEntryLocked && value.reservedUsers == 0 && value.connectedUsers == 2,
                "locked room summary preserves connected users and releases reservations"), fail);
            visitingClient = new RoomNetworkClient(); denied = false;
            yield return visitingClient.Connect(visitor.User.id, pending, () => throw new Exception("Canceled admission connected"), _ => denied = true);
            Check(denied, "dedicated server rejects previously issued token after entry lock");
            visitingClient.Dispose(); visitingClient = null;
            denied = false;
            yield return visitor.JoinRoom(lobby, room, _ => throw new Exception("Locked join created reservation"), message => denied = message.Contains("잠"));
            Check(denied, "locked room rejects new join even with a free slot");
            var start = client.LocalPlayer.ServerPosition;
            yield return Move(client, Vector2.right, 0.4f); yield return Settle();
            Check(client.IsConnected && other.IsConnected && client.LocalPlayer.ServerPosition.x > start.x + 0.5f,
                "entry lock preserves existing movement and both connections");
            yield return first.TransferRoomMaster(lobby, room, second.User.id, () => { }, fail);
            yield return WaitMaster(client, second.User.id); yield return WaitEntryLock(other, true);
            denied = false;
            yield return first.SetRoomEntryLock(lobby, room, false, () => throw new Exception("Former master unlocked room"), _ => denied = true);
            Check(denied, "former master cannot reopen locked room");
            yield return second.SetRoomEntryLock(lobby, room, false, () => { }, fail);
            yield return WaitEntryLock(client, false); yield return WaitEntryLock(other, false);
            JoinRoomResponse fresh = null;
            yield return visitor.JoinRoom(lobby, room, value => fresh = value, fail);
            Check(fresh.admissionToken != pending.admissionToken, "unlock requires a fresh admission token");
            visitingClient = new RoomNetworkClient();
            yield return visitingClient.Connect(visitor.User.id, fresh, () => { }, fail);
            yield return WaitMembership(visitor, room, true);
            yield return WaitPlayers(visitingClient, 3); yield return WaitPlayers(client, 3); yield return WaitPlayers(other, 3);
            Check(client.IsConnected && other.IsConnected && visitingClient.IsConnected,
                "reopened room accepts third client without reconnecting existing clients");
            visitingClient.Dispose(); visitingClient = null;
            yield return WaitMembership(visitor, room, false);
            yield return WaitPlayers(client, 2); yield return WaitPlayers(other, 2);
            yield return visitor.Logout(() => { }, fail); visitor.Dispose(); visitor = null;
            yield return second.TransferRoomMaster(lobby, room, first.User.id, () => { }, fail);
            yield return WaitMaster(client, first.User.id); yield return WaitMaster(other, first.User.id);
        }
        private static IEnumerator Move(RoomNetworkClient connection, Vector2 direction, float seconds)
        {
            double until = Time.realtimeSinceStartupAsDouble + seconds;
            while (Time.realtimeSinceStartupAsDouble < until) { connection.SendMovement(direction); yield return null; }
        }
        private static IEnumerator Settle(float seconds = 0.4f)
        {
            yield return Move(client, Vector2.zero, seconds);
        }
        private static IEnumerator MovementChecks()
        {
            var start = client.LocalPlayer.ServerPosition;
            var otherStart = other.LocalPlayer.ServerPosition;
            yield return Move(client, Vector2.right, 1);
            yield return Settle();
            var after = client.LocalPlayer.ServerPosition;
            Check(after.x - start.x > 2 && after.x - start.x < 5 && Mathf.Abs(after.z - start.z) < 0.1f,
                "server applies owner movement at bounded speed");
            Check(Vector3.Distance(other.LocalPlayer.ServerPosition, otherStart) < 0.1f, "movement does not move another participant");
            var remote = Array.Find(other.Players, player => player.UserId == first.User.id);
            Check(remote != null && Vector3.Distance(remote.ServerPosition, after) < 0.15f,
                "second client receives first player's authoritative position");
            bool rejected = false;
            try { Array.Find(client.Players, player => !player.IsOwner).MoveRpc(Vector2.up); }
            catch (Unity.Netcode.RpcException) { rejected = true; }
            Check(rejected, "non-owner cannot submit movement for another player");
            start = after;
            yield return Move(client, new Vector2(100, 100), 0.8f); yield return Settle();
            after = client.LocalPlayer.ServerPosition;
            Check(Vector3.Distance(start, after) > 1 && Vector3.Distance(start, after) < 4,
                "oversized diagonal input cannot increase movement speed");
            start = after;
            yield return Move(client, new Vector2(float.NaN, float.PositiveInfinity), 0.4f); yield return Settle();
            Check(Vector3.Distance(start, client.LocalPlayer.ServerPosition) < 0.1f, "server rejects non-finite movement input");
            yield return Move(client, Vector2.up, 0.2f);
            double stopBy = Time.realtimeSinceStartupAsDouble + 0.7f;
            while (Time.realtimeSinceStartupAsDouble < stopBy) yield return null;
            start = client.LocalPlayer.ServerPosition;
            stopBy = Time.realtimeSinceStartupAsDouble + 0.4f;
            while (Time.realtimeSinceStartupAsDouble < stopBy) yield return null;
            Check(Vector3.Distance(start, client.LocalPlayer.ServerPosition) < 0.1f, "lost input stops movement instead of continuing indefinitely");
            yield return Move(client, Vector2.right, 4); yield return Settle();
            Check(Mathf.Abs(client.LocalPlayer.ServerPosition.x - RoomPlayer.ArenaHalfSize) < 0.1f,
                "server clamps movement to training area boundary");
        }
        private static IEnumerator WaitMembership(TrainingApiClient api, string room, bool expected)
        {
            double until = Time.realtimeSinceStartupAsDouble + 10;
            bool matched = false;
            while (!matched && Time.realtimeSinceStartupAsDouble < until)
            {
                yield return api.GetMe(user => matched = expected ? user.currentRoomId == room : string.IsNullOrEmpty(user.currentRoomId), message => throw new Exception(message));
                if (!matched) yield return new WaitForSecondsRealtime(0.1f);
            }
            Check(matched, expected ? "backend confirms connected membership" : "backend clears disconnected membership");
        }
        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Room server end-to-end test timed out");
                if (routines.Count == 0) { Finish(0); return; }
                var routine = routines.Peek();
                if (routine.Current is AsyncOperation pending && !pending.isDone) return;
                if (!routine.MoveNext()) { routines.Pop(); return; }
                if (routine.Current is IEnumerator nested) routines.Push(nested);
            }
            catch (Exception error) { Debug.LogError("ROOM CHECK FAILED: " + error.Message); Finish(1); }
        }
        private static void Finish(int code)
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= Log;
            client?.Dispose(); other?.Dispose(); visitingClient?.Dispose(); first?.Dispose(); second?.Dispose(); visitor?.Dispose();
            routines.Clear(); EditorSettings.enterPlayModeOptionsEnabled = optionsEnabled;
            EditorSettings.enterPlayModeOptions = options; EditorApplication.Exit(code);
        }
    }
}


