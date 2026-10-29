using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using static SmartTraining.Editor.CooperativeSampleUtility;

namespace SmartTraining.Editor
{
    // 기존 공구·손·시나리오 모듈을 재사용하는 가상 장비 협동 정비 샘플입니다.
    public static class CooperativeMaintenanceBuilder
    {
        public const string Folder = "Assets/SmartTraining/Content/Scenarios/CooperativeMaintenance";
        public const string DefinitionPath = Folder + "/CooperativeMaintenance.asset";
        public const string PrefabPath = Folder + "/CooperativeMaintenance.prefab";
        public const string Address = "content/scenario/cooperative-maintenance";

        public static void Prepare()
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var definition = AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(DefinitionPath);
            if (definition == null)
            {
                definition = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(ScenarioContentBuilder.DefinitionPath));
                definition.name = "CooperativeMaintenance";
                var data = new SerializedObject(definition);
                data.FindProperty("id").stringValue = "cooperative-maintenance";
                data.FindProperty("title").stringValue = "협동 장비 접점 정비";
                data.FindProperty("tools").arraySize = 2;
                var steps = data.FindProperty("steps");
                steps.arraySize = 5;
                Step(
                    steps.GetArrayElementAtIndex(0),
                    "diagnose",
                    "고장 표시 확인",
                    "점검 담당: 왼쪽 FAULT 표시등을 확인하세요.",
                    "",
                    "fault-lamp",
                    ScenarioRole.Inspector,
                    ScenarioAction.Check
                );
                Step(
                    steps.GetArrayElementAtIndex(1),
                    "open",
                    "덮개 볼트 풀기",
                    "정비 담당: 스패너로 왼쪽 COVER BOLT를 푸세요.",
                    "spanner",
                    "cover-bolt",
                    ScenarioRole.Technician,
                    ScenarioAction.Loosen
                );
                Step(
                    steps.GetArrayElementAtIndex(2),
                    "replace",
                    "불량 접점 교체",
                    "정비 담당: 가운데 붉은 CONTACT를 클릭해 새 접점으로 교체하세요. 공구는 필요 없습니다.",
                    "",
                    "contact",
                    ScenarioRole.Technician,
                    ScenarioAction.Check
                );
                Step(
                    steps.GetArrayElementAtIndex(3),
                    "close",
                    "덮개 볼트 체결",
                    "정비 담당: 스패너로 COVER BOLT를 다시 체결하세요.",
                    "spanner",
                    "cover-bolt",
                    ScenarioRole.Technician,
                    ScenarioAction.Tighten
                );
                Step(
                    steps.GetArrayElementAtIndex(4),
                    "verify",
                    "공동 작동 확인",
                    "점검 담당: 오른쪽 TEST 버튼으로 정비 결과를 확인하세요.",
                    "",
                    "test-button",
                    ScenarioRole.Inspector,
                    ScenarioAction.Check
                );
                data.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                var root = PrefabUtility.LoadPrefabContents(ScenarioContentBuilder.PrefabPath);
                try
                {
                    root.name = "CooperativeMaintenance";
                    for (int index = root.transform.childCount - 1; index >= 0; index--)
                    {
                        Object.DestroyImmediate(root.transform.GetChild(index).gameObject);
                    }

                    var content = root.GetComponent<ScenarioContent>();
                    var data = new SerializedObject(content);
                    data.FindProperty("definition").objectReferenceValue = definition;
                    data.ApplyModifiedPropertiesWithoutUndo();
                    var metal = Material(Folder, "Metal", new Color32(100, 116, 139, 255));
                    var red = Material(Folder, "Fault", new Color32(239, 68, 68, 255));
                    var green = Material(Folder, "Ready", new Color32(74, 222, 128, 255));
                    var blue = Material(Folder, "NewContact", new Color32(56, 189, 248, 255));
                    Part(
                        root.transform,
                        "Work Bench",
                        PrimitiveType.Cube,
                        new Vector3(0, .8f, 4),
                        new Vector3(3.3f, .16f, 1.8f),
                        metal
                    );
                    Part(
                        root.transform,
                        "Machine Body",
                        PrimitiveType.Cube,
                        new Vector3(0, 1.18f, 4.2f),
                        new Vector3(2.8f, .6f, .45f),
                        metal
                    );
                    var lamp = Target(root.transform, "Fault Indicator", "fault-lamp", new Vector3(-1.05f, 1.34f, 3.82f));
                    var fault = Part(lamp, "Fault Light", PrimitiveType.Sphere, Vector3.zero, Vector3.one * .22f, red);
                    var ready = Part(lamp, "Ready Light", PrimitiveType.Sphere, Vector3.zero, Vector3.one * .22f, green);
                    var bolt = Target(root.transform, "Cover Bolt", "cover-bolt", new Vector3(-.55f, 1.31f, 3.82f));
                    Part(
                        bolt,
                        "Bolt",
                        PrimitiveType.Cylinder,
                        Vector3.zero,
                        new Vector3(.08f, .12f, .08f),
                        metal
                    );
                    var boltData = new SerializedObject(bolt.GetComponent<ScenarioObject>());
                    boltData.FindProperty("loosenOffset").vector3Value = Vector3.up * .25f;
                    boltData.FindProperty("loosenTurns").floatValue = 2;
                    var anchor = new GameObject("Work Anchor").transform;
                    anchor.SetParent(bolt, false);
                    anchor.localPosition = Vector3.up * .12f;
                    boltData.FindProperty("workAnchor").objectReferenceValue = anchor;
                    boltData.ApplyModifiedPropertiesWithoutUndo();
                    var contact = Target(root.transform, "Contact", "contact", new Vector3(.12f, 1.24f, 3.8f));
                    var damaged = Part(
                        contact,
                        "Damaged Contact",
                        PrimitiveType.Cube,
                        Vector3.zero,
                        new Vector3(.35f, .22f, .08f),
                        red
                    );
                    var replacement = Part(
                        contact,
                        "Replaced Contact",
                        PrimitiveType.Cube,
                        Vector3.zero,
                        new Vector3(.35f, .22f, .08f),
                        blue
                    );
                    var test = Target(root.transform, "Test Button", "test-button", new Vector3(.95f, 1.3f, 3.82f));
                    Part(test, "Button", PrimitiveType.Cube, Vector3.zero, Vector3.one * .22f, green);
                    Label(root.transform, "FAULT", new Vector3(-1.05f, 1.65f, 3.8f));
                    Label(root.transform, "COVER BOLT", new Vector3(-.55f, 1.65f, 3.8f));
                    Label(root.transform, "CONTACT", new Vector3(.12f, 1.65f, 3.8f));
                    Label(root.transform, "TEST", new Vector3(.95f, 1.65f, 3.8f));
                    var visuals = new SerializedObject(root.AddComponent<ScenarioStageVisuals>());
                    visuals.FindProperty("scenario").objectReferenceValue = content;
                    var changes = visuals.FindProperty("changes");
                    changes.arraySize = 2;
                    Change(changes.GetArrayElementAtIndex(0), 3, damaged, replacement);
                    Change(changes.GetArrayElementAtIndex(1), 5, fault, ready);
                    visuals.ApplyModifiedPropertiesWithoutUndo();
                    replacement.SetActive(false);
                    ready.SetActive(false);
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


    }
}
