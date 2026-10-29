using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SmartTraining.Editor
{
    // 원본 에셋은 수정하지 않고 손잡이 원점·크기·접촉점을 가진 장착용 프리팹을 만듭니다.
    public static class WorkplaceToolsBuilder
    {
        public const string Folder = "Assets/SmartTraining/Content/Scenarios/Tools/Workplace";
        internal static readonly string[] Ids =
        {
            "spanner",
            "screwdriver",
            "hammer",
            "axe",
            "drill",
            "pipe-wrench",
            "pliers",
            "saw"
        };
        internal static readonly string[] Models =
        {
            "Wrench_Combination",
            "Screwdriver_Cross",
            "Hammer_01",
            "Axe",
            "Drill",
            "Pipe_Wrench",
            "Pliers",
            "Saw"
        };
        internal static readonly string[] Titles =
        {
            "콤비네이션 렌치",
            "십자 드라이버",
            "망치",
            "도끼",
            "전동 드릴",
            "파이프 렌치",
            "플라이어",
            "톱"
        };
        private static readonly float[] lengths =
        {
            .32f,
            .32f,
            .36f,
            .52f,
            .36f,
            .38f,
            .25f,
            .52f
        };

        public static void Prepare()
        {
            foreach (string guid in AssetDatabase.FindAssets(
                "t:ScenarioDefinition",
                new[] { ScenarioContentBuilder.DirectoryPath }
            ))
            {
                ApplyCatalog(AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(AssetDatabase.GUIDToAssetPath(guid)));
            }
        }


        public static void ApplyCatalog(ScenarioDefinition definition)
        {
            if (!definition.Steps.Any(step => step.RequiresTool))
            {
                return;
            }

            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var prefabs = new GameObject[Ids.Length];
            for (int index = 0; index < Ids.Length; index++)
            {
                prefabs[index] = PrepareModel(index);
            }

            // 처음 교체한 뒤에는 사용자가 조정한 접촉점·작업 각도를 보존합니다.
            if (definition.Tools.Count == Ids.Length &&
                definition.Tools.Select((tool, index) => tool.Id == Ids[index] &&
                tool.HeldPrefab == prefabs[index]).All(match => match))
            {
                return;
            }

            var data = new SerializedObject(definition);
            var tools = data.FindProperty("tools");
            tools.arraySize = Ids.Length;
            for (int index = 0; index < Ids.Length; index++)
            {
                var tool = tools.GetArrayElementAtIndex(index);
                tool.FindPropertyRelative("id").stringValue = Ids[index];
                tool.FindPropertyRelative("title").stringValue = Titles[index];
                tool.FindPropertyRelative("heldPrefab").objectReferenceValue = prefabs[index];
                tool.FindPropertyRelative("animateWork").boolValue = true;
                tool.FindPropertyRelative("workContactPoint").vector3Value = prefabs[index].transform.Find("Work Contact").localPosition;
                tool.FindPropertyRelative("workRotation").vector3Value = Ids[index] == "spanner" || Ids[index] == "pipe-wrench" || Ids[index] == "saw" ? new Vector3(90, 0, 0) : Ids[index] == "hammer" || Ids[index] == "axe" ? new Vector3(0, 0, -90) : new Vector3(180, 0, 0);
            }

            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
        }


        private static GameObject PrepareModel(int index)
        {
            string path = Folder + "/" + Ids[index] + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                if (existing.GetComponentsInChildren<Collider>(true).Any(collider => collider.enabled))
                {
                    var loaded = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        DisableColliders(loaded);
                        PrefabUtility.SaveAsPrefabAsset(loaded, path);
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(loaded);
                    }
                }
                return existing;
            }

            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Workplace Tools/Prefabs/" + Models[index] + ".prefab");
            if (source == null)
            {
                throw new InvalidOperationException("공구 모델을 찾을 수 없습니다: " + Models[index]);
            }

            var root = new GameObject(Ids[index]);
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
                model.name = Models[index];
                var renderers = model.GetComponentsInChildren<Renderer>(true);
                var handle = renderers.FirstOrDefault(item => item.name.Contains("Handle"));
                var head = renderers.FirstOrDefault(item => item.name.Contains("Top"));
                bool reverse = head != null && handle != null && head.bounds.center.z < handle.bounds.center.z;
                model.transform.localRotation = Quaternion.FromToRotation(reverse ? Vector3.back : Vector3.forward, Vector3.up);
                var bounds = BoundsOf(renderers);
                float size = Ids[index] == "drill" ? Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) : bounds.size.y;
                model.transform.localScale *= lengths[index] / size;
                bounds = BoundsOf(renderers);
                var handleBounds = handle != null ? handle.bounds : bounds;
                var grip = new Vector3(
                    handleBounds.center.x,
                    bounds.min.y + bounds.size.y * (Ids[index] == "spanner" ? .45f : .18f),
                    handleBounds.center.z
                );
                if (Ids[index] == "drill")
                {
                    grip = model.transform.TransformPoint(new Vector3(0, -.18f, -.19f));
                }
                else if (Ids[index] == "saw")
                {
                    grip = model.transform.TransformPoint(new Vector3(0, 0, -.06f));
                }

                model.transform.localPosition -= grip;
                bounds = BoundsOf(renderers);
                var contact = new Vector3(bounds.center.x, bounds.max.y - .005f, bounds.center.z);
                if (Ids[index] == "hammer" || Ids[index] == "axe")
                {
                    contact = new Vector3(head.bounds.max.x, head.bounds.center.y, head.bounds.center.z);
                }
                else if (Ids[index] == "saw")
                {
                    contact = new Vector3(bounds.center.x, bounds.center.y, bounds.max.z - .005f);
                }
                else if (Ids[index] == "drill")
                {
                    var chuck = renderers.Single(item => item.name == "Top_Front").bounds;
                    contact = new Vector3(chuck.center.x, chuck.max.y, chuck.center.z);
                }

                var marker = new GameObject("Work Contact").transform;
                marker.SetParent(root.transform, false);
                marker.localPosition = contact;
                DisableColliders(model);

                if (Ids[index] == "drill" || Ids[index] == "pliers")
                {
                    var data = new SerializedObject(root.AddComponent<ToolArticulation>());
                    if (Ids[index] == "drill")
                    {
                        var part = renderers.Single(item => item.name == "Top_Front").transform;
                        data.FindProperty("spinningPart").objectReferenceValue = part;
                        data.FindProperty("spinningAxis").vector3Value = part.InverseTransformDirection(model.transform.forward);
                    }
                    else
                    {
                        data.FindProperty("leftJaw").objectReferenceValue = model.transform.Find("Left");
                        data.FindProperty("rightJaw").objectReferenceValue = model.transform.Find("Right");
                        data.FindProperty("jawAxis").vector3Value = model.transform.Find("Left").InverseTransformDirection(model.transform.up);
                    }

                    data.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(model);
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }


        private static void DisableColliders(GameObject model)
        {
            foreach (var collider in model.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
            }
        }


        private static Bounds BoundsOf(Renderer[] renderers)
        {
            var result = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                result.Encapsulate(renderer.bounds);
            }

            return result;
        }
    }
}
