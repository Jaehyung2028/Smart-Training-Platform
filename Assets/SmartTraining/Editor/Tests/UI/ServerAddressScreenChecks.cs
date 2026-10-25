using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace SmartTraining.Editor
{
    public static class ServerAddressScreenChecks
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

        private static object Get(ApiLobbyScreen screen, string field) => typeof(ApiLobbyScreen).GetField(field, Flags).GetValue(screen);

        private static void Set(ApiLobbyScreen screen, string field, object value) => typeof(ApiLobbyScreen).GetField(field, Flags).SetValue(screen, value);

        private static void Call(ApiLobbyScreen screen, string method) => typeof(ApiLobbyScreen).GetMethod(method, Flags).Invoke(screen, null);

        private static void Check(bool ok, string name)
        {
            if (!ok)
            {
                throw new InvalidOperationException(name);
            }

            Debug.Log("ROOM PASS: " + name);
        }

        public static IEnumerator Run(TrainingApiClient authenticated)
        {
            string key = "SmartTraining.Tests.ServerAddressScreen." + Guid.NewGuid().ToString("N");
            var settings = new ServerAddressSettings(key);
            var screen = new GameObject("server-address-screen-test").AddComponent<ApiLobbyScreen>();
            screen.enabled = false;
            TrainingApiClient healthClient = null;
            try
            {
                Set(screen, "serverAddressSettings", settings);
                Set(screen, "serverUrl", authenticated.BaseUrl + "/swagger/index.html");
                yield return (IEnumerator)typeof(ApiLobbyScreen).GetMethod("CheckHealth", Flags).Invoke(screen, null);
                healthClient = (TrainingApiClient)Get(screen, "api");
                Check(
                    healthClient.BaseUrl == authenticated.BaseUrl && ((string)Get(screen, "status")).Contains("응답했습니다"),
                    "pasted Swagger address reaches actual health endpoint at API root"
                );
                Call(screen, "SaveServerAddress");
                Check(
                    PlayerPrefs.GetString(key) == authenticated.BaseUrl && !healthClient.IsLoggedIn,
                    "address save does not create login or room membership"
                );
                healthClient.Dispose();
                healthClient = null;
                string oldRoom = authenticated.User.currentRoomId;
                Set(screen, "api", authenticated);
                Set(screen, "serverUrl", "https://localhost:7999");
                Call(screen, "SaveServerAddress");
                Call(screen, "ResetServerAddress");
                bool switched = (bool)typeof(ApiLobbyScreen).GetMethod("EnsureClient", Flags).Invoke(screen, null);
                Check(
                    !switched && Get(screen, "api") == authenticated && authenticated.IsLoggedIn && authenticated.User.currentRoomId == oldRoom,
                    "endpoint change guard preserves authenticated API and existing room membership"
                );
                Check(
                    PlayerPrefs.GetString(key) == authenticated.BaseUrl,
                    "logged-in save and reset attempts leave saved endpoint intact"
                );
                Set(screen, "serverUrl", authenticated.BaseUrl);
                Call(screen, "LoadServerAddress");
                Check(
                    (string)Get(screen, "serverUrl") == authenticated.BaseUrl && Get(screen, "api") == authenticated,
                    "settings restore does not replace active authenticated connection"
                );
            }
            finally
            {
                healthClient?.Dispose();
                Set(screen, "api", null);
                UnityEngine.Object.DestroyImmediate(screen.gameObject);
                settings.Reset();
            }
        }
    }
}
