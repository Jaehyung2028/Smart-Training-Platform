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
        private bool movementRejected;
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
            public string uiRoomSelected, uiChatText, uiInteriorChatStatus, uiOperationNotice, uiChatDraft;
            public bool uiInRoom, uiInRoomLayoutValid, uiSettingsVisible, uiConfirmVisible, uiMapVisible, uiRoomMasterControls, uiReadyEnabled, uiPhaseEnabled, uiRoomSendEnabled, uiRoomInputEnabled, uiTyping, uiMovementBlocked;
            public int uiMemberRows, uiRoomChannel;
            public float uiMoveX, uiMoveY;
            public string uiInRoomSummary, uiInRoomChat, uiRoomConfirmText, uiRoomPasswordStatus, uiRoomNoticeText;
            public bool uiHasSceneLayout, uiUsesSceneInstances, uiRoomParticipantActionsReady;
            public int uiLoginViewCount, uiInteriorViewCount, uiRoomViewCount, uiEventSystemCount;
            public bool auditImmediateBlocked, auditPasswordsEmpty, auditPasswordWidgetsEmpty, auditFocusClear, auditBackgroundBlocked;
            public string auditCaption;
            public bool modelPresent, modelVisible, contentReady;
            public ulong modelOwner, localClientId, modelNetworkId;
            public Quaternion modelRotation;
            public int modelCount;
            public string modelControlText;
            public bool modelControlVisible;
            public bool partDetailsVisible, selectedPartHighlighted;
            public string partTitle, partDescription;
            public bool partListVisible, partListSelectionVisible;
            public bool contentMenuOpen, contentOptionsOpen, contentCameraCanInteract;
            public float contentVolume;
            public bool contentParticipantsVisible;
            public int contentParticipantRows;
            public string[] contentParticipantLabels;
            public string contentParticipantButton;
            public bool contentChatVisible, contentChatCanSend, contentChatFocused;
            public int contentChatRows, contentChatOwnGreenRows;
            public string contentChatText, contentChatDraft;
            public string[] avatarUserNames, nameTagTexts;
            public float[] avatarYaws;
            public Vector3[] avatarPositions;
            public bool movementRejected;
            public int nameTagCount, ownNameTagCount;
            public float nameTagFacingError;
            public int contentCameraCount, contentAvatarCount;
            public int roomContentNodeId;
            public int learningPercent, learningRun, observedParts, requiredParts, observationNodeCount;
            public bool learningComplete;
            public string learningLabel;
            public string[] participantLearning;
            public string[] scenarioHeldTools;
            public Vector3 scenarioLocalHandPosition, scenarioLocalHandRestPosition, scenarioWorkHandPosition;
            public Quaternion scenarioWorkHandRotation;
            public float scenarioWorkContactError, scenarioWorkRotationError, scenarioToolSpinAngle, scenarioToolGrip;
            public string[] scenarioVisibleToolIds;
            public bool scenarioWorkHandPresent, scenarioToolSelectionEnabled;
            public bool scenarioResultVisible, scenarioResultRestartEnabled, scenarioResultReturnEnabled;
            public double scenarioElapsed;
            public int scenarioToolErrors, scenarioTargetErrors, scenarioRunRevision, scenarioResultRows;
            public string scenarioResultSummary;
            public string[] scenarioResultActors;
            public int[] scenarioResultToolErrors, scenarioResultTargetErrors;
            public string scenarioRole, scenarioInspector, scenarioTechnician;
            public bool scenarioInspectorEnabled, scenarioTechnicianEnabled;
            public string scenarioInspectorButton, scenarioTechnicianButton;
            public string[] nameTagDetails, scenarioVisibleObjects, scenarioRoleAssignments;
            public bool playerStringCacheStable, playerListCacheStable;
            public string[] avatarModels;
            public float[] avatarAnimationSpeeds;
            public bool scenarioContactReplaced, scenarioMachineReady;
            public bool scenarioWorking;
            public bool scenarioCanCancel, scenarioCancelVisible;
            public string scenarioActorText, scenarioWorkStatus;
            public float scenarioWorkFill;
            public float scenarioWorkProgress;
            public Vector3 scenarioBoltPosition, scenarioScrewPosition, scenarioPinPosition;
            public Quaternion scenarioScrewRotation;
            public float scenarioHammerLift;
            public Quaternion scenarioBoltRotation;
            public bool scenarioReady, scenarioComplete, scenarioToolMenuVisible;
            public int scenarioToolOptions;
            public string scenarioCardTitle, scenarioCardGoal, scenarioCardRequirement, scenarioCardProgress;
            public string scenarioNotice;
            public bool scenarioNoticeVisible;
            public int scenarioCompletedSteps, scenarioRevision;
            public string scenarioSelectedTool, scenarioFeedback;
            public Vector3 localPosition;
            public bool scenarioPresent, scenarioValid;
            public int scenarioStepCount, scenarioObjectCount;
            public bool videoPresent, videoReady, videoPlaying, videoCanControl, videoControlsVisible;
            public double videoPosition, videoDuration, videoLocalTime;
            public float videoVolume;
            public string videoError = "";
            public string videoSubtitle = "";
            public bool videoSubtitlesEnabled, videoSubtitleVisible;
            public int selectedNodeCount;
            public string[] selectedPartNames = Array.Empty<string>();
            public float modelMinimumY;
            public Vector3[] disassemblyPartPositions = Array.Empty<Vector3>();
            public int playbackDirection;
            public string playbackStatus = "";
            public bool playbackForwardEnabled, playbackPauseEnabled, playbackReverseEnabled;
            public float explosionAmount;
            public Vector3 batteryRootPosition;
            public int fadedPartCount;
            public float surroundingsAlpha = 1;
            public float batteryOpacity = 1;
            public int partListCount, partListVisibleRows, partTreeMaxDepth, selectedPartRendererCount;
            public string partListSelectedName;
            public bool contentActionsEnabled;
            public bool contentActionsPresent, contentActionsOpen, contentResetVisible, contentResetEnabled, contentActionsBottomRight;
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
        // 좌표를 직접 바꾸지 않고 일반 이동 RPC를 사용해 실제 서버의 이동 제한을 통과합니다.
        private IEnumerator WalkToScenarioWorkBench()
        {
            var player = screen.UiLocalPlayer;
            var destination = new Vector3(-0.4f, 1, 2.5f);
            float until = Time.unscaledTime + 20;
            while (player != null && player.RoomIsRunning &&
                Vector3.Distance(player.Position, destination) > 0.2f && Time.unscaledTime < until)
            {
                var direction = destination - player.Position;
                player.MoveRpc(Vector2.ClampMagnitude(new Vector2(direction.x, direction.z), 1), player.ViewYaw);
                yield return new WaitForEndOfFrame();
            }
            if (player != null)
            {
                player.MoveRpc(Vector2.zero, player.ViewYaw);
            }
        }

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
                            case "roomContent":
                                var chosenContent = Array.Find(screen.UiContents, item => item.id.ToString() == command.value);
                                screen.UiChangeRoomContent(chosenContent);
                                break;
                            case "staleReady": screen.UiLocalPlayer.SetReadyRpc(true, int.Parse(command.value)); break;
                            case "roomContentOpen": RoomClick(RoomUi.changeContent); break;
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
                            // 실제 두 클라이언트에서 소유권 경쟁·Transform 동기화·반환을 확인하는 진단 명령입니다.
                            case "contentButton":
                                var contentButton = FindUi<UnityEngine.UI.Button>()
                                    .First(button => button.GetComponentInChildren<UnityEngine.UI.Text>()?.text == command.value);
                                if (!contentButton.IsInteractable())
                                {
                                    throw new InvalidOperationException("콘텐츠 버튼이 비활성화되어 있습니다.");
                                }
                                contentButton.onClick.Invoke();
                                break;
                            case "contentVolume":
                                var options = FindUi<RectTransform>().First(item => item.name == "Content Options");
                                options.GetComponentsInChildren<UnityEngine.UI.Slider>()[2].value = float.Parse(command.value, System.Globalization.CultureInfo.InvariantCulture);
                                break;
                            case "contentEscape":
                                var session = screen.GetComponent<ContentSceneSession>();
                                var hud = (ContentHudController)typeof(ContentSceneSession).GetField("hud", Flags).GetValue(session);
                                hud.HandleBack();
                                break;
                            case "scenarioWorldCapture":
                                CaptureWorld(Path.Combine(directory, Path.GetFileName(command.value)));
                                break;
                            case "scenarioCapture":
                                CaptureCanvas(UnityEngine.Object.FindAnyObjectByType<ContentSceneLayout>().Hud.GetComponent<Canvas>(),
                                    Path.Combine(directory, "scenario.png"));
                                break;
                            case "scenarioToolScroll":
                                FindUi<UnityEngine.UI.ScrollRect>().First(item => item.name == "Tool List Scroll").verticalNormalizedPosition = float.Parse(command.value, System.Globalization.CultureInfo.InvariantCulture);
                                break;
                            case "scenarioLookAt":
                                var lookTarget = UnityEngine.Object.FindAnyObjectByType<ScenarioContent>().GetComponentsInChildren<ScenarioObject>().First(item => item.Id == command.value);
                                var scenarioCamera = UnityEngine.Object.FindAnyObjectByType<FirstPersonCameraController>();
                                var lookAngles = Quaternion.LookRotation(lookTarget.WorkAnchor.position - scenarioCamera.ViewCamera.transform.position).eulerAngles;
                                typeof(FirstPersonCameraController).GetField("yaw", Flags).SetValue(scenarioCamera, lookAngles.y);
                                typeof(FirstPersonCameraController).GetField("pitch", Flags).SetValue(scenarioCamera, Mathf.DeltaAngle(0, lookAngles.x));
                                break;
                            case "scenarioRawRole":
                                var roleArguments = command.value.Split('|');
                                SharedScenario.Find(screen.UiLocalPlayer.NetworkManager).SelectRoleRpc((ScenarioRole)int.Parse(roleArguments[0]), int.Parse(roleArguments[1]));
                                break;
                            case "scenarioSelectTool":
                                var toolButton = FindUi<UnityEngine.UI.Button>()
                                    .First(item => item.name == "Scenario Tool " + command.value);
                                if (!toolButton.IsInteractable())
                                {
                                    throw new InvalidOperationException("개인 공구 버튼이 비활성화되어 있습니다.");
                                }
                                toolButton.onClick.Invoke();
                                break;
                            case "scenarioRawSelectTool":
                                var toolParts = command.value.Split('|');
                                SharedScenario.Find(screen.UiLocalPlayer.NetworkManager).SelectToolRpc(toolParts[0], int.Parse(toolParts[1]));
                                break;
                            case "movementInputMode":
                                typeof(RoomNetworkClient).GetField("nextInputAt", Flags).SetValue(
                                    Get("network"), command.enabled ? float.MaxValue : 0f);
                                break;
                            case "movementFor":
                                operation = MoveFor(command.value);
                                break;
                            case "movementWarp":
                                screen.UiLocalPlayer.GetComponent<Unity.Netcode.Components.NetworkTransform>().SetState(
                                    command.enabled ? new Vector3(float.NaN, float.PositiveInfinity, 500) : Vector3.one * 500,
                                    Quaternion.Euler(80, 220, 30), Vector3.one * 20, teleportDisabled: false);
                                break;
                            case "movementRemote":
                                movementRejected = false;
                                try { screen.UiRoomPlayers.First(item => !item.IsOwner).MoveRpc(Vector2.right, 180); }
                                catch (RpcException) { movementRejected = true; }
                                break;
                            case "playerLookYaw":
                                var lookCamera = UnityEngine.Object.FindAnyObjectByType<FirstPersonCameraController>();
                                typeof(FirstPersonCameraController).GetField("yaw", Flags).SetValue(
                                    lookCamera, float.Parse(command.value, System.Globalization.CultureInfo.InvariantCulture)
                                );
                                break;
                            case "remotePlayerYaw":
                                screen.UiRoomPlayers.First(item => !item.IsOwner).SetViewYaw(240);
                                break;
                            case "contentParticipantsToggle":
                                FindUi<UnityEngine.UI.Button>().First(item => item.name == "Content Participants Toggle").onClick.Invoke();
                                break;
                            case "contentChatToggle":
                                FindUi<UnityEngine.UI.Button>().First(item => item.name == "Content Chat Toggle").onClick.Invoke();
                                break;
                            case "contentChatDraft":
                                var contentInput = FindUi<UnityEngine.UI.InputField>().First(item => item.name == "Content Chat Input");
                                contentInput.text = command.value;
                                if (command.enabled)
                                {
                                    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(contentInput.gameObject);
                                    contentInput.ActivateInputField();
                                }
                                break;
                            case "contentChatSend":
                                var contentSend = FindUi<UnityEngine.UI.Button>().First(item => item.name == "Content Chat Send");
                                if (!contentSend.IsInteractable()) throw new InvalidOperationException("콘텐츠 채팅 전송이 비활성화되어 있습니다.");
                                contentSend.onClick.Invoke();
                                break;
                            case "scenarioWalk":
                                operation = WalkToScenarioWorkBench();
                                break;
                            case "scenarioInteract":
                                UnityEngine.Object.FindAnyObjectByType<ScenarioContent>().Interact(command.value);
                                break;
                            case "scenarioRawInteract":
                                var rawParts = command.value.Split('|');
                                SharedScenario.Find(screen.UiLocalPlayer.NetworkManager).InteractRpc(rawParts[0], int.Parse(rawParts[1]));
                                break;
                            case "scenarioCancel":
                                var cancelWork = FindUi<UnityEngine.UI.Button>()
                                    .First(item => item.name == "Scenario Cancel Work");
                                if (!cancelWork.IsInteractable())
                                {
                                    throw new InvalidOperationException("작업 취소 버튼이 비활성화되어 있습니다.");
                                }
                                cancelWork.onClick.Invoke();
                                break;
                            case "scenarioRawCancel":
                                SharedScenario.Find(screen.UiLocalPlayer.NetworkManager).CancelWorkRpc(int.Parse(command.value));
                                break;
                            case "scenarioResultRestart":
                            case "scenarioResultReturn":
                                string resultButtonName = command.action == "scenarioResultRestart" ? "Scenario Result Restart" : "Scenario Result Return";
                                var resultButton = FindUi<UnityEngine.UI.Button>().First(item => item.name == resultButtonName);
                                if (!resultButton.IsInteractable())
                                {
                                    throw new InvalidOperationException("결과 화면의 방장 버튼을 사용할 수 없습니다.");
                                }
                                resultButton.onClick.Invoke();
                                break;
                            case "scenarioReset":
                                SharedScenario.Find(screen.UiLocalPlayer.NetworkManager).ResetRpc(
                                    UnityEngine.Object.FindAnyObjectByType<ScenarioContent>().Revision);
                                break;
                            case "observePart":
                                var observationModel = UnityEngine.Object.FindAnyObjectByType<ModelViewer>();
                                var observationNode = observationModel.Parts[int.Parse(command.value)];
                                observationModel.SelectPartFromList(observationNode);
                                break;
                            case "catalogCreate":
                                var catalogChoice = command.value.Split('|');
                                Set("selectedContentId", catalogChoice[0]);
                                Set("selectedContentNodeId", int.Parse(catalogChoice[1]));
                                Set("roomName", "카탈로그 샘플 " + catalogChoice[1]);
                                Set("maxUsersText", "4");
                                operation = Invoke("CreateRoom");
                                break;
                            case "scenarioCreate":
                                Set("roomName", "시나리오 데이터 확인");
                                Set("maxUsersText", "4");
                                Set("selectedContentId", "safety-training");
                                Set("selectedContentNodeId", 11);
                                operation = Invoke("CreateRoom");
                                break;
                            case "videoCreate":
                                Set("roomName", "공유 영상 확인");
                                Set("maxUsersText", "4");
                                Set("selectedContentId", "safety-training");
                                Set("selectedContentNodeId", 12);
                                operation = Invoke("CreateRoom");
                                break;
                            case "videoCapture":
                                var captureVideo = UnityEngine.Object.FindAnyObjectByType<VideoContentPlayer>().GetComponent<UnityEngine.Video.VideoPlayer>();
                                var captureTexture = captureVideo.targetTexture;
                                var previousTarget = RenderTexture.active;
                                var captureFrame = new Texture2D(captureTexture.width, captureTexture.height, TextureFormat.RGB24, false);
                                try
                                {
                                    RenderTexture.active = captureTexture;
                                    captureFrame.ReadPixels(new Rect(0, 0, captureTexture.width, captureTexture.height), 0, 0);
                                    captureFrame.Apply();
                                    File.WriteAllBytes(Path.Combine(directory, Path.GetFileName(command.value)), captureFrame.EncodeToPNG());
                                }
                                finally
                                {
                                    RenderTexture.active = previousTarget;
                                    Destroy(captureFrame);
                                }
                                break;
                            case "videoToggle":
                                var videoToggle = FindUi<UnityEngine.UI.Button>().First(button => button.name == "Video Play Pause");
                                if (!videoToggle.IsInteractable())
                                {
                                    throw new InvalidOperationException("영상 재생 버튼이 비활성화되어 있습니다.");
                                }
                                videoToggle.onClick.Invoke();
                                break;
                            case "videoControl":
                                var videoControl = FindUi<UnityEngine.UI.Button>().First(button => button.name == command.value);
                                if (!videoControl.IsInteractable())
                                {
                                    throw new InvalidOperationException("영상 조작 버튼이 비활성화되어 있습니다.");
                                }
                                videoControl.onClick.Invoke();
                                break;
                            case "videoSeek":
                                var videoSeek = FindUi<UnityEngine.UI.Slider>().First(slider => slider.name == "Video Seek");
                                if (!videoSeek.IsInteractable())
                                {
                                    throw new InvalidOperationException("영상 구간 이동이 비활성화되어 있습니다.");
                                }
                                videoSeek.value = float.Parse(command.value, System.Globalization.CultureInfo.InvariantCulture);
                                break;
                            case "videoVolume":
                                FindUi<UnityEngine.UI.Slider>().First(slider => slider.name == "Video Local Volume").value = float.Parse(command.value, System.Globalization.CultureInfo.InvariantCulture);
                                break;
                            case "videoRawPlay":
                                SharedVideo.Find(screen.UiLocalPlayer.NetworkManager).SetPlayingRpc(command.enabled);
                                break;
                            case "videoRawSeek":
                                SharedVideo.Find(screen.UiLocalPlayer.NetworkManager).SeekRpc(double.Parse(command.value, System.Globalization.CultureInfo.InvariantCulture));
                                break;
                            case "videoRawConfigure":
                                SharedVideo.Find(screen.UiLocalPlayer.NetworkManager).ConfigureRpc(double.Parse(command.value, System.Globalization.CultureInfo.InvariantCulture));
                                break;
                            case "modelCreate":
                                Set("roomName", "공유 모델 확인");
                                Set("maxUsersText", "4");
                                Set("selectedContentId", "sample-training");
                                Set("selectedContentNodeId", 2);
                                operation = Invoke("CreateRoom");
                                break;
                            case "partListOpen":
                                var listAction = FindUi<UnityEngine.UI.Button>().First(button => button.name == "부품 목록");
                                if (!listAction.IsInteractable())
                                {
                                    throw new InvalidOperationException("부품 목록 메뉴가 비활성화되어 있습니다.");
                                }
                                listAction.onClick.Invoke();
                                break;
                            case "partListCloseClick":
                                FindUi<UnityEngine.UI.Button>().First(button => button.name == "Close Part List").onClick.Invoke();
                                break;
                            case "partListFold":
                                var foldPanel = FindUi<RectTransform>().First(item => item.name == "Model Part List");
                                var foldButton = foldPanel.GetComponentsInChildren<UnityEngine.UI.Button>().First(button => button.name == "Expand - " + command.value);
                                if (!foldButton.IsInteractable())
                                {
                                    throw new InvalidOperationException("펼치기 버튼이 비활성화되어 있습니다.");
                                }
                                foldButton.onClick.Invoke();
                                break;
                            case "partListSelect":
                                var listPanel = FindUi<RectTransform>().First(item => item.name == "Model Part List");
                                var listRow = listPanel.GetComponentsInChildren<UnityEngine.UI.Button>().First(button => button.name == "Part - " + command.value);
                                if (!listRow.IsInteractable())
                                {
                                    throw new InvalidOperationException("부품 목록 행을 선택할 수 없습니다.");
                                }
                                listRow.onClick.Invoke();
                                break;
                            case "playback":
                                int playbackDirection = int.Parse(command.value, System.Globalization.CultureInfo.InvariantCulture);
                                string playbackLabel = playbackDirection > 0 ? "분해 재생" : playbackDirection < 0 ? "재조립" : "일시정지";
                                var playbackButton = FindUi<UnityEngine.UI.Button>().First(button => button.name == playbackLabel);
                                if (!playbackButton.IsInteractable())
                                {
                                    throw new InvalidOperationException("재생 버튼이 비활성화되어 있습니다: " + playbackLabel);
                                }
                                playbackButton.onClick.Invoke();
                                break;
                            case "playbackRaw":
                                SharedModel.Find(screen.UiLocalPlayer.NetworkManager).SetPlaybackRpc(int.Parse(command.value, System.Globalization.CultureInfo.InvariantCulture), 5);
                                break;
                            case "explosionValue":
                                var explosionSlider = FindUi<UnityEngine.UI.Slider>().First(slider => slider.name == "Model Explosion");
                                if (!explosionSlider.IsInteractable())
                                {
                                    throw new InvalidOperationException("분해 보기 슬라이더가 비활성화되어 있습니다.");
                                }
                                explosionSlider.value = float.Parse(command.value, System.Globalization.CultureInfo.InvariantCulture);
                                break;
                            case "explosionRaw":
                                SharedModel.Find(screen.UiLocalPlayer.NetworkManager).SetExplosionRpc(float.Parse(command.value, System.Globalization.CultureInfo.InvariantCulture));
                                break;
                            case "transparencyToggle":
                                var transparencyToggle = FindUi<UnityEngine.UI.Button>().First(button => button.name == "주변 반투명: 꺼짐");
                                if (!transparencyToggle.IsInteractable())
                                {
                                    throw new InvalidOperationException("투명 처리 버튼이 비활성화되어 있습니다.");
                                }
                                transparencyToggle.onClick.Invoke();
                                break;
                            case "transparencyValue":
                                var transparencySlider = FindUi<UnityEngine.UI.Slider>().First(slider => slider.name == "Surroundings Transparency");
                                if (!transparencySlider.IsInteractable())
                                {
                                    throw new InvalidOperationException("투명도 슬라이더가 비활성화되어 있습니다.");
                                }
                                transparencySlider.value = float.Parse(command.value, System.Globalization.CultureInfo.InvariantCulture);
                                break;
                            case "partSelect":
                                var viewer = UnityEngine.Object.FindAnyObjectByType<ModelViewer>();
                                var partRenderer = viewer.GetComponentsInChildren<Renderer>().First(item => item.name == command.value);
                                typeof(ModelViewer).GetMethod("SelectPart", Flags).Invoke(viewer, new object[] { partRenderer });
                                break;
                            case "partCloseClick":
                                var closePart = FindUi<UnityEngine.UI.Button>(true).First(item => item.name == "Close Part Details");
                                if (!closePart.IsInteractable())
                                {
                                    throw new InvalidOperationException("부품 설명 닫기 버튼을 사용할 수 없습니다.");
                                }
                                closePart.onClick.Invoke();
                                break;
                            case "modelAcquire":
                                SharedModel.Find(screen.UiLocalPlayer.NetworkManager).RequestControlRpc();
                                break;
                            case "modelTurn":
                                var model = SharedModel.Find(screen.UiLocalPlayer.NetworkManager);
                                if (!model.IsOwner)
                                {
                                    throw new InvalidOperationException("모델 소유자만 회전할 수 있습니다.");
                                }
                                model.transform.rotation = Quaternion.Euler(20, float.Parse(command.value, System.Globalization.CultureInfo.InvariantCulture), 10);
                                break;
                            case "contentActionsToggle":
                                var menuRoot = FindUi<RectTransform>(true).First(item => item.name == "Content Actions");
                                var actionToggle = menuRoot.GetChild(0).GetComponent<UnityEngine.UI.Button>();
                                if (!actionToggle.IsInteractable())
                                {
                                    return;
                                }
                                actionToggle.onClick.Invoke();
                                break;
                            case "contentResetClick":
                                var resetRoot = FindUi<RectTransform>(true).First(item => item.name == "Content Actions");
                                var resetButton = resetRoot.Find("Content Action List").GetComponentsInChildren<UnityEngine.UI.Button>().First(button => button.name == "회전 초기화");
                                if (!resetButton.interactable)
                                {
                                    throw new InvalidOperationException("회전 초기화 버튼이 비활성화되어 있습니다.");
                                }
                                resetButton.onClick.Invoke();
                                break;
                            case "modelReset":
                                SharedModel.Find(screen.UiLocalPlayer.NetworkManager).ResetRotationRpc();
                                break;
                            case "modelRelease":
                                var releasing = SharedModel.Find(screen.UiLocalPlayer.NetworkManager);
                                releasing.ReleaseControlRpc(releasing.transform.rotation);
                                break;
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
            var shared = SharedModel.Find(player?.NetworkManager);
            var learning = screen.UiLocalPlayer != null ? screen.UiLocalPlayer.GetComponent<TrainingProgress>() : null;
            snapshot.roomContentNodeId = screen.UiLocalPlayer?.RoomContentNodeId ?? 0;
            snapshot.learningPercent = learning?.Percent ?? 0;
            snapshot.learningRun = learning?.Run ?? 0;
            snapshot.learningComplete = learning?.IsComplete == true;
            snapshot.learningLabel = learning?.Label ?? "";
            snapshot.participantLearning = screen.UiRoomPlayers.Select(participant => participant.GetComponent<TrainingProgress>()?.Label ?? "").ToArray();
            var observedModel = UnityEngine.Object.FindAnyObjectByType<ModelViewer>();
            snapshot.observedParts = observedModel?.ObservedPartCount ?? 0;
            snapshot.requiredParts = observedModel?.RequiredPartCount ?? 0;
            snapshot.observationNodeCount = observedModel?.Parts.Count ?? 0;
            snapshot.contentReady = screen.GetComponent<ContentSceneSession>()?.SceneReady == true;
            snapshot.modelPresent = shared != null;
            if (shared != null)
            {
                snapshot.modelOwner = shared.OwnerClientId;
                snapshot.modelNetworkId = shared.NetworkObjectId;
                snapshot.localClientId = player.NetworkManager.LocalClientId;
                snapshot.modelRotation = shared.transform.rotation;
                snapshot.modelCount = player.NetworkManager.SpawnManager.SpawnedObjects.Values.Count(item => item.GetComponent<SharedModel>() != null);
                snapshot.modelVisible = shared.GetComponentsInChildren<Renderer>().Any(renderer => renderer.enabled);

            }
            var partList = FindUi<RectTransform>(true).FirstOrDefault(item => item.name == "Model Part List");
            snapshot.partListVisible = partList != null && partList.gameObject.activeInHierarchy;
            if (partList != null)
            {
                var listRows = partList.GetComponentsInChildren<UnityEngine.UI.Button>(true).Where(button => button.name.StartsWith("Part - ")).ToArray();
                snapshot.partListCount = listRows.Length;
                snapshot.partListVisibleRows = listRows.Count(button => button.gameObject.activeInHierarchy);
                var selectedRow = listRows.FirstOrDefault(button => button.GetComponentInChildren<UnityEngine.UI.Text>().text.StartsWith("선택 · "));
                snapshot.partListSelectedName = selectedRow != null ? selectedRow.name.Substring("Part - ".Length) : "";
                if (snapshot.partListVisible && selectedRow != null)
                {
                    var viewport = selectedRow.GetComponentInParent<UnityEngine.UI.ScrollRect>().viewport;
                    var selectedRect = selectedRow.GetComponent<RectTransform>();
                    snapshot.partListSelectionVisible = viewport.rect.Contains(viewport.InverseTransformPoint(selectedRect.TransformPoint(selectedRect.rect.center)));
                }
            }
            var cameras = UnityEngine.Object.FindObjectsByType<FirstPersonCameraController>();
            snapshot.contentCameraCount = cameras.Length;
            snapshot.contentCameraCanInteract = cameras.Length > 0 && cameras[0].CanInteract;
            snapshot.contentVolume = AudioListener.volume;
            var contentParticipants = FindUi<RectTransform>().FirstOrDefault(item => item.name == "Content Participants");
            snapshot.contentParticipantsVisible = contentParticipants != null;
            snapshot.contentParticipantLabels = contentParticipants == null ? Array.Empty<string>() :
                contentParticipants.GetComponentsInChildren<UnityEngine.UI.Text>().Where(item => item.name == "Content Participant Row").Select(item => item.text).ToArray();
            snapshot.contentParticipantRows = snapshot.contentParticipantLabels.Length;
            snapshot.contentParticipantButton = FindUi<UnityEngine.UI.Button>()
                .FirstOrDefault(item => item.name == "Content Participants Toggle")?.GetComponentInChildren<UnityEngine.UI.Text>().text ?? "";
            var contentChat = FindUi<RectTransform>().FirstOrDefault(item => item.name == "Content Room Chat");
            snapshot.contentChatVisible = contentChat != null;
            if (contentChat != null)
            {
                var contentMessages = contentChat.GetComponentsInChildren<UnityEngine.UI.Text>().Where(item => item.name == "Content Chat Message").ToArray();
                snapshot.contentChatRows = contentMessages.Length;
                snapshot.contentChatText = string.Join("\n", contentMessages.Select(item => item.text));
                snapshot.contentChatOwnGreenRows = contentMessages.Count(item => item.color.g > item.color.r && item.color.g > item.color.b);
                var contentInput = contentChat.GetComponentInChildren<UnityEngine.UI.InputField>();
                snapshot.contentChatDraft = contentInput.text;
                snapshot.contentChatFocused = contentInput.isFocused;
                snapshot.contentChatCanSend = contentChat.GetComponentsInChildren<UnityEngine.UI.Button>().First(item => item.name == "Content Chat Send").IsInteractable();
            }
            var avatars = screen.UiRoomPlayers.OrderBy(item => item.UserName).ToArray();
            snapshot.playerStringCacheStable = avatars.All(item => ReferenceEquals(item.UserName, item.UserName) &&
                ReferenceEquals(item.UserId, item.UserId) && ReferenceEquals(item.RoomMasterUserId, item.RoomMasterUserId));
            snapshot.playerListCacheStable = ReferenceEquals(screen.UiRoomPlayers, screen.UiRoomPlayers);
            snapshot.avatarUserNames = avatars.Select(item => item.UserName).ToArray();
            snapshot.avatarYaws = avatars.Select(item => item.transform.eulerAngles.y).ToArray();
            snapshot.avatarPositions = avatars.Select(item => item.Position).ToArray();
            snapshot.movementRejected = movementRejected;
            snapshot.avatarModels = avatars.Select(item => item.AvatarRoot != null ? item.AvatarRoot.name : "").ToArray();
            snapshot.avatarAnimationSpeeds = avatars.Select(item => item.AvatarRoot != null && item.AvatarRoot.gameObject.activeInHierarchy ? item.AvatarRoot.GetComponent<Animator>().GetFloat("Speed") : 0).ToArray();
            var nameTags = avatars.SelectMany(item => item.GetComponentsInChildren<UnityEngine.UI.Text>())
                .Where(item => item.name == "Player Name").ToArray();
            snapshot.nameTagCount = nameTags.Length;
            snapshot.nameTagTexts = nameTags.Select(item => item.text).ToArray();
            snapshot.nameTagDetails = avatars.SelectMany(item => item.GetComponentsInChildren<UnityEngine.UI.Text>())
                .Where(item => item.name == "Player Detail").Select(item => item.text).ToArray();
            snapshot.ownNameTagCount = player == null ? 0 : player.GetComponentsInChildren<UnityEngine.UI.Text>()
                .Count(item => item.name == "Player Name");
            snapshot.nameTagFacingError = cameras.Length == 0 || nameTags.Length == 0 ? 0 :
                nameTags.Max(item => Quaternion.Angle(item.transform.rotation, cameras[0].transform.rotation));
            snapshot.contentMenuOpen = FindUi<RectTransform>().Any(item => item.name == "Content Menu Overlay");
            snapshot.contentOptionsOpen = FindUi<RectTransform>().Any(item => item.name == "Content Options");
            snapshot.contentAvatarCount = screen.UiRoomPlayers.Count(participant => participant.AvatarRoot != null && participant.AvatarRoot.gameObject.activeInHierarchy);
            var scenario = UnityEngine.Object.FindAnyObjectByType<ScenarioContent>();
            snapshot.localPosition = screen.UiLocalPlayer != null ? screen.UiLocalPlayer.Position : Vector3.zero;
            snapshot.scenarioPresent = scenario != null;
            var resultRoot = FindUi<RectTransform>(true).FirstOrDefault(item => item.name == "Scenario Results");
            snapshot.scenarioResultVisible = resultRoot != null && resultRoot.gameObject.activeInHierarchy;
            if (resultRoot != null)
            {
                var resultButtons = resultRoot.GetComponentsInChildren<UnityEngine.UI.Button>(true);
                snapshot.scenarioResultRestartEnabled = snapshot.scenarioResultVisible && resultButtons.First(item => item.name == "Scenario Result Restart").IsInteractable();
                snapshot.scenarioResultReturnEnabled = snapshot.scenarioResultVisible && resultButtons.First(item => item.name == "Scenario Result Return").IsInteractable();
                snapshot.scenarioResultSummary = resultRoot.GetComponentsInChildren<UnityEngine.UI.Text>(true).First(item => item.name == "Scenario Result Summary").text;
            }
            snapshot.scenarioHeldTools = UnityEngine.Object.FindObjectsByType<HandheldItemRig>(FindObjectsSortMode.None)
                .Where(rig => rig.gameObject.activeInHierarchy && rig.Grip != null && rig.Grip.childCount > 0)
                .Select(rig => rig.name + "|" + rig.Grip.GetChild(0).name + "|" +
                    (rig.transform.parent != null && rig.transform.parent.GetComponent<Camera>() != null ? "local" : "remote"))
                .OrderBy(value => value).ToArray();
            if (scenario != null)
            {
                var menu = FindUi<RectTransform>().FirstOrDefault(item => item.name == "Scenario Tool Menu");
                snapshot.scenarioToolMenuVisible = menu != null;
                snapshot.scenarioToolOptions = menu != null ? menu.GetComponentsInChildren<UnityEngine.UI.Button>().Length : 0;
                var cardLabels = FindUi<UnityEngine.UI.Text>(true);
                snapshot.scenarioCardTitle = cardLabels.FirstOrDefault(item => item.name == "Scenario Title")?.text ?? "";
                snapshot.scenarioCardGoal = cardLabels.FirstOrDefault(item => item.name == "Scenario Current Title")?.text ?? "";
                snapshot.scenarioCardRequirement = cardLabels.FirstOrDefault(item => item.name == "Scenario Requirement")?.text ?? "";
                snapshot.scenarioCardProgress = cardLabels.FirstOrDefault(item => item.name == "Scenario Progress")?.text ?? "";
                snapshot.scenarioToolErrors = scenario.ToolErrors;
                snapshot.scenarioTargetErrors = scenario.TargetErrors;
                snapshot.scenarioElapsed = scenario.ElapsedSeconds;
                snapshot.scenarioRunRevision = scenario.RunRevision;
                var outcomes = scenario.Outcomes.ToArray();
                snapshot.scenarioResultRows = outcomes.Length;
                snapshot.scenarioResultActors = outcomes.Select(row => row.ActorName.ToString()).ToArray();
                snapshot.scenarioResultToolErrors = outcomes.Select(row => row.ToolErrors).ToArray();
                snapshot.scenarioResultTargetErrors = outcomes.Select(row => row.TargetErrors).ToArray();
                snapshot.scenarioRole = scenario.SelectedRoles.ToString();
                snapshot.scenarioRoleAssignments = ScenarioRoles.Enumerate(scenario.Definition.Roles)
                    .Select(role => role + "|" + scenario.RoleParticipantName(role)).ToArray();
                snapshot.scenarioVisibleObjects = scenario.GetComponentsInChildren<Transform>().Select(item => item.name).ToArray();
                var roleButtons = FindUi<UnityEngine.UI.Button>(true);
                var inspectorButton = roleButtons.FirstOrDefault(item => item.name == "Scenario Role Inspector");
                var technicianButton = roleButtons.FirstOrDefault(item => item.name == "Scenario Role Technician");
                snapshot.scenarioInspectorEnabled = inspectorButton != null && inspectorButton.IsInteractable();
                snapshot.scenarioTechnicianEnabled = technicianButton != null && technicianButton.IsInteractable();
                snapshot.scenarioInspectorButton = inspectorButton?.GetComponentInChildren<UnityEngine.UI.Text>().text;
                snapshot.scenarioTechnicianButton = technicianButton?.GetComponentInChildren<UnityEngine.UI.Text>().text;
                snapshot.scenarioInspector = scenario.RoleParticipantName(ScenarioRole.Inspector);
                snapshot.scenarioTechnician = scenario.RoleParticipantName(ScenarioRole.Technician);
                snapshot.scenarioContactReplaced = scenario.transform.Find("Contact/Replaced Contact")?.gameObject.activeInHierarchy == true;
                snapshot.scenarioMachineReady = scenario.transform.Find("Fault Indicator/Ready Light")?.gameObject.activeInHierarchy == true;
                snapshot.scenarioWorking = scenario.IsWorking;
                snapshot.scenarioToolSelectionEnabled = scenario.CanSelectTool;
                var rigs = UnityEngine.Object.FindObjectsByType<HandheldItemRig>(FindObjectsSortMode.None);
                var localRig = rigs.FirstOrDefault(rig => rig.transform.parent != null && rig.transform.parent.GetComponent<Camera>() != null);
                if (localRig != null)
                {
                    snapshot.scenarioLocalHandPosition = localRig.transform.position;
                    snapshot.scenarioLocalHandRestPosition = localRig.transform.parent.TransformPoint(new Vector3(.3f, -.27f, .55f));
                }
                var scenarioShared = screen.UiLocalPlayer != null ? SharedScenario.Find(screen.UiLocalPlayer.NetworkManager) : null;
                if (scenario.IsWorking && scenarioShared != null)
                {
                    var worker = rigs.FirstOrDefault(rig => rig.name == "Held Tool " + scenarioShared.State.WorkClientId);
                    var step = scenario.Definition.Steps[scenario.CompletedSteps];
                    var target = scenario.GetComponentsInChildren<ScenarioObject>().First(item => item.Id == step.TargetId);
                    var tool = scenario.Definition.GetTool(step.ToolId);
                    if (worker != null && worker.Grip.childCount > 0 && tool != null)
                    {
                        var item = worker.Grip.GetChild(0);
                        snapshot.scenarioWorkHandPresent = true;
                        snapshot.scenarioWorkHandPosition = worker.transform.position;
                        snapshot.scenarioWorkHandRotation = worker.transform.rotation;
                        ScenarioWorkMotion.ToolPose(step.Action, scenario.WorkProgress, out var offset, out var rotation);
                        snapshot.scenarioWorkContactError = Vector3.Distance(item.TransformPoint(tool.WorkContactPoint), target.WorkAnchor.position + target.WorkAnchor.rotation * offset);
                        snapshot.scenarioWorkRotationError = Quaternion.Angle(item.rotation, target.WorkAnchor.rotation * rotation * tool.WorkRotation);
                        var parts = item.GetComponent<ToolArticulation>();
                        snapshot.scenarioToolSpinAngle = parts != null ? parts.SpinAngle : 0;
                        snapshot.scenarioToolGrip = parts != null ? parts.GripAmount : 0;
                    }
                }
                var cancelWork = FindUi<UnityEngine.UI.Button>(true)
                    .FirstOrDefault(item => item.name == "Scenario Cancel Work");
                snapshot.scenarioCancelVisible = cancelWork != null && cancelWork.gameObject.activeInHierarchy;
                snapshot.scenarioCanCancel = snapshot.scenarioCancelVisible && cancelWork.IsInteractable();
                snapshot.scenarioActorText = cardLabels.FirstOrDefault(item => item.name == "Scenario Last Actor")?.text ?? "";
                snapshot.scenarioWorkStatus = cardLabels.FirstOrDefault(item => item.name == "Scenario Work Status")?.text ?? "";
                var workFill = FindUi<RectTransform>(true).FirstOrDefault(item => item.name == "Scenario Work Fill");
                snapshot.scenarioWorkFill = workFill != null ? workFill.anchorMax.x : 0;
                snapshot.scenarioWorkProgress = scenario.WorkProgress;
                var bolt = scenario.transform.Find("Bolt A");
                if (bolt != null)
                {
                    snapshot.scenarioBoltPosition = bolt.localPosition;
                    snapshot.scenarioBoltRotation = bolt.localRotation;
                }
                var screw = scenario.transform.Find("Screw A");
                var pin = scenario.transform.Find("Pin A");
                if (screw != null)
                {
                    snapshot.scenarioScrewPosition = screw.localPosition;
                    snapshot.scenarioScrewRotation = screw.localRotation;
                }
                if (pin != null)
                {
                    snapshot.scenarioPinPosition = pin.localPosition;
                }
                snapshot.scenarioHammerLift = scenario.IsWorking &&
                    scenario.Definition.Steps[scenario.CompletedSteps].Action == ScenarioAction.Strike
                    ? ScenarioWorkMotion.HammerLift(scenario.WorkProgress) : 0;
                snapshot.scenarioReady = scenario.IsReady;
                snapshot.scenarioComplete = scenario.IsComplete;
                snapshot.scenarioCompletedSteps = scenario.CompletedSteps;
                snapshot.scenarioRevision = scenario.Revision;
                snapshot.scenarioSelectedTool = scenario.SelectedTool;
                var toolScroll = FindUi<UnityEngine.UI.ScrollRect>().FirstOrDefault(item => item.name == "Tool List Scroll");
                snapshot.scenarioVisibleToolIds = toolScroll == null ? Array.Empty<string>() : toolScroll.content.GetComponentsInChildren<UnityEngine.UI.Button>().Where(button =>
                    toolScroll.viewport.rect.Contains(toolScroll.viewport.InverseTransformPoint(((RectTransform)button.transform).TransformPoint(((RectTransform)button.transform).rect.center))))
                    .Select(button => button.name.Substring("Scenario Tool ".Length)).ToArray();
                snapshot.scenarioFeedback = scenario.Feedback;
                var notice = FindUi<UnityEngine.UI.Text>(true).FirstOrDefault(item => item.name == "Scenario Feedback Text");
                snapshot.scenarioNotice = notice != null ? notice.text : "";
                snapshot.scenarioNoticeVisible = notice != null && notice.gameObject.activeInHierarchy;
                snapshot.scenarioValid = scenario.TryInitialize(out _);
                snapshot.scenarioStepCount = scenario.Definition != null ? scenario.Definition.Steps.Count : 0;
                snapshot.scenarioObjectCount = scenario.GetComponentsInChildren<ScenarioObject>(true).Length;
            }
            var videoViewer = UnityEngine.Object.FindAnyObjectByType<VideoContentPlayer>();
            snapshot.videoPresent = videoViewer != null;
            snapshot.videoControlsVisible = FindUi<RectTransform>().Any(item => item.name == "Video Controls");
            if (videoViewer != null)
            {
                snapshot.videoReady = videoViewer.IsReady;
                snapshot.videoPlaying = videoViewer.IsPlaying;
                snapshot.videoCanControl = videoViewer.CanControl;
                snapshot.videoDuration = videoViewer.Duration;
                snapshot.videoPosition = videoViewer.Position;
                snapshot.videoLocalTime = videoViewer.LocalTime;
                snapshot.videoVolume = videoViewer.Volume;
                snapshot.videoError = videoViewer.Error;
                snapshot.videoSubtitlesEnabled = videoViewer.SubtitlesEnabled;
                var subtitleText = FindUi<UnityEngine.UI.Text>(true).FirstOrDefault(label => label.name == "Video Subtitle Text");
                snapshot.videoSubtitle = subtitleText != null ? subtitleText.text : "";
                snapshot.videoSubtitleVisible = subtitleText != null && subtitleText.gameObject.activeInHierarchy;
            }
            var selectedViewer = UnityEngine.Object.FindAnyObjectByType<ModelViewer>();
            if (selectedViewer != null)
            {
                snapshot.selectedNodeCount = selectedViewer.SelectedNodes.Count;
                snapshot.selectedPartNames = selectedViewer.SelectedNodes.Select(node => node.Transform.name).ToArray();
                snapshot.modelMinimumY = selectedViewer.GetComponentsInChildren<Renderer>().Min(renderer => renderer.bounds.min.y);
                var disassembly = selectedViewer.GetComponent<ModelDisassemblyAnimation>();
                if (disassembly != null)
                {
                    snapshot.disassemblyPartPositions = disassembly.GetOrderedSteps().SelectMany(step => step.Targets)
                        .Where(target => target != null).Distinct().Select(target => target.localPosition).ToArray();
                }
                snapshot.explosionAmount = selectedViewer.ExplosionAmount;
                snapshot.playbackDirection = selectedViewer.PlaybackDirection;
                snapshot.playbackStatus = selectedViewer.PlaybackStatus;
                var playbackButtons = FindUi<UnityEngine.UI.Button>(true);
                snapshot.playbackForwardEnabled = playbackButtons.Any(button => button.name == "분해 재생" && button.interactable);
                snapshot.playbackPauseEnabled = playbackButtons.Any(button => button.name == "일시정지" && button.interactable);
                snapshot.playbackReverseEnabled = playbackButtons.Any(button => button.name == "재조립" && button.interactable);
                var battery = selectedViewer.GetComponentsInChildren<Renderer>().FirstOrDefault(renderer => renderer.name == "Battery");
                if (battery != null)
                {
                    snapshot.batteryRootPosition = selectedViewer.transform.InverseTransformPoint(battery.transform.position);
                    var batteryBlock = new MaterialPropertyBlock();
                    battery.GetPropertyBlock(batteryBlock, 0);
                    int batteryColorId = Shader.PropertyToID(battery.sharedMaterial.HasProperty("_BaseColor") ? "_BaseColor" : "_Color");
                    snapshot.batteryOpacity = batteryBlock.HasColor(batteryColorId) ? batteryBlock.GetColor(batteryColorId).a : battery.sharedMaterial.GetColor(batteryColorId).a;
                }
                snapshot.partTreeMaxDepth = selectedViewer.Parts.Count > 0 ? selectedViewer.Parts.Max(node => node.Depth) : 0;
            }
            if (selectedViewer != null)
            {
                var faded = selectedViewer.GetComponentsInChildren<Renderer>().Where(renderer =>
                    renderer.sharedMaterials.Any(material => material != null && material.name.EndsWith(" (Local Transparency)", StringComparison.Ordinal))).ToArray();
                snapshot.fadedPartCount = faded.Length;
                if (faded.Length > 0)
                {
                    var block = new MaterialPropertyBlock();
                    faded[0].GetPropertyBlock(block, 0);
                    snapshot.surroundingsAlpha = block.GetColor(Shader.PropertyToID("_BaseColor")).a;
                }
            }
            if (selectedViewer != null && selectedViewer.SelectedPart != null)
            {
                snapshot.selectedPartRendererCount = selectedViewer.SelectedRenderers.Count;
                snapshot.selectedPartHighlighted = selectedViewer.SelectedRenderers.Count > 0 && selectedViewer.SelectedRenderers.All(renderer =>
                {
                    if (renderer == null)
                    {
                        return false;
                    }
                    int property = Shader.PropertyToID(renderer.sharedMaterial.HasProperty("_BaseColor") ? "_BaseColor" : "_Color");
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block, 0);
                    var color = block.GetColor(property);
                    return Mathf.Abs(color.r - 1f) < 0.01f && Mathf.Abs(color.g - 0.55f) < 0.01f && Mathf.Abs(color.b - 0.1f) < 0.01f;
                });
            }
            var partPanel = FindUi<RectTransform>(true).FirstOrDefault(item => item.name == "Model Part Details");
            snapshot.partDetailsVisible = partPanel != null && partPanel.gameObject.activeInHierarchy;
            if (partPanel != null)
            {
                snapshot.partTitle = partPanel.GetComponentsInChildren<UnityEngine.UI.Text>(true).First(item => item.name == "Part Title").text;
                snapshot.partDescription = partPanel.GetComponentsInChildren<UnityEngine.UI.Text>(true).First(item => item.name == "Part Description").text;
            }
            var controlStatus = FindUi<UnityEngine.UI.Text>(true).FirstOrDefault(item => item.name == "Model Control Status");
            snapshot.modelControlText = controlStatus != null ? controlStatus.text : "";
            snapshot.modelControlVisible = controlStatus != null && controlStatus.gameObject.activeInHierarchy;
            var actionRoot = FindUi<RectTransform>(true).FirstOrDefault(item => item.name == "Content Actions");
            snapshot.contentActionsPresent = actionRoot != null && actionRoot.gameObject.activeInHierarchy;
            snapshot.contentActionsEnabled = actionRoot != null && actionRoot.GetChild(0).GetComponent<UnityEngine.UI.Button>().IsInteractable();
            if (actionRoot != null)
            {
                var actionList = actionRoot.Find("Content Action List");
                snapshot.contentActionsOpen = actionList.gameObject.activeInHierarchy;
                snapshot.contentActionsBottomRight = actionRoot.anchorMin == Vector2.right && actionRoot.anchorMax == Vector2.right &&
                    actionRoot.anchoredPosition.x < 0 && actionRoot.anchoredPosition.y > 0;
                snapshot.contentResetEnabled = actionList.GetComponentsInChildren<UnityEngine.UI.Button>().Any(button => button.name == "회전 초기화" && button.interactable);
                snapshot.contentResetVisible = actionList.GetComponentsInChildren<UnityEngine.UI.Button>().Any(button => button.name == "회전 초기화");
            }
            var ui = screen.GetComponentInChildren<LobbyUguiView>(true);
            if (ui != null)
            {
                snapshot.uiVisible = ui.canvas.enabled; snapshot.uiLoginVisible = ui.loginPanel.activeSelf;
                snapshot.uiFailureVisible=ui.failurePanel.activeInHierarchy;
                var lobbyScroll=ui.lobbyContent.GetComponentInParent<UnityEngine.UI.ScrollRect>(true);
                snapshot.uiFailureAtTop=ui.lobbyContent.rect.height<=lobbyScroll.viewport.rect.height+1||lobbyScroll.verticalNormalizedPosition>=.99f;
                snapshot.uiCanvasSpaceValid=FindUi<Canvas>().Where(x=>x.enabled).All(x=>((RectTransform)x.transform).rect.width>=1279&&((RectTransform)x.transform).rect.height>=799);
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
                snapshot.uiInteriorChatStatus=interior.chatStatus.text;snapshot.uiInteriorInputEnabled=interior.GetComponent<CanvasGroup>().interactable;
                snapshot.auditBackgroundBlocked=!interior.mainInteraction.interactable;
                snapshot.uiReconnectVisible=interior.reconnectPanel.activeSelf;
                snapshot.uiOperationNotice=interior.notice.text;snapshot.uiChatDraft=interior.chatInput.text;
                snapshot.uiInteriorLayoutValid=InteriorRaycasts(interior);
            }
            var roomView=RoomUi;
            if(roomView!=null)
            {
                snapshot.uiInRoom=roomView.canvas.enabled;snapshot.uiInRoomLayoutValid=RoomRaycasts(roomView);
                snapshot.uiSettingsVisible=roomView.settingsModal.activeSelf;snapshot.uiConfirmVisible=roomView.confirmationModal.activeSelf;
                snapshot.uiRoomMasterControls=roomView.settings.gameObject.activeSelf;snapshot.uiReadyEnabled=roomView.ready.IsInteractable();snapshot.uiPhaseEnabled=roomView.phase.IsInteractable();
                snapshot.uiRoomSendEnabled=roomView.sendChat.IsInteractable();snapshot.uiRoomInputEnabled=roomView.mainInteraction.interactable;
                snapshot.uiMemberRows=roomView.participantsContent.Cast<Transform>().Count(x=>x.name.StartsWith("Member - ")&&x.gameObject.activeSelf);
                var participantActions=roomView.participantsContent.GetComponentsInChildren<UnityEngine.UI.Button>();
                snapshot.uiRoomParticipantActionsReady=participantActions.Length>0&&participantActions.All(button=>button.IsInteractable());
                snapshot.uiRoomChannel=screen.UiRoomChatChannel;snapshot.uiInRoomSummary=roomView.summary.text;snapshot.uiInRoomChat=string.Join("\n",roomView.chatContent.GetComponentsInChildren<UnityEngine.UI.Text>().Select(x=>x.text));
                snapshot.uiRoomConfirmText=roomView.confirmationText.text;snapshot.uiRoomPasswordStatus=roomView.passwordStatus.text;snapshot.uiRoomNoticeText=roomView.notices.text;
                snapshot.uiTyping=UguiViewUtility.IsEditingText;snapshot.uiMovementBlocked=true;
                var move=Vector2.zero;snapshot.uiMoveX=move.x;snapshot.uiMoveY=move.y;
            }
            var layout=screen.GetComponentInChildren<UguiSceneLayout>(true);
            snapshot.uiHasSceneLayout=layout!=null;
            snapshot.uiLoginViewCount=FindUi<LobbyUguiView>(true).Length;
            snapshot.uiInteriorViewCount=FindUi<LobbyInteriorUguiView>(true).Length;
            snapshot.uiRoomViewCount=FindUi<RoomUguiView>(true).Length;
            snapshot.uiEventSystemCount=screen.gameObject.scene.GetRootGameObjects().Sum(root=>root.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true).Length);
            snapshot.uiUsesSceneInstances=layout!=null&&ui!=null&&interior!=null&&roomView!=null&&layout.controller==screen&&layout.loginView==ui&&layout.interiorView==interior&&layout.roomView==roomView&&
                !ui.name.EndsWith("(Clone)")&&!interior.name.EndsWith("(Clone)")&&!roomView.name.EndsWith("(Clone)");
            snapshot.auditImmediateBlocked=auditImmediateBlocked;snapshot.auditCaption=auditCaption;
            snapshot.auditPasswordsEmpty=new[]{"joinRoomPassword","createRoomPassword","reconnectPassword","editRoomPassword"}.All(field=>string.IsNullOrEmpty(Get(field) as string));
            snapshot.auditPasswordWidgetsEmpty=FindUi<UnityEngine.UI.InputField>(true).Where(field=>field.contentType==UnityEngine.UI.InputField.ContentType.Password).All(field=>field.text.Length==0);
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
        // 콘텐츠 HUD는 별도 콘텐츠 씬에 배치되어 있으므로 클라이언트 UI와 함께 조회합니다.
        private T[] FindUi<T>(bool includeInactive = false) where T : Component
        {
            var client = screen.GetComponentsInChildren<T>(includeInactive);
            var layout = UnityEngine.Object.FindAnyObjectByType<ContentSceneLayout>();
            if (layout == null || layout.Hud == null || (!includeInactive && !layout.Hud.gameObject.activeInHierarchy))
            {
                return client;
            }

            return client.Concat(layout.Hud.GetComponentsInChildren<T>(includeInactive)).ToArray();
        }

        // 실제 클라이언트에서 입력 유실·이상 수치·권한 거절을 확인하는 개발 전용 조작입니다.
        private IEnumerator MoveFor(string value)
        {
            var values = value.Split('|').Select(item => float.Parse(item, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            var until = Time.unscaledTime + values[3];
            while (Time.unscaledTime < until)
            {
                screen.UiLocalPlayer.MoveRpc(new Vector2(values[0], values[1]), values[2]);
                yield return new WaitForSecondsRealtime(.05f);
            }
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
        private static void CaptureWorld(string path)
        {
            var camera = UnityEngine.Object.FindAnyObjectByType<FirstPersonCameraController>().ViewCamera;
            var target = RenderTexture.GetTemporary(Screen.width, Screen.height, 24);
            var previous = camera.targetTexture;
            var active = RenderTexture.active;
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous;
                RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.Destroy(texture);
            }
        }


        private static void CaptureCanvas(Canvas canvas, string path)
        {
            // 숨겨진 검사 창은 화면 버퍼가 없을 수 있으므로 해당 Canvas만 텍스처로 렌더링합니다.
            var root = new GameObject("Probe UI Capture", typeof(Camera));
            var camera = root.GetComponent<Camera>(); camera.enabled = false;camera.clearFlags = CameraClearFlags.SolidColor;camera.backgroundColor = Color.black;
            var sceneRenderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Where(renderer => renderer.enabled && !renderer.transform.IsChildOf(canvas.transform)).ToArray();
            var target = new RenderTexture(Screen.width, Screen.height, 24);
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            var mode = canvas.renderMode;var previousCamera = canvas.worldCamera;var previousTarget = RenderTexture.active;
            try
            {
                foreach (var renderer in sceneRenderers) renderer.enabled = false;
                camera.targetTexture = target;canvas.renderMode = RenderMode.ScreenSpaceCamera;canvas.worldCamera = camera;canvas.planeDistance = 10;
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);texture.Apply();File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                foreach (var renderer in sceneRenderers) if (renderer != null) renderer.enabled = true;
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
                var rect=(RectTransform)target.transform;
                if (rect.rect.width < 1 || rect.rect.height < 1) return false;
                var point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
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
                var rect=(RectTransform)target.transform;
                if (rect.rect.width < 1 || rect.rect.height < 1) return false;
                var point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
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
