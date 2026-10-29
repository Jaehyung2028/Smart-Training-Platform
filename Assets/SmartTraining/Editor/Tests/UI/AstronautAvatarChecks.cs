using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SmartTraining.Editor
{
    public static class AstronautAvatarChecks
    {
        [MenuItem("Smart Training/Content/Check Astronaut Avatar")]
        public static void Run()
        {
            RoomPlayerPrefabBuilder.Create();
            const string path = "Assets/SmartTraining/Resources/SmartTraining/RoomPlayer.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var player = root.GetComponent<RoomPlayer>();
                var data = new SerializedObject(player);
                var model = (Transform)data.FindProperty("avatarRoot").objectReferenceValue;
                var animator = (Animator)data.FindProperty("avatarAnimator").objectReferenceValue;
                Require(
                    model != null && model.name == "Astronaut" && model.parent == root.transform,
                    "사용자 배치 모델 연결"
                );
                Require(
                    !model.gameObject.activeSelf && animator != null && !animator.applyRootMotion,
                    "초기 숨김과 Root Motion 해제"
                );
                Require(model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length > 0, "스킨 모델 유지");
                Require(
                    model.GetComponentsInChildren<Camera>(true).Length == 0 &&
                    model.GetComponentsInChildren<CharacterController>(true).Length == 0,
                    "별도 카메라·이동 컨트롤러 없음"
                );
                var controller = (AnimatorController)animator.runtimeAnimatorController;
                Require(
                    controller.parameters.Any(parameter => parameter.name == "Speed" && parameter.type == AnimatorControllerParameterType.Float),
                    "속도 파라미터"
                );
                var tree = (BlendTree)controller.layers[0].stateMachine.defaultState.motion;
                Require(
                    tree.children.Select(child => child.motion.name).SequenceEqual(new[] { "Idle", "Walk", "Run" }),
                    "세 이동 클립 연결"
                );
                Require(tree.children.All(child => ((AnimationClip)child.motion).isLooping), "이동 클립 반복");
                var count = root.transform.childCount;
                using (var view = new PlayerAvatarView(root.transform, model, animator))
                {
                    view.Show(true);
                    Require(view.HasVisual && !model.gameObject.activeSelf, "1인칭 본인 몸체 숨김");
                    view.Hide();
                    view.Show(true);
                    Require(root.transform.childCount == count, "재시작 시 모델 재생성 없음");
                }

                Require(model != null, "표시 정리 후 프리팹 소유 모델 보존");
                Debug.Log("ASTRONAUT AVATAR CHECKS PASSED (10)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }


        private static void Require(bool success, string message)
        {
            if (!success)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
