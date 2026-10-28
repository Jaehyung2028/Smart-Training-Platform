using System;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.Video;

namespace SmartTraining.Editor
{
    // 카탈로그의 샘플 9개를 준비합니다. 이미 만들어진 에셋과 사용자의 편집 내용은 보존합니다.
    public static class SampleCourseBuilder
    {
        public const string DirectoryPath = "Assets/SmartTraining/Content";

        [MenuItem("Smart Training/Content/Prepare All Course Samples")]
        public static void Prepare()
        {
            ModelContentBuilder.Prepare();
            VideoContentBuilder.Prepare();
            ScenarioContentBuilder.Prepare();
            Directory.CreateDirectory(DirectoryPath + "/Shared/Materials");
            AssetDatabase.Refresh();
            PrepareVideo("EquipmentGuide", "content/video/equipment-guide", "EQUIPMENT GUIDE",
                new[] { "1 CHECK THE TOOL", "2 CHECK THE POWER", "3 START WORK SAFELY" },
                new[] { "사용할 공구의 외관과 작업 공간을 확인합니다.", "전원과 배터리의 상태를 확인합니다.", "안전한 자세와 순서를 확인한 뒤 작업을 시작합니다." });
            PrepareVideo("EmergencyGuide", "content/video/emergency-guide", "EMERGENCY GUIDE",
                new[] { "1 CHECK THE ALARM", "2 FIND THE EXIT", "3 MEET THE TEAM" },
                new[] { "비상 상황을 확인하고 주변에 알립니다.", "안내 표지를 따라 안전한 대피 방향을 확인합니다.", "집결 장소에서 팀원의 도착 여부를 확인합니다." });
            PrepareModel("SafetyEquipment", "content/model/safety-equipment", false);
            PrepareModel("Extinguisher", "content/model/extinguisher", true);
            PrepareScenario("ToolCheck", "content/scenario/tool-check", "작업 전 공구 점검",
                new[] { "공구 외관 확인", "전원 차단 확인", "작업 구역 확인" },
                new[] { "왼쪽 공구 상태판을 클릭하세요.", "가운데 전원 차단 스위치를 클릭하세요.", "오른쪽 작업 구역 표지를 클릭하세요." },
                new[] { "공구 상태", "전원 차단", "작업 구역" });
            PrepareScenario("EvacuationCheck", "content/scenario/evacuation-check", "대피 전 안전 확인",
                new[] { "비상 알림 확인", "대피 방향 확인", "집결 장소 확인" },
                new[] { "왼쪽 비상 알림 버튼을 클릭하세요.", "가운데 대피 방향 표지를 클릭하세요.", "오른쪽 집결 장소 표지를 클릭하세요." },
                new[] { "비상 알림", "대피 방향", "집결 장소" });
            AssetDatabase.SaveAssets();
            Debug.Log("ALL COURSE SAMPLES READY (3 courses / 9 contents)");
        }

        // 각 영상은 별도 클립·한국어 자막을 가지며 기존 영상 재생 모듈을 재사용합니다.
        private static void PrepareVideo(string name, string address, string title, string[] headings, string[] subtitles)
        {
            string folder = DirectoryPath + "/Videos/" + name;
            Directory.CreateDirectory(folder);
            string movie = folder + "/" + name + ".mp4";
            string subtitle = folder + "/" + name + ".srt.txt";
            string prefab = folder + "/" + name + ".prefab";
            if (!File.Exists(movie))
            {
                VideoContentBuilder.CreateMovie(movie, title, headings);
            }
            if (!File.Exists(subtitle))
            {
                string text = "";
                for (int index = 0; index < subtitles.Length; index++)
                {
                    text += $"{index + 1}\n00:00:{index * 4:00},000 --> 00:00:{index * 4 + 3:00},900\n{subtitles[index]}\n\n";
                }
                File.WriteAllText(subtitle, text, new System.Text.UTF8Encoding(false));
            }
            AssetDatabase.Refresh();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefab) == null)
            {
                var root = PrefabUtility.LoadPrefabContents(VideoContentBuilder.PrefabPath);
                try
                {
                    root.name = name;
                    var data = new SerializedObject(root.GetComponent<VideoContentPlayer>());
                    data.FindProperty("clip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<VideoClip>(movie);
                    data.FindProperty("subtitleFile").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(subtitle);
                    data.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, prefab);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            Register(prefab, address);
        }

        // 두 모델은 서로 다른 형상과 부품 설명을 가집니다. 실제 제품이 아닌 관찰용 기본 도형 예시입니다.
        private static void PrepareModel(string name, string address, bool extinguisher)
        {
            string folder = DirectoryPath + "/Models/" + name;
            Directory.CreateDirectory(folder);
            string path = folder + "/" + name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                var root = new GameObject(name, typeof(ModelViewer));
                try
                {
                    if (extinguisher)
                    {
                        Part(root.transform, "용기", "소화 약제를 담는 용기입니다. 교육용으로 단순화한 모형입니다.", PrimitiveType.Cylinder,
                            new Vector3(0, 1, 0), new Vector3(.65f, .65f, .65f), new Color32(210, 45, 45, 255));
                        var head = new GameObject("손잡이 조립체");
                        head.transform.SetParent(root.transform, false);
                        Describe(head, "손잡이 조립체", "손잡이와 안전핀으로 구성된 상단 부품 묶음입니다.");
                        Part(head.transform, "손잡이", "약제 방출을 조작하는 부분입니다.", PrimitiveType.Cube,
                            new Vector3(0, 1.8f, 0), new Vector3(.6f, .12f, .15f), Color.gray);
                        Part(head.transform, "안전핀", "사용 전 확인하는 안전 장치입니다.", PrimitiveType.Cylinder,
                            new Vector3(.18f, 1.65f, 0), new Vector3(.08f, .17f, .08f), Color.yellow);
                        Part(root.transform, "호스", "약제를 원하는 방향으로 안내합니다.", PrimitiveType.Cylinder,
                            new Vector3(.48f, 1.1f, 0), new Vector3(.12f, .48f, .12f), Color.black);
                        Part(root.transform, "받침", "용기 하단을 지지하는 부분입니다.", PrimitiveType.Cylinder,
                            new Vector3(0, .28f, 0), new Vector3(.72f, .08f, .72f), Color.gray);
                    }
                    else
                    {
                        var helmet = new GameObject("머리 보호 장비");
                        helmet.transform.SetParent(root.transform, false);
                        Describe(helmet, "머리 보호 장비", "보호 모자와 챙을 묶은 관찰용 모형입니다.");
                        Part(helmet.transform, "보호 모자", "머리 보호 장비의 외형을 관찰합니다.", PrimitiveType.Sphere,
                            new Vector3(-.55f, 1.4f, 0), new Vector3(.8f, .55f, .8f), Color.yellow);
                        Part(helmet.transform, "챙", "모자의 앞뒤 방향과 지지 구조를 확인합니다.", PrimitiveType.Cylinder,
                            new Vector3(-.55f, 1.22f, 0), new Vector3(.95f, .04f, .95f), Color.yellow);
                        var eyewear = new GameObject("눈 보호 장비");
                        eyewear.transform.SetParent(root.transform, false);
                        Describe(eyewear, "눈 보호 장비", "좌우 렌즈와 연결부를 관찰하는 단순 모형입니다.");
                        for (int index = 0; index < 2; index++)
                        {
                            Part(eyewear.transform, index == 0 ? "왼쪽 렌즈" : "오른쪽 렌즈", "눈을 보호하는 렌즈 위치입니다.", PrimitiveType.Cube,
                                new Vector3(.35f + .35f * index, 1.25f, 0), new Vector3(.3f, .23f, .12f), new Color32(80, 170, 220, 255));
                        }
                        Part(eyewear.transform, "연결부", "좌우 렌즈를 연결하는 구조입니다.", PrimitiveType.Cube,
                            new Vector3(.525f, 1.25f, 0), new Vector3(.12f, .08f, .13f), Color.gray);
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
            SampleModelDisassemblyBuilder.Prepare(path, extinguisher);
            Register(path, address);
        }

        // 공구 없는 점검 목표도 기존 시나리오 모듈로 실행합니다. 단계마다 별도의 클릭 대상이 있습니다.
        private static void PrepareScenario(string name, string address, string title, string[] titles, string[] instructions, string[] labels)
        {
            string folder = DirectoryPath + "/Scenarios/" + name;
            Directory.CreateDirectory(folder);
            string definitionPath = folder + "/" + name + ".asset";
            string prefabPath = folder + "/" + name + ".prefab";
            var definition = AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(definitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<ScenarioDefinition>();
                var data = new SerializedObject(definition);
                data.FindProperty("id").stringValue = name;
                data.FindProperty("title").stringValue = title;
                var steps = data.FindProperty("steps");
                steps.arraySize = titles.Length;
                for (int index = 0; index < titles.Length; index++)
                {
                    var step = steps.GetArrayElementAtIndex(index);
                    step.FindPropertyRelative("id").stringValue = "check-" + index;
                    step.FindPropertyRelative("title").stringValue = titles[index];
                    step.FindPropertyRelative("instruction").stringValue = instructions[index];
                    step.FindPropertyRelative("toolId").stringValue = "";
                    step.FindPropertyRelative("targetId").stringValue = "target-" + index;
                }
                data.FindProperty("tools").arraySize = 0;
                data.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(definition, definitionPath);
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            {
                var root = new GameObject(name, typeof(ScenarioContent));
                try
                {
                    var data = new SerializedObject(root.GetComponent<ScenarioContent>());
                    data.FindProperty("definition").objectReferenceValue = definition;
                    data.ApplyModifiedPropertiesWithoutUndo();
                    for (int index = 0; index < titles.Length; index++)
                    {
                        var part = Part(root.transform, labels[index], "", PrimitiveType.Cube,
                            new Vector3((index - 1) * .85f, 1.25f, 4), new Vector3(.65f, .65f, .2f),
                            new Color32((byte)(60 + index * 45), 130, 180, 255));
                        var marker = new SerializedObject(part.AddComponent<ScenarioObject>());
                        marker.FindProperty("id").stringValue = "target-" + index;
                        marker.FindProperty("kind").enumValueIndex = (int)ScenarioObjectKind.Target;
                        marker.ApplyModifiedPropertiesWithoutUndo();
                        var caption = new GameObject("목표 이름", typeof(TextMesh));
                        caption.transform.SetParent(root.transform, false);
                        caption.transform.localPosition = new Vector3((index - 1) * .85f, .79f, 3.85f);
                        var text = caption.GetComponent<TextMesh>();
                        text.text = labels[index];
                        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                        text.fontSize = 48;
                        text.characterSize = .025f;
                        text.anchor = TextAnchor.MiddleCenter;
                        text.color = Color.white;
                        caption.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
            Register(prefabPath, address);
        }

        private static GameObject Part(Transform parent, string title, string description, PrimitiveType shape, Vector3 position, Vector3 scale, Color color)
        {
            var part = GameObject.CreatePrimitive(shape);
            part.name = title;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            string materialPath = DirectoryPath + $"/Shared/Materials/Color-{ColorUtility.ToHtmlStringRGB(color)}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.color = color;
                AssetDatabase.CreateAsset(material, materialPath);
            }
            part.GetComponent<Renderer>().sharedMaterial = material;
            if (!string.IsNullOrEmpty(description))
            {
                Describe(part, title, description);
                UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            }
            return part;
        }

        private static void Describe(GameObject part, string title, string description)
        {
            var data = new SerializedObject(part.AddComponent<ModelPartInfo>());
            data.FindProperty("title").stringValue = title;
            data.FindProperty("description").stringValue = description;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Register(string path, string address)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), settings.DefaultGroup).address = address;
            EditorUtility.SetDirty(settings);
        }
    }
}
