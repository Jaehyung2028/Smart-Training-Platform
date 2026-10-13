using System;
using System.Collections;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace SmartTraining
{
    public sealed class TrainingApiClient : IDisposable
    {
        public string BaseUrl { get; }
        public CurrentUser User { get; private set; }
        public bool IsLoggedIn => !string.IsNullOrEmpty(accessToken);
        private string accessToken;
        private DateTimeOffset expiresAt;
        private UnityWebRequest activeRequest;

        public TrainingApiClient(string baseUrl)
        {
            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "http" && uri.Scheme != "https") || !string.IsNullOrEmpty(uri.UserInfo) ||
                !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                throw new ArgumentException("서버 주소는 http:// 또는 https://로 시작해야 합니다.");
            BaseUrl = baseUrl.TrimEnd('/');
        }

        public IEnumerator CheckHealth(Action success, Action<string> failure)
        {
            yield return Send("GET", "/api/health", null, false, json =>
            {
                if (JsonUtility.FromJson<HealthResponse>(json)?.status != "ok") throw new FormatException();
                success();
            }, failure);
        }

        public IEnumerator Login(string name, Action success, Action<string> failure)
        {
            if (IsLoggedIn) { failure("먼저 로그아웃해주세요."); yield break; }
            yield return Send("POST", "/api/Auth/login", JsonUtility.ToJson(new LoginRequest { userName = name }), false,
                json => { AcceptSession(json); success(); }, failure);
        }

        public IEnumerator GetMe(Action<CurrentUser> success, Action<string> failure)
        {
            yield return WithSession(() => Send("GET", "/api/Auth/me", null, true, json =>
            {
                var user = JsonUtility.FromJson<CurrentUser>(json);
                if (user == null || string.IsNullOrEmpty(user.id)) throw new FormatException();
                User = user; success(user);
            }, failure), failure);
        }

        public IEnumerator GetLobbies(Action<LobbySummary[]> success, Action<string> failure)
        {
            yield return WithSession(() => Send("GET", "/api/Lobby", null, true, json =>
            {
                if (!json.TrimStart().StartsWith("[", StringComparison.Ordinal)) throw new FormatException();
                var list = JsonUtility.FromJson<LobbyList>("{\"items\":" + json + "}");
                if (list?.items == null) throw new FormatException();
                success(list.items);
            }, failure), failure);
        }

        public IEnumerator CreateLobby(string name, Action<string> success, Action<string> failure)
        {
            yield return WithSession(() => Send("POST", "/api/Lobby/create",
                JsonUtility.ToJson(new CreateLobbyRequest { lobbyName = name }), true, json =>
                {
                    var response = JsonUtility.FromJson<CreateLobbyResponse>(json);
                    if (response == null || !response.success || string.IsNullOrEmpty(response.lobby?.id)) throw new FormatException();
                    success(response.lobby.id);
                }, failure), failure);
        }

        public IEnumerator JoinLobby(string lobbyId, Action success, Action<string> failure)
        {
            yield return ChangeLobby("join", lobbyId, success, failure);
        }

        public IEnumerator LeaveLobby(string lobbyId, Action success, Action<string> failure)
        {
            yield return ChangeLobby("leave", lobbyId, success, failure);
        }

        private IEnumerator ChangeLobby(string action, string lobbyId, Action success, Action<string> failure)
        {
            yield return WithSession(() => Send("POST", "/api/Lobby/" + action,
                JsonUtility.ToJson(new LobbyMembershipRequest { lobbyId = lobbyId }), true, json =>
                {
                    if (JsonUtility.FromJson<ApiError>(json)?.success != true) throw new FormatException();
                    success();
                }, failure), failure);
        }

        public IEnumerator GetContents(string lobbyId, Action<ContentSummary[]> success, Action<string> failure)
        {
            if (string.IsNullOrEmpty(lobbyId)) { failure("로비를 선택해주세요."); yield break; }
            yield return WithSession(() => Send("GET", "/api/Lobby/" + Uri.EscapeDataString(lobbyId) + "/contents", null, true, json =>
            {
                if (!json.TrimStart().StartsWith("[", StringComparison.Ordinal)) throw new FormatException();
                var list = JsonUtility.FromJson<ContentList>("{\"items\":" + json + "}");
                if (list?.items == null) throw new FormatException();
                success(list.items);
            }, failure), failure);
        }

        public IEnumerator CreateRoom(string lobbyId, string name, string contentId, int maxUsers, Action<string> success, Action<string> failure)
        {
            yield return WithSession(() => Send("POST", "/api/Room/create", JsonUtility.ToJson(new CreateRoomRequest
            { lobbyId = lobbyId, roomName = name, contentId = contentId, maxUsers = maxUsers }), true, json =>
            {
                var response = JsonUtility.FromJson<CreateRoomResponse>(json);
                if (response == null || !response.success || string.IsNullOrEmpty(response.room?.id)) throw new FormatException();
                success(response.room.id);
            }, failure), failure);
        }

        public IEnumerator GetRooms(string lobbyId, Action<RoomSummary[]> success, Action<string> failure)
        {
            if (string.IsNullOrEmpty(lobbyId)) { failure("로비를 선택해주세요."); yield break; }
            yield return WithSession(() => Send("GET", "/api/Lobby/" + Uri.EscapeDataString(lobbyId) + "/rooms", null, true, json =>
            {
                if (!json.TrimStart().StartsWith("[", StringComparison.Ordinal)) throw new FormatException();
                var list = JsonUtility.FromJson<RoomList>("{\"items\":" + json + "}");
                if (list?.items == null) throw new FormatException();
                success(list.items);
            }, failure), failure);
        }

        public IEnumerator GetRoom(string lobbyId, string roomId, Action<RoomSummary> success, Action<string> failure)
        {
            if (string.IsNullOrEmpty(lobbyId) || string.IsNullOrEmpty(roomId)) { failure("방을 선택해주세요."); yield break; }
            yield return WithSession(() => Send("GET", "/api/Lobby/" + Uri.EscapeDataString(lobbyId) + "/rooms/" + Uri.EscapeDataString(roomId), null, true, json =>
            {
                var room = JsonUtility.FromJson<RoomSummary>(json);
                if (room == null || string.IsNullOrEmpty(room.id)) throw new FormatException();
                success(room);
            }, failure), failure);
        }

        public IEnumerator JoinRoom(string lobbyId, string roomId, Action<JoinRoomResponse> success, Action<string> failure)
        {
            yield return WithSession(() => Send("POST", "/api/Room/join", JsonUtility.ToJson(new RoomMembershipRequest
            { lobbyId = lobbyId, roomId = roomId }), true, json =>
            {
                var reservation = JsonUtility.FromJson<JoinRoomResponse>(json);
                if (reservation == null || !reservation.success || string.IsNullOrEmpty(reservation.admissionToken) ||
                    string.IsNullOrEmpty(reservation.room?.id)) throw new FormatException();
                success(reservation);
            }, failure, 310), failure);
        }

        public IEnumerator LeaveRoom(string lobbyId, string roomId, Action success, Action<string> failure)
        {
            yield return WithSession(() => Send("POST", "/api/Room/leave", JsonUtility.ToJson(new RoomMembershipRequest
            { lobbyId = lobbyId, roomId = roomId }), true, json =>
            {
                if (JsonUtility.FromJson<ApiError>(json)?.success != true) throw new FormatException();
                success();
            }, failure), failure);
        }

        public IEnumerator Logout(Action success, Action<string> failure)
        {
            yield return WithSession(() => Send("POST", "/api/Auth/logout", null, true, json =>
            {
                if (JsonUtility.FromJson<ApiError>(json)?.success != true) throw new FormatException();
                ClearSession(); success();
            }, failure), failure);
        }

        public IEnumerator TransferRoomMaster(string lobbyId, string roomId, string targetUserId, Action success, Action<string> failure)
        {
            yield return WithSession(() => Send("POST", "/api/Room/transfer-master", JsonUtility.ToJson(new TransferRoomMasterRequest
                { lobbyId = lobbyId, roomId = roomId, targetUserId = targetUserId }), true, json =>
            {
                var response = JsonUtility.FromJson<RoomHubResult>(json);
                if (response?.success != true || response.masterUserId != targetUserId) throw new FormatException();
                success();
            }, failure), failure);
        }

        public IEnumerator KickRoomUser(string lobbyId, string roomId, string targetUserId, Action success, Action<string> failure)
        {
            yield return WithSession(() => Send("POST", "/api/Room/kick", JsonUtility.ToJson(new KickRoomUserRequest
                { lobbyId = lobbyId, roomId = roomId, targetUserId = targetUserId }), true, json =>
            {
                if (JsonUtility.FromJson<ApiError>(json)?.success != true) throw new FormatException();
                success();
            }, failure), failure);
        }

        public IEnumerator SetRoomEntryLock(string lobbyId, string roomId, bool locked, Action success, Action<string> failure)
        {
            yield return WithSession(() => Send("POST", "/api/Room/entry-lock", JsonUtility.ToJson(new SetRoomEntryLockRequest
                { lobbyId = lobbyId, roomId = roomId, isEntryLocked = locked }), true, json =>
            {
                var response = JsonUtility.FromJson<RoomHubResult>(json);
                if (response?.success != true || response.isEntryLocked != locked) throw new FormatException();
                success();
            }, failure), failure);
        }

        private IEnumerator WithSession(Func<IEnumerator> operation, Action<string> failure)
        {
            if (!IsLoggedIn) { failure("로그인이 필요합니다."); yield break; }
            // 만료 전에 갱신합니다. 예약 토큰과 사용자 인증 토큰은 별개입니다.
            if (DateTimeOffset.UtcNow.AddSeconds(30) >= expiresAt)
            {
                bool refreshed = false;
                yield return Send("POST", "/api/Auth/refresh", null, true,
                    json => { AcceptSession(json); refreshed = true; }, failure);
                if (!refreshed) yield break;
            }
            yield return operation();
        }

        private void AcceptSession(string json)
        {
            var response = JsonUtility.FromJson<LoginResponse>(json);
            if (response == null || !response.success || string.IsNullOrEmpty(response.accessToken) ||
                response.user == null || string.IsNullOrEmpty(response.user.id) ||
                !DateTimeOffset.TryParse(response.expiresAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var expiry))
                throw new FormatException();
            accessToken = response.accessToken; expiresAt = expiry; User = response.user;
        }

        private IEnumerator Send(string method, string path, string body, bool authenticated, Action<string> success, Action<string> failure, int timeoutSeconds = 15)
        {
            if (activeRequest != null) { failure("이전 요청이 처리 중입니다."); yield break; }
            using (var request = new UnityWebRequest(BaseUrl + path, method))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = timeoutSeconds;
                if (body != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
                    request.SetRequestHeader("Content-Type", "application/json");
                }
                if (authenticated) request.SetRequestHeader("Authorization", "Bearer " + accessToken);
                activeRequest = request;
                yield return request.SendWebRequest();
                activeRequest = null;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    if (authenticated && request.responseCode == 401) ClearSession();
                    string message = request.responseCode == 0 ? "서버에 연결하지 못했습니다. 실행 상태와 주소를 확인해주세요." : "요청에 실패했습니다 (HTTP " + request.responseCode + ").";
                    try
                    {
                        var error = JsonUtility.FromJson<ApiError>(request.downloadHandler.text);
                        if (!string.IsNullOrEmpty(error?.message)) message = error.message;
                    }
                    catch (ArgumentException) { }
                    failure(message); yield break;
                }
                string payload = request.downloadHandler.text;
                try { success(payload); }
                catch (Exception error) when (error is ArgumentException || error is FormatException)
                { failure("서버 응답 형식을 확인해주세요."); }
            }
        }

        public void ClearSession() { accessToken = null; User = null; expiresAt = default; }
        // 종료 중에는 Unity 코루틴이 더 이상 진행되지 않습니다.
        // 에디터는 재로드를 통과하는 정리 큐, 빌드 앱은 최대 2초 .NET 요청을 사용합니다.
        public bool LogoutOnShutdown()
        {
            if (!IsLoggedIn) { Dispose(); return true; }
            var token = accessToken;
            var url = BaseUrl + "/api/Auth/logout";
            activeRequest?.Abort(); activeRequest = null;
            ClearSession();
            #if UNITY_EDITOR
            EditorSessionCleanup.Enqueue(BaseUrl, token);
            return true;
            #else
            var logout = Task.Run(async () =>
            {
                try
                {
                    using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) })
                    using (var request = new HttpRequestMessage(HttpMethod.Post, url))
                    {
                        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                        using (var response = await client.SendAsync(request).ConfigureAwait(false))
                            return response.IsSuccessStatusCode;
                    }
                }
                catch (HttpRequestException) { return false; }
                catch (OperationCanceledException) { return false; }
            });
            return logout.Wait(2000) && logout.GetAwaiter().GetResult();
            #endif
        }
        public void Dispose() { activeRequest?.Abort(); activeRequest = null; ClearSession(); }
    }
}

