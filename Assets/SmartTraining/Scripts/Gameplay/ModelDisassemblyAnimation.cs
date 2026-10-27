using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SmartTraining
{
    // 모델 프리팹의 분해 클립 연결입니다. Animator Controller 없이 슬라이더가 클립의 시간을 직접 지정합니다.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class ModelDisassemblyAnimation : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("0초는 조립 상태, 마지막은 분해 상태인 반복하지 않는 Transform 애니메이션입니다. 이 루트 자체는 애니메이션하지 않습니다.")]
        private AnimationClip disassemblyClip;

        [Serializable]
        public sealed class Step
        {
            [SerializeField, Min(0)]
            [Tooltip("이 단계가 시작되는 클립의 시간(초)입니다.")]
            private float time;
            [SerializeField]
            private string title = "";
            [SerializeField, TextArea(2, 6)]
            private string description = "";
            [SerializeField]
            [Tooltip("강조할 부품이나 부모 묶음입니다. 모든 하위 부품도 포함합니다.")]
            private Transform[] targets = Array.Empty<Transform>();

            public float Time => time;
            public string Title => title;
            public string Description => description;
            public IReadOnlyList<Transform> Targets => targets ?? Array.Empty<Transform>();
        }

        [SerializeField]
        private Step[] steps = Array.Empty<Step>();

        // Inspector 배열 순서와 관계없이 시간순으로 사용합니다. 범위 밖 시간·중복 시각은 제외합니다.
        public IReadOnlyList<Step> GetOrderedSteps()
        {
            if (disassemblyClip == null || disassemblyClip.length <= 0)
            {
                return Array.Empty<Step>();
            }
            return steps.Where(step => step != null && step.Time >= 0 && step.Time <= disassemblyClip.length)
                .OrderBy(step => step.Time).GroupBy(step => step.Time).Select(group => group.First()).ToArray();
        }

        public int FindStepIndex(IReadOnlyList<Step> ordered, float amount)
        {
            if (disassemblyClip == null || float.IsNaN(amount) || float.IsInfinity(amount))
            {
                return -1;
            }
            float time = Mathf.Clamp01(amount) * disassemblyClip.length;
            for (int i = ordered.Count - 1; i >= 0; i--)
            {
                if (time + 0.00001f >= ordered[i].Time)
                {
                    return i;
                }
            }
            return -1;
        }

        public AnimationClip Clip => disassemblyClip;
        public Animator Animator => GetComponent<Animator>();
    }
}
