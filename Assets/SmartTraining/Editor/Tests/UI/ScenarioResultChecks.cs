using System;
using UnityEditor;
using UnityEngine;

namespace SmartTraining.Editor
{
    public static class ScenarioResultChecks
    {
        private static int checks;

        [MenuItem("Smart Training/Content/Check Scenario Results")]
        public static void Run()
        {
            checks = 0;
            var results = new ScenarioRunResults(2);
            Check(!results.RecordError(0, ScenarioInteractionError.WrongTool), "시작 전 오류 집계 없음");
            results.Begin(10);
            Check(results.RecordError(0, ScenarioInteractionError.WrongTool), "공구 오류 집계");
            Check(results.RecordError(0, ScenarioInteractionError.WrongTarget), "대상 오류 집계");
            Check(
                !results.RecordError(1, ScenarioInteractionError.WrongTool) &&
                !results.RecordError(0, ScenarioInteractionError.None),
                "다른 단계·일반 거절은 집계 제외"
            );
            Check(!results.CompleteStep(0, "방장", 12) && results.Steps.Count == 0, "작업 시작·취소는 완료 행 없음");
            Check(results.CompleteStep(1, "방장", 15), "첫 단계 완료");
            var first = results.Steps[0];
            Check(
                first.Index == 0 &&
                first.ActorName == "방장" &&
                first.ToolErrors == 1 &&
                first.TargetErrors == 1 &&
                first.DurationSeconds == 5,
                "단계 수행자·오류·소요 시간"
            );
            Check(
                !results.CompleteStep(1, "중복", 16) && !results.CompleteStep(2, "역행", 14),
                "중복 완료·역행 시각 거절"
            );
            Check(
                results.RecordError(1, ScenarioInteractionError.WrongTool) && results.CompleteStep(2, "참가자", 20),
                "다른 참가자 다음 단계 완료"
            );
            Check(
                results.IsComplete && results.ToolErrors == 2 && results.TargetErrors == 1,
                "실습 전체 오류 합계"
            );
            Check(
                results.Steps[1].ActorName == "참가자" && results.Steps[1].ToolErrors == 1 && results.Steps[1].TargetErrors == 0,
                "이전 단계 오류가 다음 단계에 중복되지 않음"
            );
            Check(
                results.Elapsed(20) == 10 && results.Elapsed(200) == 10 && results.EndedAt == 20,
                "완료 후 소요 시간 고정"
            );
            Check(
                !results.RecordError(1, ScenarioInteractionError.WrongTarget) && !results.CompleteStep(3, "추가", 30),
                "완료 후 결과 변경 없음"
            );
            results.Begin(50);
            Check(
                results.Steps.Count == 0 &&
                results.ToolErrors == 0 &&
                results.TargetErrors == 0 &&
                results.EndedAt == 0 &&
                results.Elapsed(55) == 5,
                "재실습 시 시간·수행자·오류 초기화"
            );
            Check(
                !results.RecordError(1, ScenarioInteractionError.WrongTool) && results.CompleteStep(1, "새 참가자", 60),
                "재실습 첫 단계부터 기록"
            );
            CheckClassification();
            ScenarioWorkChecks.Run();
            Debug.Log($"SCENARIO RESULT CHECKS PASSED ({checks})");
        }


        private static void CheckClassification()
        {
            ScenarioContentBuilder.Prepare();
            var definition = AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(ScenarioContentBuilder.DefinitionPath);
            var progress = new ScenarioProgress(definition);
            Check(
                !progress.TryInteract(1, "bolt-b", 0, 1, out _, out var error) &&
                error == ScenarioInteractionError.WrongTarget,
                "대상·공구가 모두 틀리면 대상 오류만 분류"
            );
            Check(
                !progress.TryInteract(1, "bolt-a", 0, 1, out _, out error) && error == ScenarioInteractionError.WrongTool,
                "공구 미선택 분류"
            );
            progress.TrySelectTool(1, "hammer", 0, out _);
            Check(
                !progress.TryInteract(1, "bolt-a", 0, 1, out _, out error) && error == ScenarioInteractionError.WrongTool,
                "공구 오선택 분류"
            );
            Check(
                !progress.TryInteract(1, "bolt-b", 99, 1, out _, out error) && error == ScenarioInteractionError.None,
                "오래된 요청은 오답 아님"
            );
            progress.TrySelectTool(1, "spanner", 0, out _);
            Check(
                progress.TryInteract(1, "bolt-a", 0, 1, out _, out error) && error == ScenarioInteractionError.None,
                "정상 작업은 오답 아님"
            );
            Check(
                !progress.TryInteract(2, "bolt-b", 1, 1.1, out _, out error) && error == ScenarioInteractionError.None,
                "작업 중 중복 요청은 오답 아님"
            );
        }


        private static void Check(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }

            checks++;
        }
    }
}
