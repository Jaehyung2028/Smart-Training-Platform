using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace SmartTraining
{
    public sealed class RoomNetworkClient : IDisposable
    {
        private GameObject root;
        private NetworkManager manager;
        private bool failed;
        public bool IsConnected => manager != null && manager.IsConnectedClient;
        public bool WasDisconnected { get; private set; }
        public string DisconnectReason { get; private set; }
        private float nextInputAt;
        public RoomPlayer LocalPlayer => manager != null && manager.IsConnectedClient && manager.LocalClient?.PlayerObject != null
            ? manager.LocalClient.PlayerObject.GetComponent<RoomPlayer>() : null;
        public RoomPlayer[] Players => manager == null || manager.SpawnManager == null ? Array.Empty<RoomPlayer>() :
            manager.SpawnManager.SpawnedObjects.Values.Select(value => value.GetComponent<RoomPlayer>()).Where(value => value != null).ToArray();
        public void SendMovement(Vector2 direction)
        {
            var player = LocalPlayer;
            if (player == null || !player.IsOwner || Time.unscaledTime < nextInputAt) return;
            nextInputAt = Time.unscaledTime + 0.05f;
            player.MoveRpc(direction);
        }

        public IEnumerator Connect(string userId, JoinRoomResponse reservation, Action success, Action<string> failure)
        {
            if (manager != null) { failure("기존 방 연결을 먼저 종료해주세요."); yield break; }
            if (reservation?.room == null || string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(reservation.admissionToken) ||
                reservation.room.port < 1 || reservation.room.port > 65535 || string.IsNullOrEmpty(reservation.room.ipAddress) ||
                !DateTimeOffset.TryParse(reservation.expiresAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var expires) ||
                expires <= DateTimeOffset.UtcNow)
            { failure("유효한 접속 예약이 없습니다. 다시 참가해주세요."); yield break; }
            string address = reservation.room.ipAddress;
            if (!IPAddress.TryParse(address, out _))
            {
                var lookup = Dns.GetHostAddressesAsync(address);
                double deadline = Time.realtimeSinceStartupAsDouble + 5;
                while (!lookup.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                if (!lookup.IsCompleted || lookup.IsFaulted || lookup.IsCanceled)
                { failure("방 서버 주소를 확인하지 못했습니다."); yield break; }
                var resolved = lookup.Result.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork) ?? lookup.Result.FirstOrDefault();
                if (resolved == null) { failure("방 서버 주소를 확인하지 못했습니다."); yield break; }
                address = resolved.ToString();
            }
            WasDisconnected = false; DisconnectReason = null; failed = false;
            root = new GameObject("SmartTrainingNetworkClient");
            manager = root.AddComponent<NetworkManager>();
            var transport = root.AddComponent<UnityTransport>();
            transport.SetConnectionData(address, (ushort)reservation.room.port);
            transport.ConnectTimeoutMS = 1000; transport.MaxConnectAttempts = 8;
            transport.HeartbeatTimeoutMS = 1000; transport.DisconnectTimeoutMS = 10000;
            manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport, EnableSceneManagement = false, ConnectionApproval = true,
                ConnectionData = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new RoomConnectionData
                { roomId = reservation.room.id, userId = userId, admissionToken = reservation.admissionToken }))
            };
            string prefabError = null;
            try { RoomPlayerPrefab.Register(manager); }
            catch (InvalidOperationException error) { prefabError = error.Message; }
            if (prefabError != null) { Dispose(); failure(prefabError); yield break; }
            manager.OnClientDisconnectCallback += OnDisconnected;
            manager.OnTransportFailure += OnTransportFailure;
            if (!manager.StartClient()) { Dispose(); failure("방 연결을 시작하지 못했습니다."); yield break; }
            double limit = Time.realtimeSinceStartupAsDouble + Math.Min(15, Math.Max(0, (expires - DateTimeOffset.UtcNow).TotalSeconds));
            while (!IsConnected && !failed && Time.realtimeSinceStartupAsDouble < limit) yield return null;
            if (IsConnected) success();
            else { Dispose(); failure("방 서버에 접속하지 못했습니다. 예약을 정리하고 다시 참가해주세요."); }
        }
        private void OnDisconnected(ulong id) { failed = true; WasDisconnected = true; DisconnectReason = manager?.DisconnectReason; }
        private void OnTransportFailure() { failed = true; WasDisconnected = true; }
        public void Dispose()
        {
            if (manager != null)
            {
                manager.OnClientDisconnectCallback -= OnDisconnected;
                manager.OnTransportFailure -= OnTransportFailure;
                manager.NetworkConfig.ConnectionData = Array.Empty<byte>();
                root.AddComponent<NetworkShutdownCleanup>().Begin(manager);
            }
            else if (root != null) UnityEngine.Object.Destroy(root);
            manager = null; root = null; WasDisconnected = false; DisconnectReason = null; nextInputAt = 0;
        }
    }
}

