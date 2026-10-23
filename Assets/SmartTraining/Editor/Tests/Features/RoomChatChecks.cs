using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SmartTraining.Editor
{
    public static class RoomChatChecks
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static object Get(ApiLobbyScreen screen, string field) => typeof(ApiLobbyScreen).GetField(field, Flags).GetValue(screen);
        private static void Set(ApiLobbyScreen screen, string field, object value) => typeof(ApiLobbyScreen).GetField(field, Flags).SetValue(screen, value);
        private static IEnumerator Call(ApiLobbyScreen screen, string method, params object[] args) => (IEnumerator)typeof(ApiLobbyScreen).GetMethod(method, Flags).Invoke(screen, args);
        private static void Check(bool ok, string name)
        {
            if (!ok)
                throw new InvalidOperationException(name);
            Debug.Log("ROOM PASS: " + name);
        }
        private static List<LobbyChatMessage> Messages(ApiLobbyScreen screen, bool room = true) => (List<LobbyChatMessage>)Get(screen, room ? "roomChatMessages" : "chatMessages");
        private static RoomHubConnection Connection(ApiLobbyScreen screen) => (RoomHubConnection)Get(screen, "lobbyUpdates");
        private static ApiLobbyScreen Screen(TrainingApiClient api, RoomNetworkClient network, string lobby, string room)
        {
            var screen = new GameObject("room-chat-test").AddComponent<ApiLobbyScreen>();
            screen.enabled = false;
            Set(screen, "api", api);
            Set(screen, "network", network);
            Set(screen, "selectedLobbyId", lobby);
            Set(screen, "networkLobbyId", lobby);
            Set(screen, "networkRoomId", room);
            return screen;
        }
        private static IEnumerator Pump(ApiLobbyScreen a, ApiLobbyScreen b, ApiLobbyScreen c, double seconds)
        {
            double end = Time.realtimeSinceStartupAsDouble + seconds;
            while (Time.realtimeSinceStartupAsDouble < end)
            {
                Connection(a)?.Tick();
                Connection(b)?.Tick();
                Connection(c)?.Tick();
                yield return null;
            }
        }
        public static IEnumerator Run(TrainingApiClient first, TrainingApiClient second, RoomNetworkClient client, RoomNetworkClient other, string lobby, string room)
        {
            Action<string> fail = error => throw new InvalidOperationException(error);
            var observer = new TrainingApiClient(first.BaseUrl);
            var third = new RoomNetworkClient();
            ApiLobbyScreen a = null, b = null, c = null;
            try
            {
                yield return observer.Login("rch-" + Guid.NewGuid().ToString("N").Substring(0, 6), () => { }, fail);
                yield return observer.JoinLobby(lobby, () => { }, fail);
                string separateRoom = null;
                yield return observer.CreateRoom(lobby, "chat-other-room", "sample-training", 2, id => separateRoom = id, fail);
                JoinRoomResponse reservation = null;
                yield return observer.JoinRoom(lobby, separateRoom, value => reservation = value, fail);
                a = Screen(first, client, lobby, room);
                b = Screen(second, other, lobby, room);
                c = Screen(observer, null, lobby, separateRoom);
                yield return Call(a, "ConnectLobbyRooms", lobby);
                yield return Call(b, "ConnectLobbyRooms", lobby);
                yield return Call(c, "ConnectLobbyRooms", lobby);
                var pending = Connection(c).Invoke("SubscribeRoomChat", separateRoom, reservation.admissionToken);
                while (!pending.IsCompleted)
                {
                    Connection(c).Tick();
                    yield return null;
                }
                Check(pending.IsCompletedSuccessfully && !pending.Result.success, "reserved client cannot subscribe to room chat before UDP confirmation");
                yield return Call(a, "BindRoomChat");
                yield return Call(b, "BindRoomChat");
                Set(a, "chatChannel", 1);
                Set(b, "chatChannel", 1);
                Set(a, "chatInput", "방 참가자만 🙂");
                yield return Call(a, "SendRoomChat");
                yield return Pump(a, b, c, .3);
                Check(Messages(a).Count == 1 && Messages(b).Count == 1 && Messages(c).Count == 0 && Messages(c, false).Count == 0, "room chat reaches sender and participant while excluding same-lobby observer");
                Check(Messages(b)[0].roomId == room && Messages(b)[0].nickname == first.User.name && Messages(b)[0].text == "방 참가자만 🙂", "room chat wire carries authoritative room identity and unicode");
                Check(Messages(a, false).Count == 0 && Messages(b, false).Count == 0 && (string)Get(a, "chatInput") == "", "room and lobby message lists are separate and successful input clears");
                Set(a, "chatInput", "cross-channel spam");
                yield return Call(a, "SendLobbyChat");
                Check(((string)Get(a, "chatStatus")).Contains("1초"), "room and lobby sends share rate-limit rejection in Unity");
                yield return third.Connect(observer.User.id, reservation, () => { }, fail);
                double deadline = Time.realtimeSinceStartupAsDouble + 8;
                while (third.LocalPlayer == null && Time.realtimeSinceStartupAsDouble < deadline)
                    yield return null;
                Set(c, "network", third);
                yield return Call(c, "BindRoomChat");
                Set(c, "chatInput", "other room only");
                yield return Call(c, "SendRoomChat");
                yield return Pump(a, b, c, .3);
                Check(Messages(c).Count == 1 && Messages(a).Count == 1 && Messages(b).Count == 1, "two dedicated rooms keep room chats isolated within same lobby");
                yield return Pump(a, b, c, 1.1);
                Set(a, "chatInput", "로비 전체");
                yield return Call(a, "SendLobbyChat");
                yield return Pump(a, b, c, .3);
                Check(Messages(a, false).Count == 1 && Messages(b, false).Count == 1 && Messages(c, false).Count == 1, "lobby chat still reaches both rooms independently of room chat channel");
                string oldToken = (string)typeof(RoomNetworkClient).GetProperty("AdmissionToken", Flags).GetValue(other);
                yield return first.KickRoomUser(lobby, room, second.User.id, () => { }, fail);
                deadline = Time.realtimeSinceStartupAsDouble + 12;
                while (other.IsConnected && Time.realtimeSinceStartupAsDouble < deadline)
                    yield return null;
                yield return second.GetMe(_ => { }, fail);
                typeof(ApiLobbyScreen).GetMethod("SyncChatLobby", Flags).Invoke(b, null);
                Check(!other.IsConnected && Messages(b).Count == 0 && (string)Get(b, "chatInput") == "" && Messages(b, false).Count == 1, "kick clears room chat and retains lobby messages");
                var stale = Connection(b).Invoke("SendRoomChat", room, oldToken, "stale");
                while (!stale.IsCompleted)
                {
                    Connection(b).Tick();
                    yield return null;
                }
                Check(stale.IsCompletedSuccessfully && !stale.Result.success, "kicked participant cannot send on surviving lobby socket");
                other.Dispose();
                JoinRoomResponse retry = null;
                yield return second.JoinRoom(lobby, room, value => retry = value, fail);
                yield return other.Connect(second.User.id, retry, () => { }, fail);
                deadline = Time.realtimeSinceStartupAsDouble + 8;
                while (other.LocalPlayer == null && Time.realtimeSinceStartupAsDouble < deadline)
                    yield return null;
                yield return Call(b, "BindRoomChat");
                Set(b, "chatInput", "재접속 방 채팅");
                yield return Call(b, "SendRoomChat");
                yield return Pump(a, b, c, .3);
                Check(Messages(a).Exists(item => item.text == "재접속 방 채팅") && Messages(b).Count == 1 && Messages(c).Count == 1, "fresh room admission restores chat after kick and reconnect");
                var delayed = JsonUtility.ToJson(new LobbyChatMessage { id = Guid.NewGuid().ToString("N"), lobbyId = lobby, roomId = room, sequence = (long)Get(b, "roomChatAfterSequence"), text = "late old subscription" });
                typeof(ApiLobbyScreen).GetMethod("ReceiveRoomChat", Flags).Invoke(b, new object[] { Connection(b), delayed, room });
                Check(Messages(b).Count == 1 && retry.admissionToken != oldToken, "late room group message from before re-subscription is discarded");
                // 구독 완료보다 메시지가 먼저 온 경우, 응답의 기준 번호를 받은 뒤 표시합니다.
                long floor = (long)Get(b, "roomChatAfterSequence");
                int beforePending = Messages(b).Count;
                Set(b, "roomChatAfterSequence", -1L);
                var pendingMessage = new LobbyChatMessage { id = Guid.NewGuid().ToString("N"), lobbyId = lobby, roomId = room, sequence = floor + 1, text = "before subscription reply" };
                var receive = typeof(ApiLobbyScreen).GetMethod("ReceiveRoomChat", Flags);
                receive.Invoke(b, new object[] { Connection(b), delayed, room });
                receive.Invoke(b, new object[] { Connection(b), JsonUtility.ToJson(pendingMessage), room });
                Check(Messages(b).Count == beforePending, "room messages wait for subscription watermark");
                typeof(ApiLobbyScreen).GetMethod("CompleteRoomChatBinding", Flags).Invoke(b, new object[] { floor });
                Check(Messages(b).Count == beforePending + 1 && Messages(b).Exists(item => item.id == pendingMessage.id), "pending room messages exclude stale frames and retain fresh frames");
                Messages(b).RemoveAll(item => item.id == pendingMessage.id);
                Check(client.IsConnected && other.IsConnected, "room chat and reconnect preserve main Netcode connections");
                // Exercise the normal Update-driven reconnect/binding path, with the API idle.
                typeof(ApiLobbyScreen).GetMethod("DisposeLobbyUpdates", Flags).Invoke(a, null);
                a.enabled = true;
                deadline = Time.realtimeSinceStartupAsDouble + 20;
                while (!(bool)typeof(ApiLobbyScreen).GetMethod("RoomChatIsBound", Flags).Invoke(a, null) ||
                    (bool)Get(a, "busy") || (bool)Get(a, "roomsDirty"))
                {
                    if (Time.realtimeSinceStartupAsDouble > deadline)
                        throw new Exception("Automatic room chat binding timed out");
                    Connection(b)?.Tick();
                    Connection(c)?.Tick();
                    yield return null;
                }
                Check(Messages(a).Exists(item => item.text == "재접속 방 채팅"), "automatic SignalR reconnect rebinds room chat and retains current room display");
                yield return Pump(a, b, c, 1.1);
                Set(b, "chatInput", "자동 연결 확인");
                yield return Call(b, "SendRoomChat");
                yield return Pump(a, b, c, .3);
                Check(Messages(a).Exists(item => item.text == "자동 연결 확인"), "Update-driven room chat binding receives fresh messages");
            }
            finally
            {
                foreach (var screen in new[] { a, b, c })
                if (screen != null)
                {
                    Connection(screen)?.Dispose();
                    Set(screen, "network", null);
                    Set(screen, "api", null);
                    screen.enabled = false;
                    UnityEngine.Object.DestroyImmediate(screen.gameObject);
                }
                third.Dispose();
                observer.LogoutOnShutdown();
                observer.Dispose();
            }
        }
    }
}
