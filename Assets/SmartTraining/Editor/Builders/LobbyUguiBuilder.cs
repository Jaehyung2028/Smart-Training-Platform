using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SmartTraining.Editor
{
    // UI는 프리팹에서 직접 편집하며 이 도구는 열기·필수 상호작용 참조 확인만 담당합니다.
    public static class LobbyUguiBuilder
    {
        public const string AssetPath = "Assets/SmartTraining/Resources/SmartTraining/LobbyUI.prefab";

        [MenuItem("Smart Training/UI/Open Login and Lobby Prefab")]
        public static void OpenPrefab()
        {
            EnsureAssets();
            AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath));
        }


        public static void EnsureAssets()
        {
            if (AssetDatabase.LoadAssetAtPath<LobbyUguiView>(AssetPath) == null)
            {
                throw new InvalidOperationException("기존 UI 프리팹을 연결해주세요: " + AssetPath);
            }

            EnsureRootInteraction(AssetPath);
        }


        internal static void EnsureRootInteraction(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponent<CanvasGroup>() == null)
                {
                    root.AddComponent<CanvasGroup>();
                }

                // RequireComponent may already have restored it while loading the old prefab.
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
