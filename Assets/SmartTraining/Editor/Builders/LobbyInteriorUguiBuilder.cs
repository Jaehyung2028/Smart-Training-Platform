using System;
using UnityEditor;
using UnityEngine;

namespace SmartTraining.Editor
{
    // 저장된 UI 프리팹을 열고 필수 상호작용 컴포넌트만 확인합니다.
    public static class LobbyInteriorUguiBuilder
    {
        public const string InteriorPath = "Assets/SmartTraining/Resources/SmartTraining/LobbyInteriorUI.prefab";

        [MenuItem("Smart Training/UI/Open Lobby Interior Prefab")]
        public static void OpenPrefab()
        {
            EnsureAssets();
            AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<GameObject>(InteriorPath));
        }


        [MenuItem("Smart Training/UI/Check Lobby Interior Prefab")]
        public static void EnsureAssets()
        {
            if (AssetDatabase.LoadAssetAtPath<LobbyInteriorUguiView>(InteriorPath) == null)
            {
                throw new InvalidOperationException("기존 UI 프리팹을 연결해주세요: " + InteriorPath);
            }

            LobbyUguiBuilder.EnsureRootInteraction(InteriorPath);
        }
    }
}
