using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using Object = UnityEngine.Object;
using static SmartTraining.Editor.CooperativeSampleUtility;

namespace SmartTraining.Editor
{
    // 공통 협동 규칙을 사용하되 교육과정마다 단계·모형·주소가 다른 샘플을 생성합니다.
    public static class CooperativeCourseBuilder
    {
        public static void Prepare()
        {
            PrepareSample(
                "CooperativeSafety",
                "협동 방호 덮개 복구",
                "content/scenario/cooperative-safety",
                ConfigureSafety,
                BuildSafety
            );
            PrepareSample(
                "CooperativeEmergency",
                "협동 비상 유도등 복구",
                "content/scenario/cooperative-emergency",
                ConfigureEmergency,
                BuildEmergency
            );
        }


        // 기존 파일은 보존합니다. 이후 단계와 배치는 데이터·프리팹에서 직접 편집할 수 있습니다.
        private static void PrepareSample(
            string name,
            string title,
            string address,
            Action<SerializedProperty> configureSteps,
            Action<Transform, ScenarioContent, string> buildModel
        )
        {
            string folder = SampleCourseBuilder.DirectoryPath + "/Scenarios/" + name;
            string definitionPath = folder + "/" + name + ".asset";
            string prefabPath = folder + "/" + name + ".prefab";
            Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            var definition = AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(definitionPath);
            if (definition == null)
            {
                definition = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(ScenarioContentBuilder.DefinitionPath));
                definition.name = name;
                var data = new SerializedObject(definition);
                data.FindProperty("id").stringValue = address.Substring(address.LastIndexOf('/') + 1);
                data.FindProperty("title").stringValue = title;
                data.FindProperty("tools").arraySize = 2;
                var steps = data.FindProperty("steps");
                steps.arraySize = 5;
                configureSteps(steps);
                data.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(definition, definitionPath);
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                var root = PrefabUtility.LoadPrefabContents(ScenarioContentBuilder.PrefabPath);
                try
                {
                    root.name = name;
                    for (int index = root.transform.childCount - 1; index >= 0; index--)
                    {
                        Object.DestroyImmediate(root.transform.GetChild(index).gameObject);
                    }

                    var content = root.GetComponent<ScenarioContent>();
                    var data = new SerializedObject(content);
                    data.FindProperty("definition").objectReferenceValue = definition;
                    data.ApplyModifiedPropertiesWithoutUndo();
                    buildModel(root.transform, content, folder);
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(prefabPath), settings.DefaultGroup).address = address;
            EditorUtility.SetDirty(settings);
        }


        private static void ConfigureSafety(SerializedProperty steps)
        {
            Step(
                steps.GetArrayElementAtIndex(0),
                "inspect-guard",
                "방호 덮개 이상 확인",
                "점검 담당: WARN 표시등에서 훈련용 정지 장비의 덮개 이상을 확인하세요.",
                "",
                "guard-warning",
                ScenarioRole.Inspector,
                ScenarioAction.Check
            );
            Step(
                steps.GetArrayElementAtIndex(1),
                "release-guard",
                "덮개 고정 볼트 풀기",
                "정비 담당: 스패너로 BOLT를 풀어 덮개를 조정할 준비를 하세요.",
                "spanner",
                "guard-bolt",
                ScenarioRole.Technician,
                ScenarioAction.Loosen
            );
            Step(
                steps.GetArrayElementAtIndex(2),
                "close-guard",
                "방호 덮개 닫기",
                "정비 담당: 붉은 GUARD를 클릭해 방호 덮개를 닫으세요. 공구는 필요 없습니다.",
                "",
                "guard-cover",
                ScenarioRole.Technician,
                ScenarioAction.Check
            );
            Step(
                steps.GetArrayElementAtIndex(3),
                "secure-guard",
                "방호 덮개 고정",
                "정비 담당: 스패너로 BOLT를 체결해 덮개를 고정하세요.",
                "spanner",
                "guard-bolt",
                ScenarioRole.Technician,
                ScenarioAction.Tighten
            );
            Step(
                steps.GetArrayElementAtIndex(4),
                "test-interlock",
                "덮개 확인 스위치 점검",
                "점검 담당: TEST 버튼을 눌러 방호 덮개가 닫힌 상태를 확인하세요.",
                "",
                "guard-test",
                ScenarioRole.Inspector,
                ScenarioAction.Check
            );
        }


        internal static void ConfigureEmergency(SerializedProperty steps)
        {
            steps.arraySize = 7;
            Step(
                steps.GetArrayElementAtIndex(0),
                "plan-recovery",
                "복구 순서 확인",
                "진행 담당: 아래쪽 CHECK 판을 클릭해 훈련 작업 순서를 확인하세요.",
                "",
                "recovery-plan",
                ScenarioRole.Coordinator,
                ScenarioAction.Check
            );
            Step(
                steps.GetArrayElementAtIndex(1),
                "isolate-power",
                "훈련용 전원 차단 확인",
                "안전 확인 담당: 아래쪽 POWER 스위치에서 훈련용 장비의 전원 차단 상태를 확인하세요.",
                "",
                "power-isolation",
                ScenarioRole.SafetyOfficer,
                ScenarioAction.Check
            );
            Step(
                steps.GetArrayElementAtIndex(2),
                "inspect-light",
                "비상 유도등 상태 확인",
                "점검 담당: 왼쪽 EXIT LIGHT에서 유도등 이상 상태를 확인하세요.",
                "",
                "exit-light",
                ScenarioRole.Inspector,
                ScenarioAction.Check
            );
            Step(
                steps.GetArrayElementAtIndex(3),
                "open-battery",
                "배터리 덮개 나사 풀기",
                "정비 담당: 드라이버로 SCREW를 풀어 배터리 교체를 준비하세요.",
                "screwdriver",
                "battery-screw",
                ScenarioRole.Technician,
                ScenarioAction.Loosen
            );
            Step(
                steps.GetArrayElementAtIndex(4),
                "replace-battery",
                "훈련용 배터리 교체",
                "정비 담당: 가운데 BATTERY를 클릭해 새 배터리로 교체하세요. 공구는 필요 없습니다.",
                "",
                "exit-battery",
                ScenarioRole.Technician,
                ScenarioAction.Check
            );
            Step(
                steps.GetArrayElementAtIndex(5),
                "close-battery",
                "배터리 덮개 나사 체결",
                "정비 담당: 드라이버로 SCREW를 다시 체결하세요.",
                "screwdriver",
                "battery-screw",
                ScenarioRole.Technician,
                ScenarioAction.Tighten
            );
            Step(
                steps.GetArrayElementAtIndex(6),
                "test-light",
                "비상 유도등 점등 확인",
                "점검 담당: TEST 버튼을 눌러 유도등이 초록색으로 복구되는지 확인하세요.",
                "",
                "exit-test",
                ScenarioRole.Inspector,
                ScenarioAction.Check
            );
        }


        private static void BuildSafety(Transform root, ScenarioContent content, string folder)
        {
            var metal = Material(folder, "Metal", new Color32(100, 116, 139, 255));
            var yellow = Material(folder, "Housing", new Color32(234, 179, 8, 255));
            var red = Material(folder, "Fault", new Color32(239, 68, 68, 255));
            var green = Material(folder, "Ready", new Color32(74, 222, 128, 255));
            Station(root, metal, yellow);
            var warning = Target(root, "Guard Warning", "guard-warning", new Vector3(-1.05f, 1.34f, 3.82f));
            var fault = Part(warning, "Fault Light", PrimitiveType.Sphere, Vector3.zero, Vector3.one * .22f, red);
            var ready = Part(
                warning,
                "Ready Light",
                PrimitiveType.Sphere,
                Vector3.zero,
                Vector3.one * .22f,
                green
            );
            Fastener(root, "Guard Bolt", "guard-bolt", metal);
            var guard = Target(root, "Guard Cover", "guard-cover", new Vector3(.12f, 1.25f, 3.8f));
            guard.GetComponent<BoxCollider>().size = new Vector3(.52f, .42f, .2f);
            var open = Part(
                guard,
                "Open Guard",
                PrimitiveType.Cube,
                new Vector3(0, .13f, -.05f),
                new Vector3(.46f, .12f, .08f),
                red
            );
            open.transform.localRotation = Quaternion.Euler(0, 0, 25);
            var closed = Part(
                guard,
                "Closed Guard",
                PrimitiveType.Cube,
                Vector3.zero,
                new Vector3(.46f, .36f, .05f),
                green
            );
            // 격자도 닫힌 덮개에 포함시켜 함께 표시합니다.
            for (int index = 0; index < 3; index++)
            {
                Part(
                    closed.transform,
                    "Guard Bar",
                    PrimitiveType.Cube,
                    new Vector3(-.3f + index * .3f, 0, -.65f),
                    new Vector3(.07f, .82f, .4f),
                    metal
                );
            }

            var test = Target(root, "Guard Test", "guard-test", new Vector3(.95f, 1.3f, 3.82f));
            Part(test, "Test Switch", PrimitiveType.Cube, Vector3.zero, Vector3.one * .22f, green);
            Headings(root, "WARN", "BOLT", "GUARD", "TEST");
            ConfigureVisuals(root, content, open, closed, fault, ready);
        }


        private static void BuildEmergency(Transform root, ScenarioContent content, string folder)
        {
            var metal = Material(folder, "Metal", new Color32(148, 163, 184, 255));
            var housing = Material(folder, "Housing", new Color32(51, 65, 85, 255));
            var red = Material(folder, "Fault", new Color32(239, 68, 68, 255));
            var green = Material(folder, "Ready", new Color32(74, 222, 128, 255));
            var blue = Material(folder, "NewBattery", new Color32(56, 189, 248, 255));
            Station(root, metal, housing);
            var light = Target(root, "Exit Light", "exit-light", new Vector3(-1.05f, 1.34f, 3.82f));
            var fault = Part(
                light,
                "Fault Light",
                PrimitiveType.Cube,
                Vector3.zero,
                new Vector3(.34f, .22f, .08f),
                red
            );
            var ready = Part(
                light,
                "Ready Light",
                PrimitiveType.Cube,
                Vector3.zero,
                new Vector3(.34f, .22f, .08f),
                green
            );
            Fastener(root, "Battery Screw", "battery-screw", metal);
            var battery = Target(root, "Exit Battery", "exit-battery", new Vector3(.12f, 1.24f, 3.8f));
            var spent = Part(
                battery,
                "Spent Battery",
                PrimitiveType.Cube,
                Vector3.zero,
                new Vector3(.32f, .2f, .12f),
                red
            );
            var replacement = Part(
                battery,
                "New Battery",
                PrimitiveType.Cube,
                Vector3.zero,
                new Vector3(.32f, .2f, .12f),
                blue
            );
            foreach (var model in new[]
            {
                spent,
                replacement
            }

            )
            {
                Part(
                    model.transform,
                    "Battery Terminal",
                    PrimitiveType.Cube,
                    new Vector3(.3f, .6f, 0),
                    new Vector3(.16f, .2f, .5f),
                    metal
                );
                Part(
                    model.transform,
                    "Battery Terminal",
                    PrimitiveType.Cube,
                    new Vector3(-.3f, .6f, 0),
                    new Vector3(.16f, .2f, .5f),
                    metal
                );
            }

            var test = Target(root, "Exit Test", "exit-test", new Vector3(.95f, 1.3f, 3.82f));
            Part(
                test,
                "Test Switch",
                PrimitiveType.Cylinder,
                Vector3.zero,
                new Vector3(.22f, .06f, .22f),
                green
            ).transform.localRotation = Quaternion.Euler(90, 0, 0);
            Headings(root, "EXIT LIGHT", "SCREW", "BATTERY", "TEST");
            AddEmergencyControls(root, folder);
            ConfigureVisuals(root, content, spent, replacement, fault, ready, 5, 7);
        }


        internal static void AddEmergencyControls(Transform root, string folder)
        {
            var blue = Material(folder, "Controls", new Color32(56, 189, 248, 255));
            var power = Target(root, "Power Isolation", "power-isolation", new Vector3(-1.05f, 1.0f, 3.72f));
            Part(power, "Power Switch", PrimitiveType.Cube, Vector3.zero, Vector3.one * .16f, blue);
            var plan = Target(root, "Recovery Plan", "recovery-plan", new Vector3(.95f, 1.0f, 3.72f));
            Part(plan, "Plan Check", PrimitiveType.Cube, Vector3.zero, Vector3.one * .16f, blue);
            Label(root, "POWER", new Vector3(-1.05f, .91f, 3.64f));
            Label(root, "CHECK", new Vector3(.95f, .91f, 3.64f));
        }


        private static void Station(Transform root, Material table, Material housing)
        {
            Part(
                root,
                "Work Bench",
                PrimitiveType.Cube,
                new Vector3(0, .8f, 4),
                new Vector3(3.3f, .16f, 1.8f),
                table
            );
            Part(
                root,
                "Equipment Housing",
                PrimitiveType.Cube,
                new Vector3(0, 1.18f, 4.2f),
                new Vector3(2.8f, .6f, .45f),
                housing
            );
        }


        private static void Fastener(Transform root, string name, string id, Material material)
        {
            var fastener = Target(root, name, id, new Vector3(-.55f, 1.31f, 3.82f));
            Part(
                fastener,
                "Fastener",
                PrimitiveType.Cylinder,
                Vector3.zero,
                new Vector3(.08f, .12f, .08f),
                material
            );
            var anchor = new GameObject("Work Anchor").transform;
            anchor.SetParent(fastener, false);
            anchor.localPosition = Vector3.up * .12f;
            var data = new SerializedObject(fastener.GetComponent<ScenarioObject>());
            data.FindProperty("loosenOffset").vector3Value = Vector3.up * .25f;
            data.FindProperty("loosenTurns").floatValue = 2;
            data.FindProperty("workAnchor").objectReferenceValue = anchor;
            data.ApplyModifiedPropertiesWithoutUndo();
        }


        private static void Headings(Transform root, string warning, string fastener, string part, string test)
        {
            Label(root, warning, new Vector3(-1.05f, 1.65f, 3.8f));
            Label(root, fastener, new Vector3(-.55f, 1.65f, 3.8f));
            Label(root, part, new Vector3(.12f, 1.65f, 3.8f));
            Label(root, test, new Vector3(.95f, 1.65f, 3.8f));
        }


        private static void ConfigureVisuals(
            Transform root,
            ScenarioContent content,
            GameObject before,
            GameObject after,
            GameObject fault,
            GameObject ready,
            int repairStep = 3,
            int finalStep = 5
        )
        {
            var visuals = new SerializedObject(root.gameObject.AddComponent<ScenarioStageVisuals>());
            visuals.FindProperty("scenario").objectReferenceValue = content;
            var changes = visuals.FindProperty("changes");
            changes.arraySize = 2;
            Change(changes.GetArrayElementAtIndex(0), repairStep, before, after);
            Change(changes.GetArrayElementAtIndex(1), finalStep, fault, ready);
            visuals.ApplyModifiedPropertiesWithoutUndo();
            after.SetActive(false);
            ready.SetActive(false);
        }
    }
}
