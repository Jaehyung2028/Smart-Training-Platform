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
        private readonly Dictionary<string, Vector3> objects = new(StringComparer.Ordinal);
        private readonly Dictionary<ulong, double> nextRequestAt = new();
        private NetworkManager subscribedManager;
        private string localTool = "";
        private int localToolRunRevision = -1;

        public ScenarioProgressState State => state.Value;

        public string LocalSelectedTool => localToolRunRevision == State.RunRevision ? localTool : "";


        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                subscribedManager = NetworkManager;
                subscribedManager.OnClientDisconnectCallback += RemoveParticipant;
            }
        }


        public override void OnNetworkDespawn()
        {
            if (subscribedManager != null)
            {
                subscribedManager.OnClientDisconnectCallback -= RemoveParticipant;
                subscribedManager = null;
            }

            localTool = "";
            localToolRunRevision = -1;
        }


        private void RemoveParticipant(ulong clientId)
        {
            progress?.RemoveParticipant(clientId);
            nextRequestAt.Remove(clientId);
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
            objects.Clear();
            foreach (var item in source.GetComponentsInChildren<ScenarioObject>(true))
            {
                if (item.Kind == ScenarioObjectKind.Target)
                {
                    objects.Add(item.Id, item.transform.position);
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


        // 메뉴 선택은 거리와 관계없이 가능하며 서버는 실제 송신자의 개인 선택만 변경합니다.
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
                SelectedToolRpc(
                    progress.GetSelectedTool(sender),
                    progress.RunRevision,
                    RpcTarget.Single(sender, RpcTargetUse.Temp)
                );
            }

            FeedbackRpc(message, state.Value.Revision, RpcTarget.Single(sender, RpcTargetUse.Temp));
        }


        [Rpc(SendTo.SpecifiedInParams)]
        private void SelectedToolRpc(string toolId, int runRevision, RpcParams rpcParams = default)
        {
            // 새 초기화 상태가 먼저/나중에 도착하더라도 해당 실행 번호의 선택만 화면에 표시합니다.
            localTool = toolId;
            localToolRunRevision = runRevision;
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
            if (!objects.TryGetValue(objectId, out var position))
            {
                message = "오브젝트를 잘못 선택했습니다. 현재 목표의 작업 대상을 선택해주세요.";
            }
            else if (Vector3.Distance(player.ServerPosition, position) > InteractionRange)
            {
                message = "공구나 작업 대상에 더 가까이 이동해주세요.";
            }
            else if (progress.TryInteract(sender, objectId, expectedRevision, out message))
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
                Publish(player.UserName);
            }
        }


        private void Publish(string actor)
        {
            // 공동 목표는 서버가 승인한 단계 수로만 참가자 결과를 갱신합니다. 초기화 시 결과도 되돌립니다.
            foreach (var client in NetworkManager.ConnectedClients.Values)
            {
                client.PlayerObject?.GetComponent<TrainingProgress>()?.SetSharedServer(
                    progress.CompletedSteps * 100 / progress.DefinitionStepCount);
            }
            state.Value = new ScenarioProgressState
            {
                CompletedSteps = progress.CompletedSteps,
                Revision = progress.Revision,
                RunRevision = progress.RunRevision,
                LastActor = new FixedString128Bytes(actor)
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
