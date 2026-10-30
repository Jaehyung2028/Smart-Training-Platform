using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SmartTraining.Editor
{
    // 맵은 양쪽 씬에서 같은 프리팹을 참조합니다. 빌드는 배치를 덮어쓰지 않고 연결만 검사합니다.
    public static class TrainingEnvironmentSetup
    {
        public const string PrefabPath = "Assets/SmartTraining/Resources/SmartTraining/TrainingEnvironment.prefab";

        public static void Validate()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null || !prefab.GetComponentsInChildren<Collider>(true).Any(collider => !collider.isTrigger))
            {
                throw new InvalidOperationException("TrainingEnvironment 프리팹의 벽·바닥 Collider를 확인해주세요.");
            }

            foreach (var scene in new[]
            {
                ContentSceneSession.ScenePath,
                "Assets/SmartTraining/Scenes/RoomServer.unity"
            }

            )
            {
                if (!AssetDatabase.GetDependencies(scene).Contains(PrefabPath))
                {
                    throw new InvalidOperationException(scene + "에 공통 TrainingEnvironment 프리팹을 배치해주세요.");
                }
            }
        }
    }
}
