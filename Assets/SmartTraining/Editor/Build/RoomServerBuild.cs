using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SmartTraining.Editor
{
    public static class RoomServerBuild
    {
        [MenuItem("Smart Training/Build Windows Room Server")]
        public static void Build()
        {
            RoomPlayerPrefabBuilder.Create();
            var output = Environment.GetEnvironmentVariable("SMART_TRAINING_ROOM_BUILD");
            if (string.IsNullOrEmpty(output))
            {
                output = System.IO.Path.GetFullPath("Builds/RoomServer/Smart Training Room.exe");
            }

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[] { "Assets/SmartTraining/Scenes/RoomServer.unity" },
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    subtarget = (int)StandaloneBuildSubtarget.Server,
                    options = BuildOptions.None
                });
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException("Room server build failed.");
            }

            Debug.Log("ROOM SERVER BUILD PASSED");
        }
    }
}
