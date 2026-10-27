using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace SmartTraining
{
    // 서버에서 받은 진행도를 분해 클립의 시간으로 바꿉니다. 부품별 네트워크 Transform이나 Animator Controller는 필요하지 않습니다.
    public sealed class ModelExplodedView : IDisposable
    {
        private sealed class Pose
        {
            public Transform Transform;
            public Vector3 Position, Scale;
            public Quaternion Rotation;
        }

        private readonly List<Pose> poses = new();
        private readonly Animator animator;
        private readonly AnimationClip clip;
        private readonly RuntimeAnimatorController originalController;
        private readonly bool originalEnabled, originalRootMotion;
        private readonly AnimatorCullingMode originalCulling;
        private PlayableGraph graph;
        private AnimationClipPlayable playable;

        public bool IsAvailable => animator != null && clip != null && clip.length > 0 && graph.IsValid();

        public ModelExplodedView(ModelDisassemblyAnimation source)
        {
            if (source == null || source.Clip == null || source.Clip.length <= 0)
            {
                return;
            }
            animator = source.Animator;
            clip = source.Clip;
            originalController = animator.runtimeAnimatorController;
            originalEnabled = animator.enabled;
            originalRootMotion = animator.applyRootMotion;
            originalCulling = animator.cullingMode;
            foreach (var transform in source.GetComponentsInChildren<Transform>(true))
            {
                if (transform != source.transform)
                {
                    poses.Add(new Pose { Transform = transform, Position = transform.localPosition,
                        Rotation = transform.localRotation, Scale = transform.localScale });
                }
            }
            // 화면 밖에서도 같은 진행도를 평가하고, 자체 시간 재생과 루트 모션은 사용하지 않습니다.
            animator.runtimeAnimatorController = null;
            animator.enabled = true;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph = PlayableGraph.Create("Model Disassembly");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetApplyFootIK(false);
            playable.SetApplyPlayableIK(false);
            playable.SetSpeed(0);
            var output = AnimationPlayableOutput.Create(graph, "Disassembly", animator);
            output.SetSourcePlayable(playable);
            graph.Play();
            Apply(0);
        }

        public void Apply(float amount)
        {
            if (!IsAvailable || float.IsNaN(amount) || float.IsInfinity(amount))
            {
                return;
            }
            // 모델 루트는 중앙 배치와 공유 회전에 사용합니다. 클립에 루트 트랙이 들어가도 이 좌표는 보호합니다.
            var root = animator.transform;
            var position = root.localPosition;
            var rotation = root.localRotation;
            var scale = root.localScale;
            playable.SetTime(Mathf.Clamp01(amount) * clip.length);
            graph.Evaluate(0);
            root.SetLocalPositionAndRotation(position, rotation);
            root.localScale = scale;
            if (amount <= 0)
            {
                RestorePose();
            }
        }

        private void RestorePose()
        {
            foreach (var pose in poses)
            {
                if (pose.Transform != null)
                {
                    pose.Transform.SetLocalPositionAndRotation(pose.Position, pose.Rotation);
                    pose.Transform.localScale = pose.Scale;
                }
            }
        }

        public void Dispose()
        {
            if (graph.IsValid())
            {
                graph.Destroy();
            }
            if (animator != null)
            {
                animator.runtimeAnimatorController = originalController;
            }
            RestorePose();
            poses.Clear();
            if (animator != null)
            {
                animator.enabled = originalEnabled;
                animator.applyRootMotion = originalRootMotion;
                animator.cullingMode = originalCulling;
            }
        }
    }
}
