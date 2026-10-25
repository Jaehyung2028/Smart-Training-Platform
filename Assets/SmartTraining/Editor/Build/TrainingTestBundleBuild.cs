using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace SmartTraining.Editor
{
    public static class TrainingTestBundleBuild
    {
        private const string MenuPath = "Smart Training/Build/Build Windows Test Bundle";

        [Serializable]
        private sealed class Manifest
        {
            public string builtAtUtc, unityVersion;

            public string client = "Client/Smart Training Client.exe";

            public string server = "RoomServer/Smart Training Room.exe";

            public string clientKind = "Development", serverKind = "DedicatedServer";

            public bool completed = true;
        }

        [MenuItem(MenuPath, true)]
        private static bool CanBuild() => !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !BuildPipeline.isBuildingPlayer;

        [MenuItem(MenuPath)]
        public static void Build()
        {
            if (!CanBuild())
            {
                throw new BuildFailedException("플레이 또는 다른 빌드가 끝난 후 실행해주세요.");
            }

            var root = Environment.GetEnvironmentVariable("SMART_TRAINING_TEST_BUNDLE_ROOT");
            if (string.IsNullOrWhiteSpace(root))
            {
                root = "Builds/TestBundles";
            }

            var output = Path.Combine(
                Path.GetFullPath(root),
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 6)
            );
            Directory.CreateDirectory(output);
            string previousClient = Environment.GetEnvironmentVariable("SMART_TRAINING_CLIENT_BUILD");
            string previousServer = Environment.GetEnvironmentVariable("SMART_TRAINING_ROOM_BUILD");
            var previousHttp = PlayerSettings.insecureHttpOption;
            try
            {
                UguiScenePreview.Restore();
                var manifest = new Manifest
                {
                    builtAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                    unityVersion = Application.unityVersion

                };
                var server = Path.Combine(output, manifest.server);
                var client = Path.Combine(output, manifest.client);
                Environment.SetEnvironmentVariable("SMART_TRAINING_ROOM_BUILD", server);
                Environment.SetEnvironmentVariable("SMART_TRAINING_CLIENT_BUILD", client);
                Debug.Log("TEST BUNDLE: dedicated server build starting");
                RoomServerBuild.Build();
                Debug.Log("TEST BUNDLE: development client build starting");
                RoomClientBuild.Build();
                CheckPlayer(server);
                CheckPlayer(client);
                File.WriteAllText(
                    Path.Combine(output, "RUNNING.txt"),
                    "Smart Training 테스트 빌드\n\n" + "Client: Smart Training Client.exe를 실행합니다. 실행 파일과 같은 폴더의 모든 파일이 필요합니다.\n" + "RoomServer: ASP.NET이 방 입장 요청에 따라 실행하는 전용 서버입니다. 직접 실행하지 않습니다.\n" + "ASP.NET의 Room:ServerExecutablePath를 이 RoomServer 폴더의 실행 파일 경로로 지정하거나, RoomServer 폴더 전체를 기존 API 실행 폴더에 복사합니다.\n" + "먼저 ASP.NET 서버를 실행하고 클라이언트에서 서버 주소와 닉네임을 입력합니다. 다른 PC에서는 localhost 대신 서버 PC 주소를 입력합니다.\n\n" + "이 클라이언트는 Development Build이며 실제 서비스 배포용 빌드가 아닙니다.\n"
                );
                // A completed manifest exists only after both players and their required files have been verified.
                File.WriteAllText(Path.Combine(output, "build-manifest.json"), JsonUtility.ToJson(manifest, true));
                Debug.Log("TEST BUNDLE COMPLETE: " + output);
                if (!Application.isBatchMode)
                {
                    EditorUtility.RevealInFinder(output);
                }
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(output, "build-error.txt"), error.ToString());
                Debug.LogError("TEST BUNDLE FAILED: " + output);
                throw;
            }
            finally
            {
                Environment.SetEnvironmentVariable("SMART_TRAINING_CLIENT_BUILD", previousClient);
                Environment.SetEnvironmentVariable("SMART_TRAINING_ROOM_BUILD", previousServer);
                PlayerSettings.insecureHttpOption = previousHttp;
            }
        }

        private static void CheckPlayer(string executable)
        {
            var directory = Path.GetDirectoryName(executable);
            if (!File.Exists(executable) ||
                !Directory.Exists(Path.Combine(directory, Path.GetFileNameWithoutExtension(executable) + "_Data")) ||
                !File.Exists(Path.Combine(directory, "UnityPlayer.dll")))
            {
                throw new BuildFailedException("빌드 필수 파일을 확인할 수 없습니다: " + executable);
            }
        }
    }
}
