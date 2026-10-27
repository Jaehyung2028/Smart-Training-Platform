using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace SmartTraining.Editor
{
    public static class ModelContentBuilder
    {
        public const string PrefabPath = "Assets/SmartTraining/Content/Models/TrainingDrill.prefab";

        public const string AssetAddress = "content/model/training-drill";

        // 외부 모델 없이 둘러보기 흐름을 확인할 수 있는 전동 드릴 예시입니다. 기존 프리팹·재질은 덮어쓰지 않습니다.
        [MenuItem("Smart Training/Content/Prepare Model Viewer Example")]
        public static void Prepare()
        {
            PrepareNetworkPrefab();
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            AssetDatabase.Refresh();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                CreatePrefab();
            }

            PreparePartDescriptions();
            PrepareDisassemblyAnimation();
            PrepareStepGuide();

            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (settings == null)
            {
                throw new InvalidOperationException("Addressables 설정을 만들지 못했습니다.");
            }

            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(PrefabPath), settings.DefaultGroup);
            entry.address = AssetAddress;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("MODEL CONTENT READY: " + AssetAddress);
        }

        // 예시 클립은 처음 한 번만 생성합니다. 이후 Animation 창에서 수정한 키프레임과 사용자 연결은 보존합니다.
        private static void PrepareDisassemblyAnimation()
        {
            const string clipPath = "Assets/SmartTraining/Content/Models/TrainingDrillDisassembly.anim";
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var source = root.GetComponent<ModelDisassemblyAnimation>();
                if (source != null && source.Clip != null)
                {
                    return;
                }
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip == null)
                {
                    clip = new AnimationClip { name = "Training Drill Disassembly", frameRate = 30, wrapMode = WrapMode.ClampForever };
                    AddDisassemblyMove(clip, root, "Battery Assembly", Vector3.down * 0.65f, 0, 1);
                    AddDisassemblyMove(clip, root, "Drill Assembly/Drill Bit", Vector3.forward * 0.7f, 1, 2);
                    AddDisassemblyMove(clip, root, "Drill Assembly/Chuck", Vector3.forward * 0.35f, 2, 3);
                    AddDisassemblyMove(clip, root, "Motor Assembly", Vector3.left * 0.65f, 3, 5);
                    AddDisassemblyMove(clip, root, "Grip Assembly", Vector3.right * 0.65f, 3, 5);
                    AddDisassemblyMove(clip, root, "Motor Assembly/Ventilation", Vector3.right * 0.25f, 4, 5);
                    var grip = root.transform.Find("Grip Assembly");
                    if (grip != null)
                    {
                        var start = grip.localRotation;
                        var end = start * Quaternion.Euler(0, 0, -20);
                        var properties = new[] { "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" };
                        for (int i = 0; i < 4; i++)
                        {
                            SetDisassemblyCurve(clip, "Grip Assembly", properties[i], start[i], end[i], 3, 5);
                        }
                    }
                    clip.EnsureQuaternionContinuity();
                    var settings = AnimationUtility.GetAnimationClipSettings(clip);
                    settings.loopTime = false;
                    AnimationUtility.SetAnimationClipSettings(clip, settings);
                    AssetDatabase.CreateAsset(clip, clipPath);
                }
                if (source == null)
                {
                    source = root.AddComponent<ModelDisassemblyAnimation>();
                }
                // Animation 창에서 예시 클립을 바로 편집할 수 있도록 연결합니다. 실제 재생은 Playables가 담당합니다.
                var animator = source.Animator;
                if (animator.runtimeAnimatorController == null)
                {
                    const string controllerPath = "Assets/SmartTraining/Content/Models/TrainingDrillDisassembly.controller";
                    var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                    if (controller == null)
                    {
                        controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                        var state = controller.layers[0].stateMachine.AddState("Disassembly");
                        state.motion = clip;
                        EditorUtility.SetDirty(controller);
                    }
                    animator.runtimeAnimatorController = controller;
                    animator.enabled = false;
                }
                var serialized = new SerializedObject(source);
                serialized.FindProperty("disassemblyClip").objectReferenceValue = clip;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // 기존 단계 목록은 보존하고 예시 클립에만 기본 안내를 추가합니다.
        private static void PrepareStepGuide()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var source = root.GetComponent<ModelDisassemblyAnimation>();
                if (source == null || AssetDatabase.GetAssetPath(source.Clip) != "Assets/SmartTraining/Content/Models/TrainingDrillDisassembly.anim")
                {
                    return;
                }
                var serialized = new SerializedObject(source);
                var steps = serialized.FindProperty("steps");
                if (steps.arraySize > 0)
                {
                    return;
                }
                var definitions = new[]
                {
                    (Time: 0f, Title: "배터리 분리", Description: "전원부가 아래로 분리되는 과정을 확인하세요. 배터리와 받침이 하나의 묶음으로 이동합니다.", Targets: new[] { "Battery Assembly" }),
                    (Time: 1f, Title: "비트 분리", Description: "앞쪽으로 이동하는 드릴 비트를 확인하세요. 비트가 척에서 분리되는 방향을 살펴봅니다.", Targets: new[] { "Drill Assembly/Drill Bit" }),
                    (Time: 2f, Title: "척 분리", Description: "비트를 고정하는 척이 앞쪽으로 분리됩니다. 비트와 척의 연결 구조를 확인하세요.", Targets: new[] { "Drill Assembly/Chuck" }),
                    (Time: 3f, Title: "외장 분리", Description: "모터 조립체와 손잡이 묶음이 양쪽으로 벌어집니다. 손잡이의 회전과 외장 내부의 위치 관계를 확인하세요.", Targets: new[] { "Motor Assembly", "Grip Assembly" }),
                    (Time: 4f, Title: "통풍부 확인", Description: "모터 조립체에서 통풍부가 분리되는 과정을 살펴봅니다. 재조립을 누르면 과정을 역방향으로 확인할 수 있습니다.", Targets: new[] { "Motor Assembly/Ventilation" })
                };
                steps.arraySize = definitions.Length;
                for (int i = 0; i < definitions.Length; i++)
                {
                    var step = steps.GetArrayElementAtIndex(i);
                    step.FindPropertyRelative("time").floatValue = definitions[i].Time;
                    step.FindPropertyRelative("title").stringValue = definitions[i].Title;
                    step.FindPropertyRelative("description").stringValue = definitions[i].Description;
                    var targets = step.FindPropertyRelative("targets");
                    targets.arraySize = definitions[i].Targets.Length;
                    for (int target = 0; target < targets.arraySize; target++)
                    {
                        targets.GetArrayElementAtIndex(target).objectReferenceValue = root.transform.Find(definitions[i].Targets[target]);
                    }
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AddDisassemblyMove(AnimationClip clip, GameObject root, string path, Vector3 offset, float startAt, float endAt)
        {
            var part = root.transform.Find(path);
            if (part == null)
            {
                return;
            }
            var position = part.localPosition;
            var properties = new[] { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z" };
            for (int i = 0; i < 3; i++)
            {
                SetDisassemblyCurve(clip, path, properties[i], position[i], position[i] + offset[i], startAt, endAt);
            }
        }

        private static void SetDisassemblyCurve(AnimationClip clip, string path, string property, float start, float end, float startAt, float endAt)
        {
            var keys = new System.Collections.Generic.List<Keyframe> { new Keyframe(0, start) };
            if (startAt > 0)
            {
                keys.Add(new Keyframe(startAt, start));
            }
            keys.Add(new Keyframe(endAt, end));
            if (endAt < 5)
            {
                keys.Add(new Keyframe(5, end));
            }
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), new AnimationCurve(keys.ToArray()));
        }

        private static void PreparePartDescriptions()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                bool changed = false;
                changed |= AddPartInfo(root, "Motor Body", "모터 하우징", "모터와 구동 장치를 보호하는 외장입니다. 드릴의 회전을 만들어내는 구동부가 들어 있습니다.");
                changed |= AddPartInfo(root, "Grip", "손잡이", "작업 중 공구를 안정적으로 잡는 부분입니다. 미끄러지지 않도록 잡고 드릴이 기울어지지 않게 지지합니다.");
                changed |= AddPartInfo(root, "Battery", "배터리", "공구에 전원을 공급하는 충전식 전원부입니다. 작업 전 충전 상태와 장착 상태를 확인합니다.");
                changed |= AddPartInfo(root, "Battery Base", "배터리 받침", "배터리 하단을 지지하고 보호하는 부분입니다.");
                changed |= AddPartInfo(root, "Trigger", "작동 스위치", "손가락으로 눌러 공구의 작동을 조절하는 입력부입니다. 작업 준비가 끝난 뒤 조작합니다.");
                changed |= AddPartInfo(root, "Chuck", "척", "드릴 비트를 고정하는 부분입니다. 비트가 빠지거나 흔들리지 않도록 체결 상태를 확인합니다.");
                changed |= AddPartInfo(root, "Drill Bit", "드릴 비트", "대상에 구멍을 내는 교체형 작업 도구입니다. 대상 재질과 작업에 적합한 비트를 선택합니다.");
                for (int i = 0; i < 3; i++)
                {
                    changed |= AddPartInfo(root, "Vent " + i, "통풍구", "모터에서 발생하는 열을 배출하는 부분입니다. 사용 중 통풍구를 가리지 않도록 합니다.");
                }
                changed |= AddPartInfo(root, "", "전동 드릴", "회전 동력을 이용해 비트로 작업하는 공구입니다. 아래 묶음을 선택해 주요 구조를 살펴보세요.");
                changed |= EnsureGroup(root, "Motor Assembly", "모터 조립체", "모터 하우징과 통풍부를 포함하는 구동부입니다.", new[] { "Motor Body", "Vent 0", "Vent 1", "Vent 2" });
                var motor = root.transform.Find("Motor Assembly");
                if (motor != null)
                {
                    changed |= EnsureGroup(motor.gameObject, "Ventilation", "통풍부", "모터의 열을 배출하는 통풍구 묶음입니다.", new[] { "Vent 0", "Vent 1", "Vent 2" });
                }
                changed |= EnsureGroup(root, "Grip Assembly", "손잡이 조립체", "손잡이와 작동 스위치를 포함하는 조작부입니다.", new[] { "Grip", "Trigger" });
                changed |= EnsureGroup(root, "Battery Assembly", "배터리 조립체", "배터리와 받침을 포함하는 전원부입니다.", new[] { "Battery", "Battery Base" });
                changed |= EnsureGroup(root, "Drill Assembly", "드릴 작업부", "비트를 고정하는 척과 작업용 비트의 묶음입니다.", new[] { "Chuck", "Drill Bit" });
                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool AddPartInfo(GameObject root, string path, string title, string description)
        {
            var part = path.Length == 0 ? root.transform : root.transform.Find(path);
            if (part == null || part.GetComponent<ModelPartInfo>() != null)
            {
                return false;
            }
            var info = part.gameObject.AddComponent<ModelPartInfo>();
            var serialized = new SerializedObject(info);
            serialized.FindProperty("title").stringValue = title;
            serialized.FindProperty("description").stringValue = description;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool EnsureGroup(GameObject root, string name, string title, string description, string[] children)
        {
            var group = root.transform.Find(name);
            bool changed = false;
            foreach (var childName in children)
            {
                var child = root.transform.Find(childName);
                if (child == null)
                {
                    continue;
                }
                if (group == null)
                {
                    group = new GameObject(name).transform;
                    group.SetParent(root.transform, false);
                    changed = true;
                }
                child.SetParent(group, true);
                changed = true;
            }
            if (group != null)
            {
                changed |= AddPartInfo(group.gameObject, "", title, description);
            }
            return changed;
        }

        // Addressables 모델과 별개인 공통 네트워크 루트입니다. 모든 콘텐츠 모델이 같은 등록 프리팹을 재사용합니다.
        public static void PrepareNetworkPrefab()
        {
            const string path = "Assets/SmartTraining/Resources/SmartTraining/SharedModel.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                return;
            }
            var root = new GameObject("Shared Model");
            try
            {
                root.transform.position = new Vector3(0, 1, 0);
                var networkObject = root.AddComponent<NetworkObject>();
                networkObject.DontDestroyWithOwner = true;
                var networkTransform = root.AddComponent<NetworkTransform>();
                networkTransform.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
                networkTransform.SyncPositionX = networkTransform.SyncPositionY = networkTransform.SyncPositionZ = false;
                networkTransform.SyncScaleX = networkTransform.SyncScaleY = networkTransform.SyncScaleZ = false;
                networkTransform.Interpolate = true;
                networkTransform.UseQuaternionSynchronization = true;
                networkTransform.RotAngleThreshold = 0.1f;
                root.AddComponent<SharedModel>();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreatePrefab()
        {
            var root = new GameObject("Training Drill", typeof(ModelViewer));
            try
            {
                var yellow = Material("Drill Yellow", new Color32(245, 180, 30, 255));
                var dark = Material("Drill Dark", new Color32(35, 42, 52, 255));
                var steel = Material("Drill Steel", new Color32(175, 190, 205, 255));
                Part(root, "Motor Body", PrimitiveType.Cube, new Vector3(0, 1.2f, 0), new Vector3(0.6f, 0.5f, 0.9f), yellow);
                Part(root, "Grip", PrimitiveType.Cube, new Vector3(0, 0.78f, -0.2f), new Vector3(0.28f, 0.6f, 0.32f), dark);
                Part(root, "Battery", PrimitiveType.Cube, new Vector3(0, 0.42f, -0.2f), new Vector3(0.55f, 0.22f, 0.5f), yellow);
                Part(root, "Battery Base", PrimitiveType.Cube, new Vector3(0, 0.31f, -0.2f), new Vector3(0.56f, 0.06f, 0.51f), dark);
                Part(root, "Trigger", PrimitiveType.Cube, new Vector3(0, 1.01f, 0.19f), new Vector3(0.17f, 0.14f, 0.15f), dark);
                var chuck = Part(root, "Chuck", PrimitiveType.Cylinder, new Vector3(0, 1.2f, 0.56f), new Vector3(0.32f, 0.12f, 0.32f), dark);
                chuck.localRotation = Quaternion.Euler(90, 0, 0);
                var bit = Part(root, "Drill Bit", PrimitiveType.Cylinder, new Vector3(0, 1.2f, 0.9f), new Vector3(0.065f, 0.23f, 0.065f), steel);
                bit.localRotation = Quaternion.Euler(90, 0, 0);
                for (int i = 0; i < 3; i++)
                {
                    Part(root, "Vent " + i, PrimitiveType.Cube, new Vector3(0.305f, 1.08f + i * 0.1f, -0.17f), new Vector3(0.015f, 0.035f, 0.24f), dark);
                }

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Material Material(string name, Color color)
        {
            string path = Path.GetDirectoryName(PrefabPath).Replace('\\', '/') + "/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Transform Part(GameObject root, string name, PrimitiveType shape, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(shape);
            part.name = name;
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            return part.transform;
        }
    }
}
