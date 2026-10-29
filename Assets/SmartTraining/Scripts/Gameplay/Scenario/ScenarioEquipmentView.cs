using System;
using System.Collections.Generic;
using UnityEngine;

namespace SmartTraining
{
    // 시나리오의 공구 선택을 참가자별 손 모델에 연결합니다. 공통 장착 모듈에는 씬·플레이어를 전달하지 않습니다.
    internal sealed class ScenarioEquipmentView : IDisposable
    {
        private readonly HandheldItemRig rigPrefab;

        private sealed class Equipment
        {
            public HeldItemView View;
            public bool WasWorking;
            public double ReturnStartedAt = double.NegativeInfinity;
            public Vector3 ReturnPosition;
            public Quaternion ReturnRotation;
        }


        private readonly Dictionary<ulong, Equipment> views = new();
        private readonly HashSet<ulong> active = new();
        private readonly List<ulong> removed = new();

        public ScenarioEquipmentView(HandheldItemRig rigPrefab)
        {
            this.rigPrefab = rigPrefab;
        }


        public void Refresh(
            SharedScenario shared,
            ScenarioDefinition definition,
            RoomPlayer localPlayer,
            Camera camera,
            IReadOnlyDictionary<string, ScenarioObject> objects
        )
        {
            active.Clear();
            if (shared != null && shared.IsSpawned && localPlayer != null && camera != null && rigPrefab != null)
            {
                foreach (var spawned in shared.NetworkManager.SpawnManager.SpawnedObjects.Values)
                {
                    if (!spawned.TryGetComponent<RoomPlayer>(out var player) || !player.RoomIsRunning)
                    {
                        continue;
                    }

                    string toolId = shared.GetSelectedTool(player.OwnerClientId);
                    var tool = definition.GetTool(toolId);
                    var prefab = tool?.HeldPrefab;
                    if (prefab == null)
                    {
                        continue;
                    }

                    ulong id = player.OwnerClientId;
                    active.Add(id);
                    if (!views.TryGetValue(id, out var equipment))
                    {
                        equipment = new Equipment
                        {
                            View = new HeldItemView(rigPrefab, "Held Tool " + id)
                        };
                        views.Add(id, equipment);
                    }

                    RefreshPose(equipment, shared, definition, objects, tool, player, localPlayer, camera);
                }
            }

            removed.Clear();
            foreach (var pair in views)
            {
                if (!active.Contains(pair.Key))
                {
                    pair.Value.View.Dispose();
                    removed.Add(pair.Key);
                }
            }

            foreach (ulong id in removed)
            {
                views.Remove(id);
            }
        }


        private static void RefreshPose(
            Equipment equipment,
            SharedScenario shared,
            ScenarioDefinition definition,
            IReadOnlyDictionary<string, ScenarioObject> objects,
            ScenarioDefinition.Tool tool,
            RoomPlayer player,
            RoomPlayer localPlayer,
            Camera camera
        )
        {
            var view = equipment.View;
            double now = shared.NetworkManager.ServerTime.Time;
            var state = shared.State;
            var step = state.CompletedSteps < definition.Steps.Count ? definition.Steps[state.CompletedSteps] : null;
            bool working = state.IsWorking &&
                state.WorkClientId == player.OwnerClientId &&
                tool.AnimateWork &&
                step != null &&
                objects.ContainsKey(step.TargetId);
            // 취소 시에는 이전 월드 자세에서 짧게 되돌립니다. 대기 복귀·초기화의 제거 처리는 그대로 유지합니다.
            if (equipment.WasWorking && !working)
            {
                equipment.ReturnPosition = view.Position;
                equipment.ReturnRotation = view.Rotation;
                equipment.ReturnStartedAt = now;
            }

            equipment.WasWorking = working;
            bool local = player.OwnerClientId == localPlayer.OwnerClientId;
            view.Attach(
                local ? camera.transform : player.transform,
                local ? new Vector3(.3f, -.27f, .55f) : new Vector3(.55f, -.15f, .3f),
                Quaternion.Euler(-15, local ? -10 : 0, 10)
            );
            view.SetItem(tool.HeldPrefab);
            view.SetArticulation(
                working ? ScenarioWorkMotion.SpinAngle(step.Action, shared.WorkProgress) : 0,
                working ? ScenarioWorkMotion.GripAmount(step.Action, shared.WorkProgress) : 0
            );
            Vector3 restPosition = view.Position;
            Quaternion restRotation = view.Rotation;
            if (working)
            {
                var anchor = objects[step.TargetId].WorkAnchor;
                ScenarioWorkMotion.ToolPose(step.Action, shared.WorkProgress, out var offset, out var rotation);
                Vector3 contactPoint = anchor.position + anchor.rotation * offset;
                Quaternion targetRotation = anchor.rotation * rotation * tool.WorkRotation;
                view.GetContactPose(
                    tool.WorkContactPoint,
                    contactPoint,
                    targetRotation,
                    out var contactPosition,
                    out var contactRotation
                );
                float weight = ScenarioWorkMotion.ContactWeight(shared.WorkProgress);
                view.SetPose(
                    Vector3.Lerp(restPosition, contactPosition, weight),
                    Quaternion.Slerp(restRotation, contactRotation, weight)
                );
            }
            else if (now < equipment.ReturnStartedAt + ScenarioWorkMotion.CancelReturnSeconds)
            {
                float weight = Mathf.SmoothStep(
                    0,
                    1,
                    (float)((now - equipment.ReturnStartedAt) / ScenarioWorkMotion.CancelReturnSeconds)
                );
                view.SetPose(
                    Vector3.Lerp(equipment.ReturnPosition, restPosition, weight),
                    Quaternion.Slerp(equipment.ReturnRotation, restRotation, weight)
                );
            }
        }


        public void Dispose()
        {
            foreach (var view in views.Values)
            {
                view.View.Dispose();
            }

            views.Clear();
        }
    }
}
