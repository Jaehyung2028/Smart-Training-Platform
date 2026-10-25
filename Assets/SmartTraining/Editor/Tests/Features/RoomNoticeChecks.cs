using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace SmartTraining.Editor
{
    public static class RoomNoticeChecks
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

        private static void Set(ApiLobbyScreen screen, string name, object value) => typeof(ApiLobbyScreen).GetField(name, Flags).SetValue(screen, value);

        private static RoomNoticeTracker Tracker(ApiLobbyScreen screen) => (RoomNoticeTracker)typeof(ApiLobbyScreen).GetField("roomNotices", Flags).GetValue(screen);

        private static void Observe(ApiLobbyScreen screen) => typeof(ApiLobbyScreen).GetMethod("UpdateRoomNotices", Flags).Invoke(screen, null);

        private static void Check(bool value, string name)
        {
            if (!value)
            {
                throw new InvalidOperationException(name);
            }

            Debug.Log("ROOM PASS: " + name);
        }

        public static ApiLobbyScreen CreateObserver(TrainingApiClient api, RoomNetworkClient network, string lobby, string room)
        {
            var screen = new GameObject("room-notice-observer-test").AddComponent<ApiLobbyScreen>();
            screen.enabled = false;
            Set(screen, "api", api);
            Set(screen, "network", network);
            Set(screen, "networkLobbyId", lobby);
            Set(screen, "networkRoomId", room);
            Observe(screen);
            return screen;
        }

        public static void DestroyObserver(ApiLobbyScreen screen)
        {
            if (screen == null)
            {
                return;
            }

            Set(screen, "api", null);
            Set(screen, "network", null);
            UnityEngine.Object.DestroyImmediate(screen.gameObject);
        }

        public static void VerifyMasterDeparture(ApiLobbyScreen screen, string departedName, string newMasterName)
        {
            Observe(screen);
            Check(
                Tracker(screen).Messages.Any(m => m.Text == departedName + "님이 퇴장했습니다."),
                "remaining participant observes departed master notice"
            );
            Check(
                Tracker(screen).Messages.Any(m => m.Text == newMasterName + "님이 방장이 되었습니다."),
                "automatic master assignment produces room notice"
            );
            int count = Tracker(screen).Messages.Count;
            Observe(screen);
            Check(Tracker(screen).Messages.Count == count, "automatic master notice does not repeat each frame");
        }

        public static void RunPure()
        {
            var a = new RoomNoticeParticipant("a", "사용자A", 1);
            var b = new RoomNoticeParticipant("b", "사용자B", 2);
            var tracker = new RoomNoticeTracker();
            tracker.Observe(new[] { a, b }, "a");
            Check(tracker.Messages.Count == 0, "initial roster and master create no false arrival notices");
            tracker.Observe(new[] { b, a }, "a");
            Check(tracker.Messages.Count == 0, "roster ordering does not generate notices");
            tracker.Observe(new[] { a }, "a");
            Check(tracker.Messages.Single().Text == "사용자B님이 퇴장했습니다.", "departure keeps prior participant name");
            tracker.Observe(new[] { a, b }, "a");
            Check(tracker.Messages.Last().Text == "사용자B님이 입장했습니다.", "new confirmed participant creates arrival notice");
            tracker.Observe(new[] { a, b }, "b");
            Check(tracker.Messages.Last().Text == "사용자B님이 방장이 되었습니다.", "changed master creates named notice");
            tracker.Observe(new[] { a, new RoomNoticeParticipant("b", "사용자B", 3) }, "b");
            Check(
                tracker.Messages.Last().Text == "사용자B님이 다시 접속했습니다.",
                "replacement connection for same user creates reconnect notice"
            );
            tracker.ClearMessages();
            tracker.Observe(new[] { a, new RoomNoticeParticipant("b", "사용자B", 3) }, "b");
            Check(tracker.Messages.Count == 0, "clearing messages preserves roster baseline");
            tracker.Observe(new[] { a, new RoomNoticeParticipant("", "", 4) }, "a");
            tracker.Observe(new[] { a, a }, "a");
            Check(tracker.Messages.Count == 0, "incomplete and duplicate identity snapshots are ignored");
            tracker.Reset();
            tracker.Observe(new[] { a }, "a");
            tracker.Observe(new[] { a }, "future");
            Check(tracker.Messages.Count == 0, "unknown master is deferred without a fabricated name");
            tracker.Observe(new[] { a, new RoomNoticeParticipant("future", "새방장", 5) }, "future");
            Check(
                tracker.Messages.Count == 2 && tracker.Messages.Last().Text == "새방장님이 방장이 되었습니다.",
                "deferred master notice appears when participant identity arrives"
            );
            tracker.Reset();
            tracker.Observe(new[] { a, b }, "a");
            for (ulong i = 3; i < 28; i++)
            {
                tracker.Observe(new[] { a, new RoomNoticeParticipant("b", "사용자B", i) }, "a");
            }

            Check(tracker.Messages.Count == RoomNoticeTracker.Limit, "room notice history stays bounded to twenty entries");
            Check(tracker.Messages.All(m => m.ObservedAt != default), "room notices include observation timestamp");
            tracker.Reset();
            tracker.Observe(new[] { b }, "b");
            Check(tracker.Messages.Count == 0, "new room session resets history and initial roster baseline");
        }

        private static IEnumerator WaitRoster(RoomNetworkClient a, RoomNetworkClient b, int count)
        {
            double until = Time.realtimeSinceStartupAsDouble + 10;
            while (a.Players.Length != count || b.Players.Length != count || a.Players.Any(p => string.IsNullOrEmpty(p.UserId)) || b.Players.Any(p => string.IsNullOrEmpty(p.UserId)))
            {
                if (Time.realtimeSinceStartupAsDouble > until)
                {
                    throw new TimeoutException("room notice roster synchronization");
                }

                yield return null;
            }
        }

        private static IEnumerator WaitMaster(RoomNetworkClient a, RoomNetworkClient b, string userId)
        {
            double until = Time.realtimeSinceStartupAsDouble + 10;
            while (a.LocalPlayer.RoomMasterUserId != userId || b.LocalPlayer.RoomMasterUserId != userId)
            {
                if (Time.realtimeSinceStartupAsDouble > until)
                {
                    throw new TimeoutException("room notice master synchronization");
                }

                yield return null;
            }
        }

        public static IEnumerator Run(
            TrainingApiClient firstApi,
            TrainingApiClient secondApi,
            RoomNetworkClient first,
            RoomNetworkClient second,
            string lobby,
            string room
        )
        {
            var a = CreateObserver(firstApi, first, lobby, room);
            var b = CreateObserver(secondApi, second, lobby, room);
            TrainingApiClient guest = new TrainingApiClient(firstApi.BaseUrl);
            RoomNetworkClient guestNetwork = null;
            ApiLobbyScreen guestScreen = null;
            Action<string> fail = message => throw new InvalidOperationException(message);
            try
            {
                Check(
                    Tracker(a).Messages.Count == 0 && Tracker(b).Messages.Count == 0,
                    "actual existing room participants are initial baseline without arrival notices"
                );
                yield return guest.Login("notice-" + Guid.NewGuid().ToString("N").Substring(0, 6), () =>
                {
                }, fail);
                yield return guest.JoinLobby(lobby, () =>
                {
                }, fail);
                JoinRoomResponse admission = null;
                yield return guest.JoinRoom(lobby, room, value => admission = value, fail);
                Observe(a);
                Observe(b);
                Check(
                    Tracker(a).Messages.Count == 0 && Tracker(b).Messages.Count == 0,
                    "reservation alone does not create room arrival notice"
                );
                guestNetwork = new RoomNetworkClient();
                yield return guestNetwork.Connect(guest.User.id, admission, () =>
                {
                }, fail);
                double until = Time.realtimeSinceStartupAsDouble + 10;
                while (guest.User.currentRoomId != room)
                {
                    if (Time.realtimeSinceStartupAsDouble > until)
                    {
                        throw new TimeoutException("notice guest confirmation");
                    }

                    yield return guest.GetMe(_ =>
                    {
                    }, fail);
                    yield return null;
                }

                yield return WaitRoster(first, second, 3);
                Observe(a);
                Observe(b);
                string arrival = guest.User.name + "님이 입장했습니다.";
                Check(
                    Tracker(a).Messages.Count == 1 && Tracker(a).Messages[0].Text == arrival,
                    "confirmed UDP arrival produces named notice on first client"
                );
                Check(
                    Tracker(b).Messages.Count == 1 && Tracker(b).Messages[0].Text == arrival,
                    "confirmed UDP arrival produces named notice on second client"
                );
                Observe(a);
                Observe(b);
                Check(
                    Tracker(a).Messages.Count == 1 && Tracker(b).Messages.Count == 1,
                    "unchanged network snapshot does not duplicate arrival notices"
                );
                guestScreen = CreateObserver(guest, guestNetwork, lobby, room);
                Check(
                    Tracker(guestScreen).Messages.Count == 0,
                    "late joining screen does not announce existing participants as new arrivals"
                );
                yield return firstApi.TransferRoomMaster(lobby, room, secondApi.User.id, () =>
                {
                }, fail);
                yield return WaitMaster(first, second, secondApi.User.id);
                Observe(a);
                Observe(b);
                Check(
                    Tracker(a).Messages.Last().Text == secondApi.User.name + "님이 방장이 되었습니다." && Tracker(b).Messages.Last().Text == secondApi.User.name + "님이 방장이 되었습니다.",
                    "manual master transfer produces notice on both clients"
                );
                yield return secondApi.TransferRoomMaster(lobby, room, firstApi.User.id, () =>
                {
                }, fail);
                yield return WaitMaster(first, second, firstApi.User.id);
                Observe(a);
                Observe(b);
                yield return guest.LeaveRoom(lobby, room, () =>
                {
                }, fail);
                guestNetwork.Dispose();
                yield return WaitRoster(first, second, 2);
                Observe(a);
                Observe(b);
                string departure = guest.User.name + "님이 퇴장했습니다.";
                Check(
                    Tracker(a).Messages.Last().Text == departure && Tracker(b).Messages.Last().Text == departure,
                    "actual departure produces named notice on remaining clients"
                );
                Observe(guestScreen);
                Check(Tracker(guestScreen).Messages.Count == 0, "disconnected screen clears room notice history");
                Set(a, "networkRoomId", "changed-room");
                Observe(a);
                Check(Tracker(a).Messages.Count == 0, "room mismatch discards previous session notice history");
                Set(a, "networkRoomId", room);
                Observe(a);
                Check(Tracker(a).Messages.Count == 0, "restored binding establishes fresh baseline instead of replaying history");
                yield return guest.Logout(() =>
                {
                }, fail);
            }
            finally
            {
                DestroyObserver(a);
                DestroyObserver(b);
                DestroyObserver(guestScreen);
                guestNetwork?.Dispose();
                guest.Dispose();
            }
        }
    }
}
