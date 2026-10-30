using System;
using System.Reflection;
using UnityEngine;

namespace SmartTraining.Editor
{
    public static class ServerAddressChecks
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

        private static void Check(bool ok, string name)
        {
            if (!ok)
            {
                throw new InvalidOperationException(name);
            }

            Debug.Log("UNITY PASS: " + name);
        }


        private static object Get(ApiLobbyScreen screen, string field) => typeof(ApiLobbyScreen).GetField(field, Flags).GetValue(screen);

        private static void Set(ApiLobbyScreen screen, string field, object value) => typeof(ApiLobbyScreen).GetField(field, Flags).SetValue(screen, value);

        private static void Call(ApiLobbyScreen screen, string method) => typeof(ApiLobbyScreen).GetMethod(method, Flags).Invoke(screen, null);

        private static ApiLobbyScreen Screen(string initial)
        {
            var root = new GameObject("address-settings-test");
            root.SetActive(false);
            var screen = root.AddComponent<ApiLobbyScreen>();
            Set(screen, "serverUrl", initial);
            root.SetActive(true);
            screen.enabled = false;
            Call(screen, "InitializeServerAddress");
            return screen;
        }


        public static void Run()
        {
            Check(
                ServerAddress.Normalize(" HTTPS://LOCALHOST:7087/// ") == "https://localhost:7087",
                "server address normalizes host scheme surrounding spaces and trailing slash"
            );
            Check(
                ServerAddress.Normalize("https://localhost:7087/swagger/index.html") == "https://localhost:7087" &&
                ServerAddress.Normalize("https://example.test/training/swagger/") == "https://example.test/training",
                "server address removes default Swagger suffix and preserves API base path"
            );
            Check(
                ServerAddress.Normalize("http://192.168.0.10:5229/") == "http://192.168.0.10:5229" &&
                ServerAddress.Normalize("http://[::1]:5229/") == "http://[::1]:5229",
                "server address accepts LAN IPv4 and loopback IPv6"
            );
            Check(
                !ServerAddress.TryNormalize("localhost:5229", out _, out _) &&
                !ServerAddress.TryNormalize("ftp://localhost", out _, out _) &&
                !ServerAddress.TryNormalize("http://localhost:70000", out _, out _) &&
                !ServerAddress.TryNormalize("http://localhost:0", out _, out _),
                "server address rejects missing scheme unsupported transport and invalid ports"
            );
            Check(
                !ServerAddress.TryNormalize("http://user:secret@localhost", out _, out _) &&
                !ServerAddress.TryNormalize("http://localhost/?token=secret", out _, out _) &&
                !ServerAddress.TryNormalize("http://localhost/#secret", out _, out _),
                "server address refuses embedded credentials query and fragment"
            );
            Check(
                !ServerAddress.TryNormalize("http://localhost/path name", out _, out _) &&
                !ServerAddress.TryNormalize("http://localhost\n", out _, out _) &&
                !ServerAddress.TryNormalize("http://0.0.0.0:5229", out _, out _) &&
                !ServerAddress.TryNormalize("http://[::]:5229", out _, out _),
                "server address rejects whitespace controls and wildcard bind hosts"
            );
            using (var client = new TrainingApiClient(" HTTPS://LOCALHOST:7087/swagger/index.html "))
            {
                Check(
                    client.BaseUrl == "https://localhost:7087",
                    "API client shares the address validator rather than sending REST beneath Swagger"
                );
            }

            ApiLobbyScreen first = null, second = null;
            try
            {
                first = Screen("https://localhost:7087/swagger/index.html");
                Check(
                    (string)Get(first, "serverUrl") == "https://localhost:7087",
                    "scene default is normalized at startup"
                );
                Set(first, "serverUrl", "http://localhost:7999");
                Set(first, "busy", true);
                Call(first, "ResetServerAddress");
                Check(
                    (string)Get(first, "serverUrl") == "http://localhost:7999",
                    "busy screen cannot reset endpoint"
                );
                Set(first, "busy", false);
                Call(first, "ResetServerAddress");
                Check(
                    (string)Get(first, "serverUrl") == "https://localhost:7087",
                    "reset restores this scenes default endpoint"
                );
                second = Screen(ServerAddress.DefaultUrl);
                Check(
                    (string)Get(second, "serverUrl") == ServerAddress.DefaultUrl,
                    "new screen does not inherit another screens address"
                );
            }
            finally
            {
                if (first != null)
                    UnityEngine.Object.DestroyImmediate(first.gameObject);
                if (second != null)
                    UnityEngine.Object.DestroyImmediate(second.gameObject);
            }
        }
    }
}
