using System.IO;
using UnityEditor;
using UnityEngine;

namespace SmartTraining.Editor
{
    // 샘플 공구와 손 프리팹을 최초 생성하고 빈 참조만 채웁니다. 사용자가 교체한 모델은 보존합니다.
    public static class ScenarioToolPrefabBuilder
    {
        public const string Folder = ScenarioContentBuilder.DirectoryPath + "/Tools";
        public const string RigPath = Folder + "/RightHand.prefab";

        public static void Prepare(ScenarioDefinition definition, string scenarioPath)
        {
            Directory.CreateDirectory(Folder);
            ScenarioToolPracticeBuilder.PrepareDefinition(definition);
            var skin = Material("Hand", new Color(.8f, .56f, .4f), 0);
            var rig = PrepareRig(skin);
            WorkplaceToolsBuilder.ApplyCatalog(definition);
            var root = PrefabUtility.LoadPrefabContents(scenarioPath);
            try
            {
                PrepareWorkAnchor(root);
                ScenarioToolPracticeBuilder.PrepareTargets(root);
                var content = new SerializedObject(root.GetComponent<ScenarioContent>());
                if (content.FindProperty("handRig").objectReferenceValue == null)
                {
                    content.FindProperty("handRig").objectReferenceValue = rig;
                    content.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, scenarioPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }


        // 기본 예시의 장착점만 보완합니다. 사용자 지정 장착점과 변경한 볼트 배치는 보존합니다.
        private static void PrepareWorkAnchor(GameObject root)
        {
            var bolt = root.transform.Find("Bolt A");
            if (bolt == null || !bolt.TryGetComponent<ScenarioObject>(out var marker))
            {
                return;
            }

            var data = new SerializedObject(marker);
            if (data.FindProperty("workAnchor").objectReferenceValue != null)
            {
                return;
            }

            if (bolt.localScale == new Vector3(.18f, .12f, .18f))
            {
                bolt.localScale = new Vector3(.04f, .12f, .04f);
            }

            var head = bolt.Find("Bolt Head");
            if (head != null && head.localPosition == new Vector3(0, .65f, 0))
            {
                head.localPosition = new Vector3(0, 1.225f, 0);
            }

            var anchor = bolt.Find("Work Anchor");
            if (anchor == null)
            {
                anchor = new GameObject("Work Anchor").transform;
                anchor.SetParent(bolt, false);
                anchor.localPosition = head != null ? head.localPosition : Vector3.zero;
            }

            data.FindProperty("workAnchor").objectReferenceValue = anchor;
            data.ApplyModifiedPropertiesWithoutUndo();
        }


        private static HandheldItemRig PrepareRig(Material skin)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
            if (existing != null)
            {
                var contents = PrefabUtility.LoadPrefabContents(RigPath);
                try
                {
                    var arm = contents.transform.Find("Forearm");
                    if (arm != null &&
                        arm.localPosition == new Vector3(0, -.01f, -.14f) &&
                        arm.localScale == new Vector3(.065f, .07f, .22f))
                    {
                        // 손목이 수평 작업 자세가 될 때 팔이 위로 솟지 않도록 기본 샘플만 보완합니다.
                        arm.localPosition = new Vector3(0, -.14f, 0);
                        arm.localScale = new Vector3(.065f, .22f, .07f);
                        PrefabUtility.SaveAsPrefabAsset(contents, RigPath);
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }

                return AssetDatabase.LoadAssetAtPath<GameObject>(RigPath).GetComponent<HandheldItemRig>();
            }

            var root = new GameObject("RightHand", typeof(HandheldItemRig));
            try
            {
                Part(
                    root.transform,
                    "Forearm",
                    PrimitiveType.Cube,
                    new Vector3(0, -.14f, 0),
                    new Vector3(.065f, .22f, .07f),
                    skin
                );
                Part(
                    root.transform,
                    "Palm",
                    PrimitiveType.Cube,
                    Vector3.zero,
                    new Vector3(.09f, .065f, .10f),
                    skin
                );
                for (int index = 0; index < 3; index++)
                {
                    Part(
                        root.transform,
                        "Finger " + index,
                        PrimitiveType.Cube,
                        new Vector3(.038f, .022f, -.026f + index * .026f),
                        new Vector3(.03f, .04f, .02f),
                        skin
                    );
                }

                var grip = new GameObject("Grip").transform;
                grip.SetParent(root.transform, false);
                grip.localPosition = new Vector3(0, .035f, .015f);
                var data = new SerializedObject(root.GetComponent<HandheldItemRig>());
                data.FindProperty("grip").objectReferenceValue = grip;
                data.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, RigPath).GetComponent<HandheldItemRig>();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }


        private static void Part(
            Transform parent,
            string name,
            PrimitiveType type,
            Vector3 position,
            Vector3 scale,
            Material material
        )
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(part.GetComponent<Collider>());
        }


        private static Material Material(string name, Color color, float metallic)
        {
            string path = Folder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                {
                    name = name
                };
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Metallic", metallic);
                material.SetFloat("_Smoothness", .45f);
                AssetDatabase.CreateAsset(material, path);
            }

            return material;
        }
    }
}
