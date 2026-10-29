using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SmartTraining.Editor
{
    // 가져온 모델은 유지하고, 이 프로젝트의 이동 방식에 맞는 Animator와 표시 참조만 설정합니다.
    internal static class RoomPlayerAvatarBuilder
    {
        internal const string ModelPath = "Assets/Stylized_Astronaut/Character/Astronaut.fbx";
        internal const string ControllerPath = "Assets/SmartTraining/Content/Characters/RoomPlayerAstronaut.controller";

        internal static bool Configure(GameObject root)
        {
            var player = root.GetComponent<RoomPlayer>();
            var data = new SerializedObject(player);
            var model = root.transform.Find("Astronaut");
            if (model == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
                if (source == null)
                {
                    throw new InvalidOperationException("Astronaut 모델을 찾을 수 없습니다.");
                }

                model = ((GameObject)PrefabUtility.InstantiatePrefab(source, root.transform)).transform;
                model.name = "Astronaut";
            }

            var animator = model.GetComponent<Animator>();
            if (animator == null)
            {
                throw new InvalidOperationException("Astronaut 모델에 Animator가 필요합니다.");
            }

            var controller = EnsureController();
            if (data.FindProperty("avatarRoot").objectReferenceValue == model &&
                data.FindProperty("avatarAnimator").objectReferenceValue == animator &&
                animator.runtimeAnimatorController == controller &&
                !animator.applyRootMotion &&
                !model.gameObject.activeSelf)
            {
                return false;
            }

            // 처음 연결할 때만 이동 중심과 발바닥을 맞춥니다. 이후 사용자가 편집한 배치는 보존합니다.
            if (data.FindProperty("avatarRoot").objectReferenceValue == null)
            {
                model.localPosition = Vector3.zero;
                model.localRotation = Quaternion.identity;
                var renderers = model.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                {
                    throw new InvalidOperationException("Astronaut 모델의 Renderer를 찾을 수 없습니다.");
                }

                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                }

                if (bounds.size.y <= .001f)
                {
                    throw new InvalidOperationException("Astronaut 모델 크기를 확인할 수 없습니다.");
                }

                float scale = 1.8f / bounds.size.y;
                model.localScale *= scale;
                model.localPosition = new Vector3(-bounds.center.x * scale, -1 - bounds.min.y * scale, -bounds.center.z * scale);
                Debug.Log("ASTRONAUT MODEL BOUNDS: source=" + bounds + "; scale=" + model.localScale + "; offset=" + model.localPosition);
            }

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            model.gameObject.SetActive(false);
            data.FindProperty("avatarRoot").objectReferenceValue = model;
            data.FindProperty("avatarAnimator").objectReferenceValue = animator;
            PrefabUtility.RecordPrefabInstancePropertyModifications(model);
            PrefabUtility.RecordPrefabInstancePropertyModifications(model.gameObject);
            PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
            data.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }


        private static RuntimeAnimatorController EnsureController()
        {
            var existing = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (existing != null)
            {
                return existing;
            }

            var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().ToArray();
            var idle = clips.Single(clip => clip.name == "Idle");
            var walk = clips.Single(clip => clip.name == "Walk");
            var run = clips.Single(clip => clip.name == "Run");
            Directory.CreateDirectory(Path.GetDirectoryName(ControllerPath));
            AssetDatabase.Refresh();
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            var machine = controller.layers[0].stateMachine;
            var state = machine.AddState("Locomotion");
            var tree = new BlendTree
            {
                name = "Idle Walk Run",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(idle, 0);
            tree.AddChild(walk, 2);
            tree.AddChild(run, 4);
            state.motion = tree;
            machine.defaultState = state;
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }
    }
}
