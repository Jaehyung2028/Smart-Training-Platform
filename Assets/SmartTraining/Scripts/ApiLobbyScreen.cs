using System;
using System.Collections;
using UnityEngine;

namespace SmartTraining
{
    public sealed class ApiLobbyScreen : MonoBehaviour
    {
        [SerializeField] private string serverUrl = "http://localhost:5229";
        private string nickname = "훈련사용자";
        private string lobbyName = "훈련로비";
        private string roomName = "훈련방";
        private string maxUsersText = "4";
        private string status = "서버 연결을 확인한 뒤 로그인해주세요.";
        private TrainingApiClient api;
        private LobbySummary[] lobbies = Array.Empty<LobbySummary>();
        private ContentSummary[] contents = Array.Empty<ContentSummary>();
        private RoomSummary[] rooms = Array.Empty<RoomSummary>();
        private RoomSummary selectedRoom;
        private RoomNetworkClient network;
        private string networkRoomId = "", networkLobbyId = "";
        private string selectedContentId = "";
        private string selectedLobbyId = "";
        private bool busy;
        private string reconnectRoomId = "", reconnectLobbyId = "";
        private Vector2 scroll;
        private Vector2 buttonMovement;
        private float buttonReadAt;
        private bool previousRunInBackground;
        private bool clearKeyboardFocus;

        private void OnEnable() { previousRunInBackground = Application.runInBackground; Application.runInBackground = true; }

        private void OnGUI()
        {
            if (clearKeyboardFocus) { GUI.FocusControl(null); clearKeyboardFocus = false; }
            if (network?.IsConnected == true) { DrawRoom(); return; }
            bool processing = busy || WaitingForExitCleanup;
            GUILayout.BeginArea(new Rect(20, 20, Mathf.Min(700, Screen.width - 40), Screen.height - 40), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("Smart Training — 로그인 / 로비");
            GUI.enabled = !processing && !(api?.IsLoggedIn ?? false);
            GUILayout.Label("서버 주소"); serverUrl = GUILayout.TextField(serverUrl);
            GUILayout.Label("닉네임"); nickname = GUILayout.TextField(nickname, 20);
            GUI.enabled = !processing;
            if (GUILayout.Button("서버 연결 확인")) Run(CheckHealth());
            if (!(api?.IsLoggedIn ?? false))
            {
                if (GUILayout.Button("로그인")) Run(Login());
            }
            else
            {
                GUILayout.Label("사용자: " + api.User.name);
                GUILayout.Label("현재 로비: " + (string.IsNullOrEmpty(api.User.currentLobbyId) ? "없음" : api.User.currentLobbyName));
                GUILayout.Label("현재 방: " + (string.IsNullOrEmpty(api.User.currentRoomId) ? "없음" : api.User.currentRoomName));
                if (!string.IsNullOrEmpty(api.User.pendingRoomId)) GUILayout.Label("방 접속을 예약한 상태입니다.");
                if (!string.IsNullOrEmpty(networkRoomId) || !string.IsNullOrEmpty(api.User.currentRoomId) || !string.IsNullOrEmpty(api.User.pendingRoomId))
                    if (GUILayout.Button("방 퇴장 / 접속 예약 취소")) { ForgetReconnect(); Run(LeaveRoom("방에서 나왔습니다.")); }
                if (!string.IsNullOrEmpty(reconnectRoomId))
                {
                    if (GUILayout.Button("이전 방에 다시 접속")) Run(ReconnectRoom());
                    if (GUILayout.Button("재접속 대상 지우기")) { ForgetReconnect(); status = "재접속 대상을 지웠습니다."; }
                }
                if (GUILayout.Button("내 정보 / 로비 목록 새로고침")) Run(LoadState());
                if (GUILayout.Button("로그아웃")) Run(Logout());
                GUILayout.Space(12);
                GUILayout.Label("새 로비 이름 (2~10자)"); lobbyName = GUILayout.TextField(lobbyName, 10);
                if (GUILayout.Button("로비 생성")) Run(CreateLobby());
                GUI.enabled = !processing && !string.IsNullOrEmpty(api.User.currentLobbyId);
                if (GUILayout.Button("현재 로비에서 퇴장")) Run(LeaveLobby());
                GUI.enabled = !processing;
                GUILayout.Space(12);
                GUILayout.Label("로비 목록 — 선택하면 콘텐츠를 조회합니다.");
                foreach (var lobby in lobbies)
                {
                    if (GUILayout.Button((lobby.id == selectedLobbyId ? "▶ " : "") + lobby.name +
                        "  |  사용자 " + lobby.userCount + "  ·  방 " + lobby.roomCount + "  ·  콘텐츠 " + lobby.contentCount))
                        Run(SelectLobby(lobby.id));
                }
                if (lobbies.Length == 0) GUILayout.Label("조회된 로비가 없습니다.");
                var selected = Array.Find(lobbies, item => item.id == selectedLobbyId);
                if (selected != null)
                {
                    GUILayout.Space(12);
                    GUILayout.Label("선택한 로비: " + selected.name);
                    GUI.enabled = !processing && string.IsNullOrEmpty(api.User.currentLobbyId);
                    if (GUILayout.Button("선택한 로비에 입장")) Run(JoinLobby(selectedLobbyId));
                    GUI.enabled = !processing;
                    if (!string.IsNullOrEmpty(api.User.currentLobbyId) && api.User.currentLobbyId != selectedLobbyId)
                        GUILayout.Label("다른 로비로 이동하려면 현재 로비에서 먼저 퇴장해주세요.");
                    GUILayout.Label("콘텐츠");
                    foreach (var content in contents)
                    {
                        if (GUILayout.Button((content.rootContent == selectedContentId ? "▶ " : "") + content.title))
                            selectedContentId = content.rootContent;
                        if (content.children != null)
                            foreach (var child in content.children) DrawContent(child, 1);
                    }
                    if (contents.Length == 0) GUILayout.Label("표시할 콘텐츠가 없습니다.");
                    GUILayout.Space(12);
                    GUILayout.Label("새 방 이름 (기본 2~30자)"); roomName = GUILayout.TextField(roomName, 30);
                    GUILayout.Label("최대 인원 (기본 1~8명)"); maxUsersText = GUILayout.TextField(maxUsersText, 3);
                    var contentChoice = Array.Find(contents, item => item.rootContent == selectedContentId);
                    GUILayout.Label("선택한 콘텐츠: " + (contentChoice == null ? "없음 — 위 콘텐츠를 선택해주세요." : contentChoice.title));
                    GUI.enabled = !processing && api.User.currentLobbyId == selectedLobbyId && contentChoice != null;
                    if (GUILayout.Button("선택한 콘텐츠로 방 생성")) Run(CreateRoom());
                    GUI.enabled = !processing;
                    if (api.User.currentLobbyId != selectedLobbyId) GUILayout.Label("방을 생성하려면 이 로비에 입장해주세요.");
                    GUILayout.Space(12);
                    GUILayout.Label("방 목록");
                    if (GUILayout.Button("방 목록 새로고침")) Run(LoadRooms());
                    foreach (var room in rooms)
                        if (GUILayout.Button((room.id == selectedRoom?.id ? "▶ " : "") + room.name + "  |  접속 " + room.connectedUsers +
                            "/" + room.maxUsers + "  ·  예약 " + room.reservedUsers + "  ·  남은 자리 " + room.availableSlots +
                            (room.isEntryLocked ? "  ·  입장 잠김" : "  ·  입장 가능")))
                            Run(SelectRoom(room.id));
                    if (rooms.Length == 0) GUILayout.Label("생성된 방이 없습니다.");
                    if (selectedRoom != null)
                    {
                        GUILayout.Label("선택한 방: " + selectedRoom.name + " / " + selectedRoom.contentTitle);
                        GUILayout.Label("상태: " + RoomStateLabel(selectedRoom.state));
                        GUILayout.Label("새 참가자 입장: " + (selectedRoom.isEntryLocked ? "잠김" : "허용"));
                        GUI.enabled = !processing && api.User.currentLobbyId == selectedLobbyId && string.IsNullOrEmpty(networkRoomId) &&
                            string.IsNullOrEmpty(api.User.currentRoomId) && string.IsNullOrEmpty(api.User.pendingRoomId) && selectedRoom.availableSlots > 0 && !selectedRoom.isEntryLocked;
                        if (GUILayout.Button("선택한 방에 참가")) Run(JoinRoom(selectedRoom.id));
                        GUI.enabled = !processing;
                    }
                }
            }
            GUI.enabled = true;
            GUILayout.Space(12); GUILayout.Label(WaitingForExitCleanup ? "이전 접속을 정리하는 중…" : busy ? "처리 중…" : status);
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }

        private void DrawRoom()
        {
            GUILayout.BeginArea(new Rect(20, 20, Mathf.Min(700, Screen.width - 40), Screen.height - 40), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);
            GUILayout.Label("Smart Training — " + (api?.User?.currentRoomName ?? "방 접속 중"));
            var local = network.LocalPlayer;
            var master = local == null ? null : Array.Find(network.Players, player => player.UserId == local.RoomMasterUserId);
            GUILayout.Label("방장: " + (master == null ? "확인 중…" : master.UserName + (master.IsOwner ? " (나)" : "")));
            GUILayout.Label(local != null && local.IsRoomMaster ? "내 역할: 방장" : "내 역할: 참가자");
            if (local != null) GUILayout.Label("새 참가자 입장: " + (local.RoomEntryLocked ? "잠김" : "허용"));
            GUI.enabled = !busy;
            if (local != null && local.IsRoomMaster)
            {
                if (GUILayout.Button(local.RoomEntryLocked ? "새 참가자 입장 열기" : "새 참가자 입장 잠그기"))
                    Run(SetRoomEntryLock(!local.RoomEntryLocked));
                GUILayout.Label("방장 관리 — 방장 위임 / 참가자 강퇴");
                foreach (var participant in network.Players)
                    if (participant.UserId != local.UserId && !string.IsNullOrEmpty(participant.UserId))
                    {
                        var targetId = participant.UserId;
                        var targetName = participant.UserName;
                        if (GUILayout.Button(targetName + "에게 방장 넘기기")) Run(TransferRoomMaster(targetId, targetName));
                        if (GUILayout.Button(targetName + " 강퇴")) Run(KickRoomUser(targetId, targetName));
                    }
            }
            buttonMovement = RoomMovementView.Draw(network); buttonReadAt = Time.unscaledTime;
            GUILayout.Space(12);
            if (GUILayout.Button("방에서 나가기")) { ForgetReconnect(); Run(LeaveRoom("방에서 나왔습니다.")); }
            if (GUILayout.Button("로그아웃")) Run(Logout());
            GUI.enabled = true;
            GUILayout.Label(busy ? "처리 중…" : status);
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }

        private static bool WaitingForExitCleanup
        {
            get
            {
                #if UNITY_EDITOR
                return EditorSessionCleanup.HasPendingRequests;
                #else
                return false;
                #endif
            }
        }
        private IEnumerator TransferRoomMaster(string targetId, string targetName)
        {
            if (network?.IsConnected != true || api?.IsLoggedIn != true) yield break;
            bool transferred = false;
            yield return api.TransferRoomMaster(networkLobbyId, networkRoomId, targetId,
                () => { transferred = true; status = targetName + "에게 방장을 위임했습니다. 표시를 갱신하는 중입니다."; }, Failure);
            if (!transferred) yield break;
            double until = Time.realtimeSinceStartupAsDouble + 5;
            while (network?.IsConnected == true && network.LocalPlayer != null &&
                network.LocalPlayer.RoomMasterUserId != targetId && Time.realtimeSinceStartupAsDouble < until) yield return null;
            status = targetName + "에게 방장을 위임했습니다.";
        }

        private IEnumerator SetRoomEntryLock(bool locked)
        {
            if (network?.IsConnected != true || api?.IsLoggedIn != true) yield break;
            bool changed = false;
            yield return api.SetRoomEntryLock(networkLobbyId, networkRoomId, locked, () => changed = true, Failure);
            if (!changed) yield break;
            double until = Time.realtimeSinceStartupAsDouble + 5;
            while (network?.IsConnected == true && network.LocalPlayer != null && network.LocalPlayer.RoomEntryLocked != locked &&
                Time.realtimeSinceStartupAsDouble < until) yield return null;
            status = locked ? "새 참가자의 입장을 잠갔습니다. 기존 참가자의 연결은 유지됩니다." : "새 참가자의 입장을 열었습니다.";
        }

        private IEnumerator KickRoomUser(string targetId, string targetName)
        {
            if (network?.IsConnected != true || api?.IsLoggedIn != true) yield break;
            bool sent = false;
            yield return api.KickRoomUser(networkLobbyId, networkRoomId, targetId, () => sent = true, Failure);
            if (!sent) yield break;
            double until = Time.realtimeSinceStartupAsDouble + 5;
            while (network?.IsConnected == true && Array.Exists(network.Players, player => player.UserId == targetId) &&
                Time.realtimeSinceStartupAsDouble < until) yield return null;
            status = network?.IsConnected == true && !Array.Exists(network.Players, player => player.UserId == targetId)
                ? targetName + "님을 강퇴했습니다." : "강퇴 요청을 전달했습니다. 접속 종료 확인을 기다리는 중입니다.";
        }

        private static void DrawContent(ContentSummary content, int level)
        {
            GUILayout.Label(new string(' ', level * 2) + "• " + content.title);
            if (content.children != null)
                foreach (var child in content.children) DrawContent(child, level + 1);
        }
        private void Run(IEnumerator operation) { if (busy || WaitingForExitCleanup) return; busy = true; StartCoroutine(Execute(operation)); }
        private IEnumerator Execute(IEnumerator operation) { try { yield return operation; } finally { busy = false; } }
        private void ClearLobbyData() { contents = Array.Empty<ContentSummary>(); rooms = Array.Empty<RoomSummary>(); selectedContentId = ""; selectedRoom = null; }
        private void ClearView()
        {
            ForgetReconnect();
            network?.Dispose(); network = null; networkRoomId = ""; networkLobbyId = "";
            lobbies = Array.Empty<LobbySummary>(); ClearLobbyData(); selectedLobbyId = "";
        }
        private static string RoomStateLabel(int state)
        {
            switch (state)
            {
                case 0: return "서버 실행 대기";
                case 1: return "서버 시작 중";
                case 2: return "준비 완료";
                case 3: return "서버 실행 중";
                case 4: return "종료 중";
                case 5: return "종료됨";
                case 6: return "실행 실패";
                default: return "알 수 없음";
            }
        }
        private void Failure(string message) { status = message; if (api?.IsLoggedIn != true) ClearView(); }
        private bool EnsureClient()
        {
            if (api != null && api.BaseUrl == serverUrl.TrimEnd('/')) return true;
            try { var next = new TrainingApiClient(serverUrl); api?.Dispose(); api = next; return true; }
            catch (ArgumentException error) { Failure(error.Message); return false; }
        }
        private IEnumerator CheckHealth()
        {
            if (EnsureClient()) yield return api.CheckHealth(() => status = "서버가 응답했습니다.", Failure);
        }
        private IEnumerator Login()
        {
            if (!EnsureClient()) yield break;
            bool loggedIn = false;
            yield return api.Login(nickname.Trim(), () => { loggedIn = true; status = "로그인했습니다."; }, Failure);
            if (loggedIn) yield return LoadState();
        }
        private IEnumerator LoadState()
        {
            bool loaded = false;
            yield return api.GetMe(_ => loaded = true, Failure);
            if (!loaded) yield break;
            loaded = false;
            yield return api.GetLobbies(items => { lobbies = items; loaded = true; status = "로비 " + items.Length + "개를 조회했습니다."; }, Failure);
            if (!loaded) yield break;
            if (Array.Find(lobbies, item => item.id == selectedLobbyId) == null)
            {
                selectedLobbyId = api.User.currentLobbyId;
                ClearLobbyData();
            }
            if (!string.IsNullOrEmpty(selectedLobbyId)) yield return LoadLobbyData();
        }
        private IEnumerator CreateLobby()
        {
            bool created = false;
            yield return api.CreateLobby(lobbyName.Trim(), id => { selectedLobbyId = id; ClearLobbyData(); created = true; }, Failure);
            if (created) yield return LoadState();
        }
        private IEnumerator SelectLobby(string id)
        {
            selectedLobbyId = id; ClearLobbyData();
            yield return LoadLobbyData();
        }
        private IEnumerator LoadLobbyData()
        {
            bool loaded = false;
            yield return api.GetContents(selectedLobbyId, items =>
            {
                contents = items; loaded = true;
                if (Array.Find(contents, item => item.rootContent == selectedContentId) == null) selectedContentId = "";
            }, Failure);
            if (loaded) yield return LoadRooms();
        }
        private IEnumerator LoadRooms()
        {
            yield return api.GetRooms(selectedLobbyId, items =>
            {
                rooms = items;
                selectedRoom = Array.Find(rooms, item => item.id == selectedRoom?.id);
                status = "방 " + rooms.Length + "개를 조회했습니다.";
            }, Failure);
        }
        private IEnumerator SelectRoom(string roomId)
        {
            selectedRoom = null;
            yield return api.GetRoom(selectedLobbyId, roomId, room => { selectedRoom = room; status = "방 상세를 조회했습니다."; }, Failure);
        }
        private IEnumerator CreateRoom()
        {
            if (!int.TryParse(maxUsersText, out var maxUsers) || maxUsers < 1)
            { Failure("최대 인원은 1 이상의 숫자로 입력해주세요."); yield break; }
            string createdId = null;
            yield return api.CreateRoom(selectedLobbyId, roomName.Trim(), selectedContentId, maxUsers, id => createdId = id, Failure);
            if (createdId == null) yield break;
            // 생성 응답의 내부 방 객체 대신 조회 DTO로 표시를 갱신합니다.
            yield return LoadState();
            if (api.IsLoggedIn && Array.Exists(rooms, room => room.id == createdId)) yield return SelectRoom(createdId);
        }
        private IEnumerator JoinLobby(string id)
        {
            ForgetReconnect();
            bool joined = false;
            yield return api.JoinLobby(id, () => joined = true, Failure);
            if (joined) yield return LoadState();
        }
        private IEnumerator LeaveLobby()
        {
            ForgetReconnect();
            bool left = false;
            yield return api.LeaveLobby(api.User.currentLobbyId, () => left = true, Failure);
            if (left) yield return LoadState();
        }
        private IEnumerator JoinRoom(string roomId)
        {
            status = "방 서버 준비와 접속 예약을 기다리는 중…";
            JoinRoomResponse reservation = null;
            string reservationError = null;
            yield return api.JoinRoom(selectedLobbyId, roomId, value => reservation = value, message => { reservationError = message; Failure(message); });
            if (reservation == null)
            {
                // 서버가 예약했지만 응답을 받지 못한 경우도 내 정보로 확인합니다.
                if (api.IsLoggedIn) yield return api.GetMe(_ => { }, _ => { });
                if (api.IsLoggedIn && (api.User.pendingRoomId == roomId || api.User.currentRoomId == roomId))
                { networkRoomId = roomId; networkLobbyId = selectedLobbyId; yield return LeaveRoom(reservationError); }
                else if (reservationError != null) Failure(reservationError);
                yield break;
            }
            networkRoomId = reservation.room.id; networkLobbyId = reservation.room.lobbyId;
            network = new RoomNetworkClient();
            string error = null;
            yield return network.Connect(api.User.id, reservation, () => { }, message => error = message);
            if (error != null) { yield return LeaveRoom(error); yield break; }
            double confirmedBy = Time.realtimeSinceStartupAsDouble + 10;
            while (api.IsLoggedIn && network.IsConnected && Time.realtimeSinceStartupAsDouble < confirmedBy)
            {
                yield return api.GetMe(_ => { }, message => error = message);
                if (error != null || api.User.currentRoomId == networkRoomId) break;
                yield return new WaitForSecondsRealtime(0.2f);
            }
            if (error != null || !api.IsLoggedIn || !network.IsConnected || api.User.currentRoomId != networkRoomId)
            { yield return LeaveRoom(error ?? "서버의 접속 확인을 받지 못했습니다. 다시 참가해주세요."); yield break; }
            double playerBy = Time.realtimeSinceStartupAsDouble + 10;
            while (network.IsConnected && network.LocalPlayer == null && Time.realtimeSinceStartupAsDouble < playerBy) yield return null;
            if (!network.IsConnected || network.LocalPlayer == null)
            { yield return LeaveRoom("플레이어를 생성하지 못했습니다. 다시 참가해주세요."); yield break; }
            clearKeyboardFocus = true;
            ForgetReconnect();
            yield return LoadState();
            if (api.IsLoggedIn)
                status = api.User.currentRoomId == networkRoomId ? "방에 접속했습니다." : "네트워크 연결됨. 서버의 접속 확인을 기다리고 있습니다.";
        }
        private IEnumerator LeaveRoom(string message)
        {
            string roomId = !string.IsNullOrEmpty(networkRoomId) ? networkRoomId :
                !string.IsNullOrEmpty(api.User.pendingRoomId) ? api.User.pendingRoomId : api.User.currentRoomId;
            string lobbyId = !string.IsNullOrEmpty(networkLobbyId) ? networkLobbyId : api.User.currentLobbyId;
            network?.Dispose(); network = null; networkRoomId = ""; networkLobbyId = "";
            string error = null;
            if (!string.IsNullOrEmpty(roomId)) yield return api.LeaveRoom(lobbyId, roomId, () => { }, text => error = text);
            bool checkedState = false;
            if (api.IsLoggedIn) yield return api.GetMe(_ => checkedState = true, text => error = text);
            if (checkedState && api.User.currentRoomId != roomId && api.User.pendingRoomId != roomId) error = null;
            if (api.IsLoggedIn) yield return LoadState();
            status = error == null ? message : message + " 정리 확인: " + error;
        }
        private IEnumerator Logout()
        {
            ForgetReconnect();
            network?.Dispose(); network = null; networkRoomId = ""; networkLobbyId = "";
            yield return api.Logout(() => { ClearView(); status = "로그아웃했습니다."; }, Failure);
        }
        private void ForgetReconnect() { reconnectRoomId = ""; reconnectLobbyId = ""; }

        private IEnumerator HandleConnectionLost()
        {
            bool kicked = network?.DisconnectReason == "방장에 의해 강퇴되었습니다.";
            string roomId = networkRoomId, lobbyId = networkLobbyId;
            ForgetReconnect();
            yield return LeaveRoom(kicked ? "방장에 의해 강퇴되었습니다. 로비로 돌아왔습니다."
                : "방 서버 연결이 끊겼습니다. 로비에서 다시 접속할 수 있습니다.");
            if (!kicked && api?.IsLoggedIn == true && api.User.currentLobbyId == lobbyId && !string.IsNullOrEmpty(roomId))
            { reconnectRoomId = roomId; reconnectLobbyId = lobbyId; }
        }

        private IEnumerator ReconnectRoom()
        {
            string roomId = reconnectRoomId, lobbyId = reconnectLobbyId;
            if (api?.IsLoggedIn != true || string.IsNullOrEmpty(roomId) || network?.IsConnected == true) yield break;
            // 이전 REST 정리가 실패했으면 새 예약 전에 다시 확인합니다.
            yield return LeaveRoom("이전 접속을 확인했습니다.");
            if (!api.IsLoggedIn) yield break;
            bool checkedState = false;
            yield return api.GetMe(_ => checkedState = true, Failure);
            if (!checkedState) yield break;
            if (api.User.currentLobbyId != lobbyId) { ForgetReconnect(); Failure("이전 방의 로비에 있지 않습니다."); yield break; }
            if (!string.IsNullOrEmpty(api.User.currentRoomId) || !string.IsNullOrEmpty(api.User.pendingRoomId))
            { Failure("이전 접속 정리가 끝나지 않았습니다. 잠시 후 다시 접속해주세요."); yield break; }
            RoomSummary room = null;
            yield return api.GetRoom(lobbyId, roomId, value => room = value, Failure);
            if (room == null) yield break;
            if (room.isEntryLocked) { Failure("방 입장이 잠겨 있습니다. 방장이 열면 다시 접속해주세요."); yield break; }
            if (room.availableSlots <= 0) { Failure("방에 빈 자리가 없습니다. 잠시 후 다시 접속해주세요."); yield break; }
            selectedLobbyId = lobbyId;
            yield return JoinRoom(roomId);
        }

        private void Update()
        {
            if (network?.IsConnected == true)
            {
                var buttons = Application.isFocused && Time.unscaledTime - buttonReadAt < 0.1f ? buttonMovement : Vector2.zero;
                network.SendMovement(busy ? Vector2.zero : Vector2.ClampMagnitude(RoomMovementInput.Read() + buttons, 1));
            }
            if (!busy && api?.IsLoggedIn == true && network?.WasDisconnected == true)
                Run(HandleConnectionLost());
        }
        private void OnDisable()
        {
            Application.runInBackground = previousRunInBackground;
            StopAllCoroutines();
            network?.Dispose(); network = null; networkRoomId = ""; networkLobbyId = "";
            if (api != null && !api.LogoutOnShutdown())
                Debug.LogWarning("종료 시 서버 로그아웃을 확인하지 못했습니다. 서버 연결 실패 또는 만료된 세션은 서버의 만료 정리 기능이 처리합니다.");
            api?.Dispose(); api = null; busy = false; ClearView();
        }
    }
}
