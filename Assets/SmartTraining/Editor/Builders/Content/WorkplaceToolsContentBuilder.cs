using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using static SmartTraining.Editor.CooperativeSampleUtility;

namespace SmartTraining.Editor
{
    // 여덟 공구를 모두 사용하는 별도 작업대를 추가해 기존 실습·협동 단계를 보존합니다.
    public static class WorkplaceToolsContentBuilder
    {
        public const string Folder = "Assets/SmartTraining/Content/Scenarios/WorkplaceTools";
        public const string DefinitionPath = Folder + "/WorkplaceTools.asset";
        public const string PrefabPath = Folder + "/WorkplaceTools.prefab";
        public const string Address = "content/scenario/workplace-tools";
        internal static readonly string[] Targets =
        {
            "tool-bolt",
            "tool-screw",
            "tool-pin",
            "tool-log",
            "tool-drill",
            "tool-pipe",
            "tool-clip",
            "tool-board"
        };
        internal static readonly ScenarioAction[] Actions =
        {
            ScenarioAction.Loosen,
            ScenarioAction.Loosen,
            ScenarioAction.Strike,
            ScenarioAction.Chop,
            ScenarioAction.Drill,
            ScenarioAction.Loosen,
            ScenarioAction.Grip,
            ScenarioAction.Saw
        };

        public static void Prepare()
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var definition = AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(DefinitionPath);
            if (definition == null)
            {
                definition = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(ScenarioContentBuilder.DefinitionPath));
                definition.name = "WorkplaceTools";
                var data = new SerializedObject(definition);
                data.FindProperty("id").stringValue = "workplace-tools";
                data.FindProperty("title").stringValue = "공구 8종 종합 실습";
                var steps = data.FindProperty("steps");
                steps.arraySize = 9;
                string[] operations =
                {
                    "볼트 풀기",
                    "나사 풀기",
                    "핀 고정",
                    "목재 쪼개기",
                    "구멍 가공",
                    "배관 연결부 풀기",
                    "고정 클립 뽑기",
                    "판재 자르기"
                };
                for (int index = 0; index < 8; index++)
                {
                    Step(
                        steps.GetArrayElementAtIndex(index),
                        "use-" + WorkplaceToolsBuilder.Ids[index],
                        WorkplaceToolsBuilder.Titles[index] + " · " + operations[index],
                        WorkplaceToolsBuilder.Titles[index] + " 공구를 선택하고 강조된 작업 대상을 클릭하세요. 작업: " + operations[index],
                        WorkplaceToolsBuilder.Ids[index],
                        Targets[index],
                        ScenarioRole.None,
                        Actions[index]
                    );
                    steps.GetArrayElementAtIndex(index).FindPropertyRelative("durationSeconds").floatValue = 3;
                }

                Step(
                    steps.GetArrayElementAtIndex(8),
                    "inspect-tools",
                    "전체 작업 결과 확인",
                    "공구 없이 마지막 판재를 클릭해 전체 작업 결과를 확인하세요.",
                    "",
                    "tool-board",
                    ScenarioRole.None,
                    ScenarioAction.Check
                );
                data.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }

            WorkplaceToolsBuilder.ApplyCatalog(definition);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                var root = PrefabUtility.LoadPrefabContents(ScenarioContentBuilder.PrefabPath);
                try
                {
                    root.name = "WorkplaceTools";
                    for (int index = root.transform.childCount - 1; index >= 0; index--)
                    {
                        Object.DestroyImmediate(root.transform.GetChild(index).gameObject);
                    }

                    var content = root.GetComponent<ScenarioContent>();
                    var data = new SerializedObject(content);
                    data.FindProperty("definition").objectReferenceValue = definition;
                    data.ApplyModifiedPropertiesWithoutUndo();
                    BuildTargets(root.transform, content);
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(PrefabPath), settings.DefaultGroup).address = Address;
            EditorUtility.SetDirty(settings);
        }


        private static void BuildTargets(Transform root, ScenarioContent content)
        {
            var metal = Material(Folder, "Metal", new Color32(148, 163, 184, 255));
            var wood = Material(Folder, "Wood", new Color32(180, 120, 65, 255));
            var dark = Material(Folder, "Dark", new Color32(51, 65, 85, 255));
            Part(
                root,
                "Bench",
                PrimitiveType.Cube,
                new Vector3(0, .8f, 4.2f),
                new Vector3(3.5f, .16f, 1.9f),
                dark
            );
            Vector3[] positions =
            {
                new(-1.05f, 1, 3.7f),
                new(-.35f, 1, 3.7f),
                new(.35f, 1, 3.7f),
                new(1.05f, 1, 4.5f),
                new(1.05f, .96f, 3.7f),
                new(-1.05f, 1, 4.5f),
                new(-.35f, 1, 4.5f),
                new(.35f, .96f, 4.5f)
            };
            string[] names =
            {
                "BOLT",
                "SCREW",
                "PIN",
                "LOG",
                "DRILL",
                "PIPE",
                "CLIP",
                "BOARD"
            };
            var visual = new SerializedObject(root.gameObject.AddComponent<ScenarioStageVisuals>());
            visual.FindProperty("scenario").objectReferenceValue = content;
            var changes = visual.FindProperty("changes");
            changes.arraySize = 3;
            for (int index = 0; index < 8; index++)
            {
                var target = Target(root, names[index], Targets[index], positions[index]);
                var data = new SerializedObject(target.GetComponent<ScenarioObject>());
                Vector3 offset = index == 2 ? Vector3.down * .1f : index == 0 || index == 1 || index == 6 ? Vector3.up * .16f : Vector3.zero;
                data.FindProperty("loosenOffset").vector3Value = offset;
                data.FindProperty("loosenTurns").floatValue = index == 0 || index == 1 ? 2 : index == 5 ? .5f : 0;
                var anchor = new GameObject("Work Anchor").transform;
                anchor.SetParent(target, false);
                anchor.localPosition = Vector3.up * (index == 4 || index == 7 ? .04f : index == 3 ? .1f : .12f);
                data.FindProperty("workAnchor").objectReferenceValue = anchor;
                data.ApplyModifiedPropertiesWithoutUndo();
                Label(root, names[index], positions[index] + Vector3.up * .31f);
                if (index == 0 || index == 1 || index == 2 || index == 5 || index == 6)
                {
                    Part(
                        target,
                        "Fastener",
                        PrimitiveType.Cylinder,
                        Vector3.zero,
                        new Vector3(
                        index == 5 ? .045f : index == 6 ? .02f : .025f,
                        .08f,
                        index == 5 ? .045f : index == 6 ? .02f : .025f
                    ),
                        metal
                    );
                    Part(
                        target,
                        "Head",
                        PrimitiveType.Cube,
                        Vector3.up * .1f,
                        new Vector3(index == 5 ? .065f : index == 6 ? .03f : .04f, .04f, index == 5 ? .065f : index == 6 ? .03f : .04f),
                        metal
                    );
                }
                else
                {
                    var before = Part(
                        target,
                        "Unworked",
                        PrimitiveType.Cube,
                        Vector3.zero,
                        new Vector3(.4f, index == 3 ? .2f : .08f, .3f),
                        wood
                    );
                    var after = new GameObject("Worked " + names[index]);
                    after.transform.SetParent(target, false);
                    if (index == 4)
                    {
                        // 네 조각 사이를 비워 실제 빈 구멍이 보이는 판재로 바꿉니다.
                        Part(
                            after.transform,
                            "Left",
                            PrimitiveType.Cube,
                            new Vector3(-.14f, 0, 0),
                            new Vector3(.12f, .08f, .3f),
                            wood
                        );
                        Part(
                            after.transform,
                            "Right",
                            PrimitiveType.Cube,
                            new Vector3(.14f, 0, 0),
                            new Vector3(.12f, .08f, .3f),
                            wood
                        );
                        Part(
                            after.transform,
                            "Front",
                            PrimitiveType.Cube,
                            new Vector3(0, 0, -.105f),
                            new Vector3(.16f, .08f, .09f),
                            wood
                        );
                        Part(
                            after.transform,
                            "Back",
                            PrimitiveType.Cube,
                            new Vector3(0, 0, .105f),
                            new Vector3(.16f, .08f, .09f),
                            wood
                        );
                    }
                    else
                    {
                        Part(
                            after.transform,
                            "Left Piece",
                            PrimitiveType.Cube,
                            new Vector3(-.13f, 0, 0),
                            new Vector3(.16f, index == 3 ? .2f : .08f, .3f),
                            wood
                        );
                        Part(
                            after.transform,
                            "Right Piece",
                            PrimitiveType.Cube,
                            new Vector3(.13f, 0, 0),
                            new Vector3(.16f, index == 3 ? .2f : .08f, .3f),
                            wood
                        );
                    }

                    int changeIndex = index == 3 ? 0 : index == 4 ? 1 : 2;
                    Change(changes.GetArrayElementAtIndex(changeIndex), index + 1, before, after);
                    after.SetActive(false);
                }
            }

            visual.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
