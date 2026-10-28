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
        private readonly Dictionary<string, ScenarioObject> objects = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ScenarioObjectView> views = new(StringComparer.Ordinal);
        private RoomPlayer participant;
        private SharedScenario shared;
        private float nextClickAt;
        private string localMessage = "";
        private int messageRevision = -1;
        private int localMessageVersion;

        public ScenarioDefinition Definition => definition;

        public bool IsReady => shared != null && shared.IsSpawned;

        public int CompletedSteps => IsReady ? shared.State.CompletedSteps : 0;

        public int Revision => IsReady ? shared.State.Revision : 0;

        public bool IsComplete => IsReady && CompletedSteps >= definition.Steps.Count;

        public string SelectedTool => IsReady ? shared.LocalSelectedTool : "";

        public void SelectTool(string toolId)
        {
            if (CanInteract && !IsComplete)
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
                        ShowLocalMessage("오브젝트를 잘못 선택했습니다. 현재 목표의 작업 대상을 선택해주세요.");
                    }
                }
            }

            RefreshViews();
        }


        public void Interact(string objectId)
        {
            if (!CanInteract || IsComplete || Time.unscaledTime < nextClickAt)
            {
                return;
            }

            nextClickAt = Time.unscaledTime + 0.2f;
            localMessage = "";
            if (objects.TryGetValue(objectId, out var item) &&
                Vector3.Distance(participant.ServerPosition, item.transform.position) > SharedScenario.InteractionRange)
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

                int uses = 0;
                bool completed = false;
                for (int index = 0; index < CompletedSteps && index < definition.Steps.Count; index++)
                {
                    var step = definition.Steps[index];
                    if (step.TargetId == pair.Key)
                    {
                        completed = true;
                        if (step.RequiresTool)
                        {
                            uses++;
                        }
                    }
                }

                var current = !IsComplete ? definition.Steps[CompletedSteps] : null;
                bool active = current != null && pair.Key == current.TargetId;
                view.Refresh(active, uses, completed);
            }
        }


        private void OnDisable()
        {
            foreach (var view in views.Values)
            {
                view.Dispose();
            }

            views.Clear();
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
