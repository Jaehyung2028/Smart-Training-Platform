using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SmartTraining.Editor
{
    [InitializeOnLoad]
    public static class DomainReloadExitTest
    {
        private const string Key = "SmartTraining.ExitTest.";

        private static readonly Stack<IEnumerator> stack = new Stack<IEnumerator>();

        private static TrainingApiClient api;

        private static double deadline;

        static DomainReloadExitTest()
        {
            EditorApplication.update += Tick;
            deadline = EditorApplication.timeSinceStartup + 90;
        }

        public static void Run()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/SmartTraining/Scenes/ApiLobby.unity");
            SessionState.SetBool(Key + "enabled", EditorSettings.enterPlayModeOptionsEnabled);
            SessionState.SetInt(Key + "options", (int)EditorSettings.enterPlayModeOptions);
            SessionState.SetString(Key + "url", Environment.GetEnvironmentVariable("SMART_TRAINING_TEST_URL"));
            SessionState.SetString(Key + "name", "reload-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            SessionState.SetString(Key + "phase", "enter");
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorApplication.EnterPlaymode();
        }

        private static void Check(bool value, string label)
        {
            if (!value)
            {
                throw new Exception(label);
            }

            Debug.Log("UNITY PASS: " + label);
        }

        private static IEnumerator Prepare()
        {
            Action<string> fail = message => throw new Exception(message);
            api = new TrainingApiClient(SessionState.GetString(Key + "url", ""));
            var screen = UnityEngine.Object.FindFirstObjectByType<ApiLobbyScreen>();
            Check(screen != null, "default domain reload play screen");
            typeof(ApiLobbyScreen).GetField("api", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(screen, api);
            yield return api.Login(SessionState.GetString(Key + "name", ""), () =>
            {
            }, fail);
            string lobbyId = null;
            yield return CatalogLobbyTestSupport.Select(api, id => lobbyId = id, fail);
            SessionState.SetString(Key + "lobby", lobbyId);
            yield return api.JoinLobby(lobbyId, () =>
            {
            }, fail);
            yield return api.CreateRoom(lobbyId, "reload-room", lobbyId, 2, _ =>
            {
            }, fail);
            SessionState.SetString(Key + "phase", "exit");
            EditorApplication.ExitPlaymode();
        }

        private static IEnumerator Verify()
        {
            yield return new WaitUntil(() => !EditorSessionCleanup.HasPendingRequests);
            Action<string> fail = message => throw new Exception(message);
            api = new TrainingApiClient(SessionState.GetString(Key + "url", ""));
            yield return api.Login(SessionState.GetString(Key + "name", ""), () =>
            {
            }, fail);
            Check(api.IsLoggedIn, "nickname reusable after default domain reload stop");
            var lobbyId = SessionState.GetString(Key + "lobby", "");
            yield return api.GetLobbies(
                items => Check(
                    Array.Exists(items, item => item.id == lobbyId && item.roomCount == 0 && item.userCount == 0),
                    "default reload stop cleans owned room and retains catalog lobby"
                ),
                fail
            );
            yield return api.Logout(() =>
            {
            }, fail);
        }

        private static void Tick()
        {
            var phase = SessionState.GetString(Key + "phase", "");
            if (string.IsNullOrEmpty(phase))
            {
                return;
            }

            try
            {
                if (EditorApplication.timeSinceStartup > deadline)
                {
                    throw new TimeoutException("Domain reload verification timed out");
                }

                if (phase == "enter" && EditorApplication.isPlaying)
                {
                    SessionState.SetString(Key + "phase", "prepare");
                    stack.Push(Prepare());
                }

                if (phase == "exit" && !EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    SessionState.SetString(Key + "phase", "verify");
                    stack.Clear();
                    stack.Push(Verify());
                }

                if (stack.Count == 0)
                {
                    if (phase == "verify")
                    {
                        Finish(0);
                    }

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
            SessionState.SetString(Key + "phase", "");
            EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool(Key + "enabled", false);
            EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt(Key + "options", 0);
            EditorApplication.update -= Tick;
            api?.Dispose();
            EditorApplication.Exit(code);
        }
    }
}
