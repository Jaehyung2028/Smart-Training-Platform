using System;
using System.Collections;
using UnityEngine;

namespace SmartTraining
{
    public sealed partial class ApiLobbyScreen : MonoBehaviour
    {
        [SerializeField] private string serverUrl = "http://localhost:5229";
        private string nickname = "훈련사용자";
        private string roomName = "훈련방";
        private string maxUsersText = "4";
        private string createRoomPassword = "", joinRoomPassword = "", reconnectPassword = "";
        private string editRoomPassword = "";
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
        private string operationMessage = "";
        private string editRoomName = "", editRoomCapacity = "";
        private string reconnectRoomId = "", reconnectLobbyId = "";
        private bool previousRunInBackground;

        private void OnEnable() { previousRunInBackground = Application.runInBackground; Application.runInBackground = true; LoadServerAddress(); EnsureUgui(); }

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

        private void Run(IEnumerator operation) => RunOperation(operation, "작업을 처리하고 있습니다.");
        private void RunOperation(IEnumerator operation, string message)
        {
            if (busy || WaitingForExitCleanup) return;
            busy = true; operationMessage = message;
            RefreshUguiState();
            StartCoroutine(Execute(operation));
        }
        private IEnumerator Execute(IEnumerator operation)
        {
            try { yield return operation; }
            finally { busy = false; operationMessage = ""; RefreshUguiState(); }
        }
        private void ClearRoomPasswords()
        {
            createRoomPassword = joinRoomPassword = reconnectPassword = editRoomPassword = "";
        }
        private void ClearLobbyData() { contents = Array.Empty<ContentSummary>(); rooms = Array.Empty<RoomSummary>(); selectedContentId = ""; selectedRoom = null; joinRoomPassword = ""; }
        private void ClearView()
        {
            ClearLobbyJoinProgress();
            ClearLobbyJoinFailure();
            roomJoinProgress = "";
            roomJoinCanCancel = false; roomJoinCancelRequested = false;
            ClearRoomAction();
            ClearRoomNotices();
            ResetServerConnectionStatus();
            ClearParticipants();
            DisposeLobbyUpdates();
            ClearChat();
            ResetRoomBrowser();
            editRoomName = ""; editRoomCapacity = "";

            ClearRoomPasswords(); roomName = "훈련방"; maxUsersText = "4";
            ForgetReconnect();
            DisposeRoomConnection();
            lobbies = Array.Empty<LobbySummary>(); ClearLobbyData(); selectedLobbyId = "";
            ResetUguiTransientState();
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
        private void Failure(string message)
        {
            status = message;
            if (api?.IsLoggedIn == true) return;
            if (connectionSessionObserved) EndServerSession();
            else if (serverConnectionState != ServerConnectionState.SessionEnded) ClearView();
        }
        private bool EnsureClient()
        {
            try
            {
                var address = ServerAddress.Normalize(serverUrl);
                if (api != null && api.BaseUrl == address) { serverUrl = address; return true; }
                if (api?.IsLoggedIn == true || network?.IsConnected == true)
                { status = "서버 주소를 바꾸려면 먼저 로그아웃해주세요."; return false; }
                var next = new TrainingApiClient(address);
                api?.Dispose(); ClearView(); api = next; serverUrl = address; return true;
            }
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
            if (!string.IsNullOrEmpty(api.User.currentLobbyId) && selectedLobbyId != api.User.currentLobbyId) { selectedLobbyId = api.User.currentLobbyId; ClearLobbyData(); }
            if (Array.Find(lobbies, item => item.id == selectedLobbyId) == null)
            {
                selectedLobbyId = api.User.currentLobbyId;
                ClearLobbyData();
            }
            if (!string.IsNullOrEmpty(selectedLobbyId)) yield return LoadLobbyData();
            if (!string.IsNullOrEmpty(api.User.currentLobbyId)) yield return LoadLobbyParticipants(api.User.currentLobbyId);
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
            joinRoomPassword = "";
            selectedRoom = null;
            yield return api.GetRoom(selectedLobbyId, roomId, room => { selectedRoom = room; status = "방 상세를 조회했습니다."; }, Failure);
        }
        private IEnumerator CreateRoom()
        {
            if (!int.TryParse(maxUsersText, out var maxUsers) || maxUsers < 1)
            { Failure("최대 인원은 1 이상의 숫자로 입력해주세요."); yield break; }
            string createdId = null;
            yield return api.CreateRoom(selectedLobbyId, roomName.Trim(), selectedContentId, maxUsers, id => createdId = id, Failure, createRoomPassword);
            if (createdId == null) yield break;
            uiCreatedRoomId = createdId;
            createRoomPassword = "";
            // 생성 응답의 내부 방 객체 대신 조회 DTO로 표시를 갱신합니다.
            yield return LoadState();
            if (api.IsLoggedIn && Array.Exists(rooms, room => room.id == createdId)) yield return SelectRoom(createdId);
        }
        private IEnumerator LeaveLobby()
        {
            ForgetReconnect();
            bool left = false;
            yield return api.LeaveLobby(api.User.currentLobbyId, () => left = true, Failure);
            if (left)
            {
                ClearRoomPasswords(); ResetUguiTransientState();
                yield return LoadState();
            }
        }

        private void Update()
        {
            ValidateRoomAction();
            UpdateLobbyRooms();
            UpdateRoomNotices();
            if (network?.IsConnected == true)
            {
                var buttons = UseUguiRoom && Application.isFocused ? roomView.ReadMovement() : Vector2.zero;
                bool blocked = busy || (UseUguiRoom && roomView.BlocksMovement);
                network.SendMovement(blocked ? Vector2.zero : Vector2.ClampMagnitude(RoomMovementInput.Read() + buttons, 1));
            }
            if (!busy && api?.IsLoggedIn == true && network?.WasDisconnected == true)
                RunOperation(HandleConnectionLost(), "연결 끊김 정보를 정리하고 있습니다.");
        }
        private void OnDisable()
        {
            ClearRoomAction();
            Application.runInBackground = previousRunInBackground;
            StopAllCoroutines();
            DisposeLobbyUpdates();
            DisposeRoomConnection();
            if (api != null && !api.LogoutOnShutdown())
                Debug.LogWarning("종료 시 서버 로그아웃을 확인하지 못했습니다. 서버 연결 실패 또는 만료된 세션은 서버의 만료 정리 기능이 처리합니다.");
            api?.Dispose(); api = null; busy = false; operationMessage = ""; ClearView();
        }
    }
}
