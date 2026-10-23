using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace SmartTraining.Editor
{
    public static class RoomActionChecks
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static object Get(ApiLobbyScreen screen, string name) => typeof(ApiLobbyScreen).GetField(name, Flags).GetValue(screen);
        private static void Set(ApiLobbyScreen screen, string name, object value) => typeof(ApiLobbyScreen).GetField(name, Flags).SetValue(screen, value);
        private static object Call(ApiLobbyScreen screen, string name) => typeof(ApiLobbyScreen).GetMethod(name, Flags).Invoke(screen, null);
        public static void Request(ApiLobbyScreen screen, string action, string target = "")
        {
            var type = typeof(ApiLobbyScreen).GetNestedType("RoomAction", BindingFlags.NonPublic);
            typeof(ApiLobbyScreen).GetMethod("RequestRoomAction", Flags).Invoke(screen, new object[] { Enum.Parse(type, action), target });
        }
        private static bool Pending(ApiLobbyScreen screen) => Get(screen, "pendingRoomAction").ToString() != "None";
        private static void Check(bool value, string name)
        {
            if (!value)
                throw new InvalidOperationException(name);
            Debug.Log("ROOM PASS: " + name);
        }
        public static ApiLobbyScreen ObservePendingKick(TrainingApiClient api, RoomNetworkClient network, string lobby, string room, string target)
        {
            var screen = new GameObject("pending-kick-observer-test").AddComponent<ApiLobbyScreen>();
            screen.enabled = false;
            Set(screen, "api", api);
            Set(screen, "network", network);
            Set(screen, "networkLobbyId", lobby);
            Set(screen, "networkRoomId", room);
            Request(screen, "Kick", target);
            Check(Pending(screen), "departure observer opens pending target confirmation");
            return screen;
        }
        public static void VerifyDepartedTarget(ApiLobbyScreen screen)
        {
            try
            {
                Check(!(bool)Call(screen, "ValidateRoomAction") && !Pending(screen), "target departure cancels old confirmation without resending kick");
            }
            finally { Set(screen, "api", null); Set(screen, "network", null); UnityEngine.Object.DestroyImmediate(screen.gameObject); }
        }

        public static IEnumerator Confirm(ApiLobbyScreen screen)
        {
            Call(screen, "ConfirmRoomAction");
            Call(screen, "ConfirmRoomAction"); // Repeated click must not submit a second action.
            double until = Time.realtimeSinceStartupAsDouble + 20;
            while ((bool)Get(screen, "busy"))
            {
                if (Time.realtimeSinceStartupAsDouble > until)
                    throw new TimeoutException("Room action confirmation timed out");
                yield return null;
            }
            Check(!Pending(screen), "confirmed action clears pending request and ignores repeat click");
        }
        public static IEnumerator Run(TrainingApiClient masterApi, TrainingApiClient peerApi,
            RoomNetworkClient master, RoomNetworkClient peer, string lobby, string room)
        {
            var screen = new GameObject("room-action-confirmation-test").AddComponent<ApiLobbyScreen>();
            screen.enabled = false;
            Set(screen, "api", masterApi);
            Set(screen, "network", master);
            Set(screen, "networkLobbyId", lobby);
            Set(screen, "networkRoomId", room);
            Action<string> fail = message => throw new InvalidOperationException(message);
            try
            {
                foreach (string action in new[] { "TransferMaster", "Kick", "Close" })
                {
                    Request(screen, action, peerApi.User.id);
                    Check(Pending(screen) && !(bool)Get(screen, "busy"), action + " opens confirmation without sending request");
                    Call(screen, "CancelRoomAction");
                    Call(screen, "ConfirmRoomAction");
                    yield return masterApi.GetRoom(lobby, room, value => Check(value.connectedUsers == 2 && value.masterUserId == masterApi.User.id,
                        action + " cancellation preserves room, authority and participant connections"), fail);
                }
                Request(screen, "Kick", masterApi.User.id);
                Check(!Pending(screen), "confirmation rejects self-target");
                Set(screen, "busy", true);
                Request(screen, "Close");
                Set(screen, "busy", false);
                Check(!Pending(screen), "busy screen cannot queue another room action");
                Request(screen, "Kick", peerApi.User.id);
                Set(screen, "networkRoomId", "different-room");
                Check(!(bool)Call(screen, "ValidateRoomAction") && !Pending(screen), "room change invalidates pending confirmation");
                Set(screen, "networkRoomId", room);
                Request(screen, "Close");
                yield return masterApi.TransferRoomMaster(lobby, room, peerApi.User.id, () => { }, fail);
                double until = Time.realtimeSinceStartupAsDouble + 10;
                while (master.LocalPlayer.IsRoomMaster || !peer.LocalPlayer.IsRoomMaster)
                {
                    if (Time.realtimeSinceStartupAsDouble > until)
                        throw new TimeoutException("master change for confirmation test");
                    yield return null;
                }
                Check(!(bool)Call(screen, "ValidateRoomAction") && !Pending(screen), "loss of master authority cancels pending close confirmation");
                Request(screen, "Close");
                Check(!Pending(screen), "ordinary participant cannot open master action confirmation");
                yield return peerApi.TransferRoomMaster(lobby, room, masterApi.User.id, () => { }, fail);
                until = Time.realtimeSinceStartupAsDouble + 10;
                while (!master.LocalPlayer.IsRoomMaster || peer.LocalPlayer.IsRoomMaster)
                {
                    if (Time.realtimeSinceStartupAsDouble > until)
                        throw new TimeoutException("restore master after confirmation test");
                    yield return null;
                }
                Request(screen, "Kick", peerApi.User.id);
                Set(screen, "network", null);
                Call(screen, "ClearView");
                Check(!Pending(screen), "session cleanup clears pending room action");
            }
            finally
            {
                Set(screen, "api", null);
                Set(screen, "network", null);
                UnityEngine.Object.DestroyImmediate(screen.gameObject);
            }
        }
    }
}


