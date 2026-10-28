using System;
using System.Collections.Generic;

namespace SmartTraining
{
    // 목표 진행은 공동 상태이고 선택 공구는 참가자별 상태입니다. UI·네트워크 없이 규칙을 검사합니다.
    public sealed class ScenarioProgress
    {
        private readonly ScenarioDefinition definition;
        private readonly Dictionary<ulong, string> selectedTools = new();

        public int DefinitionStepCount => definition.Steps.Count;

        public int CompletedSteps { get; private set; }

        public int Revision { get; private set; }

        public int RunRevision { get; private set; }

        public bool IsComplete => CompletedSteps >= definition.Steps.Count;


        public ScenarioProgress(ScenarioDefinition definition)
        {
            if (definition == null || !definition.TryValidate(out _))
            {
                throw new ArgumentException("올바른 시나리오 단계와 공구 목록이 필요합니다.", nameof(definition));
            }

            this.definition = definition;
        }


        public string GetSelectedTool(ulong participantId)
        {
            return selectedTools.TryGetValue(participantId, out var tool) ? tool : "";
        }


        // 다른 사람의 선택에는 영향을 주지 않습니다. 공구 선택으로 공동 단계 버전도 변경하지 않습니다.
        public bool TrySelectTool(ulong participantId, string toolId, int expectedRunRevision, out string message)
        {
            if (expectedRunRevision != RunRevision)
            {
                message = "실습이 초기화됐습니다. 공구를 다시 선택해주세요.";
                return false;
            }

            if (IsComplete || toolId == null || (toolId.Length > 0 && !definition.HasTool(toolId)))
            {
                message = "현재 실습에서 선택할 수 없는 공구입니다.";
                return false;
            }

            if (toolId.Length == 0)
            {
                selectedTools.Remove(participantId);
            }
            else
            {
                selectedTools[participantId] = toolId;
            }

            message = toolId.Length > 0 ? "내 공구를 선택했습니다. 작업 대상을 클릭해주세요." : "공구 선택을 해제했습니다.";
            return true;
        }


        // 호출한 참가자 자신의 공구만 확인합니다. 클라이언트가 정답 공구를 요청에 붙여 보낼 수 없습니다.
        public bool TryInteract(ulong participantId, string objectId, int expectedRevision, out string message)
        {
            if (expectedRevision != Revision)
            {
                message = "단계가 변경됐습니다. 현재 안내를 확인해주세요.";
                return false;
            }

            if (IsComplete)
            {
                message = "모든 실습을 완료했습니다.";
                return false;
            }

            var step = definition.Steps[CompletedSteps];
            // 둘 다 틀렸다면 먼저 클릭한 대상부터 바로잡도록 안내합니다.
            if (objectId != step.TargetId)
            {
                message = "오브젝트를 잘못 선택했습니다. 현재 목표의 작업 대상을 선택해주세요.";
                return false;
            }

            // 공구가 없는 목표에서는 개인 선택을 검사하지 않습니다. 이전에 고른 공구도 그대로 유지합니다.
            if (step.RequiresTool)
            {
                string tool = GetSelectedTool(participantId);
                if (tool.Length == 0)
                {
                    message = "공구를 먼저 선택해주세요. 기능 메뉴에서 내 공구를 선택할 수 있습니다.";
                    return false;
                }

                if (tool != step.ToolId)
                {
                    message = "공구를 잘못 선택했습니다. 기능 메뉴에서 올바른 공구를 선택해주세요.";
                    return false;
                }
            }

            CompletedSteps++;
            Revision++;
            message = IsComplete ? "모든 실습을 완료했습니다." : "단계를 완료했습니다.";
            return true;
        }


        public bool TryReset(int expectedRevision)
        {
            if (expectedRevision != Revision)
            {
                return false;
            }

            CompletedSteps = 0;
            selectedTools.Clear();
            Revision++;
            RunRevision++;
            return true;
        }


        public void RemoveParticipant(ulong participantId)
        {
            selectedTools.Remove(participantId);
        }
    }
}
