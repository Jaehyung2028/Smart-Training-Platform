using System;
using System.Collections;
using UnityEngine;

namespace SmartTraining
{
    public sealed partial class ApiLobbyScreen : MonoBehaviour
    {
        [SerializeField]
        private string serverUrl = "http://localhost:5229";

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

        private ContentSceneSession contentSession;

        private string networkRoomId = "", networkLobbyId = "";

        private string selectedContentId = "";

        private int selectedContentNodeId;

        private string selectedLobbyId = "";

        private bool busy;

        private string operationMessage = "";

        private string editRoomName = "", editRoomCapacity = "";

        private string reconnectRoomId = "", reconnectLobbyId = "";

        private bool previousRunInBackground;

        internal bool UiContentSceneActive => contentSession != null && contentSession.IsActive;

        private void OnEnable()
        {
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            LoadServerAddress();
            EnsureUgui();
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

        private void Run(IEnumerator operation) => RunOperation(operation, "작업을 처리하고 있습니다.");

        private void RunOperation(IEnumerator operation, string message)
        {
            if (busy || WaitingForExitCleanup)
            {
                return;
            }

            busy = true;
            operationMessage = message;
            RefreshUguiState();
            StartCoroutine(Execute(operation));
        }

        private IEnumerator Execute(IEnumerator operation)
        {
            try
            {
                yield return operation;
            }
            finally
            {
                busy = false;
                operationMessage = "";
                RefreshUguiState();
            }
        }

        private void ClearRoomPasswords()
        {
            createRoomPassword = joinRoomPassword = reconnectPassword = editRoomPassword = "";
        }

        private void ClearLobbyData()
        {
            contents = Array.Empty<ContentSummary>();
            rooms = Array.Empty<RoomSummary>();
            selectedContentId = "";
            selectedContentNodeId = 0;
            selectedRoom = null;
            joinRoomPassword = "";
        }

        private void ClearView()
        {
            ClearCourses();
            ClearLobbyJoinProgress();
            ClearLobbyJoinFailure();
            roomJoinProgress = "";
            roomJoinCanCancel = false;
            roomJoinCancelRequested = false;
            ClearRoomAction();
            ClearRoomNotices();
            ResetServerConnectionStatus();
            ClearParticipants();
            DisposeLobbyUpdates();
            ClearChat();
            ResetRoomBrowser();
            editRoomName = "";
            editRoomCapacity = "";
            ClearRoomPasswords();
            roomName = "훈련방";
            maxUsersText = "4";
            ForgetReconnect();
            DisposeRoomConnection();
            lobbies = Array.Empty<LobbySummary>();
            ClearLobbyData();
            selectedLobbyId = "";
            ResetUguiTransientState();
        }

        private static string RoomStateLabel(int state)
        {
            switch (state)
            {
                case 0:
                    return "서버 실행 대기";
                case 1:
                    return "서버 시작 중";
                case 2:
                    return "준비 완료";
                case 3:
                    return "서버 실행 중";
                case 4:
                    return "종료 중";
                case 5:
                    return "종료됨";
                case 6:
                    return "실행 실패";
                default:
                    return "알 수 없음";
            }
        }

        private void Failure(string message)
        {
            status = message;
            if (api?.IsLoggedIn == true)
            {
                return;
            }

            if (connectionSessionObserved)
            {
                EndServerSession();
            }
            else if (serverConnectionState != ServerConnectionState.SessionEnded)
            {
                ClearView();
            }
        }

        private bool EnsureClient()
        {
            try
            {
                var address = ServerAddress.Normalize(serverUrl);
                if (api != null && api.BaseUrl == address)
                {
                    serverUrl = address;
                    return true;
                }

                if (api?.IsLoggedIn == true || network?.IsConnected == true)
                {
                    status = "서버 주소를 바꾸려면 먼저 로그아웃해주세요.";
                    return false;
                }

                var next = new TrainingApiClient(address);
                api?.Dispose();
                ClearView();
                api = next;
                serverUrl = address;
                return true;
            }
            catch (ArgumentException error)
            {
                Failure(error.Message);
                return false;
            }
        }

        private IEnumerator CheckHealth()
        {
            if (EnsureClient())
            {
                yield return api.CheckHealth(() => status = "서버가 응답했습니다.", Failure);
            }
        }

        private IEnumerator Login()
        {
            if (!EnsureClient())
            {
                yield break;
            }

            bool loggedIn = false;
            yield return api.Login(
                nickname.Trim(),
                () =>
            {
                loggedIn = true;
                status = "로그인했습니다.";
            },
                Failure
            );
            if (loggedIn)
            {
                yield return LoadState();
            }
        }

        private IEnumerator LoadState()
        {
            bool loaded = false;
            yield return api.GetMe(_ => loaded = true, Failure);
            if (!loaded)
            {
                yield break;
            }

            loaded = false;
            yield return api.GetLobbies(
                items =>
            {
                lobbies = items;
                loaded = true;
                status = "로비 " + items.Length + "개를 조회했습니다.";
            },
                Failure
            );
            if (!loaded)
            {
                yield break;
            }

            if (!string.IsNullOrEmpty(api.User.currentLobbyId) && selectedLobbyId != api.User.currentLobbyId)
            {
                selectedLobbyId = api.User.currentLobbyId;
                ClearLobbyData();
            }

            if (Array.Find(lobbies, item => item.id == selectedLobbyId) == null)
            {
                selectedLobbyId = api.User.currentLobbyId;
                ClearLobbyData();
            }

            if (!string.IsNullOrEmpty(selectedLobbyId))
            {
                yield return LoadLobbyData();
            }

            if (!string.IsNullOrEmpty(api.User.currentLobbyId))
            {
                yield return LoadLobbyParticipants(api.User.currentLobbyId);
            }
        }

        private IEnumerator SelectLobby(string id)
        {
            selectedLobbyId = id;
            ClearLobbyData();
            yield return LoadLobbyData();
        }

        // 카탈로그 트리를 선택용 목록으로도 펼칩니다. children 관계는 그대로 보존해 트리 UI에 사용합니다.
        private IEnumerator LoadLobbyData()
        {
            bool loaded = false;
            yield return api.GetContents(
                selectedLobbyId,
                items =>
            {
                contents = ContentSummary.Flatten(items);
                loaded = true;
                if (!Array.Exists(contents, item => item.rootContent == selectedContentId &&
                    item.id == selectedContentNodeId))
                {
                    selectedContentId = "";
                    selectedContentNodeId = 0;
                }
            },
                Failure
            );
            if (loaded)
            {
                yield return LoadRooms();
            }
        }

        private IEnumerator LoadRooms()
        {
            yield return api.GetRooms(
                selectedLobbyId,
                items =>
            {
                rooms = items;
                selectedRoom = Array.Find(rooms, item => item.id == selectedRoom?.id);
                status = "방 " + rooms.Length + "개를 조회했습니다.";
            },
                Failure
            );
        }

        private IEnumerator SelectRoom(string roomId)
        {
            joinRoomPassword = "";
            selectedRoom = null;
            yield return api.GetRoom(
                selectedLobbyId,
                roomId,
                room =>
            {
                selectedRoom = room;
                status = "방 상세를 조회했습니다.";
            },
                Failure
            );
        }

        // 입력값을 검사하고 선택한 콘텐츠로 방을 생성한 뒤 같은 비밀번호로 자동 입장합니다.
        private IEnumerator CreateRoom()
        {
            if (!int.TryParse(maxUsersText, out var maxUsers) || maxUsers < 1)
            {
                Failure("최대 인원은 1 이상의 숫자로 입력해주세요.");
                yield break;
            }

            string createdId = null;
            string lobbyId = selectedLobbyId;
            // 생성 직후 같은 비밀번호로 자동 입장합니다. 입력란을 비워도 이번 요청에는 보관한 값을 사용합니다.
            string password = createRoomPassword;
            string creationError = null;
            yield return api.CreateRoom(
                lobbyId,
                roomName.Trim(),
                selectedContentId,
                maxUsers,
                id => createdId = id,
                message =>
            {
                creationError = message;
                Failure(message);
            },
                password,
                selectedContentNodeId
            );
            if (createdId == null)
            {
                yield return RecoverRoomCreation(creationError ?? "방 생성 결과를 확인하지 못했습니다.");
                yield break;
            }

            uiCreatedRoomId = createdId;
            createRoomPassword = "";
            // 목록 조회를 기다리지 않고 기존 예약·Netcode 연결·접속 확인 과정을 이어서 실행합니다.
            yield return JoinRoom(createdId, password);
            if (api?.IsLoggedIn != true || api.User.currentLobbyId != lobbyId)
            {
                yield break;
            }

            if (network?.IsConnected == true && networkRoomId == createdId)
            {
                yield break;
            }

            // 생성 시 확보한 예약이 남아 있으면 취소합니다. 빈 방 삭제는 서버가 참가자·예약 수로 판단합니다.
            yield return api.LeaveRoom(lobbyId, createdId, () =>
            {
            }, _ =>
            {
            });
            string joinStatus = status;
            yield return LoadState();
            status = joinStatus;
        }

        // 응답이 유실돼도 서버에 예약이 생겼을 수 있으므로 재생성·자동 재시도 대신 실제 소속을 확인합니다.
        private IEnumerator RecoverRoomCreation(string creationError)
        {
            var source = api;
            if (source?.IsLoggedIn != true)
            {
                yield break;
            }

            bool checkedState = false;
            yield return source.GetMe(_ => checkedState = true, _ =>
            {
            });
            if (api != source || source.IsLoggedIn != true)
            {
                yield break;
            }

            if (!checkedState)
            {
                Failure(creationError + " 현재 접속 상태도 확인하지 못했습니다. 연결 복구 후 새로고침해주세요.");
                yield break;
            }

            bool hasRoom = !string.IsNullOrEmpty(source.User.pendingRoomId) || !string.IsNullOrEmpty(source.User.currentRoomId);
            yield return LoadState();
            if (api != source || !source.IsLoggedIn)
            {
                yield break;
            }

            status = creationError + (hasRoom ? " 서버에 방 접속 정보가 남아 있습니다. ‘남은 방 접속 정리’를 눌러 정리해주세요." : " 남은 방 접속 정보는 없습니다. 다시 생성할 수 있습니다.");
        }

        private IEnumerator LeaveLobby()
        {
            ForgetReconnect();
            bool left = false;
            yield return api.LeaveLobby(api.User.currentLobbyId, () => left = true, Failure);
            if (left)
            {
                ClearRoomPasswords();
                ResetUguiTransientState();
                yield return LoadState();
            }
        }

        private void Update()
        {
            UpdateContentScene();
            UpdateCoursesBeforeLogin();
            ValidateRoomAction();
            UpdateLobbyRooms();
            UpdateRoomNotices();
            UpdateRoomMovement();

            if (!busy && api?.IsLoggedIn == true && network?.WasDisconnected == true)
            {
                RunOperation(HandleConnectionLost(), "연결 끊김 정보를 정리하고 있습니다.");
            }
        }

        // 서버가 확정한 진행 상태만 씬 전환에 전달합니다. 대기 UI와 네트워크 연결은 유지합니다.
        private void UpdateContentScene()
        {
            if (contentSession == null)
            {
                contentSession = gameObject.AddComponent<ContentSceneSession>();
            }

            bool running = isActiveAndEnabled && network?.IsConnected == true && network.LocalPlayer?.RoomIsRunning == true;
            contentSession.Synchronize(this, running);
        }

        private void UpdateRoomMovement()
        {
            if (network?.IsConnected != true)
            {
                return;
            }

            network.SendMovement(ReadRoomMovement());
        }

        // 콘텐츠 중에는 1인칭 입력을, 그 외에는 기존 방 UI 입력을 사용합니다. 처리 중에는 이동을 멈춥니다.
        private Vector2 ReadRoomMovement()
        {
            if (busy)
            {
                return Vector2.zero;
            }

            if (UiContentSceneActive)
            {
                return contentSession.ReadMovement();
            }

            if (UseUguiRoom && roomView.BlocksMovement)
            {
                return Vector2.zero;
            }

            var buttons = UseUguiRoom && Application.isFocused ? roomView.ReadMovement() : Vector2.zero;
            return Vector2.ClampMagnitude(RoomMovementInput.Read() + buttons, 1);
        }

        private void OnDisable()
        {
            contentSession?.StopSession();
            ClearRoomAction();
            Application.runInBackground = previousRunInBackground;
            StopAllCoroutines();
            DisposeLobbyUpdates();
            DisposeRoomConnection();
            if (api != null && !api.LogoutOnShutdown())
            {
                Debug.LogWarning("종료 시 서버 로그아웃을 확인하지 못했습니다. 서버 연결 실패 또는 만료된 세션은 서버의 만료 정리 기능이 처리합니다.");
            }

            api?.Dispose();
            api = null;
            busy = false;
            operationMessage = "";
            ClearView();
        }
    }
}
