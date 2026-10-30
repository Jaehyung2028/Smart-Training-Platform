using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SmartTraining.Editor
{
    public static class ToolFreeScenarioChecks
    {
        private static int checks;

        [MenuItem("Smart Training/Content/Check Tool Free Scenario")]
        public static void Run()
        {
            checks = 0;
            ScenarioContentBuilder.Prepare();
            var definition = AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(ScenarioContentBuilder.DefinitionPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ScenarioContentBuilder.PrefabPath);
            Check(
                definition.TryValidate(out _) && definition.Steps.Any(step => !step.RequiresTool),
                "공구·무공구 단계 혼합"
            );
            Check(prefab.GetComponent<ScenarioContent>().TryInitialize(out _), "작업 대상 연결");
            var progress = new ScenarioProgress(definition);
            double time = 0;
            foreach (var step in definition.Steps)
            {
                // 점검 단계는 공구를 선택하지 않은 다른 참가자도 수행할 수 있습니다.
                ulong actor = step.RequiresTool ? 7ul : 8ul;
                if (step.RequiresTool)
                {
                    Check(progress.TrySelectTool(actor, step.ToolId, 0, out _), "개인 공구 선택");
                }
                else
                {
                    Check(progress.TrySelectTool(actor, definition.Tools[0].Id, 0, out _), "무공구 단계에 공구를 든 상태 준비");
                    int revision = progress.Revision;
                    int completed = progress.CompletedSteps;
                    Check(!progress.TryInteract(actor, step.TargetId, revision, time, out var rejected, out var error) &&
                        error == ScenarioInteractionError.WrongTool && rejected.Contains("선택을 해제") &&
                        progress.Revision == revision && progress.CompletedSteps == completed,
                        "공구를 들고 무공구 단계 완료 불가·진행 상태 유지");
                    Check(progress.TrySelectTool(actor, "", 0, out _), "개인 공구 선택 해제");
                    Check(progress.GetSelectedTool(actor) == "", "점검 담당은 공구 미선택");
                }

                Check(
                    !progress.TryInteract(actor, "missing", progress.Revision, time, out _),
                    "무공구 단계도 대상 확인"
                );
                Check(
                    progress.TryInteract(actor, step.TargetId, progress.Revision, time, out _),
                    "현재 단계 수행"
                );
                if (progress.IsWorking)
                {
                    Check(progress.TryCompleteWork(time + step.DurationSeconds), "작업 시간 이후 완료");
                }

                time += step.DurationSeconds + 1;
            }

            Check(progress.IsComplete, "공구·점검 전체 완료");
            var ui = new GameObject("Scenario Card Checks", typeof(RectTransform), typeof(Canvas));
            try
            {
                var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                UiPrefab.Instantiate("ScenarioControlPanel", ui.transform);
                var panelType = typeof(ScenarioContent).Assembly.GetType("SmartTraining.ScenarioControlPanel");
                var panel = Activator.CreateInstance(panelType, new object[] { ui.transform, font });
                panelType.GetMethod("Refresh").Invoke(panel, new object[] { prefab.GetComponent<ScenarioContent>(), true });
                var labels = ui.GetComponentsInChildren<Text>(true);
                Check(
                    labels.First(item => item.name == "Scenario Title").text == definition.Title,
                    "카드 제목 독립 표시"
                );
                Check(
                    labels.Any(item => item.name == "Scenario Current Title") &&
                    labels.Any(item => item.name == "Scenario Instruction"),
                    "현재 목표와 설명을 분리"
                );
                Check(
                    labels.First(item => item.name == "Scenario Progress").text == "0 / " + definition.Steps.Count + " 완료",
                    "카드 진행 개수"
                );
                Check(ui.GetComponentInChildren<ScrollRect>(true) != null, "긴 단계 목록 스크롤 지원");
                Check(labels.All(item => item.font != null && !item.raycastTarget), "텍스트 글꼴과 입력 설정");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ui);
            }

            Debug.Log($"TOOL FREE SCENARIO CHECKS PASSED ({checks})");
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
