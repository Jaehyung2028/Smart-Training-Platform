using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace SmartTraining.Editor
{
    public static class NetcodeLoopbackChecks
    {
        private static void Check(bool value, string label)
        { if (!value) throw new Exception(label); Debug.Log("UNITY PASS: " + label); }
        public static IEnumerator Run()
        {
            ushort port;
            using (var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
                port = (ushort)((IPEndPoint)socket.Client.LocalEndPoint).Port;
            var root = new GameObject("TestNetcodeServer");
            var server = root.AddComponent<NetworkManager>();
            var transport = root.AddComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", port, "127.0.0.1");
            server.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false, ConnectionApproval = true };
            RoomPlayerPrefab.Register(server);
            bool approvedPayload = false, reject = false;
            server.ConnectionApprovalCallback = (request, response) =>
            {
                var data = JsonUtility.FromJson<RoomConnectionData>(System.Text.Encoding.UTF8.GetString(request.Payload));
                approvedPayload = data.roomId == "netcode-test" && data.userId == "test-user" && data.admissionToken == "test-admission";
                response.Approved = approvedPayload && !reject; response.CreatePlayerObject = false; response.Pending = false;
                if (!response.Approved) response.Reason = "test-rejected";
            };
            var client = new RoomNetworkClient();
            string runtimeError = null;
            Application.LogCallback log = (message, trace, kind) => { if (kind == LogType.Exception || kind == LogType.Error) runtimeError = message; };
            Application.logMessageReceived += log;
            try
            {
                Check(server.StartServer(), "local Netcode server starts");
                var reservation = new JoinRoomResponse
                {
                    success = true, admissionToken = "test-admission", expiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(30).ToString("O"),
                    room = new ReservedRoom { id = "netcode-test", lobbyId = "test-lobby", ipAddress = "127.0.0.1", port = port }
                };
                yield return client.Connect("test-user", reservation, () => { }, message => throw new Exception(message));
                Check(client.IsConnected && approvedPayload, "Netcode UDP connects with admission payload");
                client.Dispose(); yield return null; yield return null;
                Check(!client.IsConnected, "Netcode disconnect clears client");
                reservation.expiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1).ToString("O");
                bool expiredRejected = false;
                yield return client.Connect("test-user", reservation, () => throw new Exception("Expired reservation connected"), _ => expiredRejected = true);
                Check(expiredRejected && !client.IsConnected, "expired reservation rejected before Netcode start");
                reject = true; reservation.expiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(30).ToString("O");
                bool approvalRejected = false;
                yield return client.Connect("test-user", reservation, () => throw new Exception("Rejected connection accepted"), _ => approvalRejected = true);
                Check(approvalRejected && !client.IsConnected, "Netcode approval rejection cleans client");
                Check(runtimeError == null, "Netcode lifecycle has no runtime error");
            }
            finally { Application.logMessageReceived -= log; client.Dispose(); root.AddComponent<NetworkShutdownCleanup>().Begin(server); }
            yield return null;
        }
    }
}
