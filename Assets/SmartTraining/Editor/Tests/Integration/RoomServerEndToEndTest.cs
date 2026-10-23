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
        {
            if (RoomEndpointChecks.ExpectHttpStartFailure && message.Contains("Insecure connection not allowed"))
                return;
            if (kind == LogType.Error || kind == LogType.Exception)
                runtimeError = message;
        }
        public static void Run()
        {
            optionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            options = EditorSettings.enterPlayModeOptions;
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/SmartTraining/Scenes/ApiLobby.unity");
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            deadline = EditorApplication.timeSinceStartup + 240;
            runtimeError = null;
            Application.logMessageReceived += Log;
            routines.Push(Checks());
            EditorApplication.update += Tick;
        }
        private static void Check(bool value, string name)
        {
            if (!value)
                throw new InvalidOperationException(name);
            Debug.Log("ROOM PASS: " + name);
        }
        private static IEnumerator LatencyChecks(RoomNetworkClient firstClient, RoomNetworkClient secondClient)
        {
            float until = Time.realtimeSinceStartup + 15;
            while (Time.realtimeSinceStartup < until &&
                (Array.Exists(firstClient.Players, p => p.RoundTripMilliseconds < 0) ||
                 Array.Exists(secondClient.Players, p => p.RoundTripMilliseconds < 0)))
                yield return null;
            Check(firstClient.Players.Length == 2 && Array.TrueForAll(firstClient.Players, p => p.RoundTripMilliseconds > 0),
                "dedicated server RTT reaches both participant objects on first client");
            Check(secondClient.Players.Length == 2 && Array.TrueForAll(secondClient.Players, p => p.RoundTripMilliseconds > 0),
                "late joining client receives RTT for both participant objects");
            int before = firstClient.LocalPlayer.RoundTripMilliseconds;
            firstClient.LocalPlayer.SetLatencyServer(999999);
            Check(firstClient.LocalPlayer.RoundTripMilliseconds == before, "client cannot overwrite server measured latency");
            Check(firstClient.LocalPlayer.LatencyLabel == before + " ms", "participant latency label reports milliseconds");
            yield return new WaitUntil(() => Array.TrueForAll(firstClient.Players, p => p.ConnectionState == ParticipantConnectionState.Connected) &&
                Array.TrueForAll(secondClient.Players, p => p.ConnectionState == ParticipantConnectionState.Connected));
            Check(true, "server heartbeat health reaches every participant on both clients");
            firstClient.LocalPlayer.SampleConnectionHealthServer(999999);
            Check(firstClient.LocalPlayer.ConnectionState == ParticipantConnectionState.Connected, "client cannot overwrite server connection health");
            var paused = firstClient.LocalPlayer;
            string masterBefore = secondClient.LocalPlayer.RoomMasterUserId;
            paused.enabled = false;
            try
            {
                double deadline = Time.realtimeSinceStartupAsDouble + 10;
                while (Time.realtimeSinceStartupAsDouble < deadline &&
                    !Array.Exists(secondClient.Players, p => p.UserId == paused.UserId && p.ConnectionState == ParticipantConnectionState.Checking))
                    yield return null;
                Check(firstClient.IsConnected && secondClient.Players.Length == 2 && secondClient.LocalPlayer.RoomMasterUserId == masterBefore &&
                    Array.Exists(secondClient.Players, p => p.UserId == paused.UserId && p.ConnectionState == ParticipantConnectionState.Checking),
                    "paused owner heartbeat marks peer checking without disconnecting or transferring master");
            }
            finally { if (paused != null) paused.enabled = true; }
            double recoverBy = Time.realtimeSinceStartupAsDouble + 10;
            while (Time.realtimeSinceStartupAsDouble < recoverBy &&
                !Array.Exists(secondClient.Players, p => p.UserId == paused.UserId && p.ConnectionState == ParticipantConnectionState.Connected))
                yield return null;
            Check(firstClient.IsConnected && Array.Exists(secondClient.Players, p => p.UserId == paused.UserId && p.ConnectionState == ParticipantConnectionState.Connected),
                "resuming real owner heartbeat restores replicated normal connection state");
        }

        private static IEnumerator Checks()
        {
            yield return RoomEndpointChecks.RunRequestStartFailure();
            EditorApplication.EnterPlaymode();
            yield return new WaitUntil(() => EditorApplication.isPlaying);
            RoomBrowserChecks.Run();
            Check(ParticipantConnectionHealth.Evaluate(0, false, -1) == ParticipantConnectionState.Measuring, "new participant health waits for measurement");
            Check(ParticipantConnectionHealth.Evaluate(1, true, 499) == ParticipantConnectionState.Connected &&
                ParticipantConnectionHealth.Evaluate(1, true, 500) == ParticipantConnectionState.Delayed, "heartbeat health classifies latency threshold");
            Check(ParticipantConnectionHealth.Evaluate(3, true, 20) == ParticipantConnectionState.Checking &&
                ParticipantConnectionHealth.Evaluate(0, true, 20) == ParticipantConnectionState.Connected, "missing heartbeat becomes checking and fresh response recovers");
            ServerAddressChecks.Run();
            RoomNoticeChecks.RunPure();
            RoomEndpointChecks.RunPure();
            typeof(ApiLobbyScreen).GetField("testPauseLobbyUpdates", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(UnityEngine.Object.FindFirstObjectByType<ApiLobbyScreen>(), true);
            var url = Environment.GetEnvironmentVariable("SMART_TRAINING_TEST_URL");
            first = new TrainingApiClient(url);
            second = new TrainingApiClient(url);
            Action<string> fail = message => throw new InvalidOperationException(message);
            yield return first.Login("room-a-" + Guid.NewGuid().ToString("N").Substring(0, 6), () => { }, fail);
            yield return second.Login("room-b-" + Guid.NewGuid().ToString("N").Substring(0, 6), () => { }, fail);
            string lobby = null, room = null;
            yield return CatalogLobbyTestSupport.Select(first, id => lobby = id, fail);
            yield return first.JoinLobby(lobby, () => { }, fail);
            yield return second.JoinLobby(lobby, () => { }, fail);
            yield return LobbyChatChecks.Run(first, second, lobby);
            yield return first.CreateRoom(lobby, "network-room", lobby, 3, id => room = id, fail);
            JoinRoomResponse reservation = null;
            yield return first.JoinRoom(lobby, room, value => reservation = value, fail);
            Check(reservation != null && !string.IsNullOrEmpty(reservation.admissionToken), "ASP.NET launches executable and receives RoomHub registration");
            yield return first.GetRoom(lobby, room, value => Check(value.reservedUsers == 1 && value.connectedUsers == 0, "reservation tracked before UDP connection"), fail);
            var bad = new JoinRoomResponse { room = reservation.room, expiresAtUtc = reservation.expiresAtUtc, admissionToken = "wrong-admission-token" };
            other = new RoomNetworkClient();
            bool denied = false;
            yield return other.Connect(first.User.id, bad, () => throw new Exception("Invalid admission accepted"), _ => denied = true);
            Check(denied, "real room server rejects invalid admission token");
            other.Dispose();
            other = null;
            yield return RoomEndpointChecks.Run(first, reservation);
            client = new RoomNetworkClient();
            yield return client.Connect(first.User.id, reservation, () => { }, fail, first.BaseUrl);
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
            yield return WaitPlayers(client, 2);
            yield return WaitPlayers(other, 2);
            yield return WaitMaster(client, first.User.id);
            yield return WaitMaster(other, first.User.id);
            Check(client.LocalPlayer.IsRoomMaster && !other.LocalPlayer.IsRoomMaster, "creator is master and second participant is not master");
            yield return RejectTransfer(second, lobby, room, first.User.id, "ordinary participant cannot transfer room master");
            // Exercise the same coroutine used by the in-room transfer button.
            var transferScreen = UnityEngine.Object.FindFirstObjectByType<ApiLobbyScreen>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(ApiLobbyScreen).GetField("api", flags).SetValue(transferScreen, first);
            typeof(ApiLobbyScreen).GetField("network", flags).SetValue(transferScreen, client);
            typeof(ApiLobbyScreen).GetField("networkLobbyId", flags).SetValue(transferScreen, lobby);
            typeof(ApiLobbyScreen).GetField("networkRoomId", flags).SetValue(transferScreen, room);
            RoomActionChecks.Request(transferScreen, "TransferMaster", second.User.id);
            yield return RoomActionChecks.Confirm(transferScreen);
            typeof(ApiLobbyScreen).GetField("network", flags).SetValue(transferScreen, null);
            typeof(ApiLobbyScreen).GetField("api", flags).SetValue(transferScreen, null);
            typeof(ApiLobbyScreen).GetField("networkLobbyId", flags).SetValue(transferScreen, "");
            typeof(ApiLobbyScreen).GetField("networkRoomId", flags).SetValue(transferScreen, "");
            yield return WaitMaster(client, second.User.id);
            yield return WaitMaster(other, second.User.id);
            Check(!client.LocalPlayer.IsRoomMaster && other.LocalPlayer.IsRoomMaster && client.IsConnected && other.IsConnected,
                "manual transfer synchronizes both clients without reconnecting");
            yield return first.GetRoom(lobby, room, value => Check(value.masterUserId == second.User.id && value.connectedUsers == 2,
                "manual transfer preserves connected count and matches ASP.NET authority"), fail);
            yield return RejectTransfer(first, lobby, room, first.User.id, "former master cannot take authority back");
            yield return RejectKick(first, lobby, room, second.User.id, "former master immediately loses kick permission");
            yield return second.TransferRoomMaster(lobby, room, first.User.id, () => { }, fail);
            yield return WaitMaster(client, first.User.id);
            yield return WaitMaster(other, first.User.id);
            Check(client.LocalPlayer.OwnerClientId != other.LocalPlayer.OwnerClientId &&
                Vector3.Distance(client.LocalPlayer.ServerPosition, other.LocalPlayer.ServerPosition) > 1,
                "participants own separate players at separate spawn positions");
            yield return first.GetRoom(lobby, room, value => Check(value.connectedUsers == 2 && value.availableSlots == 1, "two clients use one dedicated room process"), fail);
            yield return RoomNoticeChecks.Run(first, second, client, other, lobby, room);
            yield return RoomActionChecks.Run(first, second, client, other, lobby, room);
            yield return LatencyChecks(client, other);
            yield return EntryLockChecks(lobby, room);
            yield return RoomChatChecks.Run(first, second, client, other, lobby, room);
            yield return LobbyParticipantsChecks.Run(first, second, lobby, room);
            yield return ParticipantRoomChecks.Run(first, lobby, room);
            yield return ServerConnectionChecks.Run(first.BaseUrl, first.User.name);
            yield return RoomJoinCancellationChecks.Run(first.BaseUrl);
            yield return ServerAddressScreenChecks.Run(first);
            yield return ReadinessChecks(lobby, room);
            yield return SettingsChecks(lobby, room);
            yield return PhaseChecks(lobby, room);
            yield return MovementChecks();
            yield return KickChecks(lobby, room);
            yield return ReconnectChecks(lobby, room);
            yield return RunningDisconnectChecks(lobby, room);
            yield return LiveRoomListChecks(lobby, room);
            yield return GlobalLobbyListChecks(lobby, room);
            yield return first.SetRoomEntryLock(lobby, room, true, () => { }, fail);
            yield return WaitEntryLock(client, true);
            yield return WaitEntryLock(other, true);

            client.Dispose();
            client = null;
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
            yield return Move(other, Vector2.up, 0.6f);
            yield return Move(other, Vector2.zero, 0.4f);
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
            while (other.IsConnected && Time.realtimeSinceStartupAsDouble < until)
                yield return null;
            Check(!other.IsConnected, "logout disconnects Netcode client via server notification");
            yield return WaitPlayers(client, 1);
            yield return WaitMaster(client, first.User.id);
            yield return first.GetRoom(lobby, room, value => Check(value.masterUserId == first.User.id,
                "master logout transfers authority without ending dedicated server"), fail);
            other.Dispose();
            other = null;
            client.Dispose();
            client = null;
            yield return WaitMembership(first, room, false);
            yield return CloseRoomChecks(lobby);
            yield return first.Logout(() => { }, fail);
            yield return NetcodeLoopbackChecks.Run();
            Check(runtimeError == null, "player and room UI lifecycle has no runtime error");
            EditorApplication.ExitPlaymode();
            yield return new WaitUntil(() => !EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode);
        }
        private static IEnumerator WaitPlayers(RoomNetworkClient connection, int count)
        {
            double until = Time.realtimeSinceStartupAsDouble + 10;
            while ((connection.LocalPlayer == null || connection.Players.Length != count) && Time.realtimeSinceStartupAsDouble < until)
                yield return null;
            Check(connection.LocalPlayer != null && connection.Players.Length == count, "player spawn/despawn replication count " + count);
        }
        private static IEnumerator Pause(float seconds)
        {
            double until = Time.realtimeSinceStartupAsDouble + seconds;
            while (Time.realtimeSinceStartupAsDouble < until)
                yield return null;
        }
        private static IEnumerator WaitMaster(RoomNetworkClient connection, string userId)
        {
            double until = Time.realtimeSinceStartupAsDouble + 5;
            while ((connection.LocalPlayer == null || Array.Exists(connection.Players, player => player.RoomMasterUserId != userId)) &&
                Time.realtimeSinceStartupAsDouble < until)
                yield return null;
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
        private static IEnumerator WaitPhase(RoomNetworkClient connection, bool running)
        {
            double until = Time.realtimeSinceStartupAsDouble + 5;
            while (connection.LocalPlayer == null || connection.LocalPlayer.RoomIsRunning != running || connection.LocalPlayer.PhasePending)
            {
                if (Time.realtimeSinceStartupAsDouble > until)
                    throw new Exception("Phase sync timeout");
                yield return null;
            }
        }
        private static IEnumerator PhaseChecks(string lobby, string room)
        {
            Action<string> fail = message => throw new Exception(message);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var screen = UnityEngine.Object.FindFirstObjectByType<ApiLobbyScreen>();
            BindScreen(screen, first, client, lobby, room);
            client.SetReady(false);
            yield return WaitReady(other, first.User.id, false);
            bool ownershipRejected = false;
            try
            {
                Array.Find(client.Players, player => !player.IsOwner).RequestPhaseRpc(true);
            }
            catch (Unity.Netcode.RpcException) { ownershipRejected = true; }
            Check(ownershipRejected, "phase request cannot impersonate another player's RPC ownership");
            yield return (IEnumerator)typeof(ApiLobbyScreen).GetMethod("ChangeRoomPhase", flags).Invoke(screen, new object[] { true });
            Check(!client.LocalPlayer.RoomIsRunning && !other.LocalPlayer.RoomIsRunning, "dedicated server rejects start when a participant is unready");
            client.SetReady(true);
            yield return WaitReady(other, first.User.id, true);
            visitor = new TrainingApiClient(first.BaseUrl);
            yield return visitor.Login("phase-" + Guid.NewGuid().ToString("N").Substring(0, 6), () => { }, fail);
            yield return visitor.JoinLobby(lobby, () => { }, fail);
            yield return visitor.JoinRoom(lobby, room, _ => { }, fail);
            yield return (IEnumerator)typeof(ApiLobbyScreen).GetMethod("ChangeRoomPhase", flags).Invoke(screen, new object[] { true });
            Check(!client.LocalPlayer.RoomIsRunning && !client.LocalPlayer.PhasePending,
                "start rejects unfinished reservation and releases transition guard");
            yield return visitor.LeaveRoom(lobby, room, () => { }, fail);
            yield return (IEnumerator)typeof(ApiLobbyScreen).GetMethod("ChangeRoomPhase", flags).Invoke(screen, new object[] { true });
            yield return WaitPhase(client, true);
            yield return WaitPhase(other, true);
            yield return first.GetRoom(lobby, room, value => Check(value.isRunning && value.connectedUsers == 2,
                "master start synchronizes phase in ASP.NET and both clients without reconnecting"), fail);
            Check(!other.SetReady(false), "client preparation control is disabled during running phase");
            other.LocalPlayer.SetReadyRpc(false);
            double until = Time.realtimeSinceStartupAsDouble + 0.4;
            while (Time.realtimeSinceStartupAsDouble < until)
                yield return null;
            Check(client.GetReadiness().AllReady && other.GetReadiness().AllReady, "server also rejects direct ready cancellation during running phase");
            bool denied = false;
            yield return visitor.JoinRoom(lobby, room, _ => throw new Exception("Joined running room"), _ => denied = true);
            Check(denied, "running phase rejects new admission even with free slot");
            denied = false;
            yield return first.UpdateRoomSettings(lobby, room, "blocked-running", 3, _ => throw new Exception("Edited running room"), _ => denied = true);
            Check(denied, "running phase rejects room settings changes");
            BindScreen(screen, second, other, lobby, room);
            yield return (IEnumerator)typeof(ApiLobbyScreen).GetMethod("ChangeRoomPhase", flags).Invoke(screen, new object[] { false });
            Check(other.LocalPlayer.RoomIsRunning, "ordinary participant cannot return running room to waiting");
            yield return first.TransferRoomMaster(lobby, room, second.User.id, () => { }, fail);
            yield return WaitMaster(client, second.User.id);
            yield return WaitMaster(other, second.User.id);
            Check(client.LocalPlayer.RoomIsRunning && other.LocalPlayer.RoomIsRunning, "master transfer preserves running phase");
            yield return (IEnumerator)typeof(ApiLobbyScreen).GetMethod("ChangeRoomPhase", flags).Invoke(screen, new object[] { false });
            yield return WaitPhase(client, false);
            yield return WaitPhase(other, false);
            yield return WaitReady(client, first.User.id, false);
            yield return WaitReady(client, second.User.id, false);
            yield return WaitReady(other, first.User.id, false);
            yield return WaitReady(other, second.User.id, false);
            Check(client.GetReadiness().ReadyCount == 0 && other.GetReadiness().ReadyCount == 0,
                "new master returns room to waiting and resets all readiness");
            yield return visitor.JoinRoom(lobby, room, _ => { }, fail);
            yield return visitor.LeaveRoom(lobby, room, () => { }, fail);
            Check(true, "waiting phase reopens admission after return");
            yield return visitor.Logout(() => { }, fail);
            visitor.Dispose();
            visitor = null;
            yield return second.TransferRoomMaster(lobby, room, first.User.id, () => { }, fail);
            yield return WaitMaster(client, first.User.id);
            yield return WaitMaster(other, first.User.id);
            client.SetReady(true);
            other.SetReady(true);
            yield return WaitReady(client, second.User.id, true);
            yield return WaitReady(other, first.User.id, true);
            BindScreen(screen, null, null, "", "");
        }

        private static IEnumerator SettingsChecks(string lobby, string room)
        {
            Action<string> fail = message => throw new Exception(message);
            bool denied = false;
            yield return second.UpdateRoomSettings(lobby, room, "unauthorized", 3, _ => throw new Exception("Unauthorized settings accepted"), _ => denied = true);
            Check(denied, "ordinary participant cannot edit room settings");
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var screen = UnityEngine.Object.FindFirstObjectByType<ApiLobbyScreen>();
            BindScreen(screen, first, client, lobby, room);
            ulong owner = client.LocalPlayer.OwnerClientId;
            yield return (IEnumerator)typeof(ApiLobbyScreen).GetMethod("UpdateRoomSettings", flags).Invoke(screen, new object[] { "renamed-room", "2" });
            double until = Time.realtimeSinceStartupAsDouble + 5;
            while (other.LocalPlayer.RoomName != "renamed-room" || other.LocalPlayer.RoomCapacity != 2)
            {
                if (Time.realtimeSinceStartupAsDouble > until)
                    throw new Exception("Settings sync timeout");
                yield return null;
            }
            Check(client.LocalPlayer.RoomName == "renamed-room" && client.LocalPlayer.RoomCapacity == 2 &&
                client.LocalPlayer.OwnerClientId == owner && client.GetReadiness().AllReady && other.GetReadiness().AllReady,
                "settings button synchronizes name and capacity while preserving connection and readiness");
            yield return second.GetMe(user => Check(user.currentRoomName == "renamed-room", "renaming updates participant API room name"), fail);
            denied = false;
            yield return first.UpdateRoomSettings(lobby, room, "rejected-name", 1, _ => throw new Exception("Too small capacity accepted"), _ => denied = true);
            yield return first.GetRoom(lobby, room, value => Check(denied && value.name == "renamed-room" && value.maxUsers == 2 && value.availableSlots == 0,
                "shrink below connected count is atomic and preserves prior settings"), fail);
            yield return first.UpdateRoomSettings(lobby, room, "renamed-room", 3, _ => { }, fail);
            visitor = new TrainingApiClient(first.BaseUrl);
            yield return visitor.Login("config-" + Guid.NewGuid().ToString("N").Substring(0, 6), () => { }, fail);
            yield return visitor.JoinLobby(lobby, () => { }, fail);
            JoinRoomResponse admission = null;
            yield return visitor.JoinRoom(lobby, room, value => admission = value, fail);
            denied = false;
            yield return first.UpdateRoomSettings(lobby, room, "cannot-shrink", 2, _ => throw new Exception("Reserved slot removed"), _ => denied = true);
            yield return first.GetRoom(lobby, room, value => Check(denied && value.maxUsers == 3 && value.reservedUsers == 1 && value.name == "renamed-room",
                "capacity change respects outstanding reservation without cancelling it"), fail);
            visitingClient = new RoomNetworkClient();
            yield return visitingClient.Connect(visitor.User.id, admission, () => { }, fail);
            yield return WaitPlayers(visitingClient, 3);
            until = Time.realtimeSinceStartupAsDouble + 5;
            while (visitingClient.LocalPlayer.RoomName != "renamed-room" || visitingClient.LocalPlayer.RoomCapacity != 3)
            {
                if (Time.realtimeSinceStartupAsDouble > until)
                    throw new Exception("Late settings timeout");
                yield return null;
            }
            Check(!visitingClient.LocalPlayer.IsReady, "late join receives updated room settings and independent readiness");
            visitingClient.Dispose();
            visitingClient = null;
            yield return WaitMembership(visitor, room, false);
            yield return WaitPlayers(client, 2);
            yield return WaitPlayers(other, 2);
            yield return visitor.Logout(() => { }, fail);
            visitor.Dispose();
            visitor = null;
            BindScreen(screen, null, null, "", "");
        }

        private static IEnumerator WaitReady(RoomNetworkClient connection, string userId, bool expected)
        {
            double until = Time.realtimeSinceStartupAsDouble + 5;
            RoomPlayer player;
            while ((player = Array.Find(connection.Players, value => value.UserId == userId)) == null || player.IsReady != expected)
            {
                if (Time.realtimeSinceStartupAsDouble > until)
                    throw new Exception("Ready replication timed out");
                yield return null;
            }
        }
        private static IEnumerator ReadinessChecks(string lobby, string room)
        {
            Check(client.GetReadiness().ParticipantCount == 2 && client.GetReadiness().ReadyCount == 0 && !client.GetReadiness().AllReady,
                "all participants start unready including master");
            var disconnected = new RoomNetworkClient();
            Check(!disconnected.SetReady(true) && !disconnected.GetReadiness().AllReady, "disconnected client cannot prepare or report all ready");
            bool rejected = false;
            try
            {
                Array.Find(client.Players, player => !player.IsOwner).SetReadyRpc(true);
            }
            catch (Unity.Netcode.RpcException) { rejected = true; }
            Check(rejected, "non-owner cannot change another participant readiness");
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var screen = UnityEngine.Object.FindFirstObjectByType<ApiLobbyScreen>();
            BindScreen(screen, first, client, lobby, room);
            yield return (IEnumerator)typeof(ApiLobbyScreen).GetMethod("SetReady", flags).Invoke(screen, new object[] { true });
            yield return WaitReady(other, first.User.id, true);
            Check(client.GetReadiness().ReadyCount == 1 && other.GetReadiness().ReadyCount == 1 && !client.GetReadiness().AllReady &&
                !other.LocalPlayer.IsReady, "ready button synchronizes only its owner and keeps partial group unready");
            Check(other.SetReady(true), "ordinary participant can prepare");
            yield return WaitReady(client, second.User.id, true);
            yield return WaitReady(other, second.User.id, true);
            Check(client.GetReadiness().AllReady && other.GetReadiness().AllReady, "all ready is consistent on both clients including master");
            visitor = new TrainingApiClient(first.BaseUrl);
            Action<string> fail = message => throw new Exception(message);
            yield return visitor.Login("ready-" + Guid.NewGuid().ToString("N").Substring(0, 6), () => { }, fail);
            yield return visitor.JoinLobby(lobby, () => { }, fail);
            JoinRoomResponse admission = null;
            yield return visitor.JoinRoom(lobby, room, value => admission = value, fail);
            visitingClient = new RoomNetworkClient();
            yield return visitingClient.Connect(visitor.User.id, admission, () => { }, fail);
            yield return WaitPlayers(visitingClient, 3);
            yield return WaitPlayers(client, 3);
            yield return WaitPlayers(other, 3);
            yield return WaitReady(visitingClient, first.User.id, true);
            yield return WaitReady(visitingClient, second.User.id, true);
            Check(!visitingClient.LocalPlayer.IsReady && visitingClient.GetReadiness().ReadyCount == 2 &&
                !client.GetReadiness().AllReady && client.GetReadiness().ParticipantCount == 3,
                "late join receives existing readiness and starts unready, clearing all-ready");
            visitingClient.Dispose();
            visitingClient = null;
            yield return WaitMembership(visitor, room, false);
            yield return WaitPlayers(client, 2);
            yield return WaitPlayers(other, 2);
            Check(client.GetReadiness().AllReady && other.GetReadiness().AllReady,
                "departed unready participant no longer counts toward group readiness");
            yield return visitor.Logout(() => { }, fail);
            visitor.Dispose();
            visitor = null;
            yield return (IEnumerator)typeof(ApiLobbyScreen).GetMethod("SetReady", flags).Invoke(screen, new object[] { false });
            yield return WaitReady(other, first.User.id, false);
            Check(!client.GetReadiness().AllReady && client.GetReadiness().ReadyCount == 1 && other.LocalPlayer.IsReady,
                "cancel ready button clears all-ready without changing other participant");
            client.SetReady(true);
            yield return WaitReady(other, first.User.id, true);
            yield return first.TransferRoomMaster(lobby, room, second.User.id, () => { }, message => throw new Exception(message));
            yield return WaitMaster(client, second.User.id);
            yield return WaitMaster(other, second.User.id);
            Check(client.GetReadiness().AllReady && other.GetReadiness().AllReady, "manual master transfer preserves participant readiness");
            yield return second.TransferRoomMaster(lobby, room, first.User.id, () => { }, message => throw new Exception(message));
            yield return WaitMaster(client, first.User.id);
            yield return WaitMaster(other, first.User.id);
            BindScreen(screen, null, null, "", "");
        }

        private static IEnumerator ReconnectChecks(string lobby, string room)
        {
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var screen = UnityEngine.Object.FindFirstObjectByType<ApiLobbyScreen>();
            BindScreen(screen, first, client, lobby, room);
            var noticeObserver = RoomNoticeChecks.CreateObserver(second, other, lobby, room);
            ulong previousOwner = client.LocalPlayer.OwnerClientId;
            var manager = (Unity.Netcode.NetworkManager)typeof(RoomNetworkClient).GetField("manager", flags).GetValue(client);
            var transport = (Unity.Netcode.Transports.UTP.UnityTransport)manager.NetworkConfig.NetworkTransport;
            Check(transport.DisconnectTimeoutMS == 10000 && transport.HeartbeatTimeoutMS == 1000,
                "client uses bounded heartbeat and disconnect timeout");
            transport.DisconnectLocalClient();
            double until = Time.realtimeSinceStartupAsDouble + 20;
            while (string.IsNullOrEmpty((string)typeof(ApiLobbyScreen).GetField("reconnectRoomId", flags).GetValue(screen)) ||
                (bool)typeof(ApiLobbyScreen).GetField("busy", flags).GetValue(screen))
            {
                if (Time.realtimeSinceStartupAsDouble > until)
                    throw new Exception("Disconnect UI cleanup timed out");
                yield return null;
            }
            Check(first.IsLoggedIn && first.User.currentLobbyId == lobby && string.IsNullOrEmpty(first.User.currentRoomId) &&
                string.IsNullOrEmpty(first.User.pendingRoomId), "unexpected disconnect UI clears membership and retains login and lobby");
            yield return WaitPlayers(other, 1);
            yield return WaitMaster(other, second.User.id);
            RoomNoticeChecks.VerifyMasterDeparture(noticeObserver, first.User.name, second.User.name);
            RoomNoticeChecks.DestroyObserver(noticeObserver);
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
            Check(!client.LocalPlayer.IsReady && !client.GetReadiness().AllReady, "reconnecting participant starts unready");
            Check(string.IsNullOrEmpty((string)typeof(ApiLobbyScreen).GetField("reconnectRoomId", flags).GetValue(screen)),
                "successful reconnect clears retry target");
            yield return WaitPlayers(other, 2);
            yield return WaitMaster(client, second.User.id);
            yield return WaitPlayers(client, 2);
            yield return LatencyChecks(client, other);
            Check(!client.LocalPlayer.IsRoomMaster, "reconnect preserves automatically transferred master authority");
            BindScreen(screen, null, null, "", "");
            yield return second.TransferRoomMaster(lobby, room, first.User.id, () => { }, message => throw new Exception(message));
            yield return WaitMaster(client, first.User.id);
            yield return WaitMaster(other, first.User.id);
        }

        private static IEnumerator CloseRoomChecks(string lobby)
        {
            Action<string> fail = message => throw new Exception(message);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            string room = null;
            const string roomPassword = "native-room-42";
            yield return first.CreateRoom(lobby, "close-test", lobby, 3, id => room = id, fail, roomPassword);
            yield return first.GetRoom(lobby, room, value => Check(value.hasPassword, "protected room is marked in public query without password"), fail);
            yield return second.Login("close-b-" + Guid.NewGuid().ToString("N").Substring(0, 6), () => { }, fail);
            yield return second.JoinLobby(lobby, () => { }, fail);
            JoinRoomResponse admission = null;
            bool wrongPassword = false;
            yield return first.JoinRoom(lobby, room, _ => throw new Exception("Password bypass"), _ => wrongPassword = true);
            Check(wrongPassword, "protected room rejects creator admission without password");
            yield return first.GetRoom(lobby, room, value => Check(value.reservedUsers == 1 && value.connectedUsers == 0, "incorrect password preserves only initial creation reservation"), fail);
            yield return first.JoinRoom(lobby, room, value => admission = value, fail, roomPassword);
            client = new RoomNetworkClient();
            yield return client.Connect(first.User.id, admission, () => { }, fail);
            yield return second.JoinRoom(lobby, room, value => admission = value, fail, roomPassword);
            other = new RoomNetworkClient();
            yield return other.Connect(second.User.id, admission, () => { }, fail);
            yield return WaitPlayers(client, 2);
            yield return WaitPlayers(other, 2);
            bool denied = false;
            yield return second.CloseRoom(lobby, room, () => throw new Exception("Member closed room"), _ => denied = true);
            Check(denied && client.IsConnected && other.IsConnected, "close API rejects ordinary participant while preserving connections");
            yield return WaitMaster(client, first.User.id);
            yield return WaitMaster(other, first.User.id);
            var reconnectScreen = UnityEngine.Object.FindFirstObjectByType<ApiLobbyScreen>();
            BindScreen(reconnectScreen, first, client, lobby, room);
            var manager = (Unity.Netcode.NetworkManager)typeof(RoomNetworkClient).GetField("manager", flags).GetValue(client);
            ((Unity.Netcode.Transports.UTP.UnityTransport)manager.NetworkConfig.NetworkTransport).DisconnectLocalClient();
            double reconnectUntil = Time.realtimeSinceStartupAsDouble + 20;
            while (typeof(ApiLobbyScreen).GetField("network", flags).GetValue(reconnectScreen) != null ||
                (bool)typeof(ApiLobbyScreen).GetField("busy", flags).GetValue(reconnectScreen))
            {
                if (Time.realtimeSinceStartupAsDouble > reconnectUntil)
                    throw new Exception("Password reconnect cleanup timed out");
                yield return null;
            }
            yield return WaitMaster(other, second.User.id);
            yield return (IEnumerator)typeof(ApiLobbyScreen).GetMethod("ReconnectRoom", flags).Invoke(reconnectScreen, null);
            Check(typeof(ApiLobbyScreen).GetField("network", flags).GetValue(reconnectScreen) == null,
                "protected reconnect requires password again");
            typeof(ApiLobbyScreen).GetField("reconnectPassword", flags).SetValue(reconnectScreen, roomPassword);
            yield return (IEnumerator)typeof(ApiLobbyScreen).GetMethod("ReconnectRoom", flags).Invoke(reconnectScreen, null);
            client = (RoomNetworkClient)typeof(ApiLobbyScreen).GetField("network", flags).GetValue(reconnectScreen);
            Check(client != null && client.IsConnected && string.IsNullOrEmpty((string)typeof(ApiLobbyScreen).GetField("reconnectPassword", flags).GetValue(reconnectScreen)),
                "correct password reconnects with fresh player and clears password input");
            yield return WaitPlayers(client, 2);
            yield return WaitPlayers(other, 2);
            yield return second.TransferRoomMaster(lobby, room, first.User.id, () => { }, fail);
            yield return WaitMaster(client, first.User.id);
            yield return WaitMaster(other, first.User.id);
            yield return PasswordSettingsChecks(lobby, room, roomPassword);
            client.SetReady(true);
            other.SetReady(true);
            yield return WaitReady(client, second.User.id, true);
            yield return WaitReady(other, first.User.id, true);
            client.LocalPlayer.RequestPhaseRpc(true);
            yield return WaitPhase(client, true);
            yield return WaitPhase(other, true);
            var screen = UnityEngine.Object.FindFirstObjectByType<ApiLobbyScreen>();
            var root = new GameObject("CloseTargetScreen");
            var target = root.AddComponent<ApiLobbyScreen>();
            typeof(ApiLobbyScreen).GetField("testPauseLobbyUpdates", flags).SetValue(target, true);
            BindScreen(screen, first, client, lobby, room);
            BindScreen(target, second, other, lobby, room);
            RoomActionChecks.Request(screen, "Close");
            yield return RoomActionChecks.Confirm(screen);
            double until = Time.realtimeSinceStartupAsDouble + 15;
            while (typeof(ApiLobbyScreen).GetField("network", flags).GetValue(target) != null || (bool)typeof(ApiLobbyScreen).GetField("busy", flags).GetValue(target))
            {
                if (Time.realtimeSinceStartupAsDouble > until)
                    throw new Exception("Room close target cleanup timed out");
                yield return null;
            }
            Check(first.IsLoggedIn && second.IsLoggedIn && first.User.currentLobbyId == lobby && second.User.currentLobbyId == lobby &&
                string.IsNullOrEmpty(first.User.currentRoomId) && string.IsNullOrEmpty(second.User.currentRoomId),
                "closing running room returns both users to lobby and preserves login");
            Check(((string)typeof(ApiLobbyScreen).GetField("status", flags).GetValue(target)).Contains("방을 닫았습니다") &&
                string.IsNullOrEmpty((string)typeof(ApiLobbyScreen).GetField("reconnectRoomId", flags).GetValue(target)),
                "participant sees master close reason and no reconnect target");
            yield return first.GetRooms(lobby, items => Check(!Array.Exists(items, item => item.id == room), "closed room disappears from REST list"), fail);
            Check(!client.IsConnected && !other.IsConnected, "room close disconnects every Netcode connection");
            BindScreen(target, null, null, "", "");
            UnityEngine.Object.Destroy(root);
            BindScreen(screen, null, null, "", "");
            client.Dispose();
            other.Dispose();
            client = null;
            other = null;
            yield return second.Logout(() => { }, fail);
        }

        private static IEnumerator PasswordSettingsChecks(string lobby, string room, string oldPassword)
        {
            Action<string> fail = message => throw new Exception(message);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var screen = UnityEngine.Object.FindFirstObjectByType<ApiLobbyScreen>();
            BindScreen(screen, first, client, lobby, room);
            bool denied = false;
            yield return second.UpdateRoomPassword(lobby, room, "member-password", _ => throw new Exception("Member updated password"), _ => denied = true);
            Check(denied, "password change API rejects ordinary participant");
            visitor = new TrainingApiClient(first.BaseUrl);
            yield return visitor.Login("pw-" + Guid.NewGuid().ToString("N").Substring(0, 6), () => { }, fail);
            yield return visitor.JoinLobby(lobby, () => { }, fail);
            JoinRoomResponse stale = null;
            yield return visitor.JoinRoom(lobby, room, value => stale = value, fail, oldPassword);
            ulong firstOwner = client.LocalPlayer.OwnerClientId, secondOwner = other.LocalPlayer.OwnerClientId;
            const string nextPassword = "changed-native-42";
            yield return (IEnumerator)typeof(ApiLobbyScreen).GetMethod("UpdateRoomPassword", flags).Invoke(screen, new object[] { nextPassword });
            yield return first.GetRoom(lobby, room, value => Check(value.reservedUsers == 0 && value.connectedUsers == 2 && value.hasPassword,
                "password update cancels unused reservation and preserves two confirmed connections"), fail);
            visitingClient = new RoomNetworkClient();
            denied = false;
            yield return visitingClient.Connect(visitor.User.id, stale, () => throw new Exception("Stale reservation entered"), _ => denied = true);
            Check(denied, "actual dedicated server rejects admission issued before password change");
            visitingClient.Dispose();
            visitingClient = null;
            denied = false;
            yield return visitor.JoinRoom(lobby, room, _ => throw new Exception("Old password accepted"), _ => denied = true, oldPassword);
            Check(denied, "old room password is rejected after update");
            yield return visitor.JoinRoom(lobby, room, _ => { }, fail, nextPassword);
            yield return visitor.LeaveRoom(lobby, room, () => { }, fail);
            var root = new GameObject("PasswordObserverScreen");
            var observer = root.AddComponent<ApiLobbyScreen>();
            BindScreen(observer, second, other, lobby, room);
            typeof(ApiLobbyScreen).GetField("selectedLobbyId", flags).SetValue(observer, lobby);
            Func<RoomSummary> displayed = () => Array.Find((RoomSummary[])typeof(ApiLobbyScreen).GetField("rooms", flags).GetValue(observer), item => item.id == room);
            double until = Time.realtimeSinceStartupAsDouble + 20;
            while (displayed() == null || (bool)typeof(ApiLobbyScreen).GetField("busy", flags).GetValue(observer))
            {
                if (Time.realtimeSinceStartupAsDouble > until)
                    throw new Exception("Password observer subscription timed out");
                yield return null;
            }
            yield return (IEnumerator)typeof(ApiLobbyScreen).GetMethod("UpdateRoomPassword", flags).Invoke(screen, new object[] { "" });
            until = Time.realtimeSinceStartupAsDouble + 8;
            while (displayed().hasPassword)
            {
                if (Time.realtimeSinceStartupAsDouble > until)
                    throw new Exception("Password removal marker timed out");
                yield return null;
            }
            Check(true, "password removal automatically clears protection marker on other screen");
            yield return visitor.JoinRoom(lobby, room, _ => { }, fail);
            yield return visitor.LeaveRoom(lobby, room, () => { }, fail);
            Check(true, "password removed room accepts new admission without password");
            yield return (IEnumerator)typeof(ApiLobbyScreen).GetMethod("UpdateRoomPassword", flags).Invoke(screen, new object[] { nextPassword });
            until = Time.realtimeSinceStartupAsDouble + 8;
            while (!displayed().hasPassword)
            {
                if (Time.realtimeSinceStartupAsDouble > until)
                    throw new Exception("Password protection marker timed out");
                yield return null;
            }
            Check(client.IsConnected && other.IsConnected && client.LocalPlayer.OwnerClientId == firstOwner && other.LocalPlayer.OwnerClientId == secondOwner,
                "password set on public room restores marker while existing owned players remain connected");
            typeof(ApiLobbyScreen).GetMethod("DisposeLobbyUpdates", flags).Invoke(observer, null);
            BindScreen(observer, null, null, "", "");
            UnityEngine.Object.Destroy(root);
            yield return visitor.Logout(() => { }, fail);
            visitor.Dispose();
            visitor = null;
        }

        private static IEnumerator GlobalLobbyListChecks(string lobby, string room)
        {
            Action<string> fail = message => throw new Exception(message);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var screen = UnityEngine.Object.FindFirstObjectByType<ApiLobbyScreen>();
            typeof(ApiLobbyScreen).GetField("testPauseLobbyUpdates", flags).SetValue(screen, false);
            // Exercise the scene's default HTTP address, including its redirect to HTTPS.
            string http = Environment.GetEnvironmentVariable("SMART_TRAINING_TEST_HTTP_URL");
            visitor = new TrainingApiClient(string.IsNullOrEmpty(http) ? first.BaseUrl : http);
            yield return visitor.Login("counts-" + Guid.NewGuid().ToString("N").Substring(0, 6), () => { }, fail);
            BindScreen(screen, visitor, null, "", "");
            typeof(ApiLobbyScreen).GetField("selectedLobbyId", flags).SetValue(screen, "");
            Func<LobbySummary> displayed = () => Array.Find((LobbySummary[])typeof(ApiLobbyScreen).GetField("lobbies", flags).GetValue(screen), item => item.id == lobby);
            double until = Time.realtimeSinceStartupAsDouble + 20;
            while (displayed() == null || (bool)typeof(ApiLobbyScreen).GetField("busy", flags).GetValue(screen))
            {
                if (Time.realtimeSinceStartupAsDouble > until)
                    throw new Exception("Global lobby initial list timed out");
                yield return null;
            }
            Check(string.IsNullOrEmpty(visitor.User.currentLobbyId) && displayed().roomCount == 1 && displayed().userCount == 2,
                "lobby counts load automatically before lobby entry over configured HTTP address");
            string extraRoom = null;
            var publisher = new TrainingApiClient(first.BaseUrl);
            yield return publisher.Login("count-" + Guid.NewGuid().ToString("N").Substring(0, 6), () => { }, fail);
            yield return publisher.JoinLobby(lobby, () => { }, fail);
            yield return publisher.CreateRoom(lobby, "count-room", lobby, 3, id => extraRoom = id, fail);
            until = Time.realtimeSinceStartupAsDouble + 8;
            while (displayed().roomCount != 2)
            {
                if (Time.realtimeSinceStartupAsDouble > until)
                    throw new Exception("Global room count timed out");
                yield return null;
            }
            Check(true, "room creation updates lobby row room count without manual refresh");
            yield return publisher.Logout(() => { }, fail);
            publisher.Dispose();
            until = Time.realtimeSinceStartupAsDouble + 8;
            while (displayed().roomCount != 1 || displayed().userCount != 2)
            {
                if (Time.realtimeSinceStartupAsDouble > until)
                    throw new Exception("Cancelled creation count timed out");
                yield return null;
            }
            Check(true, "creator logout cancels initial reservation and removes empty room");
            var member = new TrainingApiClient(first.BaseUrl);
            yield return member.Login("member-" + Guid.NewGuid().ToString("N").Substring(0, 6), () => { }, fail);
            yield return member.JoinLobby(lobby, () => { }, fail);
            until = Time.realtimeSinceStartupAsDouble + 8;
            while (displayed().userCount != 3)
            {
                if (Time.realtimeSinceStartupAsDouble > until)
                    throw new Exception("Global participant count timed out");
                yield return null;
            }
            Check(true, "lobby entry updates lobby row user count without manual refresh");
            yield return member.LeaveLobby(lobby, () => { }, fail);
            yield return member.Logout(() => { }, fail);
            member.Dispose();
            until = Time.realtimeSinceStartupAsDouble + 8;
            while (displayed().userCount != 2)
            {
                if (Time.realtimeSinceStartupAsDouble > until)
                    throw new Exception("Global departure count timed out");
                yield return null;
            }
            Check(true, "lobby departure automatically reduces displayed participant count");
            typeof(ApiLobbyScreen).GetMethod("DisposeLobbyUpdates", flags).Invoke(screen, null);
            typeof(ApiLobbyScreen).GetField("testPauseLobbyUpdates", flags).SetValue(screen, true);
            BindScreen(screen, null, null, "", "");
            yield return visitor.Logout(() => { }, fail);
            visitor.Dispose();
            visitor = null;
        }

        private static IEnumerator LiveRoomListChecks(string lobby, string room)
        {
            Action<string> fail = message => throw new Exception(message);
            using (var anonymous = new RoomHubConnection())
            {
                var unauthorized = anonymous.Connect(first.BaseUrl, "/LobbyUpdatesHub");
                double deadline = Time.realtimeSinceStartupAsDouble + 12;
                while (!unauthorized.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline)
                {
                    anonymous.Tick();
                    yield return null;
                }
                Check(unauthorized.IsFaulted, "SignalR lobby updates rejects anonymous WebSocket connection");
                _ = unauthorized.Exception;
            }
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var screen = UnityEngine.Object.FindFirstObjectByType<ApiLobbyScreen>();
            typeof(ApiLobbyScreen).GetField("testPauseLobbyUpdates", flags).SetValue(screen, false);
            BindScreen(screen, second, other, lobby, room);
            typeof(ApiLobbyScreen).GetField("selectedLobbyId", flags).SetValue(screen, lobby);
            Func<RoomSummary> displayed = () => Array.Find((RoomSummary[])typeof(ApiLobbyScreen).GetField("rooms", flags).GetValue(screen), item => item.id == room);
            double until = Time.realtimeSinceStartupAsDouble + 20;
            while (displayed() == null || (bool)typeof(ApiLobbyScreen).GetField("busy", flags).GetValue(screen))
            {
                if (Time.realtimeSinceStartupAsDouble > until)
                    throw new Exception("Initial live room list timed out");
                yield return null;
            }
            Check(displayed().connectedUsers == 2, "authenticated SignalR subscription loads initial room list automatically");
            typeof(ApiLobbyScreen).GetField("roomSearch", flags).SetValue(screen, "LIVE-");
            typeof(ApiLobbyScreen).GetField("availableRoomsOnly", flags).SetValue(screen, true);
            typeof(ApiLobbyScreen).GetField("roomProtectionFilter", flags).SetValue(screen, RoomProtectionFilter.Public);
            typeof(ApiLobbyScreen).GetField("roomSort", flags).SetValue(screen, RoomSortOrder.Vacancies);
            Func<RoomSummary[]> visible = () => (RoomSummary[])typeof(ApiLobbyScreen).GetMethod("GetDisplayedRooms", flags).Invoke(screen, null);
            Check(visible().Length == 0, "room search initially hides nonmatching server snapshot");
            yield return first.UpdateRoomSettings(lobby, room, "live-renamed", 4, _ => { }, fail);
            until = Time.realtimeSinceStartupAsDouble + 8;
            while (displayed().name != "live-renamed" || displayed().maxUsers != 4)
            {
                if (Time.realtimeSinceStartupAsDouble > until)
                    throw new Exception("Live settings update timed out");
                yield return null;
            }
            Check(true, "other participant sees name and capacity change without refresh button");
            Check(visible().Length == 1 && visible()[0].id == room, "live rename enters filtered result without clearing search conditions");
            yield return first.SetRoomEntryLock(lobby, room, true, () => { }, fail);
            until = Time.realtimeSinceStartupAsDouble + 8;
            while (!displayed().isEntryLocked)
            {
                if (Time.realtimeSinceStartupAsDouble > until)
                    throw new Exception("Live lock update timed out");
                yield return null;
            }
            Check(true, "other participant sees entry lock automatically");
            Check(visible().Length == 0, "live entry lock removes room from available-only result");
            var connection = (RoomHubConnection)typeof(ApiLobbyScreen).GetField("lobbyUpdates", flags).GetValue(screen);
            connection.Dispose();
            until = Time.realtimeSinceStartupAsDouble + 20;
            while (typeof(ApiLobbyScreen).GetField("lobbyUpdates", flags).GetValue(screen) == connection ||
                typeof(ApiLobbyScreen).GetField("lobbyUpdates", flags).GetValue(screen) == null ||
                (bool)typeof(ApiLobbyScreen).GetField("busy", flags).GetValue(screen))
            {
                if (Time.realtimeSinceStartupAsDouble > until)
                    throw new Exception("Live notification reconnect timed out");
                yield return null;
            }
            yield return first.SetRoomEntryLock(lobby, room, false, () => { }, fail);
            until = Time.realtimeSinceStartupAsDouble + 8;
            while (displayed().isEntryLocked)
            {
                if (Time.realtimeSinceStartupAsDouble > until)
                    throw new Exception("Live recovery snapshot timed out");
                yield return null;
            }
            Check(other.IsConnected && client.IsConnected, "notification reconnect refreshes snapshot while Netcode connections survive");
            Check(visible().Length == 1 && (string)typeof(ApiLobbyScreen).GetField("roomSearch", flags).GetValue(screen) == "LIVE-" &&
                (RoomSortOrder)typeof(ApiLobbyScreen).GetField("roomSort", flags).GetValue(screen) == RoomSortOrder.Vacancies,
                "unlock and notification reconnect restore filtered result while preserving sort and search");
            typeof(ApiLobbyScreen).GetMethod("ResetRoomBrowser", flags).Invoke(screen, null);
            yield return first.UpdateRoomSettings(lobby, room, "network-room", 3, _ => { }, fail);
            typeof(ApiLobbyScreen).GetMethod("DisposeLobbyUpdates", flags).Invoke(screen, null);
            typeof(ApiLobbyScreen).GetField("selectedLobbyId", flags).SetValue(screen, "");
            typeof(ApiLobbyScreen).GetField("testPauseLobbyUpdates", flags).SetValue(screen, true);
            BindScreen(screen, null, null, "", "");
        }

        private static IEnumerator RunningDisconnectChecks(string lobby, string room)
        {
            Action<string> fail = message => throw new Exception(message);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var screen = UnityEngine.Object.FindFirstObjectByType<ApiLobbyScreen>();
            BindScreen(screen, first, client, lobby, room);
            client.SetReady(true);
            other.SetReady(true);
            yield return WaitReady(client, second.User.id, true);
            yield return WaitReady(other, first.User.id, true);
            yield return (IEnumerator)typeof(ApiLobbyScreen).GetMethod("ChangeRoomPhase", flags).Invoke(screen, new object[] { true });
            yield return WaitPhase(client, true);
            yield return WaitPhase(other, true);
            var manager = (Unity.Netcode.NetworkManager)typeof(RoomNetworkClient).GetField("manager", flags).GetValue(client);
            ((Unity.Netcode.Transports.UTP.UnityTransport)manager.NetworkConfig.NetworkTransport).DisconnectLocalClient();
            double until = Time.realtimeSinceStartupAsDouble + 20;
            while (typeof(ApiLobbyScreen).GetField("network", flags).GetValue(screen) != null ||
                (bool)typeof(ApiLobbyScreen).GetField("busy", flags).GetValue(screen))
            {
                if (Time.realtimeSinceStartupAsDouble > until)
                    throw new Exception("Running disconnect cleanup timed out");
                yield return null;
            }
            yield return WaitPlayers(other, 1);
            yield return WaitMaster(other, second.User.id);
            Check(other.IsConnected && !other.WasDisconnected && other.LocalPlayer.RoomIsRunning && !other.LocalPlayer.PhasePending,
                "unexpected running master disconnect preserves survivor connection and transfers authority without stuck transition");
            yield return first.GetRoom(lobby, room, value => Check(value.isRunning && value.connectedUsers == 1,
                "ASP.NET running phase matches surviving client after master disconnect"), fail);
            yield return (IEnumerator)typeof(ApiLobbyScreen).GetMethod("ReconnectRoom", flags).Invoke(screen, null);
            Check(typeof(ApiLobbyScreen).GetField("network", flags).GetValue(screen) == null && string.IsNullOrEmpty(first.User.pendingRoomId),
                "running room rejects reconnect without leaving an admission reservation");
            other.LocalPlayer.RequestPhaseRpc(false);
            yield return WaitPhase(other, false);
            yield return (IEnumerator)typeof(ApiLobbyScreen).GetMethod("ReconnectRoom", flags).Invoke(screen, null);
            client = (RoomNetworkClient)typeof(ApiLobbyScreen).GetField("network", flags).GetValue(screen);
            Check(client != null && client.IsConnected && !client.LocalPlayer.IsReady && !client.LocalPlayer.RoomIsRunning,
                "reconnect succeeds after new master returns to waiting and resets readiness");
            yield return WaitPlayers(client, 2);
            yield return WaitPlayers(other, 2);
            yield return second.TransferRoomMaster(lobby, room, first.User.id, () => { }, fail);
            yield return WaitMaster(client, first.User.id);
            yield return WaitMaster(other, first.User.id);
            BindScreen(screen, null, null, "", "");
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
            var pendingObserver = RoomActionChecks.ObservePendingKick(first, client, lobby, room, second.User.id);
            RoomActionChecks.Request(screen, "Kick", second.User.id);
            yield return RoomActionChecks.Confirm(screen);
            RoomActionChecks.VerifyDepartedTarget(pendingObserver);
            double until = Time.realtimeSinceStartupAsDouble + 10;
            while ((bool)typeof(ApiLobbyScreen).GetField("busy", flags).GetValue(targetScreen) ||
                typeof(ApiLobbyScreen).GetField("network", flags).GetValue(targetScreen) != null)
            {
                if (Time.realtimeSinceStartupAsDouble > until)
                    throw new TimeoutException("Kick target UI cleanup timed out");
                yield return null;
            }
            Check(!other.IsConnected && second.IsLoggedIn && second.User.currentLobbyId == lobby &&
                string.IsNullOrEmpty(second.User.currentRoomId), "kick returns target UI to lobby while preserving login");
            Check(((string)typeof(ApiLobbyScreen).GetField("status", flags).GetValue(targetScreen)).Contains("강퇴"),
                "target sees actual server-supplied kick reason");
            Check(string.IsNullOrEmpty((string)typeof(ApiLobbyScreen).GetField("reconnectRoomId", flags).GetValue(targetScreen)),
                "kick does not offer connection recovery target");
            yield return WaitPlayers(client, 1);
            yield return WaitMaster(client, first.User.id);
            yield return first.GetRoom(lobby, room, value => Check(value.connectedUsers == 1 && value.reservedUsers == 0 && value.availableSlots == 2,
                "confirmed UDP kick frees exactly one room slot"), fail);
            Check(client.IsConnected && client.LocalPlayer.OwnerClientId == ownerId &&
                ((string)typeof(ApiLobbyScreen).GetField("status", flags).GetValue(screen)).Contains("강퇴했습니다"),
                "kick button confirms departure while master connection survives");
            BindScreen(targetScreen, null, null, "", "");
            UnityEngine.Object.Destroy(targetRoot);
            BindScreen(screen, null, null, "", "");
            other.Dispose();
            other = null;
            JoinRoomResponse admission = null;
            yield return second.JoinRoom(lobby, room, value => admission = value, fail);
            other = new RoomNetworkClient();
            yield return other.Connect(second.User.id, admission, () => { }, fail);
            yield return WaitMembership(second, room, true);
            yield return WaitPlayers(client, 2);
            yield return WaitPlayers(other, 2);
            yield return WaitMaster(other, first.User.id);
            Check(other.LocalPlayer.OwnerClientId != oldOtherId && client.LocalPlayer.OwnerClientId == ownerId &&
                !other.LocalPlayer.IsRoomMaster, "kicked user rejoins with fresh connection while master and room remain intact");
            Check(!other.LocalPlayer.IsReady && client.GetReadiness().ReadyCount == 1 && !client.GetReadiness().AllReady,
                "kicked participant rejoins unready while remaining participant keeps readiness");
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
                Time.realtimeSinceStartupAsDouble < until)
                yield return null;
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
            yield return WaitEntryLock(client, true);
            yield return WaitEntryLock(other, true);
            yield return visitor.GetMe(user => Check(string.IsNullOrEmpty(user.pendingRoomId) && user.currentLobbyId == lobby,
                "lock button cancels pending admission and preserves lobby"), fail);
            yield return first.GetRoom(lobby, room, value => Check(value.isEntryLocked && value.reservedUsers == 0 && value.connectedUsers == 2,
                "locked room summary preserves connected users and releases reservations"), fail);
            visitingClient = new RoomNetworkClient();
            denied = false;
            yield return visitingClient.Connect(visitor.User.id, pending, () => throw new Exception("Canceled admission connected"), _ => denied = true);
            Check(denied, "dedicated server rejects previously issued token after entry lock");
            visitingClient.Dispose();
            visitingClient = null;
            denied = false;
            yield return visitor.JoinRoom(lobby, room, _ => throw new Exception("Locked join created reservation"), message => denied = message.Contains("잠"));
            Check(denied, "locked room rejects new join even with a free slot");
            var start = client.LocalPlayer.ServerPosition;
            yield return Move(client, Vector2.right, 0.4f);
            yield return Settle();
            Check(client.IsConnected && other.IsConnected && client.LocalPlayer.ServerPosition.x > start.x + 0.5f,
                "entry lock preserves existing movement and both connections");
            yield return first.TransferRoomMaster(lobby, room, second.User.id, () => { }, fail);
            yield return WaitMaster(client, second.User.id);
            yield return WaitEntryLock(other, true);
            denied = false;
            yield return first.SetRoomEntryLock(lobby, room, false, () => throw new Exception("Former master unlocked room"), _ => denied = true);
            Check(denied, "former master cannot reopen locked room");
            yield return second.SetRoomEntryLock(lobby, room, false, () => { }, fail);
            yield return WaitEntryLock(client, false);
            yield return WaitEntryLock(other, false);
            JoinRoomResponse fresh = null;
            yield return visitor.JoinRoom(lobby, room, value => fresh = value, fail);
            Check(fresh.admissionToken != pending.admissionToken, "unlock requires a fresh admission token");
            visitingClient = new RoomNetworkClient();
            yield return visitingClient.Connect(visitor.User.id, fresh, () => { }, fail);
            yield return WaitMembership(visitor, room, true);
            yield return WaitPlayers(visitingClient, 3);
            yield return WaitPlayers(client, 3);
            yield return WaitPlayers(other, 3);
            Check(client.IsConnected && other.IsConnected && visitingClient.IsConnected,
                "reopened room accepts third client without reconnecting existing clients");
            visitingClient.Dispose();
            visitingClient = null;
            yield return WaitMembership(visitor, room, false);
            yield return WaitPlayers(client, 2);
            yield return WaitPlayers(other, 2);
            yield return visitor.Logout(() => { }, fail);
            visitor.Dispose();
            visitor = null;
            yield return second.TransferRoomMaster(lobby, room, first.User.id, () => { }, fail);
            yield return WaitMaster(client, first.User.id);
            yield return WaitMaster(other, first.User.id);
        }
        private static IEnumerator Move(RoomNetworkClient connection, Vector2 direction, float seconds)
        {
            double until = Time.realtimeSinceStartupAsDouble + seconds;
            while (Time.realtimeSinceStartupAsDouble < until)
            {
                connection.SendMovement(direction);
                yield return null;
            }
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
            try
            {
                Array.Find(client.Players, player => !player.IsOwner).MoveRpc(Vector2.up);
            }
            catch (Unity.Netcode.RpcException) { rejected = true; }
            Check(rejected, "non-owner cannot submit movement for another player");
            start = after;
            yield return Move(client, new Vector2(100, 100), 0.8f);
            yield return Settle();
            after = client.LocalPlayer.ServerPosition;
            Check(Vector3.Distance(start, after) > 1 && Vector3.Distance(start, after) < 4,
                "oversized diagonal input cannot increase movement speed");
            start = after;
            yield return Move(client, new Vector2(float.NaN, float.PositiveInfinity), 0.4f);
            yield return Settle();
            Check(Vector3.Distance(start, client.LocalPlayer.ServerPosition) < 0.1f, "server rejects non-finite movement input");
            yield return Move(client, Vector2.up, 0.2f);
            double stopBy = Time.realtimeSinceStartupAsDouble + 0.7f;
            while (Time.realtimeSinceStartupAsDouble < stopBy)
                yield return null;
            start = client.LocalPlayer.ServerPosition;
            stopBy = Time.realtimeSinceStartupAsDouble + 0.4f;
            while (Time.realtimeSinceStartupAsDouble < stopBy)
                yield return null;
            Check(Vector3.Distance(start, client.LocalPlayer.ServerPosition) < 0.1f, "lost input stops movement instead of continuing indefinitely");
            yield return Move(client, Vector2.right, 4);
            yield return Settle();
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
                if (!matched)
                    yield return new WaitForSecondsRealtime(0.1f);
            }
            Check(matched, expected ? "backend confirms connected membership" : "backend clears disconnected membership");
        }
        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline)
                    throw new TimeoutException("Room server end-to-end test timed out");
                if (routines.Count == 0)
                {
                    Finish(0);
                    return;
                }
                var routine = routines.Peek();
                if (routine.Current is AsyncOperation pending && !pending.isDone)
                    return;
                if (!routine.MoveNext())
                {
                    routines.Pop();
                    return;
                }
                if (routine.Current is IEnumerator nested)
                    routines.Push(nested);
            }
            catch (Exception error) { Debug.LogError("ROOM CHECK FAILED: " + error.Message); Finish(1); }
        }
        private static void Finish(int code)
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= Log;
            client?.Dispose();
            other?.Dispose();
            visitingClient?.Dispose();
            first?.Dispose();
            second?.Dispose();
            visitor?.Dispose();
            routines.Clear();
            EditorSettings.enterPlayModeOptionsEnabled = optionsEnabled;
            EditorSettings.enterPlayModeOptions = options;
            EditorApplication.Exit(code);
        }
    }
}













