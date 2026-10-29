using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SmartTraining.Editor
{
    public static class WorkplaceToolsChecks
    {
        [MenuItem("Smart Training/Content/Check Workplace Tools")]
        public static void Run()
        {
            SampleCourseBuilder.Prepare();
            var definition = AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(WorkplaceToolsContentBuilder.DefinitionPath);
            Require(
                definition.TryValidate(out _) && definition.Steps.Count == 9 && definition.Tools.Count == 8,
                "8종 공구와 9단계"
            );
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WorkplaceToolsContentBuilder.PrefabPath);
            Require(prefab.GetComponent<ScenarioContent>().TryInitialize(out _), "모든 작업 대상 연결");
            var progress = new ScenarioProgress(definition);
            var used = new HashSet<string>();
            double time = 0;
            foreach (var step in definition.Steps)
            {
                if (step.RequiresTool)
                {
                    var tool = definition.GetTool(step.ToolId);
                    Require(
                        AssetDatabase.GetAssetPath(tool.HeldPrefab).StartsWith(WorkplaceToolsBuilder.Folder),
                        "가져온 공구 장착 프리팹"
                    );
                    Require(
                        tool.AnimateWork && tool.HeldPrefab.transform.Find("Work Contact") != null,
                        "모션과 접촉점"
                    );
                    Require(
                        !progress.TryInteract(2, step.TargetId, progress.Revision, time, out _),
                        "공구 없는 다른 참가자 거절"
                    );
                    Require(progress.TrySelectTool(1, tool.Id, 0, out _), "개인 공구 선택");
                    used.Add(tool.Id);
                }

                Require(
                    progress.TryInteract(1, step.TargetId, progress.Revision, time, out _),
                    "올바른 공구·대상 승인"
                );
                if (progress.IsWorking)
                {
                    int revision = progress.Revision;
                    Require(!progress.TryCompleteWork(time + .1), "작업 시간 유지");
                    Require(progress.TryCancelWork(1, revision, time + .1, out _), "작업 취소");
                    Require(
                        progress.TryInteract(1, step.TargetId, progress.Revision, time, out _) &&
                        progress.TryCompleteWork(time + step.DurationSeconds + .01),
                        "재시작 후 완료"
                    );
                }

                time += 4;
            }

            Require(used.Count == 8 && progress.IsComplete, "모든 공구 사용 후 완료");
            Require(progress.TryReset(progress.Revision) && progress.CompletedSteps == 0, "재실습 초기화");
            ScenarioWorkMotion.ToolPose(ScenarioAction.Saw, .23f, out var first, out _);
            ScenarioWorkMotion.ToolPose(ScenarioAction.Saw, .29f, out var second, out _);
            Require(Mathf.Abs(first.z - second.z) > .05f, "톱 왕복 동작");
            Require(ScenarioWorkMotion.SpinAngle(ScenarioAction.Drill, .6f) > 360, "드릴 회전");
            Require(ScenarioWorkMotion.GripAmount(ScenarioAction.Grip, .4f) == 1, "플라이어 집기");
            Require(ScenarioWorkMotion.TargetProgress(ScenarioAction.Chop, .5f) == .5f, "도끼 두 번 내려치기");
            foreach (var tool in definition.Tools)
            {
                Require(
                    tool.HeldPrefab.GetComponentsInChildren<MeshFilter>(true).Any(mesh => AssetDatabase.GetAssetPath(mesh.sharedMesh).StartsWith("Assets/Workplace Tools/")),
                    "원본 에셋 메시 사용"
                );
            }

            Debug.Log("WORKPLACE TOOLS CHECKS PASSED (8 tools / 9 steps)");
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
