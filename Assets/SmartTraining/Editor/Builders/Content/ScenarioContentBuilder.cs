using System;
using System.IO;
using UnityEditor;
using Unity.Netcode;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace SmartTraining.Editor
{
    // 단계 원본과 배치 예시를 최초 한 번 만듭니다. 기존 에셋의 사용자 수정은 덮어쓰지 않습니다.
    public static class ScenarioContentBuilder
    {
        public const string DirectoryPath = "Assets/SmartTraining/Content/Scenarios";
        public const string DefinitionPath = DirectoryPath + "/ToolAssembly.asset";
        public const string PrefabPath = DirectoryPath + "/ToolAssembly.prefab";
        public const string AssetAddress = "content/scenario/tool-assembly";

        [MenuItem("Smart Training/Content/Prepare Scenario Example")]
        public static void Prepare()
        {
            PrepareNetworkPrefab();
            Directory.CreateDirectory(DirectoryPath);
            var definition = AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(DefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<ScenarioDefinition>();
                var data = new SerializedObject(definition);
                data.FindProperty("id").stringValue = "tool-assembly";
                data.FindProperty("title").stringValue = "볼트 풀기 실습";
                var steps = data.FindProperty("steps");
                steps.arraySize = 3;
                SetStep(
                    steps.GetArrayElementAtIndex(0),
                    "loosen-first",
                    "첫 번째 볼트 풀기",
                    "스패너로 첫 번째 볼트를 푸세요.",
                    "bolt-a"
                );
                SetStep(
                    steps.GetArrayElementAtIndex(1),
                    "loosen-second",
                    "두 번째 볼트 풀기",
                    "스패너로 두 번째 볼트를 푸세요.",
                    "bolt-b"
                );
                SetStep(
                    steps.GetArrayElementAtIndex(2),
                    "inspect-result",
                    "작업 결과 확인",
                    "공구 없이 두 번째 볼트를 클릭해 작업 결과를 확인하세요.",
                    "bolt-b"
                );
                steps.GetArrayElementAtIndex(2).FindPropertyRelative("toolId").stringValue = "";
                data.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }

            PrepareWorkExample(definition);
            EnsureToolCatalog(definition);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                CreatePrefab(definition);
            }

            RemoveLegacyToolProp();
            EnsureBoltHead();
            ScenarioToolPrefabBuilder.Prepare(definition, PrefabPath);
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(PrefabPath), settings.DefaultGroup).address = AssetAddress;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (!prefab.GetComponent<ScenarioContent>().TryInitialize(out var error))
            {
                throw new InvalidOperationException(error);
            }

            Debug.Log("SCENARIO DATA READY: " + AssetAddress);
        }


        // 기본 예시의 이전 3단계만 갱신합니다. 별도로 구성한 시나리오나 이미 편집한 작업 설정은 보존합니다.
        private static void PrepareWorkExample(ScenarioDefinition definition)
        {
            if (definition.Id != "tool-assembly" ||
                definition.Steps.Count != 3 ||
                definition.Steps[0].Id != "loosen-first" ||
                definition.Steps[1].Id != "loosen-second" ||
                definition.Steps[2].Id != "inspect-result")
            {
                return;
            }

            var data = new SerializedObject(definition);
            data.FindProperty("title").stringValue = "볼트 풀기·다시 체결 실습";
            var steps = data.FindProperty("steps");
            SetStep(
                steps.GetArrayElementAtIndex(0),
                "loosen-first",
                "볼트 풀기",
                "스패너로 강조된 볼트를 푸세요.",
                "bolt-a"
            );
            SetStep(
                steps.GetArrayElementAtIndex(1),
                "tighten-first",
                "볼트 다시 체결",
                "스패너로 같은 볼트를 다시 체결하세요.",
                "bolt-a"
            );
            SetStep(
                steps.GetArrayElementAtIndex(2),
                "inspect-result",
                "체결 결과 확인",
                "공구 없이 체결한 볼트를 클릭해 결과를 확인하세요.",
                "bolt-a"
            );
            for (int index = 0; index < 2; index++)
            {
                var step = steps.GetArrayElementAtIndex(index);
                step.FindPropertyRelative("action").enumValueIndex = (int)(index == 0 ? ScenarioAction.Loosen : ScenarioAction.Tighten);
                step.FindPropertyRelative("durationSeconds").floatValue = 1.5f;
            }

            steps.GetArrayElementAtIndex(2).FindPropertyRelative("toolId").stringValue = "";
            steps.GetArrayElementAtIndex(2).FindPropertyRelative("action").enumValueIndex = (int)ScenarioAction.Check;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }


        // 원통에 사각 머리를 추가해 실제 회전 방향을 눈으로 확인할 수 있게 합니다. 기존 볼트 배치는 유지합니다.
        private static void EnsureBoltHead()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var bolt = root.transform.Find("Bolt A");
                if (bolt == null || bolt.Find("Bolt Head") != null)
                {
                    return;
                }

                var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
                head.name = "Bolt Head";
                head.transform.SetParent(bolt, false);
                head.transform.localPosition = new Vector3(0, .65f, 0);
                head.transform.localScale = new Vector3(1.4f, .45f, 1.4f);
                head.GetComponent<Renderer>().sharedMaterial = bolt.GetComponent<Renderer>().sharedMaterial;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }


        private static void EnsureToolCatalog(ScenarioDefinition definition)
        {
            WorkplaceToolsBuilder.ApplyCatalog(definition);
        }


        // 예전 예시의 클릭용 스패너만 제거합니다. 기존 작업대·볼트·사용자 배치는 보존합니다.
        private static void RemoveLegacyToolProp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var legacy = prefab != null ? prefab.transform.Find("Spanner") : null;
            if (legacy == null ||
                !legacy.TryGetComponent<ScenarioObject>(out var marker) ||
                marker.Id != "spanner" ||
                marker.Kind != ScenarioObjectKind.Tool)
            {
                return;
            }

            var instance = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                UnityEngine.Object.DestroyImmediate(instance.transform.Find("Spanner").gameObject);
                PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(instance);
            }
        }


        public static void PrepareNetworkPrefab()
        {
            const string path = "Assets/SmartTraining/Resources/SmartTraining/SharedScenario.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                return;
            }

            var root = new GameObject("Shared Scenario", typeof(NetworkObject), typeof(SharedScenario));
            try
            {
                root.GetComponent<NetworkObject>().DontDestroyWithOwner = true;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }


        private static void SetStep(
            SerializedProperty step,
            string id,
            string title,
            string instruction,
            string targetId
        )
        {
            step.FindPropertyRelative("id").stringValue = id;
            step.FindPropertyRelative("title").stringValue = title;
            step.FindPropertyRelative("instruction").stringValue = instruction;
            step.FindPropertyRelative("toolId").stringValue = "spanner";
            step.FindPropertyRelative("targetId").stringValue = targetId;
        }


        private static void CreatePrefab(ScenarioDefinition definition)
        {
            var root = new GameObject("Tool Assembly", typeof(ScenarioContent));
            try
            {
                var data = new SerializedObject(root.GetComponent<ScenarioContent>());
                data.FindProperty("definition").objectReferenceValue = definition;
                data.ApplyModifiedPropertiesWithoutUndo();
                CreatePart(
                    root.transform,
                    "Work Bench",
                    PrimitiveType.Cube,
                    new Vector3(0, 0.8f, 4),
                    new Vector3(3, 0.2f, 1.5f)
                );
                Mark(
                    CreatePart(
                    root.transform,
                    "Bolt A",
                    PrimitiveType.Cylinder,
                    new Vector3(0, 1.02f, 4),
                    new Vector3(0.18f, 0.12f, 0.18f)
                ),
                    "bolt-a",
                    ScenarioObjectKind.Target
                );
                Mark(
                    CreatePart(
                    root.transform,
                    "Bolt B",
                    PrimitiveType.Cylinder,
                    new Vector3(0.7f, 1.02f, 4),
                    new Vector3(0.18f, 0.12f, 0.18f)
                ),
                    "bolt-b",
                    ScenarioObjectKind.Target
                );
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }


        private static GameObject CreatePart(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            string materialPath = DirectoryPath + "/ScenarioParts.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                {
                    name = "Scenario Parts"
                };
                material.SetColor("_BaseColor", new Color(0.45f, 0.55f, 0.65f));
                AssetDatabase.CreateAsset(material, materialPath);
            }

            part.GetComponent<Renderer>().sharedMaterial = material;
            return part;
        }


        private static void Mark(GameObject item, string id, ScenarioObjectKind kind)
        {
            var data = new SerializedObject(item.AddComponent<ScenarioObject>());
            data.FindProperty("id").stringValue = id;
            data.FindProperty("kind").enumValueIndex = (int)kind;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
