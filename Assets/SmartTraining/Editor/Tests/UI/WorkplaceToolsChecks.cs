using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace SmartTraining.Editor
{
    // 별도 공구 콘텐츠 대신 여섯 기존 실습의 공구 분배·역할·작업 대상·초기화를 함께 검사합니다.
    public static class WorkplaceToolsChecks
    {
        [MenuItem("Smart Training/Content/Check Workplace Tools")]
        public static void Run()
        {
            SampleCourseBuilder.Prepare();
            var used = new[]
            {
                new HashSet<string>(),
                new HashSet<string>()
            };
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets(
                "t:ScenarioDefinition",
                new[] { ScenarioContentBuilder.DirectoryPath }
            ))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var definition = AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(path);
                Require(definition.TryValidate(out _), "유효한 단계 데이터: " + path);
                Require(definition.Tools.Count == 8, "모든 실습의 8종 선택 메뉴");
                Require(definition.Steps.Count(step => !step.RequiresTool) >= 2, "콘텐츠마다 무공구 점검 혼합");
                Require(
                    definition.Steps.Where(step => step.RequiresTool).Select(step => step.ToolId).Distinct().Count() >= 3,
                    "콘텐츠마다 세 종류 이상 공구 사용"
                );
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace(".asset", ".prefab"));
                Require(
                    prefab.GetComponent<ScenarioContent>().TryInitialize(out var error),
                    path + ": " + error
                );
                var progress = new ScenarioProgress(definition);
                foreach (var role in ScenarioRoles.Enumerate(definition.Roles))
                {
                    Require(progress.TrySelectRole((ulong)role, role, 0, out _), "역할 배정");
                }

                double time = 0;
                foreach (var step in definition.Steps)
                {
                    ulong actor = step.RequiredRole == ScenarioRole.None ? 1ul : (ulong)step.RequiredRole;
                    if (step.RequiresTool)
                    {
                        used[definition.RequiresRoles ? 1 : 0].Add(step.ToolId);
                        Require(
                            !progress.TryInteract(99, step.TargetId, progress.Revision, time, out _),
                            "타인의 공구·역할 공유 불가"
                        );
                        Require(progress.TrySelectTool(actor, step.ToolId, 0, out _), "올바른 공구 선택");
                    }
                    else
                    {
                        Require(progress.TrySelectTool(actor, "", 0, out _), "공구 선택 해제");
                    }

                    Require(
                        progress.TryInteract(actor, step.TargetId, progress.Revision, time, out _),
                        "대상 승인: " + step.Title
                    );
                    if (progress.IsWorking)
                    {
                        Require(!progress.TryCompleteWork(time + .1), "작업 시간 유지");
                        Require(progress.TryCancelWork(actor, progress.Revision, time + .1, out _), "작업 취소");
                        Require(
                            progress.TryInteract(actor, step.TargetId, progress.Revision, time, out _) &&
                            progress.TryCompleteWork(time + step.DurationSeconds + .01),
                            "재시도와 완료"
                        );
                    }

                    time += step.DurationSeconds + 1;
                }

                Require(progress.IsComplete && progress.TryReset(progress.Revision), "전체 완료 후 재실습");
                Require(progress.CompletedSteps == 0 && !progress.IsWorking, "재실습 진행 초기화");
                foreach (var tool in definition.Tools)
                {
                    Require(
                        tool.AnimateWork && tool.HeldPrefab.transform.Find("Work Contact") != null,
                        "공구 동작·접촉점"
                    );
                    Require(
                        tool.HeldPrefab.GetComponentsInChildren<MeshFilter>(true).Any(mesh => AssetDatabase.GetAssetPath(mesh.sharedMesh).StartsWith("Assets/Workplace Tools/")),
                        "공용 모델 사용"
                    );
                }

                foreach (var visual in prefab.GetComponentsInChildren<ScenarioStageVisuals>(true))
                {
                    var changes = new SerializedObject(visual).FindProperty("changes");
                    for (int index = 0; index < changes.arraySize; index++)
                    {
                        int step = changes.GetArrayElementAtIndex(index).FindPropertyRelative("completedSteps").intValue;
                        Require(step > 0 && step <= definition.Steps.Count, "시각 효과 전환 단계 범위");
                    }
                }

                count++;
            }

            Require(
                count == 6 &&
                used.All(tools => tools.SetEquals(new[] { "spanner", "screwdriver", "hammer", "axe", "drill", "pipe-wrench", "pliers", "saw" })),
                "일반·협동 실습 각각 8종 전체 사용"
            );
            Require(
                !AssetDatabase.IsValidFolder(ScenarioContentBuilder.DirectoryPath + "/WorkplaceTools"),
                "별도 종합 실습 삭제"
            );
            Require(
                !AddressableAssetSettingsDefaultObject.Settings.groups.Where(group => group != null).SelectMany(group => group.entries).Any(entry => entry.address == "content/scenario/workplace-tools"),
                "기존 주소 제거"
            );
            Debug.Log("MIXED SCENARIO CHECKS PASSED (6 scenarios / 8 tools in each family)");
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
