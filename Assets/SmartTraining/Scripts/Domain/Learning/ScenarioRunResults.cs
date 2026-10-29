using System;
using System.Collections.Generic;

namespace SmartTraining
{
    public enum ScenarioInteractionError
    {
        None,
        WrongTool,
        WrongTarget
    }


    // 한 실습의 서버 기록입니다. UI·Netcode 없이 시각, 단계 완료, 단계별 오류 합계를 관리합니다.
    public sealed class ScenarioRunResults
    {
        public sealed class StepResult
        {
            public int Index { get; }

            public string ActorName { get; }

            public int ToolErrors { get; }

            public int TargetErrors { get; }

            public double DurationSeconds { get; }


            public StepResult(
                int index,
                string actorName,
                int toolErrors,
                int targetErrors,
                double durationSeconds
            )
            {
                Index = index;
                ActorName = actorName;
                ToolErrors = toolErrors;
                TargetErrors = targetErrors;
                DurationSeconds = durationSeconds;
            }
        }


        private readonly int stepCount;
        private readonly List<StepResult> steps = new();
        private int stepToolErrors, stepTargetErrors;
        private double stepStartedAt;
        private bool started;

        public IReadOnlyList<StepResult> Steps => steps;

        public double StartedAt { get; private set; }

        public double EndedAt { get; private set; }

        public int ToolErrors { get; private set; }

        public int TargetErrors { get; private set; }

        public bool IsComplete => steps.Count == stepCount;


        public ScenarioRunResults(int stepCount)
        {
            if (stepCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(stepCount));
            }

            this.stepCount = stepCount;
        }


        public void Begin(double now)
        {
            if (!ValidTime(now))
            {
                throw new ArgumentOutOfRangeException(nameof(now));
            }

            steps.Clear();
            StartedAt = stepStartedAt = now;
            EndedAt = 0;
            ToolErrors = TargetErrors = stepToolErrors = stepTargetErrors = 0;
            started = true;
        }


        // 권한·버전·거리 검사 후 현재 단계에 대한 공구·대상 오류만 전달받습니다.
        public bool RecordError(int stepIndex, ScenarioInteractionError error)
        {
            if (!started || IsComplete || stepIndex != steps.Count)
            {
                return false;
            }

            if (error == ScenarioInteractionError.WrongTool)
            {
                ToolErrors++;
                stepToolErrors++;
                return true;
            }

            if (error == ScenarioInteractionError.WrongTarget)
            {
                TargetErrors++;
                stepTargetErrors++;
                return true;
            }

            return false;
        }


        // 완료 수는 한 단계씩 늘어납니다. 중복 완료와 취소·오래된 상태의 기록은 거절합니다.
        public bool CompleteStep(int completedSteps, string actorName, double now)
        {
            if (!started || IsComplete || completedSteps != steps.Count + 1 || !ValidTime(now) || now < stepStartedAt)
            {
                return false;
            }

            steps.Add(new StepResult(steps.Count, actorName ?? "", stepToolErrors, stepTargetErrors, now - stepStartedAt));
            stepStartedAt = now;
            stepToolErrors = stepTargetErrors = 0;
            if (IsComplete)
            {
                EndedAt = now;
            }

            return true;
        }


        public double Elapsed(double now)
        {
            return started && ValidTime(now) ? Math.Max(0, (IsComplete ? EndedAt : now) - StartedAt) : 0;
        }


        private static bool ValidTime(double now)
        {
            return !double.IsNaN(now) && !double.IsInfinity(now) && now >= 0;
        }
    }
}
