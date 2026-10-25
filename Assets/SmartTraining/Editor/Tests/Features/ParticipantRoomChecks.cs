using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace SmartTraining.Editor
{
    public static class ParticipantRoomChecks
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

        private static object Get(ApiLobbyScreen screen, string field) => typeof(ApiLobbyScreen).GetField(field, Flags).GetValue(screen);

        private static void Set(ApiLobbyScreen screen, string field, object value) => typeof(ApiLobbyScreen).GetField(field, Flags).SetValue(screen, value);

        private static IEnumerator View(ApiLobbyScreen screen, string id) => (IEnumerator)typeof(ApiLobbyScreen).GetMethod("ViewParticipantRoom", Flags).Invoke(screen, new object[] { id });

        private static RoomSummary Selected(ApiLobbyScreen screen) => (RoomSummary)Get(screen, "selectedRoom");

        private static void Check(bool ok, string name)
        {
            if (!ok)
            {
                throw new InvalidOperationException(name);
            }

            Debug.Log("ROOM PASS: " + name);
        }

        public static IEnumerator Run(TrainingApiClient first, string lobby, string room)
        {
            Action<string> fail = error => throw new InvalidOperationException(error);
            var observer = new TrainingApiClient(first.BaseUrl);
            ApiLobbyScreen screen = null;
            try
            {
                yield return observer.Login("prv-" + Guid.NewGuid().ToString("N").Substring(0, 6), () =>
                {
                }, fail);
                yield return observer.JoinLobby(lobby, () =>
                {
                }, fail);
                string foreign = null;
                yield return CatalogLobbyTestSupport.Select(observer, id => foreign = id, fail, lobby);
                screen = new GameObject("participant-room-test").AddComponent<ApiLobbyScreen>();
                screen.enabled = false;
                Set(screen, "api", observer);
                Set(screen, "selectedLobbyId", foreign);
                Set(screen, "roomSearch", "cannot-match");
                Set(screen, "availableRoomsOnly", true);
                Set(screen, "roomProtectionFilter", RoomProtectionFilter.Protected);
                Set(screen, "joinRoomPassword", "old-input");
                yield return View(screen, first.User.id);
                Check(
                    Selected(screen)?.id == room && (string)Get(screen, "selectedLobbyId") == lobby,
                    "participant room shortcut selects current-lobby room while browsing another lobby"
                );
                Check(
                    (string)Get(screen, "roomSearch") == "" && !(bool)Get(screen, "availableRoomsOnly") && (RoomProtectionFilter)Get(screen, "roomProtectionFilter") == RoomProtectionFilter.All && (string)Get(screen, "joinRoomPassword") == "",
                    "participant room shortcut resets hiding filters and previous password"
                );
                Check(
                    ((ContentSummary[])Get(screen, "contents")).Length > 0 && (string)Get(screen, "selectedContentId") == Selected(screen).contentId,
                    "participant room shortcut loads current lobby content and selects room detail"
                );
                Check(
                    observer.User.currentRoomId == "" && observer.User.pendingRoomId == "" && Get(screen, "network") == null,
                    "viewing participant room does not reserve or connect automatically"
                );
                yield return View(screen, observer.User.id);
                Check(
                    Selected(screen) == null && ((string)Get(screen, "status")).Contains("접속 중이 아닙니다"),
                    "lobby-waiting participant cannot provide room shortcut"
                );
                yield return View(screen, "missing-participant");
                Check(
                    Selected(screen) == null && ((string)Get(screen, "status")).Contains("떠났습니다"),
                    "stale participant shortcut clears selection and explains departure"
                );
                yield return first.SetRoomEntryLock(lobby, room, true, () =>
                {
                }, fail);
                yield return View(screen, first.User.id);
                Check(
                    Selected(screen).isEntryLocked && !RoomBrowser.CanJoin(Selected(screen)) && ((string)Get(screen, "status")).Contains("잠갔습니다"),
                    "locked room remains viewable with admission lock explanation"
                );
                yield return first.SetRoomEntryLock(lobby, room, false, () =>
                {
                }, fail);
                yield return first.UpdateRoomPassword(lobby, room, "view-secret", _ =>
                {
                }, fail);
                yield return View(screen, first.User.id);
                Check(
                    Selected(screen).hasPassword && (string)Get(screen, "joinRoomPassword") == "" && ((string)Get(screen, "status")).Contains("비밀번호"),
                    "protected room shortcut requires fresh password input and keeps protection"
                );
                yield return first.UpdateRoomPassword(lobby, room, "", _ =>
                {
                }, fail);
                yield return first.UpdateRoomSettings(lobby, room, "network-room", 2, _ =>
                {
                }, fail);
                yield return View(screen, first.User.id);
                Check(
                    Selected(screen).availableSlots == 0 && !RoomBrowser.CanJoin(Selected(screen)) && ((string)Get(screen, "status")).Contains("남은 자리"),
                    "full participant room can be viewed but not joined"
                );
                yield return first.UpdateRoomSettings(lobby, room, "network-room", 3, _ =>
                {
                }, fail);
                JoinRoomResponse reservation = null;
                yield return observer.JoinRoom(lobby, room, value => reservation = value, fail);
                yield return View(screen, first.User.id);
                Check(
                    observer.User.pendingRoomId == room && Get(screen, "network") == null && ((string)Get(screen, "status")).Contains("예약을 취소"),
                    "shortcut preserves existing reservation and requests explicit cancellation"
                );
                yield return observer.LeaveRoom(lobby, room, () =>
                {
                }, fail);
                Set(screen, "api", first);
                yield return View(screen, first.User.id);
                Check(
                    first.User.currentRoomId == room && ((string)Get(screen, "status")).Contains("현재 방에서 나가"),
                    "shortcut does not leave caller existing room implicitly"
                );
            }
            finally
            {
                if (screen != null)
                {
                    Set(screen, "api", null);
                    UnityEngine.Object.DestroyImmediate(screen.gameObject);
                }

                observer.LogoutOnShutdown();
                observer.Dispose();
            }
        }
    }
}
