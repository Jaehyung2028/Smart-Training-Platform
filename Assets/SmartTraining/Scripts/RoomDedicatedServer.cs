using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace SmartTraining
{
    public sealed class RoomDedicatedServer : MonoBehaviour
    {
        private readonly Dictionary<ulong, RoomConnectionData> clients = new Dictionary<ulong, RoomConnectionData>();
        private readonly HashSet<string> claimedUsers = new HashSet<string>();
        private NetworkManager manager;
        private GameObject playerPrefab;
        private RoomHubConnection hub;
        private string roomId;
        private string masterUserId = "";
        private bool entryLocked;
        private bool stopping, registered;
        private IEnumerator Start()
        {
            Application.runInBackground = true;
            var args = Environment.GetCommandLineArgs();
            roomId = Argument(args, "-Id");
            var token = Environment.GetEnvironmentVariable("SMART_TRAINING_SERVER_TOKEN");
            var apiUrl = Argument(args, "-apiUrl");
            if (string.IsNullOrEmpty(roomId) || string.IsNullOrEmpty(token) ||
                !ushort.TryParse(Argument(args, "-port"), out var port) || port == 0 ||
                !Uri.TryCreate(apiUrl, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
            { StopServer("방 서버 실행 설정이 올바르지 않습니다."); yield break; }

            var root = new GameObject("SmartTrainingRoomNetwork");
            manager = root.AddComponent<NetworkManager>();
            var transport = root.AddComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");
            transport.HeartbeatTimeoutMS = 1000; transport.DisconnectTimeoutMS = 10000;
            manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false,
                ConnectionApproval = true, ClientConnectionBufferTimeout = 20 };
            string prefabError = null;
            try { playerPrefab = RoomPlayerPrefab.Register(manager); }
            catch (InvalidOperationException error) { prefabError = error.Message; }
            if (prefabError != null) { StopServer(prefabError); yield break; }
            manager.ConnectionApprovalCallback += Approve;
            manager.OnClientConnectedCallback += Connected;
            manager.OnClientDisconnectCallback += Disconnected;
            manager.OnTransportFailure += TransportFailed;
            if (!manager.StartServer()) { StopServer("방 서버 포트를 열지 못했습니다."); yield break; }
            hub = new RoomHubConnection(); hub.DisconnectUser += DisconnectUser;
            hub.KickUser += KickUser;
            var connect = hub.Connect(apiUrl);
            yield return Await(connect);
            if (!Succeeded(connect)) yield break;
            var register = hub.Invoke("Register", roomId, token);
            token = null;
            yield return Await(register);
            if (!Succeeded(register)) yield break;
            registered = true; Debug.Log("ROOM SERVER READY");
            StartCoroutine(SynchronizeRoomMaster());
        }
        private IEnumerator SynchronizeRoomMaster()
        {
            while (!stopping)
            {
                var query = hub.Invoke("GetRoomMaster");
                yield return Await(query);
                if (!Succeeded(query)) yield break;
                if (!query.Result.success) { StopServer("방장 정보를 조회하지 못했습니다."); yield break; }
                masterUserId = query.Result.masterUserId ?? "";
                entryLocked = query.Result.isEntryLocked;
                foreach (var connection in manager.ConnectedClients.Values)
                    if (connection.PlayerObject != null)
                    {
                        connection.PlayerObject.GetComponent<RoomPlayer>().SetRoomMasterServer(masterUserId);
                        connection.PlayerObject.GetComponent<RoomPlayer>().SetRoomEntryLockServer(entryLocked);
                    }
                yield return new WaitForSecondsRealtime(1);
            }
        }
        private static string Argument(string[] args, string name)
        {
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1];
            return null;
        }
        private void Update()
        {
            if (stopping || hub == null) return;
            hub.Tick();
            if (hub.Failure != null) StopServer(hub.Failure);
        }
        private IEnumerator Await(Task task)
        { while (!task.IsCompleted && !stopping) yield return null; }
        private bool Succeeded(Task task)
        {
            if (!stopping && task.IsCompletedSuccessfully) return true;
            if (!stopping)
            {
                var cause = task.Exception?.GetBaseException();
                StopServer("ASP.NET 방 관리 서버와 통신하지 못했습니다. " +
                    (cause == null ? "요청이 취소되었습니다." : cause.GetType().Name + ": " + cause.Message));
            }
            return false;
        }
        private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.CreatePlayerObject = false; response.Approved = false;
            RoomConnectionData data = null;
            try { if (request.Payload != null && request.Payload.Length <= 2048) data = JsonUtility.FromJson<RoomConnectionData>(Encoding.UTF8.GetString(request.Payload)); }
            catch (Exception) { }
            if (!registered || data == null || data.roomId != roomId || string.IsNullOrEmpty(data.userId) ||
                string.IsNullOrEmpty(data.admissionToken) || !claimedUsers.Add(data.userId))
            { response.Reason = "유효한 방 참가 예약이 필요합니다."; return; }
            clients.Add(request.ClientNetworkId, data);
            response.Pending = true;
            StartCoroutine(Validate(request.ClientNetworkId, data, response));
        }
        private IEnumerator Validate(ulong id, RoomConnectionData data, NetworkManager.ConnectionApprovalResponse response)
        {
            var validation = hub.Invoke("ValidateConnection", data.userId, data.admissionToken);
            yield return Await(validation);
            if (stopping) yield break;
            bool approved = validation.IsCompletedSuccessfully && validation.Result.success && clients.ContainsKey(id);
            response.Approved = approved; response.Pending = false;
            if (!approved)
            {
                response.Reason = "접속 예약이 만료되었거나 유효하지 않습니다.";
                RemoveClient(id);
            }
        }
        private void Connected(ulong id)
        { if (clients.TryGetValue(id, out var data)) StartCoroutine(Confirm(id, data)); }
        private IEnumerator Confirm(ulong id, RoomConnectionData data)
        {
            var confirmation = hub.Invoke("ConfirmConnection", data.userId, data.admissionToken);
            yield return Await(confirmation);
            if (stopping) yield break;
            if (!confirmation.IsCompletedSuccessfully || !confirmation.Result.success)
            { manager.DisconnectClient(id); yield break; }
            // The connection may have closed while ASP.NET was confirming it.
            if (!clients.ContainsKey(id) || !manager.ConnectedClients.TryGetValue(id, out var client) || client.PlayerObject != null) yield break;
            var participant = confirmation.Result;
            if (participant.userId != data.userId || string.IsNullOrEmpty(participant.userName) || participant.roomIndex < 0)
            { manager.DisconnectClient(id); yield break; }
            var player = Instantiate(playerPrefab).GetComponent<RoomPlayer>();
            player.InitializeServer(participant);
            player.NetworkObject.SpawnAsPlayerObject(id);
            player.SetRoomMasterServer(masterUserId);
            player.SetRoomEntryLockServer(entryLocked);
        }
        private void Disconnected(ulong id)
        {
            var data = RemoveClient(id);
            if (!stopping && data != null) StartCoroutine(NotifyDisconnected(data));
        }
        private IEnumerator NotifyDisconnected(RoomConnectionData data)
        {
            var notification = hub.Invoke("ClientDisconnected", data.userId, data.admissionToken);
            yield return Await(notification);
            // A logout may already have removed membership; a failed result is harmless here.
        }
        private RoomConnectionData RemoveClient(ulong id)
        {
            if (!clients.TryGetValue(id, out var data)) return null;
            clients.Remove(id); claimedUsers.Remove(data.userId); return data;
        }
        private void DisconnectUser(string userId, string token)
        {
            foreach (var pair in new List<KeyValuePair<ulong, RoomConnectionData>>(clients))
                if (pair.Value.userId == userId && pair.Value.admissionToken == token) manager.DisconnectClient(pair.Key);
        }
        private void TransportFailed() => StopServer("방 서버 네트워크가 종료되었습니다.");
        private void KickUser(string userId, string token)
        {
            foreach (var pair in new List<KeyValuePair<ulong, RoomConnectionData>>(clients))
                if (pair.Value.userId == userId && pair.Value.admissionToken == token)
                    manager.DisconnectClient(pair.Key, "방장에 의해 강퇴되었습니다.");
        }
        private void StopServer(string reason)
        {
            if (stopping) return;
            Debug.LogError(reason); stopping = true;
            hub?.Dispose(); manager?.Shutdown(true); Application.Quit(1);
        }
        private void OnDestroy()
        {
            stopping = true; hub?.Dispose();
            if (manager != null)
            {
                manager.ConnectionApprovalCallback -= Approve;
                manager.OnClientConnectedCallback -= Connected;
                manager.OnClientDisconnectCallback -= Disconnected;
                manager.OnTransportFailure -= TransportFailed;
                manager.Shutdown(true);
            }
        }
    }
}
