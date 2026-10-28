using System;
using Unity.Netcode;
using UnityEngine;

namespace SmartTraining
{
    // 전용 서버의 영상 재생 상태입니다. 권한 판단은 요청자가 보낸 사용자 ID가 아닌 실제 연결의 RoomPlayer를 사용합니다.
    [RequireComponent(typeof(NetworkObject))]
    public sealed class SharedVideo : NetworkBehaviour
    {
        public const string ResourcePath = "SmartTraining/SharedVideo";
        private readonly NetworkVariable<VideoPlaybackState> playback = new();

        public double Duration => playback.Value.Duration;
        public double Position => IsSpawned ? playback.Value.Evaluate(NetworkManager.ServerTime.Time) : 0;
        public bool IsPlaying => IsSpawned && playback.Value.IsPlaying(NetworkManager.ServerTime.Time);

        public static GameObject Register(NetworkManager manager)
        {
            var prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab == null || prefab.GetComponent<SharedVideo>() == null)
            {
                throw new InvalidOperationException("SharedVideo 네트워크 프리팹을 준비해주세요.");
            }
            manager.AddNetworkPrefab(prefab);
            return prefab;
        }

        public static SharedVideo Find(NetworkManager manager)
        {
            if (manager?.SpawnManager != null)
            {
                foreach (var spawned in manager.SpawnManager.SpawnedObjects.Values)
                {
                    if (spawned.TryGetComponent<SharedVideo>(out var video))
                    {
                        return video;
                    }
                }
            }
            return null;
        }

        // 재생 조작은 모든 참가자에게 허용하고, 최초 영상 길이 등록만 방장이 담당합니다.
        private bool CanControl(ulong clientId, bool masterOnly = false)
        {
            return NetworkManager.ConnectedClients.TryGetValue(clientId, out var client) &&
                client.PlayerObject != null && client.PlayerObject.TryGetComponent<RoomPlayer>(out var player) &&
                player.RoomIsRunning && !player.PhasePending && (!masterOnly || player.IsRoomMaster);
        }

        // 서버는 영상 디코더를 실행하지 않습니다. 방장이 준비한 클립의 길이를 최초 한 번만 등록합니다.
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void ConfigureRpc(double duration, RpcParams rpcParams = default)
        {
            if (!IsServer || !IsSpawned || !CanControl(rpcParams.Receive.SenderClientId, masterOnly: true) || Duration > 0 ||
                double.IsNaN(duration) || double.IsInfinity(duration) || duration < 0.1 || duration > 21600)
            {
                return;
            }
            playback.Value = new VideoPlaybackState(0, NetworkManager.ServerTime.Time, duration, false);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SetPlayingRpc(bool playing, RpcParams rpcParams = default)
        {
            if (!IsServer || !IsSpawned || Duration <= 0 || !CanControl(rpcParams.Receive.SenderClientId))
            {
                return;
            }
            double position = Position;
            // 영상이 끝난 뒤 재생을 누르면 처음부터 시작합니다.
            if (playing && position >= Duration)
            {
                position = 0;
            }
            playback.Value = new VideoPlaybackState(position, NetworkManager.ServerTime.Time, Duration, playing);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SeekRpc(double position, RpcParams rpcParams = default)
        {
            if (!IsServer || !IsSpawned || Duration <= 0 || !CanControl(rpcParams.Receive.SenderClientId) ||
                double.IsNaN(position) || double.IsInfinity(position))
            {
                return;
            }
            SetPosition(position);
        }

        // 현재 위치는 서버에서 계산합니다. 여러 참가자의 연속 요청도 서버가 처리한 순서대로 누적됩니다.
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void SkipRpc(int seconds, RpcParams rpcParams = default)
        {
            if (!IsServer || !IsSpawned || Duration <= 0 || !CanControl(rpcParams.Receive.SenderClientId) ||
                (seconds != -5 && seconds != 5))
            {
                return;
            }
            SetPosition(Position + seconds);
        }

        // 정지는 일시정지와 달리 재생 위치까지 0초로 되돌립니다. 하나의 상태 변경으로 공유합니다.
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void StopRpc(RpcParams rpcParams = default)
        {
            if (!IsServer || !IsSpawned || Duration <= 0 || !CanControl(rpcParams.Receive.SenderClientId))
            {
                return;
            }
            playback.Value = new VideoPlaybackState(0, NetworkManager.ServerTime.Time, Duration, false);
        }

        // 절대 위치 탐색과 5초 이동에 같은 범위 검사·재생 상태 유지 규칙을 적용합니다.
        private void SetPosition(double position)
        {
            bool playing = IsPlaying;
            position = Math.Clamp(position, 0, Duration);
            playback.Value = new VideoPlaybackState(position, NetworkManager.ServerTime.Time, Duration, playing && position < Duration);
        }

        private void Update()
        {
            if (IsServer && IsSpawned && playback.Value.Playing && !IsPlaying)
            {
                playback.Value = new VideoPlaybackState(Duration, NetworkManager.ServerTime.Time, Duration, false);
            }
        }
    }
}
