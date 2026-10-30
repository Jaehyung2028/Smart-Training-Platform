using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace SmartTraining
{
    // Addressables 기본 프리팹과 단계 데이터를 연결하고 공구·대상 참조를 시작 시 검사합니다.
    [DisallowMultipleComponent]
    public sealed class ScenarioContent : MonoBehaviour
    {
        [SerializeField]
        private ScenarioDefinition definition;
        [SerializeField]
        private HandheldItemRig handRig;
        private ScenarioEquipmentView equipmentView;
        private readonly Dictionary<string, ScenarioObject> objects = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ScenarioObjectView> views = new(StringComparer.Ordinal);
        private RoomPlayer participant;
        private SharedScenario shared;
        private float nextClickAt;
        private string localMessage = "";
        private int messageRevision = -1;
        private int localMessageVersion;

        public ScenarioDefinition Definition => definition;

        public ScenarioRole SelectedRoles => IsReady ? shared.LocalRoles : ScenarioRole.None;


        public string RoleParticipantName(ScenarioRole role)
        {
            return IsReady ? shared.GetRoleParticipantName(role) : "미배정";
        }


        public bool IsRoleAssigned(ScenarioRole role)
        {
            return IsReady && shared.IsRoleAssigned(role);
        }


        // 공통 이름표와 참가자 목록에는 콘텐츠를 모르는 표시 문자열만 전달합니다.
        public string ParticipantRoleLabel(ulong clientId)
        {
            if (!IsReady || !definition.RequiresRoles)
            {
                return "";
            }

            var role = shared.GetRoles(clientId);
            return role == ScenarioRole.None ? "역할 미선택" : ScenarioRoles.Label(role, "\n");
        }


        public void SelectRole(ScenarioRole role)
        {
            if (CanInteract)
            {
                localMessage = "";
                shared.SelectRoleRpc(role, RunRevision);
            }
        }


        public bool IsReady => shared != null && shared.IsSpawned;

        public int CompletedSteps => IsReady ? shared.State.CompletedSteps : 0;

        public int Revision => IsReady ? shared.State.Revision : 0;

        public bool IsWorking => IsReady && shared.State.IsWorking;

        public float WorkProgress => IsReady ? shared.WorkProgress : 0;

        public bool CanCancelWork => CanInteract && IsWorking && shared.State.WorkClientId == participant.OwnerClientId;

        public bool IsComplete => IsReady && CompletedSteps >= definition.Steps.Count;

        public string SelectedTool => IsReady ? shared.LocalSelectedTool : "";

        public int RunRevision => IsReady ? shared.State.RunRevision : -1;

        public int ToolErrors => IsReady ? shared.State.ToolErrors : 0;

        public int TargetErrors => IsReady ? shared.State.TargetErrors : 0;

        public IEnumerable<ScenarioStepOutcome> Outcomes => IsReady ? shared.GetOutcomes() : Array.Empty<ScenarioStepOutcome>();

        public double ElapsedSeconds => IsReady ? Math.Max(
            0,
            (IsComplete ? shared.State.RunEndedAt : shared.NetworkManager.ServerTime.Time) - shared.State.RunStartedAt
        ) : 0;


        public void SelectTool(string toolId)
        {
            if (CanSelectTool)
            {
                localMessage = "";
                shared.SelectToolRpc(toolId, shared.State.RunRevision);
            }
        }


        public string LastActor => IsReady ? shared.State.LastActor.ToString() : "";

        public bool CanInteract => IsReady &&
            participant != null &&
            participant.IsSpawned &&
            participant.RoomIsRunning &&
            !participant.PhasePending;

        public bool CanReset => CanInteract && participant.IsRoomMaster;

        public bool CanSelectTool => CanInteract && !IsComplete && !(IsWorking && shared.State.WorkClientId == participant.OwnerClientId);

        public string Feedback => localMessage.Length > 0 && messageRevision == Revision ? localMessage : IsReady && shared.FeedbackRevision == Revision ? shared.Feedback : "";

        public int FeedbackVersion => localMessageVersion + (IsReady ? shared.FeedbackVersion : 0);

        public ScenarioObject Hovered { get; private set; }


        public void Bind(RoomPlayer player)
        {
            participant = player;
            if (shared == null || !shared.IsSpawned)
            {
                shared = SharedScenario.Find(player != null ? player.NetworkManager : null);
            }
        }


        public void UpdateInteraction(Camera camera, RoomPlayer player, bool allowed)
        {
            Bind(player);
            // 볼트 자세를 먼저 갱신해야 같은 프레임의 작업 접촉점과 공구가 정확히 맞습니다.
            RefreshViews();
            equipmentView ??= new ScenarioEquipmentView(handRig);
            equipmentView.Refresh(shared, definition, player, camera, objects);
            Hovered = null;
            if (allowed &&
                CanInteract &&
                camera != null &&
                Mouse.current != null &&
                !(EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject()))
            {
                bool hitObject = false;
                var ray = camera.ScreenPointToRay(Mouse.current.position.ReadValue());
                if (Physics.Raycast(ray, out var hit, 30, ~0, QueryTriggerInteraction.Ignore))
                {
                    hitObject = true;
                    var item = hit.collider.GetComponentInParent<ScenarioObject>();
                    if (item != null && objects.TryGetValue(item.Id, out var registered) && registered == item)
                    {
                        Hovered = item;
                    }
                }

                if (Mouse.current.leftButton.wasPressedThisFrame && !Mouse.current.rightButton.isPressed)
                {
                    if (Hovered != null)
                    {
                        Interact(Hovered.Id);
                    }
                    else if (hitObject)
                    {
                        // 작업대처럼 등록된 작업 대상이 아닌 3D 오브젝트도 무응답 대신 이유를 안내합니다.
                        if (Vector3.Distance(participant.Position, hit.point) > SharedScenario.InteractionRange)
                        {
                            ShowLocalMessage("작업 대상에 더 가까이 이동해주세요.");
                        }
                        else
                        {
                            Interact("__unregistered_target");
                        }
                    }
                }
            }
        }


        public void Interact(string objectId)
        {
            if (!CanInteract || IsComplete || Time.unscaledTime < nextClickAt)
            {
                return;
            }

            nextClickAt = Time.unscaledTime + 0.2f;
            if (IsWorking)
            {
                ShowLocalMessage("현재 작업이 진행 중입니다. 끝날 때까지 기다려주세요.");
                return;
            }

            localMessage = "";
            if (objects.TryGetValue(objectId, out var item) &&
                Vector3.Distance(participant.Position, item.transform.position) > SharedScenario.InteractionRange)
            {
                ShowLocalMessage("작업 대상에 더 가까이 이동해주세요.");
                return;
            }

            shared.InteractRpc(objectId, Revision);
        }


        private void ShowLocalMessage(string message)
        {
            localMessageVersion++;
            localMessage = message;
            messageRevision = Revision;
        }


        public void CancelWork()
        {
            if (CanCancelWork)
            {
                localMessage = "";
                shared.CancelWorkRpc(Revision);
            }
        }


        public void ResetPractice()
        {
            if (CanReset)
            {
                shared.ResetRpc(Revision);
            }
        }


        private void RefreshViews()
        {
            if (!IsReady)
            {
                return;
            }

            foreach (var pair in objects)
            {
                if (!views.TryGetValue(pair.Key, out var view))
                {
                    view = new ScenarioObjectView(pair.Value);
                    views.Add(pair.Key, view);
                }

                float loosenAmount = ScenarioObjectPose.Evaluate(
                    definition.Steps,
                    pair.Key,
                    CompletedSteps,
                    IsWorking,
                    WorkProgress
                );
                bool completed = false;
                for (int index = 0; index < CompletedSteps && index < definition.Steps.Count; index++)
                {
                    var step = definition.Steps[index];
                    if (step.TargetId == pair.Key)
                    {
                        completed = true;
                    }
                }

                var current = !IsComplete ? definition.Steps[CompletedSteps] : null;
                bool active = current != null && pair.Key == current.TargetId;
                view.Refresh(active, loosenAmount, completed);
            }
        }


        private void OnDisable()
        {
            foreach (var view in views.Values)
            {
                view.Dispose();
            }

            views.Clear();
            equipmentView?.Dispose();
            equipmentView = null;
            shared = null;
            participant = null;
            Hovered = null;
        }


        public bool TryInitialize(out string message)
        {
            if (definition == null)
            {
                message = "시나리오 프리팹에 Scenario Definition을 연결해주세요.";
                return false;
            }

            if (handRig != null && handRig.Grip == null)
            {
                message = "손 프리팹의 Grip 장착 지점을 지정해주세요.";
                return false;
            }

            if (!definition.TryValidate(out message))
            {
                return false;
            }

            objects.Clear();
            foreach (var item in GetComponentsInChildren<ScenarioObject>(true))
            {
                if (item.Kind != ScenarioObjectKind.Target)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(item.Id) ||
                    System.Text.Encoding.UTF8.GetByteCount(item.Id) > 60 ||
                    !objects.TryAdd(item.Id, item))
                {
                    message = "시나리오 공구·대상의 ID는 비어 있거나 중복될 수 없습니다.";
                    return false;
                }
            }

            foreach (var step in definition.Steps)
            {
                if (!objects.TryGetValue(step.TargetId, out var target) || target.Kind != ScenarioObjectKind.Target)
                {
                    message = "시나리오 작업 대상을 찾을 수 없습니다: " + step.TargetId;
                    return false;
                }
            }

            message = "";
            return true;
        }
    }
}
