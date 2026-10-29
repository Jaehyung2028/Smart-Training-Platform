using System;
using UnityEngine;

namespace SmartTraining
{
    // 완료된 공동 단계에 맞춰 불량·교체 부품과 표시등을 바꿉니다. 네트워크 상태를 중복 전송하지 않습니다.
    public sealed class ScenarioStageVisuals : MonoBehaviour
    {
        [Serializable]
        private struct Change
        {
            public int completedSteps;
            public GameObject before, after;
        }


        [SerializeField]
        private ScenarioContent scenario;
        [SerializeField]
        private Change[] changes;
        private int displayed = -1;

        private void Update()
        {
            int completed = scenario != null ? scenario.CompletedSteps : 0;
            if (displayed == completed)
            {
                return;
            }

            displayed = completed;
            foreach (var change in changes)
            {
                if (change.before != null)
                {
                    change.before.SetActive(completed < change.completedSteps);
                }

                if (change.after != null)
                {
                    change.after.SetActive(completed >= change.completedSteps);
                }
            }
        }
    }
}
