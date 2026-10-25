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
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.Refresh();
            var root = new GameObject("RoomPlayer");
            try
            {
                root.AddComponent<NetworkObject>();
                root.AddComponent<RoomPlayer>();
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
