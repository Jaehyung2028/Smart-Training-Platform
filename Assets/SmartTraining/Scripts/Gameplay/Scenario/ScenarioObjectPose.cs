using System.Collections.Generic;
using UnityEngine;

namespace SmartTraining
{
    // 완료한 작업의 최종 자세와 진행 중 작업을 같은 규칙으로 계산합니다. 시간이나 네트워크를 직접 조회하지 않습니다.
    public static class ScenarioObjectPose
    {
        public static float Evaluate(
            IReadOnlyList<ScenarioDefinition.Step> steps,
            string targetId,
            int completedSteps,
            bool working,
            float workProgress
        )
        {
            float amount = 0;
            for (int index = 0; index < completedSteps && index < steps.Count; index++)
            {
                var step = steps[index];
                if (step.TargetId == targetId && step.Action != ScenarioAction.Check)
                {
                    amount = step.Action == ScenarioAction.Tighten ? 0 : 1;
                }
            }

            if (working && completedSteps < steps.Count)
            {
                var step = steps[completedSteps];
                if (step.TargetId == targetId && step.Action != ScenarioAction.Check)
                {
                    float destination = step.Action == ScenarioAction.Tighten ? 0 : 1;
                    amount = Mathf.Lerp(amount, destination, ScenarioWorkMotion.TargetProgress(step.Action, workProgress));
                }
            }

            return amount;
        }
    }
}
