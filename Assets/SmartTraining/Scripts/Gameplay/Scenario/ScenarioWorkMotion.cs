using UnityEngine;

namespace SmartTraining
{
    // 서버 작업 시간의 앞뒤는 손 이동, 가운데는 회전·타격에 사용합니다. 네트워크·씬에 의존하지 않습니다.
    public static class ScenarioWorkMotion
    {
        public const float ApproachEnd = .2f;
        public const float ReturnStart = .8f;
        public const float CancelReturnSeconds = .18f;
        public const int StrikeCount = 3;
        public const float HammerLiftDistance = .18f;

        public static float TurnProgress(float progress)
        {
            return Mathf.SmoothStep(0, 1, Mathf.InverseLerp(ApproachEnd, ReturnStart, progress));
        }


        // 핀은 실제 타격 시점마다 들어갑니다. 손을 들어 올리는 동안에는 대상이 먼저 움직이지 않습니다.
        public static float TargetProgress(ScenarioAction action, float progress)
        {
            if (action != ScenarioAction.Strike && action != ScenarioAction.Chop)
            {
                return TurnProgress(progress);
            }

            int count = action == ScenarioAction.Chop ? 2 : StrikeCount;
            float strokes = Mathf.InverseLerp(ApproachEnd, ReturnStart, progress) * count;
            return Mathf.Clamp(Mathf.FloorToInt(strokes + .0001f), 0, count) / (float)count;
        }


        public static float HammerLift(float progress)
        {
            if (progress <= ApproachEnd || progress >= ReturnStart)
            {
                return 0;
            }

            float phase = Mathf.Repeat(Mathf.InverseLerp(ApproachEnd, ReturnStart, progress) * StrikeCount, 1);
            float lift = Mathf.Sin(phase * Mathf.PI);
            return lift * lift;
        }


        // 접촉점 기준의 추가 이동·각도만 계산합니다. 모든 화면이 같은 진행률로 같은 동작을 구합니다.
        public static void ToolPose(ScenarioAction action, float progress, out Vector3 offset, out Quaternion rotation)
        {
            float phase = Mathf.InverseLerp(ApproachEnd, ReturnStart, progress);
            offset = Vector3.zero;
            rotation = Quaternion.identity;
            if (action == ScenarioAction.Strike || action == ScenarioAction.Chop)
            {
                float lift = action == ScenarioAction.Strike ? HammerLift(progress) : (progress <= ApproachEnd || progress >= ReturnStart ? 0 : Mathf.Pow(Mathf.Sin(Mathf.Repeat(phase * 2, 1) * Mathf.PI), 2));
                offset.y = lift * (action == ScenarioAction.Chop ? .3f : HammerLiftDistance);
                rotation = Quaternion.AngleAxis(-lift * (action == ScenarioAction.Chop ? 55 : 35), Vector3.forward);
            }
            else if (action == ScenarioAction.Saw)
            {
                offset.z = Mathf.Sin(phase * Mathf.PI * 10) * .09f;
            }
            else if (action == ScenarioAction.Drill)
            {
                offset.y = .03f * (1 - 2 * TurnProgress(progress));
                offset.x = Mathf.Sin(phase * Mathf.PI * 24) * .002f;
            }
            else if (action == ScenarioAction.Grip)
            {
                rotation = Quaternion.AngleAxis(Mathf.Sin(phase * Mathf.PI) * 8, Vector3.forward);
            }
        }


        public static float SpinAngle(ScenarioAction action, float progress)
        {
            return action == ScenarioAction.Drill ? TurnProgress(progress) * 360 * 12 : 0;
        }


        public static float GripAmount(ScenarioAction action, float progress)
        {
            return action == ScenarioAction.Grip ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(ApproachEnd, .35f, progress)) : 0;
        }


        public static float ContactWeight(float progress)
        {
            if (progress < ApproachEnd)
            {
                return Mathf.SmoothStep(0, 1, progress / ApproachEnd);
            }

            if (progress > ReturnStart)
            {
                return Mathf.SmoothStep(1, 0, (progress - ReturnStart) / (1 - ReturnStart));
            }

            return 1;
        }
    }
}
