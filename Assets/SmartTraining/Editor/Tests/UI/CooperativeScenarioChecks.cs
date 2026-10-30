using System;
using UnityEditor;
using UnityEngine;

namespace SmartTraining.Editor
{
    public static class CooperativeScenarioChecks
    {
        private static int checks;

        public static void Run()
        {
            SampleCourseBuilder.Prepare();
            checks = 0;
            var basic = AssetDatabase.LoadAssetAtPath<ScenarioDefinition>("Assets/SmartTraining/Content/Scenarios/CooperativeMaintenance/CooperativeMaintenance.asset");
            var progress = new ScenarioProgress(basic);
            Check(!progress.TryInteract(1, "fault-lamp", 0, out _), "역할 없는 요청 거절");
            Check(progress.TrySelectRole(1, ScenarioRole.Inspector, 0, out _), "점검 배정");
            Check(progress.TryInteract(1, "fault-lamp", 0, out _), "다른 역할이 비어 있어도 담당 단계 수행");
            Check(progress.TrySelectRole(1, ScenarioRole.Technician, 0, out _), "진행 후 빈 역할 겸임");
            Check(
                progress.GetRoles(1) == (ScenarioRole.Inspector | ScenarioRole.Technician),
                "기존 역할 보존"
            );
            Check(!progress.TrySelectRole(2, ScenarioRole.Technician, 0, out _), "다른 사람 역할 중복 거절");
            Check(
                !progress.TrySelectRole(1, ScenarioRole.SafetyOfficer, 0, out _),
                "현재 콘텐츠에 없는 역할 거절"
            );
            Check(!progress.TrySelectRole(1, (ScenarioRole)3, 0, out _), "역할을 한 번에 여러 개 요청할 수 없음");
            progress.TrySelectTool(1, "spanner", 0, out _);
            Check(
                progress.TryInteract(1, "cover-bolt", progress.Revision, 10, out _) && progress.IsWorking,
                "겸임자가 작업"
            );
            Check(!progress.TrySelectRole(1, ScenarioRole.Inspector, 0, out _), "본인 작업 중 역할 요청 제한");
            progress.RemoveParticipant(2);
            Check(progress.IsWorking, "관찰자 퇴장은 작업에 영향 없음");
            progress.RemoveParticipant(1);
            Check(
                !progress.IsWorking && progress.GetRoles(1) == ScenarioRole.None,
                "작업자 퇴장 시 취소와 모든 역할 반환"
            );
            Check(progress.TrySelectRole(2, ScenarioRole.Technician, 0, out _), "후임 배정");
            progress.TrySelectTool(2, "spanner", 0, out _);
            Check(
                progress.TryInteract(2, "cover-bolt", progress.Revision, 20, out _) && progress.TryCompleteWork(22),
                "후임 작업 완료"
            );
            Check(
                progress.TryReset(progress.Revision) && progress.GetRoles(2) == ScenarioRole.None,
                "초기화 역할 제거"
            );
            Check(!progress.TrySelectRole(2, ScenarioRole.Technician, 0, out _), "이전 실습 요청 거절");
            var four = AssetDatabase.LoadAssetAtPath<ScenarioDefinition>("Assets/SmartTraining/Content/Scenarios/CooperativeEmergency/CooperativeEmergency.asset");
            Check(
                four.TryValidate(out _) && four.Steps.Count == 10 && (int)four.Roles == 15,
                "네 역할 열 단계"
            );
            Complete(four, false);
            Complete(four, true);
            Debug.Log("COOPERATIVE SCENARIO CHECKS PASSED (" + checks + ")");
        }


        private static void Complete(ScenarioDefinition definition, bool solo)
        {
            var progress = new ScenarioProgress(definition);
            double time = 0;
            foreach (var step in definition.Steps)
            {
                ulong actor = solo ? 1ul : (ulong)step.RequiredRole;
                Check(progress.TrySelectRole(actor, step.RequiredRole, 0, out _), "빈 역할을 필요 시 배정");
                Check(
                    !progress.TryInteract(99, step.TargetId, progress.Revision, time, out _),
                    "역할 없는 참가자 수행 거절"
                );
                if (step.RequiresTool)
                {
                    progress.TrySelectTool(actor, step.ToolId, 0, out _);
                }

                Check(
                    progress.TryInteract(actor, step.TargetId, progress.Revision, time, out _),
                    "담당자 수행"
                );
                if (progress.IsWorking)
                {
                    Check(progress.TryCompleteWork(time + step.DurationSeconds + .1), "서버 시간 작업 완료");
                }

                time += step.DurationSeconds + 1;
            }

            Check(progress.IsComplete, solo ? "혼자 네 역할 겸임 완료" : "네 명 역할 분담 완료");
        }


        private static void Check(bool success, string message)
        {
            if (!success)
            {
                throw new InvalidOperationException(message);
            }

            checks++;
        }
    }
}
