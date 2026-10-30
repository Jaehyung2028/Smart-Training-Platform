using System;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace SmartTraining
{
    [RequireComponent(typeof(NetworkObject), typeof(NetworkTransform), typeof(NetworkRigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    public sealed class RoomPlayer : NetworkBehaviour
    {
        [SerializeField, Tooltip("RoomPlayer 하위에 배치한 표시 전용 모델입니다.")]
        private Transform avatarRoot;
        [SerializeField]
        private Animator avatarAnimator;
        [SerializeField, Tooltip("콘텐츠에서 다른 참가자의 머리 위에 표시할 이름표 프리팹입니다.")]
        private WorldNameTagReferences nameTagPrefab;

        internal Transform AvatarRoot => avatarRoot;


        private void Awake()
        {
            networkTransform = GetComponent<NetworkTransform>();
            body = GetComponent<Rigidbody>();
            // 전용 서버·대기실·본인 1인칭에서 모델이 먼저 노출되지 않도록 합니다.
            if (avatarRoot != null)
            {
                avatarRoot.gameObject.SetActive(false);
            }
        }


        public const float MoveSpeed = 4f;
        public const float ArenaHalfSize = 12f;
        public const float InputTimeout = 0.3f;
        private NetworkTransform networkTransform;
        private Rigidbody body;
        private float inputYaw;

        // 카메라가 전달한 로컬 입력입니다. 위치·회전 결과의 공유는 NetworkTransform이 담당합니다.
        public float ViewYaw { get; private set; }


        private readonly NetworkVariable<FixedString64Bytes> userId = new NetworkVariable<FixedString64Bytes>();
        private readonly NetworkVariable<FixedString128Bytes> userName = new NetworkVariable<FixedString128Bytes>();
        private Camera presentationCamera;
        private string presentationDetail = "";
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


        // 서버에서는 판정에 사용하는 원본 좌표이고, 클라이언트에서는 NetworkTransform이 보간한 좌표입니다.
        public Vector3 Position => IsServer ? body.position : transform.position;


        private FixedString64Bytes cachedUserIdValue;
        private string cachedUserId = "";

        public string UserId
        {
            get
            {
                var value = userId.Value;
                if (!value.Equals(cachedUserIdValue))
                {
                    cachedUserIdValue = value;
                    cachedUserId = value.ToString();
                }

                return cachedUserId;
            }
        }


        private FixedString128Bytes cachedUserNameValue;
        private string cachedUserName = "";

        public string UserName
        {
            get
            {
                var value = userName.Value;
                if (!value.Equals(cachedUserNameValue))
                {
                    cachedUserNameValue = value;
                    cachedUserName = value.ToString();
                }

                return cachedUserName;
            }
        }


        private readonly NetworkVariable<FixedString64Bytes> roomMasterId = new NetworkVariable<FixedString64Bytes>();
        private FixedString64Bytes cachedRoomMasterUserIdValue;
        private string cachedRoomMasterUserId = "";

        public string RoomMasterUserId
        {
            get
            {
                var value = roomMasterId.Value;
                if (!value.Equals(cachedRoomMasterUserIdValue))
                {
                    cachedRoomMasterUserIdValue = value;
                    cachedRoomMasterUserId = value.ToString();
                }

                return cachedRoomMasterUserId;
            }
        }


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
                inputYaw = 0;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                networkTransform.Teleport(SpawnPosition(spawnIndex), Quaternion.identity, transform.localScale);
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
            body.position = SpawnPosition(participant.roomIndex);
            body.rotation = Quaternion.identity;
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
                ready.Value = false;
                initialParticipant = null;
            }

            ViewYaw = transform.eulerAngles.y;
            if (IsServer)
            {
                // 기본 NetworkTransform의 소유자 SetState 요청으로 이동 검사를 우회하지 못하게 합니다.
                // 실제 좌표 변경은 서버 물리 계산과 Teleport에서만 확정합니다.
                networkTransform.OnClientRequestChange = (_, _, _) => (body.position, body.rotation, transform.localScale);
            }

            // 늦게 상태를 받거나 재접속해도 현재 진행 상태에 맞춰 캐릭터를 표시합니다.
            roomRunning.OnValueChanged += OnRoomRunningChanged;
            RefreshVisual();
        }


        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                networkTransform.OnClientRequestChange = null;
            }

            roomRunning.OnValueChanged -= OnRoomRunningChanged;
            RemoveVisual();
            presentationVisible = false;
            input = Vector2.zero;
        }


        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner, Delivery = RpcDelivery.Unreliable)]
        // 방향·시선 입력을 한 요청으로 받습니다. 서버가 검사·이동하고 NetworkTransform이 결과를 공유합니다.
        public void MoveRpc(Vector2 direction, float viewYaw)
        {
            if (!IsServer ||
                !IsSpawned ||
                !RoomIsRunning ||
                PhasePending ||
                !Finite(direction.x) ||
                !Finite(direction.y) ||
                !Finite(viewYaw))
            {
                return;
            }

            input = Vector2.ClampMagnitude(direction, 1);
            inputYaw = Mathf.Repeat(viewYaw, 360);
            inputReceivedAt = Time.realtimeSinceStartupAsDouble;
        }


        // 카메라나 씬을 조회하지 않고 수평 시선 입력만 보관합니다. 기존 이동 요청에 함께 담아 보냅니다.
        public void SetViewYaw(float yaw)
        {
            if (!IsSpawned || !IsOwner || !RoomIsRunning || PhasePending || !Finite(yaw))
            {
                return;
            }

            ViewYaw = Mathf.Repeat(yaw, 360);
        }


        private void LateUpdate()
        {
            if (!IsSpawned)
            {
                return;
            }

            avatarView?.Refresh(UserName, presentationCamera, presentationDetail, Time.unscaledDeltaTime);
        }


        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        // 물리 계산은 서버의 FixedUpdate에서만 합니다. 벽·바닥 충돌은 Rigidbody와 Collider가 판정합니다.
        // NetworkRigidbody가 다른 클라이언트의 물리 계산을 끄고 NetworkTransform이 결과를 전달합니다.
        private void FixedUpdate()
        {
            if (!IsSpawned || !IsServer || body.isKinematic)
            {
                return;
            }

            bool canMove = RoomIsRunning && !PhasePending && Time.realtimeSinceStartupAsDouble - inputReceivedAt <= InputTimeout;
            var direction = canMove ? input : Vector2.zero;
            var velocity = new Vector3(direction.x * MoveSpeed, body.linearVelocity.y, direction.y * MoveSpeed);
            // 기존 훈련 영역 제한도 물리 속도로 적용합니다. Transform을 직접 옮겨 충돌을 건너뛰지 않습니다.
            var next = body.position + velocity * Time.fixedDeltaTime;
            velocity.x = (Mathf.Clamp(next.x, -ArenaHalfSize, ArenaHalfSize) - body.position.x) / Time.fixedDeltaTime;
            velocity.z = (Mathf.Clamp(next.z, -ArenaHalfSize, ArenaHalfSize) - body.position.z) / Time.fixedDeltaTime;
            body.linearVelocity = velocity;
            body.MoveRotation(Quaternion.Euler(0, inputYaw, 0));
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
        }


        private void OnRoomRunningChanged(bool previous, bool current)
        {
            ViewYaw = 0;
            RefreshVisual();
        }


        // 콘텐츠 수명주기 측이 표시 가능 여부를 전달합니다. 네트워크 참가자는 특정 씬의 상태를 조회하지 않습니다.
        public void SetPresentationVisible(bool visible, Camera viewCamera = null, string detail = "")
        {
            presentationCamera = visible ? viewCamera : null;
            presentationDetail = visible ? detail : "";
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
                avatarView ??= new PlayerAvatarView(transform, avatarRoot, avatarAnimator, nameTagPrefab);
                if (!avatarView.HasVisual)
                {
                    avatarView.Show(IsOwner);
                    avatarView.Refresh(UserName, presentationCamera, presentationDetail);
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
