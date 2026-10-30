using System;
using UnityEditor;
using UnityEngine;

namespace SmartTraining.Editor
{
    // 미리 편집한 프리팹의 연결만 확인합니다. 빌드 중 모델·배치·데이터를 생성하거나 수정하지 않습니다.
    public static class ScenarioContentBuilder
    {
        public const string DirectoryPath = "Assets/SmartTraining/Content/Scenarios";
        public const string DefinitionPath = DirectoryPath + "/ToolAssembly.asset";
        public const string PrefabPath = "Assets/SmartTraining/Content/Scenarios/ToolAssembly.prefab";
        public const string AssetAddress = "content/scenario/tool-assembly";

        public static void Prepare()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)?.GetComponent<ScenarioContent>() == null)
            {
                throw new InvalidOperationException("콘텐츠 프리팹과 ScenarioContent 연결을 확인해주세요: " + PrefabPath);
            }

            PrepareNetworkPrefab();
        }


        public static void PrepareNetworkPrefab()
        {
            const string path = "Assets/SmartTraining/Resources/SmartTraining/SharedScenario.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path)?.GetComponent<SharedScenario>() == null)
            {
                throw new InvalidOperationException("공유 상태 프리팹을 확인해주세요: " + path);
            }
        }
    }
}
