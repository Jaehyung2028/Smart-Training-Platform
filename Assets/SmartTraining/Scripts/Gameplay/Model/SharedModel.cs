using System;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace SmartTraining
{
    // 서버가 스폰하는 공용 모델의 네트워크 루트입니다. 표시할 Addressables 모델은 클라이언트에서 자식으로 붙습니다.
    // 회전은 Owner 방식 NetworkTransform이 동기화하고, 이 클래스는 조작권과 분해 재생 상태를 관리합니다.
    [RequireComponent(typeof(NetworkObject), typeof(NetworkTransform))]
    public sealed class SharedModel : NetworkBehaviour
    {
        public const string ResourcePath = "SmartTraining/SharedModel";
        public const float InteractionDistance = 6f;

        // 서버만 재생 상태를 확정합니다. 재접속한 참가자도 현재 시각의 같은 포즈를 계산합니다.
        private readonly NetworkVariable<ModelPlaybackState> playback = new(new ModelPlaybackState(0, 0, 0, 5));
        public float ExplosionAmount => IsSpawned ? playback.Value.Evaluate(NetworkManager.ServerTime.Time) : 0;
        public int PlaybackDirection => IsSpawned ? playback.Value.GetDirection(NetworkManager.ServerTime.Time) : 0;

        private NetworkTransform networkTransform;
        private float lastActivityAt;
        private Quaternion lastRotation;
        private Quaternion initialRotation;

        public static GameObject Register(NetworkManager manager)
        {
            var prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab == null || prefab.GetComponent<SharedModel>() == null ||
                prefab.GetComponent<NetworkTransform>()?.AuthorityMode != NetworkTransform.AuthorityModes.Owner)
            {
                throw new InvalidOperationException("SharedModel 네트워크 프리팹을 준비해주세요.");
            }
            manager.AddNetworkPrefab(prefab);
            return prefab;
        }

        public static SharedModel Find(NetworkManager manager)
        {
            if (manager?.SpawnManager == null)
            {
                return null;
            }
            foreach (var spawned in manager.SpawnManager.SpawnedObjects.Values)
            {
                if (spawned.TryGetComponent<SharedModel>(out var model))
                {
                    return model;
                }
            }
            return null;
        }

        public override void OnNetworkSpawn()
        {
            networkTransform = GetComponent<NetworkTransform>();
            if (IsServer)
            {
                initialRotation = transform.rotation;
                NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager != null)
            {
                NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }

        private bool CanManipulate(ulong clientId)
        {
            return NetworkManager.ConnectedClients.TryGetValue(clientId, out var client) &&
                client.PlayerObject != null && client.PlayerObject.TryGetComponent<RoomPlayer>(out var player) &&
                player.RoomIsRunning && !player.PhasePending &&
                Vector3.Distance(player.Position, transform.position) <= InteractionDistance + 1f;
        }

        // 서버가 먼저 받은 요청 하나만 승인합니다. 다른 사용자가 조작 중이면 소유권을 빼앗지 않습니다.
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestControlRpc(RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            if (!IsServer || !IsSpawned || !NetworkObject.IsOwnedByServer || !CanManipulate(sender))
            {
                return;
            }
            lastActivityAt = Time.unscaledTime;
            lastRotation = transform.rotation;
            NetworkObject.ChangeOwnership(sender);
        }

        // 초기화는 공유 상태 변경입니다. 가까운 참가자만 요청할 수 있고, 누군가 조작 중이면 서버도 거절합니다.
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void ResetRotationRpc(RpcParams rpcParams = default)
        {
            if (!IsServer || !IsSpawned || !NetworkObject.IsOwnedByServer || !CanManipulate(rpcParams.Receive.SenderClientId))
            {
                return;
            }
            networkTransform.SetState(rotIn: initialRotation);
        }

        // 분해도는 위치마다 RPC를 보내지 않고 0~1의 공통 값 하나로 동기화합니다.
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SetExplosionRpc(float amount, RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            if (!IsServer || !IsSpawned || !CanManipulate(sender) ||
                (!NetworkObject.IsOwnedByServer && OwnerClientId != sender) ||
                float.IsNaN(amount) || float.IsInfinity(amount))
            {
                return;
            }
            // 직접 구간을 옮기면 자동 재생을 중지하고 요청한 위치에 고정합니다.
            playback.Value = new ModelPlaybackState(Mathf.Clamp01(amount), 0, NetworkManager.ServerTime.Time, playback.Value.Duration);
        }

        // 1은 분해, -1은 재조립, 0은 일시정지입니다. 현재 진행도를 유지하면서 재생 방향만 바꿉니다.
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SetPlaybackRpc(int direction, float duration, RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            if (!IsServer || !IsSpawned || !CanManipulate(sender) ||
                (!NetworkObject.IsOwnedByServer && OwnerClientId != sender) ||
                direction < -1 || direction > 1 || float.IsNaN(duration) || float.IsInfinity(duration) ||
                duration < 0.1f || duration > 3600)
            {
                return;
            }
            double now = NetworkManager.ServerTime.Time;
            float amount = playback.Value.Evaluate(now);
            if (direction == playback.Value.GetDirection(now) || (direction > 0 && amount >= 1) || (direction < 0 && amount <= 0))
            {
                return;
            }
            playback.Value = new ModelPlaybackState(amount, direction, now, duration);
        }

        // 마지막 각도는 종료 요청에 한 번만 포함합니다. 마지막 Transform 패킷보다 반환 요청이 먼저 와도 각도를 보존합니다.
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void ReleaseControlRpc(Quaternion finalRotation, RpcParams rpcParams = default)
        {
            if (!IsServer || !IsSpawned || NetworkObject.IsOwnedByServer || OwnerClientId != rpcParams.Receive.SenderClientId)
            {
                return;
            }
            if (IsValidRotation(finalRotation))
            {
                ReclaimControl(finalRotation.normalized);
            }
            else
            {
                ReclaimControl(transform.rotation);
            }
        }

        private static bool IsValidRotation(Quaternion rotation)
        {
            float length = rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w;
            return !float.IsNaN(length) && !float.IsInfinity(length) && length > 0.5f && length < 1.5f;
        }

        private void ReclaimControl(Quaternion rotation)
        {
            NetworkObject.RemoveOwnership();
            // 소유권 회수 후 서버가 최종 상태를 커밋합니다. 별도의 회전 변수나 자체 보간은 사용하지 않습니다.
            networkTransform.SetState(rotIn: rotation);
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (IsSpawned && OwnerClientId == clientId)
            {
                ReclaimControl(transform.rotation);
            }
        }

        private void Update()
        {
            if (!IsServer || !IsSpawned)
            {
                return;
            }
            // 끝에 도달했을 때만 정지 상태를 확정합니다. 재생 도중에는 네트워크 상태를 계속 덮어쓰지 않습니다.
            if (playback.Value.Direction != 0 && playback.Value.GetDirection(NetworkManager.ServerTime.Time) == 0)
            {
                playback.Value = new ModelPlaybackState(ExplosionAmount, 0, NetworkManager.ServerTime.Time, playback.Value.Duration);
            }
            if (NetworkObject.IsOwnedByServer)
            {
                return;
            }
            if (Quaternion.Angle(lastRotation, transform.rotation) > 0.01f)
            {
                lastRotation = transform.rotation;
                lastActivityAt = Time.unscaledTime;
            }
            // 종료 요청 누락·거리 이탈·대기 전환에 대비합니다. 회전 없이 30초가 지나도 조작권을 회수합니다.
            if (!CanManipulate(OwnerClientId) || Time.unscaledTime - lastActivityAt > 30f)
            {
                ReclaimControl(transform.rotation);
            }
        }
    }
}
