using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace SmartTraining
{
    public sealed class RoomPlayer : NetworkBehaviour
    {
        public const float MoveSpeed = 4f;
        public const float ArenaHalfSize = 12f;
        public const float InputTimeout = 0.3f;
        private readonly NetworkVariable<Vector3> position = new NetworkVariable<Vector3>(Vector3.zero);
        private readonly NetworkVariable<FixedString64Bytes> userId = new NetworkVariable<FixedString64Bytes>();
        private readonly NetworkVariable<FixedString128Bytes> userName = new NetworkVariable<FixedString128Bytes>();
        private Vector2 input;
        private double inputReceivedAt;
        private Material material;
        private RoomHubResult initialParticipant;
        public Vector3 ServerPosition => position.Value;
        public string UserId => userId.Value.ToString();
        public string UserName => userName.Value.ToString();
        private readonly NetworkVariable<FixedString64Bytes> roomMasterId = new NetworkVariable<FixedString64Bytes>();
        public string RoomMasterUserId => roomMasterId.Value.ToString();
        public bool IsRoomMaster => !string.IsNullOrEmpty(UserId) && UserId == RoomMasterUserId;
        private readonly NetworkVariable<bool> roomEntryLocked = new NetworkVariable<bool>();
        public bool RoomEntryLocked => roomEntryLocked.Value;
        public void SetRoomEntryLockServer(bool value)
        {
            if (IsServer && IsSpawned) roomEntryLocked.Value = value;
        }
        public void SetRoomMasterServer(string masterId)
        {
            if (IsServer && IsSpawned) roomMasterId.Value = new FixedString64Bytes(masterId ?? "");
        }

        // Only called by RoomDedicatedServer after ASP.NET has confirmed membership.
        public void InitializeServer(RoomHubResult participant)
        {
            initialParticipant = participant;
            transform.position = SpawnPosition(participant.roomIndex);
        }
        private static Vector3 SpawnPosition(int index) => new Vector3((index % 4 - 1.5f) * 3, 1, (index / 4 - 0.5f) * 3);
        public override void OnNetworkSpawn()
        {
            if (IsServer && initialParticipant != null)
            {
                userId.Value = new FixedString64Bytes(initialParticipant.userId);
                userName.Value = new FixedString128Bytes(initialParticipant.userName);
                position.Value = SpawnPosition(initialParticipant.roomIndex);
                initialParticipant = null;
            }
            transform.position = position.Value;
            if (IsServer) NetworkManager.NetworkTickSystem.Tick += ServerTick;
            #if !UNITY_SERVER
            if (IsClient) CreateVisual();
            #endif
        }
        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager.NetworkTickSystem != null) NetworkManager.NetworkTickSystem.Tick -= ServerTick;
            input = Vector2.zero;
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner, Delivery = RpcDelivery.Unreliable)]
        public void MoveRpc(Vector2 direction)
        {
            if (!IsServer || !Finite(direction.x) || !Finite(direction.y)) return;
            input = Vector2.ClampMagnitude(direction, 1);
            inputReceivedAt = Time.realtimeSinceStartupAsDouble;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private void ServerTick()
        {
            if (!IsSpawned) return;
            if (Time.realtimeSinceStartupAsDouble - inputReceivedAt > InputTimeout) input = Vector2.zero;
            var next = position.Value + new Vector3(input.x, 0, input.y) * (MoveSpeed / NetworkManager.NetworkConfig.TickRate);
            next.x = Mathf.Clamp(next.x, -ArenaHalfSize, ArenaHalfSize);
            next.z = Mathf.Clamp(next.z, -ArenaHalfSize, ArenaHalfSize);
            position.Value = next;
            transform.position = next;
        }
        private void Update()
        {
            if (IsSpawned && !IsServer)
                transform.position = Vector3.Lerp(transform.position, position.Value, 1 - Mathf.Exp(-16 * Time.unscaledDeltaTime));
        }
        private void CreateVisual()
        {
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "PlayerVisual"; visual.transform.SetParent(transform, false);
            var collider = visual.GetComponent<Collider>(); if (collider != null) Destroy(collider);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) return;
            material = new Material(shader) { color = IsOwner ? new Color(0.2f, 0.85f, 0.45f) : new Color(0.25f, 0.6f, 1f) };
            visual.GetComponent<Renderer>().sharedMaterial = material;
        }
        public override void OnDestroy() { if (material != null) Destroy(material); base.OnDestroy(); }
    }
}

