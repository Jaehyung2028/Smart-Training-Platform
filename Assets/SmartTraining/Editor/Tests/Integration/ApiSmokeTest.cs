using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SmartTraining.Editor
{
    public static class ApiSmokeTest
    {
        private static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();
        private static TrainingApiClient api;
        private static double deadline;
        private static bool previousPlayOptionsEnabled;
        private static EnterPlayModeOptions previousPlayOptions;
        public static void Run()
        {
            previousPlayOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            previousPlayOptions = EditorSettings.enterPlayModeOptions;
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/SmartTraining/Scenes/ApiLobby.unity");
            Check(scene.IsValid() && UnityEngine.Object.FindFirstObjectByType<ApiLobbyScreen>() != null, "scene loads with screen component");
            api = new TrainingApiClient(Environment.GetEnvironmentVariable("SMART_TRAINING_TEST_URL") ?? "http://localhost:5229");
            deadline = EditorApplication.timeSinceStartup + 90;
            stack.Push(Checks()); EditorApplication.update += Tick;
        }
        private static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException(name);
            Debug.Log("UNITY PASS: " + name);
        }
        private static IEnumerator Checks()
        {
            bool ok = false;
            Action<string> fail = message => throw new InvalidOperationException(message);
            yield return api.CheckHealth(() => ok = true, fail);
            Check(ok, "health");
            yield return api.Login("unity-" + Guid.NewGuid().ToString("N").Substring(0, 8), () => { }, fail);
            Check(api.IsLoggedIn && api.User != null, "login and session");
            var id = api.User.id;
            string fixtureLobbyId = null;
            yield return api.GetMe(user => Check(user.id == id && user.lobbyIndex == -1, "current user JSON fields"), fail);
            yield return api.GetLobbies(items =>
            {
                Check(items != null && items.Length == 1 && items[0].name == "unitytest" &&
                    items[0].roomCount == 0 && items[0].contentCount == 1, "nonempty lobby array JSON fields");
                fixtureLobbyId = items[0].id;
            }, fail);
            typeof(TrainingApiClient).GetField("expiresAt", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(api, DateTimeOffset.UtcNow);
            yield return api.GetMe(user => Check(user.id == id, "automatic refresh retains identity"), fail);
            var lobbyName = "ut-" + Guid.NewGuid().ToString("N").Substring(0, 6);
            string lobbyId = null;
            yield return api.CreateLobby(lobbyName, value => lobbyId = value, fail);
            Check(!string.IsNullOrEmpty(lobbyId), "lobby creation ID");
            yield return api.GetLobbies(items =>
                Check(Array.Exists(items, item => item.id == lobbyId && item.name == lobbyName && item.userCount == 0), "created lobby listed without automatic membership"), fail);
            bool duplicateRejected = false;
            yield return api.CreateLobby(lobbyName, _ => throw new InvalidOperationException("Duplicate name accepted"), _ => duplicateRejected = true);
            Check(duplicateRejected && api.IsLoggedIn, "duplicate lobby error retains login");
            yield return api.GetContents(lobbyId, items =>
                Check(items.Length == 1 && items[0].rootContent == "sample-training" && items[0].children.Length == 1 &&
                    items[0].children[0].parentId == items[0].id && items[0].children[0].depth == 1, "content tree and root ID JSON"), fail);
            yield return api.JoinLobby(lobbyId, () => { }, fail);
            yield return api.GetMe(user => Check(user.currentLobbyId == lobbyId && user.currentLobbyName == lobbyName, "lobby entry reflected in current user"), fail);
            yield return api.GetLobbies(items => Check(Array.Exists(items, item => item.id == lobbyId && item.userCount == 1), "lobby entry count"), fail);
            bool repeatRejected = false;
            yield return api.JoinLobby(lobbyId, () => throw new InvalidOperationException("Repeated entry accepted"), _ => repeatRejected = true);
            Check(repeatRejected && api.IsLoggedIn, "repeat entry returns server error");
            yield return api.LeaveLobby(lobbyId, () => { }, fail);
            yield return api.GetMe(user => Check(string.IsNullOrEmpty(user.currentLobbyId) && user.lobbyIndex == -1, "lobby departure clears current user"), fail);
            yield return api.GetLobbies(items => Check(!Array.Exists(items, item => item.id == lobbyId), "last user departure removes empty lobby"), fail);
            bool missingRejected = false;
            yield return api.GetContents(lobbyId, _ => throw new InvalidOperationException("Missing lobby contents accepted"), _ => missingRejected = true);
            Check(missingRejected && api.IsLoggedIn, "missing lobby content error retains login");
            yield return api.CreateLobby(lobbyName, value => lobbyId = value, fail);
            yield return api.GetRooms(lobbyId, items => Check(items.Length == 0, "new lobby room list empty"), fail);
            bool roomEntryRequired = false;
            yield return api.CreateRoom(lobbyId, "test-room", "sample-training", 4,
                _ => throw new InvalidOperationException("Room created without lobby entry"), _ => roomEntryRequired = true);
            Check(roomEntryRequired && api.IsLoggedIn, "room creation requires lobby membership");
            yield return api.JoinLobby(lobbyId, () => { }, fail);
            bool badContent = false;
            yield return api.CreateRoom(lobbyId, "test-room", "missing-content", 4,
                _ => throw new InvalidOperationException("Unknown content accepted"), _ => badContent = true);
            Check(badContent, "unknown content rejected");
            bool badCapacity = false;
            yield return api.CreateRoom(lobbyId, "test-room", "sample-training", 9,
                _ => throw new InvalidOperationException("Excess capacity accepted"), _ => badCapacity = true);
            Check(badCapacity, "room capacity validated by server");
            string roomId = null;
            yield return api.CreateRoom(lobbyId, "test-room", "sample-training", 4, value => roomId = value, fail);
            Check(!string.IsNullOrEmpty(roomId), "room creation ID");
            yield return api.GetRooms(lobbyId, items => Check(items.Length == 1 && items[0].id == roomId && items[0].lobbyId == lobbyId &&
                items[0].contentId == "sample-training" && items[0].maxUsers == 4 && items[0].availableSlots == 4 &&
                items[0].connectedUsers == 0 && items[0].reservedUsers == 0 && items[0].state == 0 && items[0].masterUserId == id,
                "room summary JSON and empty capacity"), fail);
            yield return api.GetRoom(lobbyId, roomId, room => Check(room.id == roomId && room.name == "test-room" &&
                !string.IsNullOrEmpty(room.contentTitle), "room detail JSON"), fail);
            yield return api.GetMe(user => Check(string.IsNullOrEmpty(user.currentRoomId) && string.IsNullOrEmpty(user.pendingRoomId),
                "room creation does not reserve or connect"), fail);
            bool missingServerRejected = false;
            yield return api.JoinRoom(lobbyId, roomId, _ => throw new Exception("Missing Unity server unexpectedly reserved"), _ => missingServerRejected = true);
            Check(missingServerRejected && api.IsLoggedIn, "missing dedicated server returns error without losing login");
            yield return api.GetMe(user => Check(string.IsNullOrEmpty(user.pendingRoomId), "failed room launch leaves no pending reservation"), fail);
            yield return api.GetLobbies(items => Check(Array.Exists(items, item => item.id == lobbyId && item.roomCount == 1), "lobby room count updates"), fail);
            bool otherLobbyRejected = false;
            yield return api.GetRoom(fixtureLobbyId, roomId, _ => throw new InvalidOperationException("Room resolved from wrong lobby"), _ => otherLobbyRejected = true);
            Check(otherLobbyRejected, "room detail requires matching lobby");
            yield return api.LeaveLobby(lobbyId, () => { }, fail);
            yield return api.GetLobbies(items => Check(Array.Exists(items, item => item.id == lobbyId && item.roomCount == 1 && item.userCount == 0),
                "lobby retained while created room exists"), fail);
            yield return api.Logout(() => { }, fail);
            Check(!api.IsLoggedIn && api.User == null, "logout clears session");
            using (var observer = new TrainingApiClient(api.BaseUrl))
            {
                yield return observer.Login("observe-" + Guid.NewGuid().ToString("N").Substring(0, 8), () => { }, fail);
                yield return observer.GetLobbies(items => Check(!Array.Exists(items, item => item.id == lobbyId), "logout cleans owned empty room and lobby"), fail);
                yield return observer.Logout(() => { }, fail);
            }
            bool denied = false;
            yield return api.GetMe(_ => throw new InvalidOperationException("Unauthenticated query accepted"), _ => denied = true);
            Check(denied, "query blocked after logout");
            bool invalidName = false;
            yield return api.Login("", () => throw new InvalidOperationException("Empty name accepted"), message => invalidName = !string.IsNullOrEmpty(message));
            Check(invalidName && !api.IsLoggedIn, "server error message without session");
            var exitName = "exit-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            yield return api.Login(exitName, () => { }, fail);
            string exitLobbyId = null;
            yield return api.CreateLobby("ex-" + Guid.NewGuid().ToString("N").Substring(0, 6), value => exitLobbyId = value, fail);
            yield return api.JoinLobby(exitLobbyId, () => { }, fail);
            yield return api.CreateRoom(exitLobbyId, "exit-room", "sample-training", 2, _ => { }, fail);
            // 검증용 실행에서만 도메인 재로드를 끄고, 종료 후 원래 옵션을 복원합니다.
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorApplication.EnterPlaymode();
            yield return new WaitUntil(() => EditorApplication.isPlaying);
            var screen = UnityEngine.Object.FindFirstObjectByType<ApiLobbyScreen>();
            Check(screen != null, "play mode screen exists");
            yield return NetcodeLoopbackChecks.Run();
            typeof(ApiLobbyScreen).GetField("api", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(screen, api);
            EditorApplication.ExitPlaymode();
            yield return new WaitUntil(() => !EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode);
            Check(!api.IsLoggedIn && api.User == null, "play stop clears local session");
            yield return new WaitUntil(() => !EditorSessionCleanup.HasPendingRequests);
            Check(api.LogoutOnShutdown(), "repeated shutdown without session is safe");
            yield return api.Login(exitName, () => { }, fail);
            Check(api.IsLoggedIn, "nickname reusable immediately after play stop");
            yield return api.GetLobbies(items => Check(!Array.Exists(items, item => item.id == exitLobbyId), "play stop removes owned empty room and lobby"), fail);
            yield return api.Logout(() => { }, fail);
        }
        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Unity API verification timed out");
                if (stack.Count == 0) { Finish(0); return; }
                var routine = stack.Peek();
                if (routine.Current is AsyncOperation pending && !pending.isDone) return;
                if (!routine.MoveNext()) { stack.Pop(); return; }
                if (routine.Current is IEnumerator nested) stack.Push(nested);
            }
            catch (Exception error) { Debug.LogError("UNITY CHECK FAILED: " + error.Message); Finish(1); }
        }
        private static void Finish(int code)
        {
            EditorSettings.enterPlayModeOptionsEnabled = previousPlayOptionsEnabled;
            EditorSettings.enterPlayModeOptions = previousPlayOptions;
            EditorApplication.update -= Tick;
            foreach (var routine in stack) (routine as IDisposable)?.Dispose();
            stack.Clear(); api?.Dispose(); EditorApplication.Exit(code);
        }
    }
}
