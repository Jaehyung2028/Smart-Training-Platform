using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SmartTraining.Editor
{
    // 서버 시계·중복 요청·초기화와 화면 자세 계산을 함께 확인합니다.
    public static class ScenarioWorkChecks
    {
        private static int checks;

        [MenuItem("Smart Training/Content/Check Scenario Work")]
        public static void Run()
        {
            checks = 0;
            ScenarioContentBuilder.Prepare();
            var original = AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(ScenarioContentBuilder.DefinitionPath);
            CheckToolActions(original);
            // 기본 풀기·체결·확인 규칙 검사는 별도 3단계 복제본으로 유지합니다.
            var definition = UnityEngine.Object.Instantiate(original);
            var fixture = new SerializedObject(definition);
            var fixtureSteps = fixture.FindProperty("steps");
            fixtureSteps.MoveArrayElement(fixtureSteps.arraySize - 1, 2);
            fixtureSteps.arraySize = 3;
            fixtureSteps.GetArrayElementAtIndex(2).FindPropertyRelative("targetId").stringValue = "bolt-a";
            fixture.ApplyModifiedPropertiesWithoutUndo();
            var progress = new ScenarioProgress(definition);
            Check(!progress.TryInteract(1, "bolt-a", 0, 10, out _), "공구 없는 풀기 거절");
            Check(progress.TrySelectTool(1, "spanner", 0, out _), "개인 공구 선택");
            Check(!progress.TryInteract(1, "bolt-b", 0, 10, out _), "잘못된 대상 거절");
            Check(!progress.TryInteract(1, "bolt-a", 0, double.NaN, out _), "잘못된 시각 거절");
            Check(progress.TryInteract(1, "bolt-a", 0, 10, out _), "풀기 시작");
            Check(
                progress.IsWorking && progress.CompletedSteps == 0 && progress.Revision == 1,
                "시작만으로 완료되지 않음"
            );
            Check(!progress.TryInteract(2, "bolt-a", 1, 10.5, out _), "작업 중 중복 요청 거절");
            Check(
                !progress.TryCompleteWork(11.49) && !progress.TryCompleteWork(double.PositiveInfinity),
                "시간 이전과 비정상 시각 완료 거절"
            );
            Check(
                Mathf.Approximately(ScenarioObjectPose.Evaluate(definition.Steps, "bolt-a", 0, true, .5f), .5f),
                "중간 회전·이동 자세"
            );
            Check(
                ScenarioObjectPose.Evaluate(definition.Steps, "bolt-b", 0, true, .5f) == 0,
                "다른 대상은 움직이지 않음"
            );
            progress.RemoveParticipant(1);
            Check(
                progress.GetSelectedTool(1) == "" && progress.TryCompleteWork(11.5),
                "시작 참가자 퇴장 후 공동 작업 완료"
            );
            Check(
                !progress.TryCompleteWork(12) && progress.CompletedSteps == 1 && progress.Revision == 2,
                "한 번만 완료"
            );
            Check(
                ScenarioObjectPose.Evaluate(definition.Steps, "bolt-a", 1, false, 0) == 1,
                "분리 자세 유지"
            );
            Check(!progress.TryInteract(2, "bolt-a", 2, 12, out _), "다른 참가자의 공구를 공유하지 않음");
            Check(
                progress.TrySelectTool(2, "spanner", 0, out _) && progress.TryInteract(2, "bolt-a", 2, 12, out _),
                "다시 체결 시작"
            );
            Check(
                Mathf.Approximately(ScenarioObjectPose.Evaluate(definition.Steps, "bolt-a", 1, true, .5f), .5f),
                "역방향 체결 자세"
            );
            Check(progress.TryCompleteWork(13.5) && progress.CompletedSteps == 2, "체결 완료");
            Check(
                ScenarioObjectPose.Evaluate(definition.Steps, "bolt-a", 2, false, 0) == 0,
                "원래 자세 복구"
            );
            Check(
                progress.TryInteract(3, "bolt-a", 4, 14, out _) && progress.IsComplete,
                "공구 없는 확인 단계"
            );
            Check(progress.TryReset(5) && progress.RunRevision == 1, "실습 초기화");
            Check(
                progress.TrySelectTool(2, "spanner", 1, out _) && progress.TryInteract(2, "bolt-a", 6, 20, out _),
                "다음 작업 시작"
            );
            Check(
                progress.TryReset(7) && !progress.IsWorking && !progress.TryCompleteWork(100),
                "작업 중 초기화하면 늦은 완료 취소"
            );
            Check(
                !progress.TrySelectTool(2, "spanner", 1, out _) && !progress.TryInteract(2, "bolt-a", 7, 101, out _),
                "이전 실행 요청 거절"
            );
            Check(progress.CompletedSteps == 0 && progress.GetSelectedTool(2) == "", "진행과 공구 초기화");
            CheckWorkMotion(definition);
            CheckCancellation(definition);
            ScenarioDataChecks.Run();
            ScenarioPracticeChecks.Run();
            ToolFreeScenarioChecks.Run();
            UnityEngine.Object.DestroyImmediate(definition);
            Debug.Log($"SCENARIO WORK CHECKS PASSED ({checks})");
        }


        private static void CheckToolActions(ScenarioDefinition definition)
        {
            Check(
                definition.Steps.Count == 6 &&
                definition.Steps[2].ToolId == "screwdriver" &&
                definition.Steps[4].Action == ScenarioAction.Strike &&
                !definition.Steps[5].RequiresTool,
                "공구별 6단계 원본"
            );
            Check(
                definition.GetTool("screwdriver").AnimateWork && definition.GetTool("hammer").AnimateWork,
                "드라이버·망치 작업 설정"
            );
            Check(
                ScenarioWorkMotion.TargetProgress(ScenarioAction.Strike, .3f) == 0,
                "들어 올리는 중 핀 이동 없음"
            );
            Check(
                Mathf.Approximately(ScenarioWorkMotion.TargetProgress(ScenarioAction.Strike, .4f), 1f / 3),
                "첫 타격에서 1/3 이동"
            );
            Check(
                Mathf.Approximately(ScenarioWorkMotion.TargetProgress(ScenarioAction.Strike, .6f), 2f / 3),
                "두 번째 타격에서 2/3 이동"
            );
            Check(
                ScenarioWorkMotion.TargetProgress(ScenarioAction.Strike, .8f) == 1 && ScenarioWorkMotion.HammerLift(.8f) == 0,
                "세 번째 타격 후 복귀"
            );
            Check(
                ScenarioWorkMotion.HammerLift(.3f) > .99f && ScenarioWorkMotion.HammerLift(.4f) < .001f,
                "망치 들기와 타격 시점 일치"
            );
            var progress = new ScenarioProgress(definition);
            double time = 10;
            for (int index = 0; index < 5; index++)
            {
                var step = definition.Steps[index];
                Check(
                    progress.TrySelectTool(1, step.ToolId, 0, out _) &&
                    progress.TryInteract(1, step.TargetId, progress.Revision, time, out _),
                    "각 공구의 올바른 작업 시작"
                );
                Check(!progress.TryCompleteWork(time + step.DurationSeconds - .01), "완료 시간 이전 단계 유지");
                if (step.Action == ScenarioAction.Strike)
                {
                    int revision = progress.Revision;
                    Check(
                        progress.TryCancelWork(1, revision, time + 1.2, out _) && progress.CompletedSteps == 4,
                        "타격 취소는 완료 수 유지"
                    );
                    Check(
                        ScenarioObjectPose.Evaluate(definition.Steps, "pin-a", 4, false, 0) == 0,
                        "타격 취소 시 핀 원위치"
                    );
                    Check(progress.TryInteract(1, step.TargetId, progress.Revision, time, out _), "타격 재시작");
                }

                Check(progress.TryCompleteWork(time + step.DurationSeconds), "공구 작업 완료");
                time += 5;
            }

            Check(
                progress.TryInteract(2, "pin-a", progress.Revision, time, out _) && progress.IsComplete,
                "다른 참가자가 공구 없이 최종 확인"
            );
            Check(
                ScenarioObjectPose.Evaluate(definition.Steps, "pin-a", 6, false, 0) == 1,
                "완료한 핀 상태 유지"
            );
        }


        private static void CheckWorkMotion(ScenarioDefinition definition)
        {
            Check(
                ScenarioWorkMotion.TurnProgress(.1f) == 0 && ScenarioWorkMotion.TurnProgress(.9f) == 1,
                "접근·복귀 구간에는 볼트 회전 정지"
            );
            Check(
                ScenarioWorkMotion.ContactWeight(0) == 0 &&
                ScenarioWorkMotion.ContactWeight(.5f) == 1 &&
                ScenarioWorkMotion.ContactWeight(1) == 0,
                "접근·접촉·복귀 가중치"
            );
            Check(
                ScenarioObjectPose.Evaluate(definition.Steps, "bolt-a", 0, true, .1f) == 0,
                "손 접근 전 볼트가 움직이지 않음"
            );
            Check(
                ScenarioObjectPose.Evaluate(definition.Steps, "bolt-a", 1, true, .9f) == 0,
                "손 복귀 전 체결 자세 완성"
            );
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(ScenarioToolPrefabBuilder.RigPath).GetComponent<HandheldItemRig>();
            var parent = new GameObject("Contact Pose Check").transform;
            var view = new HeldItemView(rig, "Contact Pose Rig");
            try
            {
                view.Attach(parent, new Vector3(.3f, -.27f, .55f), Quaternion.Euler(-15, -10, 10));
                var tool = definition.GetTool("spanner");
                Check(tool.AnimateWork && tool.HeldPrefab != null, "스패너 작업 프리팹 설정");
                view.SetItem(tool.HeldPrefab);
                Vector3 target = new Vector3(2, 1.2f, 4);
                Quaternion targetRotation = Quaternion.Euler(90, 38, 0);
                view.GetContactPose(tool.WorkContactPoint, target, targetRotation, out var position, out var rotation);
                view.SetPose(position, rotation);
                var instance = parent.GetComponentInChildren<HandheldItemRig>().Grip.GetChild(0);
                Check(
                    Vector3.Distance(instance.TransformPoint(tool.WorkContactPoint), target) < .0001f,
                    "실제 모델 접촉점이 작업점과 일치"
                );
                Check(Quaternion.Angle(instance.rotation, targetRotation) < .01f, "실제 모델 작업 방향 일치");
                Check(instance.GetComponentsInChildren<Collider>().All(collider => !collider.enabled), "공구가 작업 클릭을 가로채지 않음");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent.gameObject);
            }
        }


        private static void CheckCancellation(ScenarioDefinition definition)
        {
            var progress = new ScenarioProgress(definition);
            Check(
                progress.TrySelectTool(1, "spanner", 0, out _) && progress.TryInteract(1, "bolt-a", 0, 10, out _),
                "취소 확인 작업 시작"
            );
            Check(progress.WorkClientId == 1, "서버가 작업자 연결 ID 보관");
            Check(
                !progress.TrySelectTool(1, "hammer", 0, out _) && progress.GetSelectedTool(1) == "spanner",
                "작업자는 작업 도중 공구 변경 불가"
            );
            Check(
                progress.TrySelectTool(2, "hammer", 0, out _) && progress.GetSelectedTool(2) == "hammer",
                "다른 참가자는 공구 선택 가능"
            );
            Check(!progress.TryCancelWork(2, 1, 10.2, out _) && progress.IsWorking, "다른 참가자는 취소 불가");
            Check(!progress.TryCancelWork(1, 0, 10.2, out _), "이전 버전 취소 거절");
            Check(!progress.TryCancelWork(1, 1, double.NaN, out _), "비정상 시각 취소 거절");
            Check(progress.TryCancelWork(1, 1, 10.5, out _), "작업자 본인 취소");
            Check(
                !progress.IsWorking &&
                progress.WorkClientId == 0 &&
                progress.WorkDuration == 0 &&
                progress.CompletedSteps == 0 &&
                progress.Revision == 2,
                "작업만 해제하고 단계 유지"
            );
            Check(
                progress.RunRevision == 0 && progress.GetSelectedTool(1) == "spanner",
                "취소해도 실행 번호와 개인 공구 유지"
            );
            Check(!progress.TryCompleteWork(100), "취소한 작업의 늦은 완료 없음");
            Check(
                ScenarioObjectPose.Evaluate(definition.Steps, "bolt-a", 0, false, 0) == 0,
                "풀기 취소는 원래 자세"
            );
            Check(
                progress.TryInteract(1, "bolt-a", 2, 20, out _) && !progress.TryCancelWork(1, 1, 20.2, out _),
                "이전 작업의 취소가 재시작 작업에 영향 없음"
            );
            Check(
                !progress.TryCancelWork(1, 3, 21.5, out _) && progress.TryCompleteWork(21.5),
                "완료 시각에 도착한 취소보다 완료 우선"
            );
            Check(
                !progress.TryCancelWork(1, 4, 21.6, out _) && progress.CompletedSteps == 1,
                "완료된 작업 취소 불가"
            );
            Check(
                progress.TrySelectTool(2, "spanner", 0, out _) && progress.TryInteract(2, "bolt-a", 4, 30, out _),
                "다른 참가자가 체결 작업 시작"
            );
            Check(!progress.TryCancelWork(1, 5, 30.2, out _), "이전 작업자는 새 작업 취소 불가");
            Check(
                progress.TryCancelWork(2, 5, 30.5, out _) && progress.CompletedSteps == 1,
                "체결 취소는 이전 완료 수 유지"
            );
            Check(
                ScenarioObjectPose.Evaluate(definition.Steps, "bolt-a", 1, false, 0) == 1,
                "체결 취소는 분리된 자세로 복구"
            );
            Check(
                progress.TryInteract(2, "bolt-a", 6, 40, out _) && progress.TryReset(7),
                "작업 중 전체 초기화"
            );
            Check(
                !progress.IsWorking && progress.WorkClientId == 0 && !progress.TryCancelWork(2, 7, 40.5, out _),
                "초기화가 작업자와 이전 취소 요청 해제"
            );
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
