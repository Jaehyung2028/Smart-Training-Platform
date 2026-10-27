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
            Check(
                scene.IsValid() && UnityEngine.Object.FindFirstObjectByType<ApiLobbyScreen>() != null,
                "scene loads with screen component"
            );
            api = new TrainingApiClient(Environment.GetEnvironmentVariable("SMART_TRAINING_TEST_URL") ?? "http://localhost:5229");
            deadline = EditorApplication.timeSinceStartup + 400;
            stack.Push(Checks());
            EditorApplication.update += Tick;
        }

        private static void Check(bool condition, string name)
        {
            if (!condition)
            {
                throw new InvalidOperationException(name);
            }

            Debug.Log("UNITY PASS: " + name);
        }

        // 현재 운영 구조대로 카탈로그 로비를 선택합니다. 사용자가 로비를 만드는 API는 없습니다.
        private static IEnumerator Checks()
        {
            Action<string> fail = message => throw new InvalidOperationException(message);
            bool healthy = false;
            yield return api.CheckHealth(() => healthy = true, fail);
            Check(healthy, "health");
            yield return api.GetCourses(items => Check(items.Length > 0 && Array.TrueForAll(items, item => !string.IsNullOrEmpty(item.id) && !string.IsNullOrEmpty(item.name)),
                "course catalog loads before login without a token"), fail);
            string nickname = "unity-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            yield return api.Login(nickname, () =>
            {
            }, fail);
            Check(api.IsLoggedIn && api.User != null, "login and session");
            string userId = api.User.id;
            yield return api.GetMe(user => Check(user.id == userId && string.IsNullOrEmpty(user.currentLobbyId), "initial current user"), fail);
            typeof(TrainingApiClient).GetField("expiresAt", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(api, DateTimeOffset.UtcNow);
            yield return api.GetMe(user => Check(user.id == userId, "automatic token refresh retains user"), fail);
            string lobbyId = null, roomId = null, contentId = null;
            yield return CatalogLobbyTestSupport.Select(api, id => lobbyId = id, fail);
            yield return api.GetContents(
                lobbyId,
                items =>
            {
                Check(
                    items != null && items.Length == 1 && items[0].rootContent == lobbyId,
                    "catalog lobby has its own content root"
                );
                contentId = items[0].rootContent;
            },
                fail
            );
            bool membershipRequired = false;
            yield return api.CreateRoom(
                lobbyId,
                "smoke-room",
                contentId,
                2,
                _ => throw new InvalidOperationException("Room created without lobby membership"),
                _ => membershipRequired = true
            );
            Check(membershipRequired && api.IsLoggedIn, "room creation requires lobby membership");
            yield return api.JoinLobby(lobbyId, () =>
            {
            }, fail);
            yield return api.GetMe(user => Check(user.currentLobbyId == lobbyId, "catalog lobby entry reflected in current user"), fail);
            bool repeated = false;
            yield return api.JoinLobby(lobbyId, () => throw new InvalidOperationException("Duplicate entry accepted"), _ => repeated = true);
            Check(repeated && api.IsLoggedIn, "duplicate lobby entry rejected");
            yield return api.LeaveLobby(lobbyId, () =>
            {
            }, fail);
            yield return api.GetLobbies(
                items => Check(
                    Array.Exists(items, item => item.id == lobbyId && item.userCount == 0),
                    "empty catalog lobby remains listed"
                ),
                fail
            );
            yield return api.JoinLobby(lobbyId, () =>
            {
            }, fail);
            yield return api.CreateRoom(lobbyId, "smoke-" + Guid.NewGuid().ToString("N").Substring(0, 6), contentId, 2, id => roomId = id, fail);
            yield return api.GetRoom(
                lobbyId,
                roomId,
                room => Check(
                    room.id == roomId && room.contentId == contentId && room.masterUserId == userId,
                    "room creation uses token identity and catalog content"
                ),
                fail
            );
            // 생성은 성공했지만 생성 응답을 화면이 받지 못한 상황의 복구 경로를 직접 호출합니다.
            var recoveryScreen = UnityEngine.Object.FindFirstObjectByType<ApiLobbyScreen>();
            var recoveryFlags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var recoveryApiField = typeof(ApiLobbyScreen).GetField("api", recoveryFlags);
            var previousApi = recoveryApiField.GetValue(recoveryScreen);
            recoveryApiField.SetValue(recoveryScreen, api);
            try
            {
                yield return (IEnumerator)typeof(ApiLobbyScreen).GetMethod("RecoverRoomCreation", recoveryFlags).Invoke(recoveryScreen, new object[] { "방 생성 응답을 확인하지 못했습니다." });
                Check(
                    api.User.pendingRoomId == roomId && (bool)typeof(ApiLobbyScreen).GetProperty("UiHasRoomCleanup", recoveryFlags).GetValue(recoveryScreen),
                    "lost creation response recovery reveals authoritative reservation and cleanup action"
                );
                Check(
                    ((string)typeof(ApiLobbyScreen).GetField("status", recoveryFlags).GetValue(recoveryScreen)).Contains("남은 방 접속 정리"),
                    "creation recovery explains existing reservation without creating another room"
                );
            }
            finally
            {
                recoveryApiField.SetValue(recoveryScreen, previousApi);
            }

            yield return api.GetMe(
                user => Check(
                    string.IsNullOrEmpty(user.currentRoomId) && user.pendingRoomId == roomId,
                    "creation reserves initial master without claiming Netcode connection"
                ),
                fail
            );
            yield return api.Logout(() =>
            {
            }, fail);
            Check(!api.IsLoggedIn && api.User == null, "logout clears local credentials");
            yield return api.Login(nickname, () =>
            {
            }, fail);
            yield return api.GetLobbies(
                items => Check(
                    Array.Exists(items, item => item.id == lobbyId && item.roomCount == 0 && item.userCount == 0),
                    "logout clears owned room and retains fixed catalog lobby"
                ),
                fail
            );
            yield return api.Logout(() =>
            {
            }, fail);
            bool denied = false;
            yield return api.GetMe(_ => throw new InvalidOperationException("Unauthenticated query accepted"), _ => denied = true);
            Check(denied, "query blocked after logout");
        }

        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline)
                {
                    throw new TimeoutException("Unity API verification timed out");
                }

                if (stack.Count == 0)
                {
                    Finish(0);
                    return;
                }

                var routine = stack.Peek();
                if (routine.Current is AsyncOperation pending && !pending.isDone)
                {
                    return;
                }

                if (!routine.MoveNext())
                {
                    stack.Pop();
                    return;
                }

                if (routine.Current is IEnumerator nested)
                {
                    stack.Push(nested);
                }
            }
            catch (Exception error)
            {
                Debug.LogError("UNITY CHECK FAILED: " + error.Message);
                Finish(1);
            }
        }

        private static void Finish(int code)
        {
            EditorSettings.enterPlayModeOptionsEnabled = previousPlayOptionsEnabled;
            EditorSettings.enterPlayModeOptions = previousPlayOptions;
            EditorApplication.update -= Tick;
            foreach (var routine in stack)
            {
                (routine as IDisposable)?.Dispose();
            }

            stack.Clear();
            api?.Dispose();
            EditorApplication.Exit(code);
        }
    }
}
