using System;
using System.Collections.Generic;

namespace SmartTraining
{
    // 목표 진행은 공동 상태이고 선택 공구는 참가자별 상태입니다. UI·네트워크 없이 규칙을 검사합니다.
    public sealed class ScenarioProgress
    {
        private readonly ScenarioDefinition definition;
        private readonly Dictionary<ulong, string> selectedTools = new();
        private readonly Dictionary<ScenarioRole, ulong> roles = new();

        public ScenarioRole GetRoles(ulong participantId)
        {
            var result = ScenarioRole.None;
            foreach (var assignment in roles)
            {
                if (assignment.Value == participantId)
                {
                    result |= assignment.Key;
                }
            }

            return result;
        }


        // 역할당 한 명을 유지하며, 비어 있는 역할만 추가로 겸임할 수 있습니다.
        public bool TrySelectRole(ulong participantId, ScenarioRole role, int expectedRunRevision, out string message)
        {
            if (expectedRunRevision != RunRevision ||
                IsComplete ||
                (IsWorking &&
                WorkClientId == participantId) ||
                role == ScenarioRole.None ||
                !Enum.IsDefined(typeof(ScenarioRole), role) ||
                (definition.Roles & role) != role)
            {
                message = "현재 역할을 추가할 수 없습니다.";
                return false;
            }

            if (roles.TryGetValue(role, out var owner))
            {
                message = owner == participantId ? "이미 담당하고 있는 역할입니다." : "다른 참가자가 담당하고 있는 역할입니다.";
                return owner == participantId;
            }

            roles.Add(role, participantId);
            message = "역할을 추가했습니다: " + ScenarioRoles.Label(role);
            return true;
        }


        public bool IsWorking { get; private set; }

        public ulong WorkClientId { get; private set; }

        public double WorkStartedAt { get; private set; }

        public float WorkDuration { get; private set; }

        public IReadOnlyList<ScenarioDefinition.Step> Steps => definition.Steps;

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

            if (IsWorking && participantId == WorkClientId)
            {
                message = "작업 중에는 공구를 바꿀 수 없습니다. 작업을 취소한 뒤 선택해주세요.";
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
            return TryInteract(participantId, objectId, expectedRevision, 0, out message);
        }


        public bool TryInteract(
            ulong participantId,
            string objectId,
            int expectedRevision,
            double serverTime,
            out string message
        )
        {
            return TryInteract(participantId, objectId, expectedRevision, serverTime, out message, out _);
        }


        public bool TryInteract(
            ulong participantId,
            string objectId,
            int expectedRevision,
            double serverTime,
            out string message,
            out ScenarioInteractionError error
        )
        {
            error = ScenarioInteractionError.None;
            if (expectedRevision != Revision)
            {
                message = "단계가 변경됐습니다. 현재 안내를 확인해주세요.";
                return false;
            }

            if (IsWorking)
            {
                message = "현재 작업이 진행 중입니다. 끝날 때까지 기다려주세요.";
                return false;
            }

            if (double.IsNaN(serverTime) || double.IsInfinity(serverTime))
            {
                message = "작업 시간을 확인할 수 없습니다.";
                return false;
            }

            if (IsComplete)
            {
                message = "모든 실습을 완료했습니다.";
                return false;
            }

            var step = definition.Steps[CompletedSteps];
            if (step.RequiredRole != ScenarioRole.None && (GetRoles(participantId) & step.RequiredRole) != step.RequiredRole)
            {
                message = "이 단계는 " + ScenarioRoles.Label(step.RequiredRole) + "이 수행해야 합니다.";
                return false;
            }

            // 둘 다 틀렸다면 먼저 클릭한 대상부터 바로잡도록 안내합니다.
            if (objectId != step.TargetId)
            {
                error = ScenarioInteractionError.WrongTarget;
                message = "오브젝트를 잘못 선택했습니다. 현재 목표의 작업 대상을 선택해주세요.";
                return false;
            }

            // 공구가 없는 목표에서는 개인 선택을 검사하지 않습니다. 이전에 고른 공구도 그대로 유지합니다.
            if (step.RequiresTool)
            {
                string tool = GetSelectedTool(participantId);
                if (tool.Length == 0)
                {
                    error = ScenarioInteractionError.WrongTool;
                    message = "공구를 먼저 선택해주세요. 기능 메뉴에서 내 공구를 선택할 수 있습니다.";
                    return false;
                }

                if (tool != step.ToolId)
                {
                    error = ScenarioInteractionError.WrongTool;
                    message = "공구를 잘못 선택했습니다. 기능 메뉴에서 올바른 공구를 선택해주세요.";
                    return false;
                }
            }

            if (step.DurationSeconds > 0)
            {
                IsWorking = true;
                WorkClientId = participantId;
                WorkStartedAt = serverTime;
                WorkDuration = step.DurationSeconds;
                Revision++;
                message = "작업을 시작했습니다. 완료될 때까지 기다려주세요.";
                return true;
            }

            CompletedSteps++;
            Revision++;
            message = IsComplete ? "모든 실습을 완료했습니다." : "단계를 완료했습니다.";
            return true;
        }


        // 서버 시계로만 완료합니다. 프레임 지연으로 여러 번 호출돼도 한 번만 다음 단계로 넘어갑니다.
        public bool TryCompleteWork(double serverTime)
        {
            if (!IsWorking ||
                double.IsNaN(serverTime) ||
                double.IsInfinity(serverTime) ||
                serverTime < WorkStartedAt + WorkDuration)
            {
                return false;
            }

            ClearWork();
            CompletedSteps++;
            Revision++;
            return true;
        }


        // 실제 송신자와 현재 작업 버전을 확인합니다. 취소는 완료 수·공구 선택을 바꾸지 않습니다.
        public bool TryCancelWork(ulong participantId, int expectedRevision, double serverTime, out string message)
        {
            if (expectedRevision != Revision)
            {
                message = "작업 상태가 변경됐습니다. 현재 안내를 확인해주세요.";
                return false;
            }

            if (!IsWorking)
            {
                message = "취소할 작업이 없습니다.";
                return false;
            }

            if (participantId != WorkClientId)
            {
                message = "작업을 시작한 참가자만 취소할 수 있습니다.";
                return false;
            }

            if (double.IsNaN(serverTime) || double.IsInfinity(serverTime) || serverTime >= WorkStartedAt + WorkDuration)
            {
                message = "이미 완료된 작업은 취소할 수 없습니다.";
                return false;
            }

            ClearWork();
            Revision++;
            message = "작업을 취소했습니다. 같은 단계에서 다시 시작할 수 있습니다.";
            return true;
        }


        private void ClearWork()
        {
            IsWorking = false;
            WorkClientId = 0;
            WorkStartedAt = 0;
            WorkDuration = 0;
        }


        public bool TryReset(int expectedRevision)
        {
            if (expectedRevision != Revision)
            {
                return false;
            }

            CompletedSteps = 0;
            ClearWork();
            selectedTools.Clear();
            roles.Clear();
            Revision++;
            RunRevision++;
            return true;
        }


        public void RemoveParticipant(ulong participantId)
        {
            selectedTools.Remove(participantId);
            foreach (var role in ScenarioRoles.Enumerate(GetRoles(participantId)))
            {
                roles.Remove(role);
            }

            if (definition.RequiresRoles && IsWorking && WorkClientId == participantId)
            {
                ClearWork();
                Revision++;
            }
        }
    }
}
