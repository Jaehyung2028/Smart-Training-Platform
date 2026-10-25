#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;

namespace SmartTraining
{
    // 개별 Development 클라이언트를 외부 검사 도구와 연결할 때만 사용하는 로컬 진단 기능입니다.
    // 일반 실행에서는 이 컴포넌트를 생성하거나 검사 명령 파일을 읽지 않습니다.
    [DefaultExecutionOrder(1000)]
    public sealed class MultiClientProbe : MonoBehaviour
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private string directory;
        private ApiLobbyScreen screen;
        private int completed;
        private float nextPoll;
        private string error = "";
        private bool snapshotDue;
        private bool auditImmediateBlocked;
        private string auditCaption = "";
        [Serializable] private sealed class Command
        { public int id; public string action; public string value; public string lobbyId; public bool enabled; }
        [Serializable] private sealed class Snapshot
        {
            public int completed; public int processId; public bool busy; public bool loggedIn; public bool connected;
            public bool running; public bool ready; public bool allReady; public bool canCancel; public bool cancelRequested; public string userId; public string roomId;
            public string pendingRoomId; public string reconnectRoomId; public string masterId; public string status;
            public string progress; public string error; public string endpoint; public string[] participants; public string[] checkingParticipants;
            public string lobbyId; public string lobbyProgress; public bool canCancelLobby;
            public string selectedLobbyId; public LobbySummary[] lobbies; public RoomSummary[] rooms; public LobbyParticipant[] lobbyParticipants;
            public string lobbyRetryId; public string lobbyError;
            public bool uiVisible, uiLoginVisible, uiProgressVisible, uiEnterEnabled, uiLayoutValid;
            public int uiRows, uiWidth, uiHeight; public string uiStatus, uiDetail, uiFont;
            public bool uiFailureVisible, uiFailureAtTop, uiCanvasSpaceValid;
            public bool uiInteriorVisible, uiInteriorLayoutValid, uiCreateVisible, uiRoomProgressVisible, uiJoinRoomEnabled, uiChatEnabled, uiInteriorInputEnabled, uiReconnectVisible;
            public int uiRoomRows, uiParticipantRows, uiChatLines;
            public string uiRoomSelected, uiInteriorStatus, uiChatText, uiInteriorChatStatus, uiOperationNotice, uiChatDraft;
            public bool uiInRoom, uiInRoomLayoutValid, uiSettingsVisible, uiConfirmVisible, uiMapVisible, uiRoomMasterControls, uiReadyEnabled, uiPhaseEnabled, uiRoomSendEnabled, uiRoomInputEnabled, uiTyping, uiMovementBlocked;
            public int uiMemberRows, uiRoomChannel;
            public float uiMoveX, uiMoveY;
            public string uiInRoomSummary, uiInRoomChat, uiRoomConfirmText, uiRoomPasswordStatus, uiRoomNoticeText;
            public bool uiHasSceneLayout, uiUsesSceneInstances, uiRoomParticipantActionsReady;
            public int uiLoginViewCount, uiInteriorViewCount, uiRoomViewCount, uiEventSystemCount;
            public bool auditImmediateBlocked, auditPasswordsEmpty, auditPasswordWidgetsEmpty, auditFocusClear, auditBackgroundBlocked;
            public string auditCaption;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartIfRequested()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--smarttraining-probe");
            if (index < 0 || index + 1 >= args.Length) return;
            var root = new GameObject("IndependentClientProbe");
            var probe = root.AddComponent<MultiClientProbe>();
            probe.directory = Path.GetFullPath(args[index + 1]); Directory.CreateDirectory(probe.directory);
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
        }
        private object Get(string field) => typeof(ApiLobbyScreen).GetField(field, Flags).GetValue(screen);
        private void Set(string field, object value) => typeof(ApiLobbyScreen).GetField(field, Flags).SetValue(screen, value);
        private IEnumerator Invoke(string method, params object[] args) =>
            (IEnumerator)typeof(ApiLobbyScreen).GetMethod(method, Flags).Invoke(screen, args);
        private bool Busy => screen != null && (bool)Get("busy");
        private void Update()
        {
            if (directory == null || Time.unscaledTime < nextPoll) return;
            nextPoll = Time.unscaledTime + 0.1f;
            if (screen == null) screen = FindFirstObjectByType<ApiLobbyScreen>();
            if (screen == null) return;
            snapshotDue = true;
            try
            {
                var path = Path.Combine(directory, "command.json");
                if (File.Exists(path))
                {
                    var command = ReadCommand(path);
                    if (command != null && command.id > completed && (!Busy || command.action == "cancel" || command.action == "cancelLobby" || command.action == "uiCancel" || command.action == "uiCapture" || command.action == "uiRoomCancel" || command.action == "uiInteriorCapture" || command.action == "auditBurstCancel" || command.action == "auditAttemptBusy"))
                    {
                        error = "";
                        IEnumerator operation = null;
                        switch (command.action)
                        {
                            case "auditBurstLogin":
                                var auditLogin=screen.GetComponentInChildren<LobbyUguiView>(true);auditLogin.addressInput.text=command.lobbyId;auditLogin.nicknameInput.text=command.value;AuditBurst(auditLogin.loginButton);break;
                            case "auditBurstCreate":
                                var auditCreate=screen.GetComponentInChildren<LobbyInteriorUguiView>(true);auditCreate.createName.text=command.value;auditCreate.createCapacity.text="4";auditCreate.createPassword.text="pass1234";AuditBurst(auditCreate.submitCreate);break;
                            case "auditBurstJoinPaused": Set("testPauseRoomJoinAfterConnection",true);goto case "auditBurstJoin";
                            case "auditBurstJoin":
                                var auditJoin=screen.GetComponentInChildren<LobbyInteriorUguiView>(true);auditJoin.joinPassword.text=command.value;AuditBurst(auditJoin.join);break;
                            case "auditBurstCancel": AuditBurst(screen.GetComponentInChildren<LobbyInteriorUguiView>(true).cancelJoin);break;
                            case "auditBurstSettings": RoomUi.nameInput.text=command.value;RoomUi.capacityInput.text="4";AuditBurst(RoomUi.saveSettings);break;
                            case "auditBurstReady": AuditBurst(RoomUi.ready);break;
                            case "auditBurstPhase": AuditBurst(RoomUi.phase);break;
                            case "auditBurstLeave": AuditBurst(RoomUi.leave);break;
                            case "auditBurstRoomLogout": AuditBurst(RoomUi.logout);break;
                            case "auditRoomDraft": RoomUi.chatInput.text=command.value;break;
                            case "auditFocusPassword":
                                var auditField=command.value=="create"?screen.GetComponentInChildren<LobbyInteriorUguiView>(true).createPassword:
                                    command.value=="join"?screen.GetComponentInChildren<LobbyInteriorUguiView>(true).joinPassword:RoomUi.passwordInput;
                                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(auditField.gameObject);auditField.ActivateInputField();break;
                            case "auditCreateDraft": screen.GetComponentInChildren<LobbyInteriorUguiView>(true).createPassword.text=command.value;break;
                            case "auditJoinDraft": screen.GetComponentInChildren<LobbyInteriorUguiView>(true).joinPassword.text=command.value;break;
                            case "auditRevokeSession": operation=RevokeUiSession();break;
                            case "auditAttemptBusy":
                                for(int repeat=0;repeat<20;repeat++){screen.UiLogout();screen.UiSubmitCreate();screen.UiSaveRoomSettings();screen.UiEnter();}break;
                            case "roomReady": RoomClick(RoomUi.ready); break;
                            case "roomPhase": RoomClick(RoomUi.phase); break;
                            case "roomSettings": RoomClick(RoomUi.settings); break;
                            case "roomSettingsClose": RoomClick(RoomUi.closeSettings); break;
                            case "roomSettingsSave": RoomUi.nameInput.text=command.value;RoomUi.capacityInput.text=command.lobbyId;RoomClick(RoomUi.saveSettings);break;
                            case "roomPasswordDraft": RoomUi.passwordInput.text=command.value;break;
                            case "roomPasswordSave": RoomClick(RoomUi.savePassword);break;
                            case "roomPasswordClear": RoomClick(RoomUi.clearPassword);break;
                            case "roomLock": RoomClick(RoomUi.entryLock);break;
                            case "roomClose": RoomClick(RoomUi.closeRoom);break;
                            case "roomConfirm": RoomClick(RoomUi.confirm);break;
                            case "roomCancel": RoomClick(RoomUi.cancel);break;
                            case "roomTransfer": RoomClick(RoomUi.participantsContent.Find("Member - "+command.value).Find("Actions/Transfer").GetComponent<UnityEngine.UI.Button>());break;
                            case "roomKick": RoomClick(RoomUi.participantsContent.Find("Member - "+command.value).Find("Actions/Kick").GetComponent<UnityEngine.UI.Button>());break;
                            case "roomRoster": RoomClick(command.enabled?RoomUi.lobbyParticipants:RoomUi.roomParticipants);break;
                            case "roomChannel": RoomClick(command.enabled?RoomUi.roomChat:RoomUi.lobbyChat);break;
                            case "roomChat": RoomUi.chatInput.text=command.value;RoomClick(RoomUi.sendChat);break;
                            case "roomChatFocus":
                                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(RoomUi.chatInput.gameObject);RoomUi.chatInput.ActivateInputField();break;
                            case "roomChatBlur": RoomUi.chatInput.DeactivateInputField();UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);break;
                            case "roomLeave": RoomClick(RoomUi.leave);break;
                            case "roomLogout": RoomClick(RoomUi.logout);break;
                            case "roomMap": RoomClick(command.enabled?RoomUi.openMap:RoomUi.closeMap);break;
                            case "roomMoveDown": RoomUi.movementButtons[0].OnPointerDown(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){button=UnityEngine.EventSystems.PointerEventData.InputButton.Left});break;
                            case "roomMoveUp": RoomUi.movementButtons[0].OnPointerUp(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current));break;
                            case "roomCapture": CaptureCanvas(RoomUi.canvas,Path.Combine(directory,Path.GetFileName(command.value)));break;
                            case "uiCreateOpen": screen.GetComponentInChildren<LobbyInteriorUguiView>(true).create.onClick.Invoke(); break;
                            case "uiCreateSubmit":
                                var createUi=screen.GetComponentInChildren<LobbyInteriorUguiView>(true);
                                createUi.createName.text=command.value;createUi.createCapacity.text=command.lobbyId;createUi.createPassword.text=command.enabled?"pass1234":"";
                                createUi.submitCreate.onClick.Invoke();break;
                            case "uiCreateClose": screen.GetComponentInChildren<LobbyInteriorUguiView>(true).closeCreate.onClick.Invoke();break;
                            case "uiRoomSearch": screen.GetComponentInChildren<LobbyInteriorUguiView>(true).search.text=command.value;break;
                            case "uiRoomProtection": screen.GetComponentInChildren<LobbyInteriorUguiView>(true).protection.onClick.Invoke();break;
                            case "uiRoomAvailable": screen.GetComponentInChildren<LobbyInteriorUguiView>(true).available.onClick.Invoke();break;
                            case "uiRoomSort": screen.GetComponentInChildren<LobbyInteriorUguiView>(true).sort.onClick.Invoke();break;
                            case "uiRoomReset": screen.GetComponentInChildren<LobbyInteriorUguiView>(true).resetFilters.onClick.Invoke();break;
                            case "uiRoomSelect": screen.GetComponentInChildren<LobbyInteriorUguiView>(true).roomsContent.Find("Room - "+command.value).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();break;
                            case "uiParticipantRoom": screen.GetComponentInChildren<LobbyInteriorUguiView>(true).participantsContent.Find("Participant - "+command.value).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();break;
                            case "uiRoomJoinPaused": Set("testPauseRoomJoinAfterConnection",true);goto case "uiRoomJoin";
                            case "uiRoomJoin":
                                var roomUi=screen.GetComponentInChildren<LobbyInteriorUguiView>(true);roomUi.joinPassword.text=command.value;roomUi.join.onClick.Invoke();break;
                            case "uiRoomCancel": screen.GetComponentInChildren<LobbyInteriorUguiView>(true).cancelJoin.onClick.Invoke();break;
                            case "uiLobbyChat":
                                var chatUi=screen.GetComponentInChildren<LobbyInteriorUguiView>(true);chatUi.chatInput.text=command.value;chatUi.sendChat.onClick.Invoke();break;
                            case "uiChatDraft": screen.GetComponentInChildren<LobbyInteriorUguiView>(true).chatInput.text=command.value;break;
                            case "uiLobbyLeave": screen.GetComponentInChildren<LobbyInteriorUguiView>(true).leave.onClick.Invoke();break;
                            case "uiInteriorReconnect":
                                var reconnectUi=screen.GetComponentInChildren<LobbyInteriorUguiView>(true);reconnectUi.reconnectPassword.text=command.value;reconnectUi.reconnect.onClick.Invoke();break;
                            case "uiInteriorLogout": screen.GetComponentInChildren<LobbyInteriorUguiView>(true).logout.onClick.Invoke();break;
                            case "uiInteriorCapture": CaptureCanvas(screen.GetComponentInChildren<LobbyInteriorUguiView>(true).canvas,Path.Combine(directory,Path.GetFileName(command.value)));break;
                            case "uiLogin":
                                var loginView = screen.GetComponentInChildren<LobbyUguiView>(true);
                                if (!loginView.loginButton.interactable) throw new InvalidOperationException("Login button disabled");
                                loginView.addressInput.text = command.lobbyId; loginView.nicknameInput.text = command.value;
                                loginView.loginButton.onClick.Invoke(); break;
                            case "uiSelect":
                                var row = screen.GetComponentInChildren<LobbyUguiView>(true).lobbyContent.Find("Lobby - " + command.value).GetComponent<UnityEngine.UI.Button>();
                                if (!row.interactable) throw new InvalidOperationException("Lobby row disabled");
                                row.onClick.Invoke(); break;
                            case "uiJoinPaused": Set("testPauseLobbyJoinAfterApproval", true); goto case "uiJoin";
                            case "uiJoin": screen.GetComponentInChildren<LobbyUguiView>(true).enterButton.onClick.Invoke(); break;
                            case "uiCancel": screen.GetComponentInChildren<LobbyUguiView>(true).cancelButton.onClick.Invoke(); break;
                            case "uiRefresh": screen.GetComponentInChildren<LobbyUguiView>(true).refreshButton.onClick.Invoke(); break;
                            case "uiLogout": screen.GetComponentInChildren<LobbyUguiView>(true).logoutButton.onClick.Invoke(); break;
                            case "uiCapture": CaptureUi(screen.GetComponentInChildren<LobbyUguiView>(true), Path.Combine(directory, Path.GetFileName(command.value))); break;
                            case "uiResize": var dimensions = command.value.Split('x'); Screen.SetResolution(int.Parse(dimensions[0]), int.Parse(dimensions[1]), FullScreenMode.Windowed); break;
                            case "uiTestFailure": Set("lobbyJoinError",command.value);break;
                            case "login": Set("serverUrl", command.lobbyId); Set("nickname", command.value); operation = Invoke("Login"); break;
                            case "lobby": operation = Invoke("JoinLobby", command.value); break;
                            case "lobbyPaused": Set("testPauseLobbyJoinAfterApproval", true); operation = Invoke("JoinLobby", command.value); break;
                            case "retryLobby": operation = Invoke("RetryLobbyJoin"); break;
                            case "cancelLobby": typeof(ApiLobbyScreen).GetMethod("RequestCancelLobbyJoin", Flags).Invoke(screen, null); break;
                            case "leaveLobby": operation = Invoke("LeaveLobby"); break;
                            case "join": Set("selectedLobbyId", command.lobbyId); operation = Invoke("JoinRoom", command.value, null); break;
                            case "cancel": typeof(ApiLobbyScreen).GetMethod("RequestCancelRoomJoin", Flags).Invoke(screen, null); break;
                            case "leave": operation = Invoke("LeaveRoom", "테스트 퇴장"); break;
                            case "ready": operation = Invoke("SetReady", command.enabled); break;
                            case "phase": operation = Invoke("ChangeRoomPhase", command.enabled); break;
                            case "reconnect": operation = Invoke("ReconnectRoom"); break;
                            case "logout": operation = Invoke("Logout"); break;
                            case "disconnect":
                                var network = (RoomNetworkClient)Get("network");
                                var manager = (NetworkManager)typeof(RoomNetworkClient).GetField("manager", Flags).GetValue(network);
                                manager.NetworkConfig.NetworkTransport.DisconnectLocalClient();
                                break;
                            default: throw new InvalidOperationException("Unknown probe command");
                        }
                        if (operation != null) typeof(ApiLobbyScreen).GetMethod("Run", Flags).Invoke(screen, new object[] { operation });
                        completed = command.id;
                    }
                }
            }
            catch (Exception exception) { error = exception.ToString(); Debug.LogError("MULTICLIENT PROBE: " + error); }
        }
        private void LateUpdate()
        {
            if (!snapshotDue || screen == null) return;
            snapshotDue = false;
            WriteSnapshot();
        }
        private void WriteSnapshot()
        {
            var api = (TrainingApiClient)Get("api");
            var client = (RoomNetworkClient)Get("network"); var player = client?.LocalPlayer;
            var snapshot = new Snapshot
            {
                completed = completed, processId = System.Diagnostics.Process.GetCurrentProcess().Id, busy = Busy,
                loggedIn = api?.IsLoggedIn == true, connected = client?.IsConnected == true,
                lobbyId = api?.User?.currentLobbyId ?? "",
                lobbyProgress = (string)Get("lobbyJoinProgress"), canCancelLobby = (bool)Get("lobbyJoinCanCancel"),
                lobbyRetryId = (string)Get("lobbyJoinRetryId"), lobbyError = (string)Get("lobbyJoinError"),
                selectedLobbyId = (string)Get("selectedLobbyId"), lobbies = (LobbySummary[])Get("lobbies"),
                rooms = (RoomSummary[])Get("rooms"), lobbyParticipants = (LobbyParticipant[])Get("lobbyParticipants"),
                canCancel = (bool)Get("roomJoinCanCancel"), cancelRequested = (bool)Get("roomJoinCancelRequested"),
                userId = api?.User?.id ?? "", roomId = api?.User?.currentRoomId ?? "", pendingRoomId = api?.User?.pendingRoomId ?? "",
                reconnectRoomId = (string)Get("reconnectRoomId"), masterId = player?.RoomMasterUserId ?? "",
                running = player?.RoomIsRunning == true, ready = player?.IsReady == true, allReady = client?.GetReadiness().AllReady == true,
                status = (string)Get("status"), progress = (string)Get("roomJoinProgress"), error = error, endpoint = client?.ServerEndpoint ?? "",
                participants = client?.Players.Select(item => item.UserId).Where(id => !string.IsNullOrEmpty(id)).ToArray() ?? Array.Empty<string>(),
                checkingParticipants = client?.Players.Where(item => item.ConnectionState == ParticipantConnectionState.Checking).Select(item => item.UserId).ToArray() ?? Array.Empty<string>()
            };
            var ui = screen.GetComponentInChildren<LobbyUguiView>(true);
            if (ui != null)
            {
                snapshot.uiVisible = ui.canvas.enabled; snapshot.uiLoginVisible = ui.loginPanel.activeSelf;
                snapshot.uiFailureVisible=ui.failurePanel.activeInHierarchy;
                var lobbyScroll=ui.lobbyContent.GetComponentInParent<UnityEngine.UI.ScrollRect>(true);
                snapshot.uiFailureAtTop=ui.lobbyContent.rect.height<=lobbyScroll.viewport.rect.height+1||lobbyScroll.verticalNormalizedPosition>=.99f;
                snapshot.uiCanvasSpaceValid=screen.GetComponentsInChildren<Canvas>().Where(x=>x.enabled).All(x=>((RectTransform)x.transform).rect.width>=1279&&((RectTransform)x.transform).rect.height>=799);
                snapshot.uiWidth = Screen.width; snapshot.uiHeight = Screen.height;
                snapshot.uiProgressVisible = ui.progressPanel.activeSelf; snapshot.uiEnterEnabled = ui.enterButton.interactable;
                snapshot.uiRows = ui.lobbyContent.GetComponentsInChildren<UnityEngine.UI.Button>().Length;
                snapshot.uiStatus = ui.statusText.text; snapshot.uiDetail = ui.selectedDetail.text; snapshot.uiFont = ui.statusText.font?.name;
                snapshot.uiLayoutValid = UiButtonsHaveRaycasts(ui);
            }
            var interior=screen.GetComponentInChildren<LobbyInteriorUguiView>(true);
            if(interior!=null)
            {
                snapshot.uiInteriorVisible=interior.canvas.enabled;snapshot.uiCreateVisible=interior.createModal.activeSelf;snapshot.uiRoomProgressVisible=interior.progressModal.activeSelf;
                snapshot.uiRoomRows=interior.roomsContent.GetComponentsInChildren<UnityEngine.UI.Button>().Length;snapshot.uiParticipantRows=interior.participantsContent.GetComponentsInChildren<UnityEngine.UI.Button>().Length;
                snapshot.uiChatLines=interior.chatContent.GetComponentsInChildren<UnityEngine.UI.Text>().Count(x=>x!=interior.emptyChat);
                snapshot.uiChatText=string.Join("\n",interior.chatContent.GetComponentsInChildren<UnityEngine.UI.Text>().Select(x=>x.text));
                snapshot.uiRoomSelected=screen.UiRoom?.id??"";snapshot.uiJoinRoomEnabled=interior.join.interactable;snapshot.uiChatEnabled=interior.sendChat.interactable;
                snapshot.uiInteriorStatus=interior.status.text;snapshot.uiInteriorChatStatus=interior.chatStatus.text;snapshot.uiInteriorInputEnabled=interior.GetComponent<CanvasGroup>().interactable;
                snapshot.auditBackgroundBlocked=!interior.mainInteraction.interactable;
                snapshot.uiReconnectVisible=interior.reconnectPanel.activeSelf;
                snapshot.uiOperationNotice=interior.notice.text;snapshot.uiChatDraft=interior.chatInput.text;
                snapshot.uiInteriorLayoutValid=InteriorRaycasts(interior);
            }
            var roomView=RoomUi;
            if(roomView!=null)
            {
                snapshot.uiInRoom=roomView.canvas.enabled;snapshot.uiInRoomLayoutValid=RoomRaycasts(roomView);
                snapshot.uiSettingsVisible=roomView.settingsModal.activeSelf;snapshot.uiConfirmVisible=roomView.confirmationModal.activeSelf;snapshot.uiMapVisible=roomView.mapModal.activeSelf;
                snapshot.uiRoomMasterControls=roomView.settings.gameObject.activeSelf;snapshot.uiReadyEnabled=roomView.ready.IsInteractable();snapshot.uiPhaseEnabled=roomView.phase.IsInteractable();
                snapshot.uiRoomSendEnabled=roomView.sendChat.IsInteractable();snapshot.uiRoomInputEnabled=roomView.mainInteraction.interactable;
                snapshot.uiMemberRows=roomView.participantsContent.Cast<Transform>().Count(x=>x.name.StartsWith("Member - ")&&x.gameObject.activeSelf);
                var participantActions=roomView.participantsContent.GetComponentsInChildren<UnityEngine.UI.Button>();
                snapshot.uiRoomParticipantActionsReady=participantActions.Length>0&&participantActions.All(button=>button.IsInteractable());
                snapshot.uiRoomChannel=screen.UiRoomChatChannel;snapshot.uiInRoomSummary=roomView.summary.text;snapshot.uiInRoomChat=string.Join("\n",roomView.chatContent.GetComponentsInChildren<UnityEngine.UI.Text>().Select(x=>x.text));
                snapshot.uiRoomConfirmText=roomView.confirmationText.text;snapshot.uiRoomPasswordStatus=roomView.passwordStatus.text;snapshot.uiRoomNoticeText=roomView.notices.text;
                snapshot.uiTyping=RoomMovementInput.IsEditingText;snapshot.uiMovementBlocked=roomView.BlocksMovement;
                var move=roomView.ReadMovement();snapshot.uiMoveX=move.x;snapshot.uiMoveY=move.y;
            }
            var layout=screen.GetComponentInChildren<UguiSceneLayout>(true);
            snapshot.uiHasSceneLayout=layout!=null;
            snapshot.uiLoginViewCount=screen.GetComponentsInChildren<LobbyUguiView>(true).Length;
            snapshot.uiInteriorViewCount=screen.GetComponentsInChildren<LobbyInteriorUguiView>(true).Length;
            snapshot.uiRoomViewCount=screen.GetComponentsInChildren<RoomUguiView>(true).Length;
            snapshot.uiEventSystemCount=screen.gameObject.scene.GetRootGameObjects().Sum(root=>root.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true).Length);
            snapshot.uiUsesSceneInstances=layout!=null&&ui!=null&&interior!=null&&roomView!=null&&layout.controller==screen&&layout.loginView==ui&&layout.interiorView==interior&&layout.roomView==roomView&&
                !ui.name.EndsWith("(Clone)")&&!interior.name.EndsWith("(Clone)")&&!roomView.name.EndsWith("(Clone)");
            snapshot.auditImmediateBlocked=auditImmediateBlocked;snapshot.auditCaption=auditCaption;
            snapshot.auditPasswordsEmpty=new[]{"joinRoomPassword","createRoomPassword","reconnectPassword","editRoomPassword"}.All(field=>string.IsNullOrEmpty(Get(field) as string));
            snapshot.auditPasswordWidgetsEmpty=screen.GetComponentsInChildren<UnityEngine.UI.InputField>(true).Where(field=>field.contentType==UnityEngine.UI.InputField.ContentType.Password).All(field=>field.text.Length==0);
            snapshot.auditFocusClear=UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject==null;
            var temporary = Path.Combine(directory, "state.tmp"); File.WriteAllText(temporary, JsonUtility.ToJson(snapshot));
            File.Copy(temporary, Path.Combine(directory, "state.json"), true);
            File.AppendAllText(Path.Combine(directory, "trace.jsonl"), JsonUtility.ToJson(snapshot) + Environment.NewLine);
        }
        private static void CaptureUi(LobbyUguiView ui, string path)
            => CaptureCanvas(ui.canvas,path);
        private RoomUguiView RoomUi => screen.GetComponentInChildren<RoomUguiView>(true);
        private void AuditBurst(UnityEngine.UI.Button button)
        {
            button.onClick.Invoke();
            auditImmediateBlocked=!button.IsInteractable();auditCaption=screen.UiStatus;
            for(int repeat=1;repeat<100;repeat++)button.onClick.Invoke();
        }
        private IEnumerator RevokeUiSession()
        {
            var current=(TrainingApiClient)Get("api");
            using(var revoker=new TrainingApiClient(current.BaseUrl))
            {
                foreach(var name in new[]{"accessToken","expiresAt"})
                    typeof(TrainingApiClient).GetField(name,Flags).SetValue(revoker,typeof(TrainingApiClient).GetField(name,Flags).GetValue(current));
                yield return revoker.Logout(()=>{},message=>throw new InvalidOperationException(message));
            }
            yield return current.GetMe(_=>{},message=>typeof(ApiLobbyScreen).GetMethod("Failure",Flags).Invoke(screen,new object[]{message}));
        }
        private static Command ReadCommand(string path)
        {
            // 검사 도구가 파일을 교체하는 순간과 읽기가 겹치면 다음 주기에 다시 읽습니다.
            string json;
            try { json = File.ReadAllText(path); }
            catch (IOException) { return null; }
            return JsonUtility.FromJson<Command>(json);
        }
        private static void RoomClick(UnityEngine.UI.Button button)
        {
            if (!button.gameObject.activeInHierarchy) return;
            var events=UnityEngine.EventSystems.EventSystem.current;
            UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject,new UnityEngine.EventSystems.PointerEventData(events){button=UnityEngine.EventSystems.PointerEventData.InputButton.Left},UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
        }
        private static void CaptureCanvas(Canvas canvas, string path)
        {
            // 숨겨진 검사 창은 화면 버퍼가 없을 수 있으므로 해당 Canvas만 텍스처로 렌더링합니다.
            var root = new GameObject("Probe UI Capture", typeof(Camera));
            var camera = root.GetComponent<Camera>(); camera.enabled = false;camera.clearFlags = CameraClearFlags.SolidColor;camera.backgroundColor = Color.black;
            var target = new RenderTexture(Screen.width, Screen.height, 24);
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            var mode = canvas.renderMode;var previousCamera = canvas.worldCamera;var previousTarget = RenderTexture.active;
            try
            {
                camera.targetTexture = target;canvas.renderMode = RenderMode.ScreenSpaceCamera;canvas.worldCamera = camera;canvas.planeDistance = 10;
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);texture.Apply();File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode = mode;canvas.worldCamera = previousCamera;camera.targetTexture = null;RenderTexture.active = previousTarget;
                Destroy(texture);Destroy(target);Destroy(root);Canvas.ForceUpdateCanvases();
            }
        }
        private static bool UiButtonsHaveRaycasts(LobbyUguiView ui)
        {
            if (!ui.canvas.enabled) return true;
            var events = UnityEngine.EventSystems.EventSystem.current;
            if (events == null) return false;
            Canvas.ForceUpdateCanvases();
            foreach (var target in ui.GetComponentsInChildren<UnityEngine.UI.Selectable>())
            {
                if (!target.interactable) continue;
                var rect = (RectTransform)target.transform;
                var point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
                if (target.transform.IsChildOf(ui.lobbyContent) && !RectTransformUtility.RectangleContainsScreenPoint(ui.lobbyContent.parent as RectTransform, point)) continue;
                if (point.x < 0 || point.x > Screen.width || point.y < 0 || point.y > Screen.height) return false;
                var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                events.RaycastAll(new UnityEngine.EventSystems.PointerEventData(events) { position = point }, hits);
                if (hits.Count == 0 || (hits[0].gameObject.transform != target.transform && !hits[0].gameObject.transform.IsChildOf(target.transform))) return false;
            }
            return true;
        }
        private static bool InteriorRaycasts(LobbyInteriorUguiView ui)
        {
            if(!ui.canvas.enabled)return true;
            var events=UnityEngine.EventSystems.EventSystem.current;if(events==null)return false;Canvas.ForceUpdateCanvases();
            foreach(var target in ui.GetComponentsInChildren<UnityEngine.UI.Selectable>())
            {
                if(!target.IsInteractable())continue;
                var rect=(RectTransform)target.transform;var point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
                var scroll=target.GetComponentInParent<UnityEngine.UI.ScrollRect>();if(scroll!=null&&!RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport,point))continue;
                if(point.x<0||point.x>Screen.width||point.y<0||point.y>Screen.height)return false;
                var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();events.RaycastAll(new UnityEngine.EventSystems.PointerEventData(events){position=point},hits);
                if(hits.Count==0||(hits[0].gameObject.transform!=target.transform&&!hits[0].gameObject.transform.IsChildOf(target.transform)))return false;
            }
            return true;
        }
        private static bool RoomRaycasts(RoomUguiView ui)
        {
            if(!ui.canvas.enabled)return true;
            var events=UnityEngine.EventSystems.EventSystem.current;if(events==null)return false;Canvas.ForceUpdateCanvases();
            foreach(var target in ui.GetComponentsInChildren<UnityEngine.UI.Selectable>())
            {
                if(!target.IsInteractable())continue;
                var rect=(RectTransform)target.transform;var point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
                var scroll=target.GetComponentInParent<UnityEngine.UI.ScrollRect>();if(scroll!=null&&!RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport,point))continue;
                if(point.x<0||point.x>Screen.width||point.y<0||point.y>Screen.height){Debug.LogWarning("ROOM RAYCAST OUTSIDE: "+target.name);return false;}
                var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();events.RaycastAll(new UnityEngine.EventSystems.PointerEventData(events){position=point},hits);
                if(hits.Count==0||(hits[0].gameObject.transform!=target.transform&&!hits[0].gameObject.transform.IsChildOf(target.transform))){Debug.LogWarning("ROOM RAYCAST BLOCKED: "+target.name+" by "+(hits.Count>0?hits[0].gameObject.name:"none"));return false;}
            }
            return true;
        }
    }
}
#endif
