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
        // 참가자 네트워크 정보는 대기 중에도 유지하지만, 화면 캐릭터는 시작 후에만 만듭니다.
        private PlayerAvatarView avatarView;
        private bool presentationVisible;
        private int spawnIndex;
        private RoomHubResult initialParticipant;
        private readonly NetworkVariable<byte> connectionHealth = new NetworkVariable<byte>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );
        private double heartbeatReceivedAt;
        private bool heartbeatReceived;
        private float nextHeartbeatAt;

        public ParticipantConnectionState ConnectionState => (ParticipantConnectionState)connectionHealth.Value;

        public string ConnectionStatusLabel => ParticipantConnectionHealth.Label(ConnectionState);


        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        // 서버가 마지막 수신 시각을 기록해 참가자의 연결 상태를 판단하도록 합니다.
        public void HeartbeatRpc()
        {
            if (!IsServer || !IsSpawned)
            {
                return;
            }

            heartbeatReceivedAt = Time.realtimeSinceStartupAsDouble;
            heartbeatReceived = true;
        }


        public void SampleConnectionHealthServer(ulong milliseconds)
        {
            if (!IsServer || !IsSpawned)
            {
                return;
            }

            SetLatencyServer(milliseconds);
            connectionHealth.Value = (byte)ParticipantConnectionHealth.Evaluate(
                Time.realtimeSinceStartupAsDouble - heartbeatReceivedAt,
                heartbeatReceived,
                RoundTripMilliseconds
            );
        }


        private readonly NetworkVariable<int> roundTripMilliseconds = new NetworkVariable<int>(
            -1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public int RoundTripMilliseconds => roundTripMilliseconds.Value;

        public string LatencyLabel => RoundTripMilliseconds < 0 ? "측정 중" : RoundTripMilliseconds + " ms";


        public void SetLatencyServer(ulong milliseconds)
        {
            if (!IsServer || !IsSpawned || milliseconds == 0)
            {
                return;
            }

            roundTripMilliseconds.Value = milliseconds > int.MaxValue ? int.MaxValue : (int)milliseconds;
        }


        public Vector3 ServerPosition => position.Value;

        public string UserId => userId.Value.ToString();

        public string UserName => userName.Value.ToString();


        private readonly NetworkVariable<FixedString64Bytes> roomMasterId = new NetworkVariable<FixedString64Bytes>();

        public string RoomMasterUserId => roomMasterId.Value.ToString();

        public bool IsRoomMaster => !string.IsNullOrEmpty(UserId) && UserId == RoomMasterUserId;


        private readonly NetworkVariable<bool> roomEntryLocked = new NetworkVariable<bool>();

        public bool RoomEntryLocked => roomEntryLocked.Value;


        private readonly NetworkVariable<bool> ready = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public bool IsReady => ready.Value;


        private readonly NetworkVariable<FixedString512Bytes> roomName = new NetworkVariable<FixedString512Bytes>();
        private readonly NetworkVariable<int> roomCapacity = new NetworkVariable<int>();

        public string RoomName => roomName.Value.ToString();

        public int RoomCapacity => roomCapacity.Value;


        // 교육과정의 카탈로그는 참가자 모두 가지고 있으므로 노드 ID만 동기화합니다.
        private readonly NetworkVariable<int> roomContentNodeId = new();

        public int RoomContentNodeId => roomContentNodeId.Value;


        public void SetRoomContentServer(int nodeId)
        {
            if (!IsServer || !IsSpawned || roomContentNodeId.Value == nodeId)
            {
                return;
            }

            ready.Value = false;
            GetComponent<TrainingProgress>()?.ClearServer();
            roomContentNodeId.Value = nodeId;
        }


        private readonly NetworkVariable<bool> roomRunning = new NetworkVariable<bool>();
        private readonly NetworkVariable<bool> phasePending = new NetworkVariable<bool>();
        private readonly NetworkVariable<int> phaseReply = new NetworkVariable<int>();
        private readonly NetworkVariable<FixedString512Bytes> phaseMessage = new NetworkVariable<FixedString512Bytes>();

        public bool RoomIsRunning => roomRunning.Value;

        public bool PhasePending => phasePending.Value;

        public int PhaseReply => phaseReply.Value;

        public string PhaseMessage => phaseMessage.Value.ToString();


        private Action<RoomPlayer, bool> phaseHandler;

        public void SetPhaseHandlerServer(Action<RoomPlayer, bool> handler)
        {
            phaseHandler = handler;
        }


        public void SetRoomPhaseServer(bool running, bool pending, bool resetReady = false, string contentType = "")
        {
            if (!IsServer || !IsSpawned)
            {
                return;
            }

            if (!roomRunning.Value && running)
            {
                GetComponent<TrainingProgress>()?.BeginServer(contentType);
            }

            if (roomRunning.Value != running)
            {
                // 대기 복귀와 새 시작에서 이전 입력과 좌표를 다음 훈련에 넘기지 않습니다.
                input = Vector2.zero;
                inputReceivedAt = 0;
                position.Value = SpawnPosition(spawnIndex);
                transform.position = position.Value;
            }

            roomRunning.Value = running;
            phasePending.Value = pending;
            if (resetReady)
            {
                ready.Value = false;
            }
        }


        public void ReplyPhaseServer(string message)
        {
            if (!IsServer || !IsSpawned)
            {
                return;
            }

            phaseMessage.Value = new FixedString512Bytes(message ?? "");
            phaseReply.Value++;
        }


        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        public void RequestPhaseRpc(bool running)
        {
            if (IsServer && IsSpawned)
            {
                phaseHandler?.Invoke(this, running);
            }
        }


        public void SetRoomSettingsServer(string name, int capacity)
        {
            if (IsServer && IsSpawned)
            {
                roomName.Value = new FixedString512Bytes(name ?? "");
                roomCapacity.Value = capacity;
            }
        }


        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
        // 현재 소유자가 준비 상태를 요청합니다. 진행 중이거나 상태 전환 중이면 바꾸지 않습니다.
        public void SetReadyRpc(bool value, int expectedContentNodeId)
        {
            // 콘텐츠 변경 전에 보낸 준비 요청이 늦게 도착해 새 콘텐츠의 준비를 대신하지 못하게 합니다.
            if (IsServer &&
                IsSpawned &&
                !IsRoomMaster &&
                !RoomIsRunning &&
                !PhasePending &&
                expectedContentNodeId == RoomContentNodeId)
            {
                ready.Value = value;
            }
        }


        public void SetRoomEntryLockServer(bool value)
        {
            if (IsServer && IsSpawned)
            {
                roomEntryLocked.Value = value;
            }
        }


        public void SetRoomMasterServer(string masterId)
        {
            if (IsServer && IsSpawned)
            {
                string nextMaster = masterId ?? "";
                bool roleChanged = IsRoomMaster != (!string.IsNullOrEmpty(UserId) && UserId == nextMaster);
                roomMasterId.Value = new FixedString64Bytes(nextMaster);
                if (roleChanged)
                {
                    // 이전 방장이 일반 참가자가 되면 직접 준비해야 하고, 새 방장은 준비 대상에서 빠집니다.
                    ready.Value = false;
                }
            }
        }


        // ASP.NET이 참가를 확정한 뒤 RoomDedicatedServer에서만 호출합니다.
        public void InitializeServer(RoomHubResult participant)
        {
            initialParticipant = participant;
            spawnIndex = participant.roomIndex;
            transform.position = SpawnPosition(participant.roomIndex);
        }


        private static Vector3 SpawnPosition(int index) => new Vector3((index % 4 - 1.5f) * 3, 1, (index / 4 - 0.5f) * 3);

        public override void OnNetworkSpawn()
        {
            heartbeatReceivedAt = Time.realtimeSinceStartupAsDouble;
            heartbeatReceived = false;
            nextHeartbeatAt = 0;
            if (IsServer && initialParticipant != null)
            {
                userId.Value = new FixedString64Bytes(initialParticipant.userId);
                userName.Value = new FixedString128Bytes(initialParticipant.userName);
                position.Value = SpawnPosition(initialParticipant.roomIndex);
                ready.Value = false;
                initialParticipant = null;
            }

            transform.position = position.Value;
            if (IsServer)
            {
                NetworkManager.NetworkTickSystem.Tick += ServerTick;
            }

            // 늦게 상태를 받거나 재접속해도 현재 진행 상태에 맞춰 캐릭터를 표시합니다.
            roomRunning.OnValueChanged += OnRoomRunningChanged;
            RefreshVisual();
        }


        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager.NetworkTickSystem != null)
            {
                NetworkManager.NetworkTickSystem.Tick -= ServerTick;
            }

            roomRunning.OnValueChanged -= OnRoomRunningChanged;
            RemoveVisual();
            presentationVisible = false;
            input = Vector2.zero;
        }


        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner, Delivery = RpcDelivery.Unreliable)]
        // 소유자의 방향 입력만 받아 비정상 수치와 과도한 입력 크기를 검사합니다. 최종 위치는 서버가 계산합니다.
        public void MoveRpc(Vector2 direction)
        {
            if (!IsServer || !IsSpawned || !RoomIsRunning || PhasePending || !Finite(direction.x) || !Finite(direction.y))
            {
                return;
            }

            input = Vector2.ClampMagnitude(direction, 1);
            inputReceivedAt = Time.realtimeSinceStartupAsDouble;
        }


        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        // 서버 Tick마다 위치를 갱신합니다. 오래된 입력은 중지하고 이동 영역을 제한합니다.
        private void ServerTick()
        {
            if (!IsSpawned || !RoomIsRunning || PhasePending)
            {
                input = Vector2.zero;
                return;
            }

            if (Time.realtimeSinceStartupAsDouble - inputReceivedAt > InputTimeout)
            {
                input = Vector2.zero;
            }

            var next = position.Value + new Vector3(input.x, 0, input.y) * (MoveSpeed / NetworkManager.NetworkConfig.TickRate);
            next.x = Mathf.Clamp(next.x, -ArenaHalfSize, ArenaHalfSize);
            next.z = Mathf.Clamp(next.z, -ArenaHalfSize, ArenaHalfSize);
            position.Value = next;
            transform.position = next;
        }


        private void Update()
        {
            if (IsSpawned && IsClient && IsOwner && Time.unscaledTime >= nextHeartbeatAt)
            {
                nextHeartbeatAt = Time.unscaledTime + 1f;
                HeartbeatRpc();
            }

            if (IsSpawned && IsClient)
            {
                RefreshVisual();
            }

            if (IsSpawned && !IsServer)
            {
                transform.position = Vector3.Lerp(
                    transform.position,
                    position.Value,
                    1 - Mathf.Exp(-16 * Time.unscaledDeltaTime)
                );
            }
        }


        private void OnRoomRunningChanged(bool previous, bool current)
        {
            RefreshVisual();
        }


        // 콘텐츠 수명주기 측이 표시 가능 여부를 전달합니다. 네트워크 참가자는 특정 씬의 상태를 조회하지 않습니다.
        public void SetPresentationVisible(bool visible)
        {
            if (presentationVisible == visible)
            {
                return;
            }

            presentationVisible = visible;
            RefreshVisual();
        }


        private void RefreshVisual()
        {
#if !UNITY_SERVER
            if (IsSpawned && IsClient && RoomIsRunning && presentationVisible)
            {
                avatarView ??= new PlayerAvatarView(transform);
                if (!avatarView.HasVisual)
                {
                    transform.position = position.Value;
                    avatarView.Show(IsOwner);
                }
            }
            else
            {
                RemoveVisual();
            }
#endif
        }


        private void RemoveVisual()
        {
            avatarView?.Hide();
        }


        public override void OnDestroy()
        {
            roomRunning.OnValueChanged -= OnRoomRunningChanged;
            avatarView?.Dispose();
            base.OnDestroy();
        }
    }
}
