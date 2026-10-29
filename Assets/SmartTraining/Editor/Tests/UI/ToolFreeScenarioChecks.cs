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
                definition.TryValidate(out _) &&
                definition.Steps.Count == 6 &&
                definition.Steps[0].RequiresTool &&
                definition.Steps[1].RequiresTool &&
                !definition.Steps[5].RequiresTool,
                "기존 예시에 공구 필요·불필요 단계를 혼합"
            );
            Check(prefab.GetComponent<ScenarioContent>().TryInitialize(out _), "작업 대상 연결");
            var progress = new ScenarioProgress(definition);
            Check(progress.TrySelectTool(7, "spanner", 0, out _), "개인 공구 선택");
            Check(progress.TryInteract(7, "bolt-a", 0, out _), "첫 볼트 작업");
            Check(progress.IsWorking && progress.TryCompleteWork(1.5), "풀기 작업이 끝난 뒤 단계 완료");
            Check(
                progress.TryInteract(7, "bolt-a", 2, 2, out _) && !progress.IsComplete,
                "다시 체결 작업 시작"
            );
            Check(progress.TryCompleteWork(3.5), "체결 완료 후 확인 목표가 남음");
            for (int index = 2; index < 5; index++)
            {
                var step = definition.Steps[index];
                double start = 10 + index * 5;
                Check(
                    progress.TrySelectTool(7, step.ToolId, 0, out _) &&
                    progress.TryInteract(7, step.TargetId, progress.Revision, start, out _) &&
                    progress.TryCompleteWork(start + step.DurationSeconds),
                    "다른 공구 단계 완료"
                );
            }

            Check(!progress.TryInteract(8, "bolt-b", progress.Revision, out _), "공구 없는 목표도 대상 검사");
            Check(
                progress.GetSelectedTool(8) == "" &&
                progress.TryInteract(8, "pin-a", progress.Revision, out _) &&
                progress.IsComplete,
                "공구를 고르지 않은 다른 참가자가 확인 완료"
            );
            var ui = new GameObject("Scenario Card Checks", typeof(RectTransform), typeof(Canvas));
            try
            {
                var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
                    labels.First(item => item.name == "Scenario Progress").text == "0 / 6 완료",
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
