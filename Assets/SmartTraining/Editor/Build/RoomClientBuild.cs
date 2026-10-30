using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SmartTraining.Editor
{
    public static class RoomClientBuild
    {
        [MenuItem("Smart Training/Build Windows Test Client")]
        public static void Build()
        {
            UguiScenePreview.Restore();
            PlayerSettings.insecureHttpOption = InsecureHttpOption.DevelopmentOnly;
            ContentAddressablesSetup.Prepare();
            ModelContentBuilder.Prepare();
            VideoContentBuilder.Prepare();
            SampleCourseBuilder.Prepare();
            RoomPlayerPrefabBuilder.Create();
            TrainingEnvironmentSetup.Validate();
            LobbyUguiBuilder.EnsureAssets();
            LobbyInteriorUguiBuilder.EnsureAssets();
            RoomUguiBuilder.EnsureAssets();
            var output = Environment.GetEnvironmentVariable("SMART_TRAINING_CLIENT_BUILD");
            if (string.IsNullOrEmpty(output))
            {
                output = System.IO.Path.GetFullPath("Builds/Client/Smart Training Client.exe");
            }

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[]
                {
                    "Assets/SmartTraining/Scenes/ApiLobby.unity",
                    ContentSceneSession.ScenePath
                },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                subtarget = (int)StandaloneBuildSubtarget.Player,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException("Room client build failed.");
            }

            Debug.Log("ROOM CLIENT BUILD PASSED (DevelopmentOnly HTTP)");
        }
    }
}
