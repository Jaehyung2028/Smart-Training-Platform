using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace SmartTraining
{
    // 서버가 원본 단계로 판정하고 결과만 NetworkVariable로 공유합니다. 클라이언트는 완료 여부를 지정할 수 없습니다.
    [RequireComponent(typeof(NetworkObject))]
    public sealed class SharedScenario : NetworkBehaviour
    {
        public const string ResourcePath = "SmartTraining/SharedScenario";
        public const float InteractionRange = 3.5f;
        private readonly NetworkVariable<ScenarioProgressState> state = new();
        private ScenarioProgress progress;
        private ScenarioRunResults results;
        private NetworkList<ScenarioStepOutcome> outcomes;
        private string workingActor = "";
        private readonly Dictionary<string, Vector3> objectOffsets = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Vector3> objects = new(StringComparer.Ordinal);
        private readonly Dictionary<ulong, double> nextRequestAt = new();
        private NetworkManager subscribedManager;
        private NetworkList<ScenarioToolSelection> toolSelections;
        private NetworkList<ScenarioRoleAssignment> roles;

        private void Awake()
        {
            toolSelections = new NetworkList<ScenarioToolSelection>();
            outcomes = new NetworkList<ScenarioStepOutcome>();
            roles = new NetworkList<ScenarioRoleAssignment>();
        }


        public ScenarioProgressState State => state.Value;


        public IEnumerable<ScenarioStepOutcome> GetOutcomes()
        {
            if (IsSpawned)
            {
                foreach (var outcome in outcomes)
                {
                    if (outcome.RunRevision == State.RunRevision)
                    {
                        yield return outcome;
                    }
                }
            }
        }


        public float WorkProgress => IsSpawned ? State.GetWorkProgress(NetworkManager.ServerTime.Time) : 0;


        private void Update()
        {
            if (IsServer && IsSpawned && progress != null && progress.TryCompleteWork(NetworkManager.ServerTime.Time))
            {
                Publish(workingActor);
            }
        }


        public ScenarioRole LocalRoles => GetRoles(NetworkManager.LocalClientId);


        public ScenarioRole GetRoles(ulong clientId)
        {
            var result = ScenarioRole.None;
            foreach (var item in roles)
            {
                if (item.ClientId == clientId && item.RunRevision == State.RunRevision)
                {
                    result |= item.Role;
                }
            }

            return result;
        }


        // 표시 이름 대신 동기화된 배정 정보로 역할의 사용 여부를 판단합니다.
        public bool IsRoleAssigned(ScenarioRole role)
        {
            foreach (var item in roles)
            {
                if (item.Role == role && item.RunRevision == State.RunRevision)
                {
                    return true;
                }
            }

            return false;
        }


        public string GetRoleParticipantName(ScenarioRole role)
        {
            foreach (var item in roles)
            {
                if (item.Role != role || item.RunRevision != State.RunRevision)
                {
                    continue;
                }

                foreach (var spawned in NetworkManager.SpawnManager.SpawnedObjects.Values)
                {
                    if (spawned.TryGetComponent<RoomPlayer>(out var player) && player.OwnerClientId == item.ClientId)
                    {
                        return player.UserName;
                    }
                }
            }

            return "미배정";
        }


        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SelectRoleRpc(ScenarioRole role, int expectedRunRevision, RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            if (GetParticipant(sender) == null)
            {
                return;
            }

            if (progress.TrySelectRole(sender, role, expectedRunRevision, out var message))
            {
                // 재전송으로 기존 겸임 정보를 지우지 않고 새 역할만 추가합니다.
                if (!IsRoleAssigned(role))
                {
                    roles.Add(new ScenarioRoleAssignment { ClientId = sender, Role = role, RunRevision = progress.RunRevision });
                }
            }

            FeedbackRpc(message, state.Value.Revision, RpcTarget.Single(sender, RpcTargetUse.Temp));
        }


        public string LocalSelectedTool => IsSpawned ? GetSelectedTool(NetworkManager.LocalClientId) : "";


        public string GetSelectedTool(ulong clientId)
        {
            if (!IsSpawned)
            {
                return "";
            }

            foreach (var selection in toolSelections)
            {
                if (selection.ClientId == clientId && selection.RunRevision == State.RunRevision)
                {
                    return selection.ToolId.ToString();
                }
            }

            return "";
        }


        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                subscribedManager = NetworkManager;
                subscribedManager.OnClientDisconnectCallback += RemoveParticipant;
                workingActor = "";
                results.Begin(NetworkManager.ServerTime.Time);
                Publish("");
            }
        }


        public override void OnNetworkDespawn()
        {
            if (subscribedManager != null)
            {
                subscribedManager.OnClientDisconnectCallback -= RemoveParticipant;
                subscribedManager = null;
            }
        }


        private void RemoveParticipant(ulong clientId)
        {
            progress?.RemoveParticipant(clientId);
            for (int index = roles.Count - 1; index >= 0; index--)
            {
                if (roles[index].ClientId == clientId)
                {
                    roles.RemoveAt(index);
                }
            }

            if (IsServer && IsSpawned)
            {
                Publish(workingActor);
            }

            nextRequestAt.Remove(clientId);
            SetToolSelection(clientId, "");
        }


        public string Feedback { get; private set; } = "";

        public int FeedbackRevision { get; private set; } = -1;

        public int FeedbackVersion { get; private set; }


        public static GameObject Register(NetworkManager manager)
        {
            var prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab == null || prefab.GetComponent<SharedScenario>() == null)
            {
                throw new InvalidOperationException("SharedScenario 네트워크 프리팹을 준비해주세요.");
            }

            manager.AddNetworkPrefab(prefab);
            return prefab;
        }


        public static SharedScenario Find(NetworkManager manager)
        {
            if (manager?.SpawnManager != null)
            {
                foreach (var spawned in manager.SpawnManager.SpawnedObjects.Values)
                {
                    if (spawned.TryGetComponent<SharedScenario>(out var scenario))
                    {
                        return scenario;
                    }
                }
            }

            return null;
        }


        // Addressables 프리팹을 생성하지 않고 공구·대상 위치와 단계 원본만 읽습니다.
        public void InitializeServer(ScenarioContent source)
        {
            progress = new ScenarioProgress(source.Definition);
            results = new ScenarioRunResults(source.Definition.Steps.Count);
            objects.Clear();
            objectOffsets.Clear();
            foreach (var item in source.GetComponentsInChildren<ScenarioObject>(true))
            {
                if (item.Kind == ScenarioObjectKind.Target)
                {
                    objects.Add(item.Id, item.transform.position);
                    objectOffsets.Add(
                        item.Id,
                        item.transform.parent != null ? item.transform.parent.TransformVector(item.LoosenOffset) : item.LoosenOffset
                    );
                }
            }
        }


        private RoomPlayer GetParticipant(ulong sender)
        {
            if (!IsServer ||
                !IsSpawned ||
                progress == null ||
                !NetworkManager.ConnectedClients.TryGetValue(sender, out var client) ||
                client.PlayerObject == null ||
                !client.PlayerObject.TryGetComponent<RoomPlayer>(out var player) ||
                !player.RoomIsRunning ||
                player.PhasePending)
            {
                return null;
            }

            return player;
        }


        // 실제 송신자의 선택만 판정한 뒤 표시용 목록을 갱신합니다. 다른 참가자의 공구 판정에는 영향을 주지 않습니다.
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SelectToolRpc(string toolId, int expectedRunRevision, RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            if (GetParticipant(sender) == null)
            {
                return;
            }

            if (progress.TrySelectTool(sender, toolId, expectedRunRevision, out var message))
            {
                SetToolSelection(sender, progress.GetSelectedTool(sender));
            }

            FeedbackRpc(message, state.Value.Revision, RpcTarget.Single(sender, RpcTargetUse.Temp));
        }


        private void SetToolSelection(ulong clientId, string toolId)
        {
            for (int index = 0; index < toolSelections.Count; index++)
            {
                if (toolSelections[index].ClientId == clientId)
                {
                    if (toolId.Length == 0)
                    {
                        toolSelections.RemoveAt(index);
                    }
                    else
                    {
                        toolSelections[index] = new ScenarioToolSelection
                        {
                            ClientId = clientId,
                            ToolId = new FixedString64Bytes(toolId),
                            RunRevision = progress.RunRevision
                        };
                    }

                    return;
                }
            }

            if (toolId.Length > 0)
            {
                toolSelections.Add(new ScenarioToolSelection { ClientId = clientId, ToolId = new FixedString64Bytes(toolId), RunRevision = progress.RunRevision });
            }
        }


        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void InteractRpc(string objectId, int expectedRevision, RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            var player = GetParticipant(sender);
            if (player == null || string.IsNullOrEmpty(objectId) || objectId.Length > 60)
            {
                return;
            }

            double now = NetworkManager.ServerTime.Time;
            if (nextRequestAt.TryGetValue(sender, out var next) && now < next)
            {
                return;
            }

            nextRequestAt[sender] = now + 0.15;
            string message;
            var error = ScenarioInteractionError.None;
            if (expectedRevision != progress.Revision || progress.IsWorking || progress.IsComplete)
            {
                progress.TryInteract(sender, objectId, expectedRevision, now, out message, out error);
            }
            else if (!objects.TryGetValue(objectId, out var position))
            {
                progress.TryInteract(sender, objectId, expectedRevision, now, out message, out error);
            }
            else if (Vector3.Distance(
                player.ServerPosition,
                position + objectOffsets[objectId] * ScenarioObjectPose.Evaluate(
                progress.Steps,
                objectId,
                progress.CompletedSteps,
                progress.IsWorking,
                WorkProgress
            )
            ) > InteractionRange)
            {
                message = "공구나 작업 대상에 더 가까이 이동해주세요.";
            }
            else if (progress.TryInteract(sender, objectId, expectedRevision, now, out message, out error))
            {
                workingActor = player.UserName;
                Publish(workingActor);
            }

            if (results.RecordError(progress.CompletedSteps, error))
            {
                Publish(workingActor);
            }

            FeedbackRpc(message, state.Value.Revision, RpcTarget.Single(sender, RpcTargetUse.Temp));
        }


        // 방장 권한 대신 작업자의 실제 연결 ID를 검사합니다. 이전 작업의 취소 요청은 버전으로 거절합니다.
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void CancelWorkRpc(int expectedRevision, RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            var player = GetParticipant(sender);
            if (player == null)
            {
                return;
            }

            double now = NetworkManager.ServerTime.Time;
            // 완료와 취소가 같은 프레임에 도착해도 서버의 완료 시각을 우선합니다.
            if (progress.TryCompleteWork(now))
            {
                Publish(workingActor);
            }

            if (progress.TryCancelWork(sender, expectedRevision, now, out var message))
            {
                Publish(player.UserName);
            }

            FeedbackRpc(message, state.Value.Revision, RpcTarget.Single(sender, RpcTargetUse.Temp));
        }


        // 다른 참가자가 실습하는 도중 임의로 초기화하지 못하도록 현재 방장만 허용합니다.
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void ResetRpc(int expectedRevision, RpcParams rpcParams = default)
        {
            var player = GetParticipant(rpcParams.Receive.SenderClientId);
            if (player == null || !player.IsRoomMaster)
            {
                return;
            }

            if (progress.TryReset(expectedRevision))
            {
                toolSelections.Clear();
                outcomes.Clear();
                roles.Clear();
                workingActor = "";
                results.Begin(NetworkManager.ServerTime.Time);
                Publish(player.UserName);
            }
        }


        private void Publish(string actor)
        {
            // 작업 시작·취소는 완료 행을 만들지 않습니다. 실제 완료된 단계만 한 번 기록합니다.
            if (results.CompleteStep(progress.CompletedSteps, actor, NetworkManager.ServerTime.Time))
            {
                var row = results.Steps[results.Steps.Count - 1];
                outcomes.Add(new ScenarioStepOutcome { Index = row.Index, RunRevision = progress.RunRevision, ActorName = new FixedString128Bytes(row.ActorName), DurationSeconds = row.DurationSeconds, ToolErrors = row.ToolErrors, TargetErrors = row.TargetErrors });
            }

            // 공동 목표는 서버가 승인한 단계 수로만 참가자 결과를 갱신합니다. 초기화 시 결과도 되돌립니다.
            foreach (var client in NetworkManager.ConnectedClients.Values)
            {
                client.PlayerObject?.GetComponent<TrainingProgress>()?.SetSharedServer(progress.CompletedSteps * 100 / progress.DefinitionStepCount);
            }

            state.Value = new ScenarioProgressState
            {
                CompletedSteps = progress.CompletedSteps,
                Revision = progress.Revision,
                RunRevision = progress.RunRevision,
                LastActor = new FixedString128Bytes(actor),
                WorkStartedAt = progress.WorkStartedAt,
                WorkDuration = progress.WorkDuration,
                WorkClientId = progress.WorkClientId,
                RunStartedAt = results.StartedAt,
                RunEndedAt = results.EndedAt,
                ToolErrors = results.ToolErrors,
                TargetErrors = results.TargetErrors
            };
        }


        [Rpc(SendTo.SpecifiedInParams)]
        private void FeedbackRpc(string message, int revision, RpcParams rpcParams = default)
        {
            FeedbackVersion++;
            Feedback = message;
            FeedbackRevision = revision;
        }
    }
}
