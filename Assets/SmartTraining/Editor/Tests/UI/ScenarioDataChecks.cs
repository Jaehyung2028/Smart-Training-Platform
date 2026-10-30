using System;
using UnityEditor;
using UnityEngine;

namespace SmartTraining.Editor
{
    public static class ScenarioDataChecks
    {
        [MenuItem("Smart Training/Content/Check Scenario Data")]
        public static void Run()
        {
            ScenarioContentBuilder.Prepare();
            var definition = AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(ScenarioContentBuilder.DefinitionPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ScenarioContentBuilder.PrefabPath);
            Check(definition.TryValidate(out _) && definition.Steps.Count == 9, "예시 단계 데이터");
            Check(prefab.GetComponent<ScenarioContent>().TryInitialize(out _), "프리팹 공구·대상 연결");
            var empty = ScriptableObject.CreateInstance<ScenarioDefinition>();
            try
            {
                Check(!empty.TryValidate(out _), "빈 데이터 거절");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(empty);
            }

            InvalidDefinition(
                definition,
                data => data.FindProperty("steps").GetArrayElementAtIndex(1).FindPropertyRelative("id").stringValue = definition.Steps[0].Id
            );
            InvalidDefinition(
                definition,
                data => data.FindProperty("steps").GetArrayElementAtIndex(1).FindPropertyRelative("targetId").stringValue = ""
            );
            InvalidDefinition(
                definition,
                data => data.FindProperty("steps").GetArrayElementAtIndex(0).FindPropertyRelative("toolId").stringValue = "unregistered-tool"
            );
            InvalidObject(prefab, "Bolt A", data => data.FindProperty("id").stringValue = "missing");
            InvalidObject(prefab, "Bolt A", data => data.FindProperty("id").stringValue = "bolt-b");
            InvalidObject(
                prefab,
                "Bolt A",
                data => data.FindProperty("kind").enumValueIndex = (int)ScenarioObjectKind.Tool
            );
            Debug.Log("SCENARIO DATA CHECKS PASSED (9)");
        }


        private static void InvalidDefinition(ScenarioDefinition source, Action<SerializedObject> change)
        {
            var copy = UnityEngine.Object.Instantiate(source);
            try
            {
                var data = new SerializedObject(copy);
                change(data);
                data.ApplyModifiedPropertiesWithoutUndo();
                Check(!copy.TryValidate(out _), "잘못된 단계 데이터 거절");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(copy);
            }
        }


        private static void InvalidObject(GameObject source, string name, Action<SerializedObject> change)
        {
            var copy = UnityEngine.Object.Instantiate(source);
            try
            {
                var data = new SerializedObject(copy.transform.Find(name).GetComponent<ScenarioObject>());
                change(data);
                data.ApplyModifiedPropertiesWithoutUndo();
                Check(!copy.GetComponent<ScenarioContent>().TryInitialize(out _), "잘못된 공구·대상 연결 거절");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(copy);
            }
        }


        private static void Check(bool condition, string name)
        {
            if (!condition)
            {
                throw new InvalidOperationException(name);
            }
        }
    }
}
