using System;
using UnityEditor;
using UnityEngine;

namespace SmartTraining.Editor
{
    // 저장된 UI 프리팹을 열고 필수 상호작용 컴포넌트만 확인합니다.
    public static class RoomUguiBuilder
    {
        public const string AssetPath = "Assets/SmartTraining/Resources/SmartTraining/RoomUI.prefab";

        [MenuItem("Smart Training/UI/Open Room Prefab")]
        public static void OpenPrefab()
        {
            EnsureAssets();
            AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath));
        }


        [MenuItem("Smart Training/UI/Check Room Prefab")]
        public static void EnsureAssets()
        {
            if (AssetDatabase.LoadAssetAtPath<RoomUguiView>(AssetPath) == null)
            {
                throw new InvalidOperationException("기존 UI 프리팹을 연결해주세요: " + AssetPath);
            }

            LobbyUguiBuilder.EnsureRootInteraction(AssetPath);
        }
    }
}
