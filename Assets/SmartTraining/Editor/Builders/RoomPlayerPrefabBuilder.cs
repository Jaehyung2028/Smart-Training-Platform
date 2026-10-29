using System.IO;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace SmartTraining.Editor
{
    public static class RoomPlayerPrefabBuilder
    {
        [MenuItem("Smart Training/Create Room Player Prefab")]
        public static void Create()
        {
            const string path = "Assets/SmartTraining/Resources/SmartTraining/RoomPlayer.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                var existing = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool changed = RoomPlayerAvatarBuilder.Configure(existing);
                    if (existing.GetComponent<TrainingProgress>() == null)
                    {
                        existing.AddComponent<TrainingProgress>();
                        changed = true;
                    }

                    if (changed)
                    {
                        PrefabUtility.SaveAsPrefabAsset(existing, path);
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(existing);
                }

                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.Refresh();
            var root = new GameObject("RoomPlayer");
            try
            {
                root.AddComponent<NetworkObject>();
                root.AddComponent<RoomPlayer>();
                root.AddComponent<TrainingProgress>();
                RoomPlayerAvatarBuilder.Configure(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
