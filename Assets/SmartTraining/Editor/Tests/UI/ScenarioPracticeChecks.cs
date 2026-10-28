using System;
using UnityEditor;
using UnityEngine;

namespace SmartTraining.Editor
{
    public static class ScenarioPracticeChecks
    {
        private static int checks;

        [MenuItem("Smart Training/Content/Check Scenario Practice")]
        public static void Run()
        {
            checks = 0;
            ScenarioDataChecks.Run();
            var definition = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(ScenarioContentBuilder.DefinitionPath));
            var fixture = new SerializedObject(definition);
            fixture.FindProperty("steps").arraySize = 2;
            fixture.ApplyModifiedPropertiesWithoutUndo();
            var progress = new ScenarioProgress(definition);
            const ulong first = 7, second = 8;
            Check(
                !progress.TryInteract(first, "bolt-a", 0, out var missingTool) && missingTool.Contains("공구를 먼저 선택"),
                "공구 미선택 안내"
            );
            Check(progress.TrySelectTool(first, "spanner", 0, out _), "첫 사용자의 메뉴 공구 선택");
            Check(progress.CompletedSteps == 0 && progress.Revision == 0, "선택으로 공동 단계가 바뀌지 않음");
            Check(progress.GetSelectedTool(second) == "", "다른 사용자는 공구가 없음");
            Check(!progress.TryInteract(second, "bolt-a", 0, out _), "다른 사용자의 공구로 작업할 수 없음");
            Check(progress.TrySelectTool(second, "hammer", 0, out _), "오답 공구도 메뉴에서 선택 가능");
            Check(
                !progress.TryInteract(second, "bolt-a", 0, out var wrongTool) && wrongTool.Contains("공구를 잘못 선택"),
                "잘못된 공구 안내"
            );
            Check(
                !progress.TryInteract(first, "bolt-b", 0, out var wrongObject) && wrongObject.Contains("오브젝트를 잘못 선택"),
                "잘못된 오브젝트 안내"
            );
            Check(
                !progress.TryInteract(second, "bolt-b", 0, out var bothWrong) && bothWrong == wrongObject,
                "공구와 대상이 모두 틀리면 대상부터 안내"
            );
            Check(progress.GetSelectedTool(first) == "spanner", "다른 사용자 선택이 내 공구를 바꾸지 않음");
            Check(
                !progress.TrySelectTool(first, "unknown", 0, out _) && progress.GetSelectedTool(first) == "spanner",
                "등록되지 않은 메뉴 공구 거절"
            );
            Check(!progress.TryInteract(first, "spanner", 0, out _), "공구 ID를 작업 대상으로 사용할 수 없음");
            Check(
                progress.TryInteract(first, "bolt-a", 0, out _) && progress.CompletedSteps == 1,
                "자신이 선택한 정답 공구로 목표 완료"
            );
            Check(!progress.TryInteract(second, "bolt-b", 1, out _), "공동 진행 이후에도 각자 공구 검사");
            Check(
                progress.TrySelectTool(second, "spanner", 0, out _) && progress.Revision == 1,
                "단계 진행과 관계없이 개인 공구 변경"
            );
            Check(!progress.TryInteract(first, "bolt-a", 0, out _), "이전 단계 중복 요청 거절");
            Check(
                progress.TryInteract(second, "bolt-b", 1, out _) && progress.IsComplete,
                "다른 사용자도 자신의 공구로 다음 목표 완료"
            );
            Check(!progress.TryReset(0) && progress.IsComplete, "오래된 초기화 거절");
            Check(
                progress.TryReset(2) && progress.RunRevision == 1 && progress.Revision == 3,
                "초기화 번호와 공동 버전 변경"
            );
            Check(
                progress.GetSelectedTool(first) == "" && progress.GetSelectedTool(second) == "",
                "초기화는 모든 개인 공구를 해제"
            );
            Check(!progress.TrySelectTool(first, "spanner", 0, out _), "초기화 이전의 늦은 선택 요청 거절");
            Check(progress.TrySelectTool(first, "spanner", 1, out _), "새 실습의 공구 선택");
            Check(progress.TrySelectTool(second, "hammer", 1, out _), "각자 다른 공구 유지");
            progress.RemoveParticipant(first);
            Check(
                progress.GetSelectedTool(first) == "" && progress.GetSelectedTool(second) == "hammer",
                "접속 종료는 해당 사용자 선택만 제거"
            );
            Check(
                progress.TrySelectTool(second, "", 1, out _) && progress.GetSelectedTool(second) == "",
                "메뉴에서 선택 해제"
            );
            Check(!progress.TryInteract(second, "bolt-a", 3, out _), "선택 해제 이후 작업 거절");
            var singleGoal = UnityEngine.Object.Instantiate(definition);
            try
            {
                var data = new SerializedObject(singleGoal);
                data.FindProperty("steps").arraySize = 1;
                data.ApplyModifiedPropertiesWithoutUndo();
                var single = new ScenarioProgress(singleGoal);
                Check(
                    single.TrySelectTool(first, "spanner", 0, out _) && !single.IsComplete,
                    "단일 목표의 개인 공구 선택은 미완료"
                );
                Check(!single.TryInteract(second, "bolt-a", 0, out _), "단일 목표도 타인의 공구로 완료 불가");
                Check(
                    single.TryInteract(first, "bolt-a", 0, out _) && single.IsComplete,
                    "단일 목표는 자기 공구로 대상 클릭 시 전체 완료"
                );
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(singleGoal);
            }

            CheckOptionalTools(definition);
            var ui = new GameObject("Scenario Menu Checks", typeof(RectTransform), typeof(Canvas));
            try
            {
                var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                var menu = new ContentActionMenu(ui.transform, font);
                var section = menu.AddSection("Scenario Tool Menu");
                var menuType = typeof(ScenarioContent).Assembly.GetType("SmartTraining.ScenarioToolMenu");
                var tools = Activator.CreateInstance(menuType, new object[] { section, font });
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ScenarioContentBuilder.PrefabPath);
                menuType.GetMethod("Refresh").Invoke(tools, new object[] { prefab.GetComponent<ScenarioContent>(), true, false });
                Check(section.parent.name == "Content Action List", "공구 선택은 기존 기능 메뉴 내부에 배치");
                var buttons = section.GetComponentsInChildren<UnityEngine.UI.Button>(true);
                Check(buttons.Length == definition.Tools.Count + 1, "공구별 버튼과 선택 해제 버튼");
                foreach (var button in buttons)
                {
                    var text = button.GetComponentInChildren<UnityEngine.UI.Text>();
                    Check(
                        text != null && !string.IsNullOrWhiteSpace(text.text) && text.color.a > 0,
                        "모든 공구 버튼에 보이는 텍스트"
                    );
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ui);
            }

            UnityEngine.Object.DestroyImmediate(definition);
            Debug.Log($"SCENARIO PRACTICE CHECKS PASSED ({checks})");
        }


        private static void CheckOptionalTools(ScenarioDefinition source)
        {
            var copy = UnityEngine.Object.Instantiate(source);
            try
            {
                var data = new SerializedObject(copy);
                data.FindProperty("steps").GetArrayElementAtIndex(0).FindPropertyRelative("toolId").stringValue = "";
                data.ApplyModifiedPropertiesWithoutUndo();
                Check(copy.TryValidate(out _) && !copy.Steps[0].RequiresTool, "빈 ToolId는 공구 불필요");
                var mixed = new ScenarioProgress(copy);
                Check(
                    !mixed.TryInteract(7, "bolt-b", 0, out var targetError) && targetError.Contains("오브젝트"),
                    "공구 불필요 목표도 작업 대상은 검사"
                );
                Check(
                    mixed.TryInteract(7, "bolt-a", 0, out _) && mixed.CompletedSteps == 1,
                    "공구 미선택으로 공구 없는 목표 완료"
                );
                Check(
                    !mixed.TryInteract(7, "bolt-b", 1, out var missingTool) && missingTool.Contains("공구를 먼저"),
                    "다음 공구 필요 목표는 미선택 검사 유지"
                );
                mixed.TrySelectTool(7, "hammer", 0, out _);
                Check(
                    !mixed.TryInteract(7, "bolt-b", 1, out var wrongTool) && wrongTool.Contains("공구를 잘못"),
                    "공구 필요 목표는 오답 공구 검사 유지"
                );
                mixed.TrySelectTool(7, "spanner", 0, out _);
                Check(
                    mixed.TryInteract(7, "bolt-b", 1, out _) && mixed.IsComplete,
                    "공구 필요·불필요 목표를 한 시나리오에서 혼합"
                );
                var selected = new ScenarioProgress(copy);
                selected.TrySelectTool(8, "hammer", 0, out _);
                Check(
                    selected.TryInteract(8, "bolt-a", 0, out _) && selected.GetSelectedTool(8) == "hammer",
                    "공구 없는 목표는 기존 선택을 무시하고 유지"
                );
                data.Update();
                data.FindProperty("steps").GetArrayElementAtIndex(1).FindPropertyRelative("toolId").stringValue = "   ";
                data.FindProperty("tools").arraySize = 0;
                data.ApplyModifiedPropertiesWithoutUndo();
                Check(copy.TryValidate(out _) && !copy.Steps[1].RequiresTool, "전부 공구 불필요하면 공구 목록도 생략 가능");
                var noTools = new ScenarioProgress(copy);
                Check(
                    noTools.TryInteract(9, "bolt-a", 0, out _) && noTools.TryInteract(9, "bolt-b", 1, out _) && noTools.IsComplete,
                    "공구 목록 없는 시나리오를 끝까지 완료"
                );
                data.Update();
                data.FindProperty("steps").GetArrayElementAtIndex(0).FindPropertyRelative("toolId").stringValue = "spanner";
                data.ApplyModifiedPropertiesWithoutUndo();
                Check(!copy.TryValidate(out _), "필요한 공구가 목록에 없으면 데이터 거절");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(copy);
            }
        }


        private static void Check(bool condition, string name)
        {
            if (!condition)
            {
                throw new InvalidOperationException(name);
            }

            checks++;
        }
    }
}
