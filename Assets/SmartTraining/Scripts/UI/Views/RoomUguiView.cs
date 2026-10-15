using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SmartTraining
{
    [RequireComponent(typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster))]
    public sealed class RoomUguiView : MonoBehaviour
    {
        public Canvas canvas;
        public CanvasGroup mainInteraction;
        public Font fontOverride;
        public Text heading, summary, connection, notice, status, participantTitle, emptyParticipants;
        public Text notices, emptyChat, chatStatus, settingsStatus, passwordStatus, confirmationText;
        public Button ready, phase, settings, leave, logout, roomParticipants, lobbyParticipants;
        public Button roomChat, lobbyChat, sendChat, clearNotices, refreshParticipants;
        public Button saveSettings, savePassword, clearPassword, entryLock, closeRoom, closeSettings, confirm, cancel;
        public Button openMap, closeMap;
        public InputField nameInput, capacityInput, passwordInput, chatInput;
        public GameObject settingsModal, confirmationModal, mapModal, operationNoticePanel;
        public RectTransform participantsContent, participantTemplate, chatContent, map, markerTemplate;
        public Text chatTemplate;
        public ScrollRect chatScroll;
        public RoomMoveButton[] movementButtons;
        private ApiLobbyScreen screen;
        private CanvasGroup interaction;
        private GraphicRaycaster raycaster;
        private Font runtimeFont;
        private bool wired, wasVisible, showLobbyParticipants;
        private string context = "", messagesContext = "";
        private float nextLists;
        private readonly Dictionary<string, RectTransform> rows = new(), markers = new();
        private readonly Dictionary<string, Text> messages = new();

        public void Bind(ApiLobbyScreen controller)
        {
            screen = controller;
            EnsureComponents();
            if (wired) return;
            UguiViewUtility.EnsureEventSystem(transform);
            runtimeFont = UguiViewUtility.ApplyFont(gameObject, fontOverride);
            ready.onClick.AddListener(() => screen.UiToggleReady());
            phase.onClick.AddListener(() => screen.UiTogglePhase());
            settings.onClick.AddListener(OpenSettings);
            closeSettings.onClick.AddListener(CloseSettings);
            openMap.onClick.AddListener(() => { if (screen.UiRoomCanAct) { mapModal.SetActive(true); UguiViewUtility.ClearSelection(transform); } });
            closeMap.onClick.AddListener(() => { mapModal.SetActive(false); UguiViewUtility.ClearSelection(transform); });
            leave.onClick.AddListener(() => screen.UiLeaveRoom());
            logout.onClick.AddListener(() => screen.UiRoomLogout());
            roomParticipants.onClick.AddListener(() => { if (screen.UiRoomCanAct) { showLobbyParticipants = false; nextLists = 0; } });
            lobbyParticipants.onClick.AddListener(() => { if (screen.UiRoomCanAct) { showLobbyParticipants = true; nextLists = 0; } });
            refreshParticipants.onClick.AddListener(() => screen.UiRefreshRoomParticipants());
            roomChat.onClick.AddListener(() => { if (screen.UiRoomCanAct) screen.UiSelectRoomChat(1); });
            lobbyChat.onClick.AddListener(() => { if (screen.UiRoomCanAct) screen.UiSelectRoomChat(0); });
            chatInput.onValueChanged.AddListener(value => screen.UiChatInput = value);
            sendChat.onClick.AddListener(() => screen.UiSendRoomScreenChat());
            clearNotices.onClick.AddListener(() => screen.UiClearRoomNotices());
            nameInput.onValueChanged.AddListener(value => screen.UiEditRoomName = value);
            capacityInput.onValueChanged.AddListener(value => screen.UiEditRoomCapacity = value);
            passwordInput.onValueChanged.AddListener(value => screen.UiEditRoomPassword = value);
            saveSettings.onClick.AddListener(() => screen.UiSaveRoomSettings());
            savePassword.onClick.AddListener(() => screen.UiSaveRoomPassword());
            clearPassword.onClick.AddListener(() => screen.UiClearRoomPassword());
            entryLock.onClick.AddListener(() => screen.UiToggleRoomLock());
            closeRoom.onClick.AddListener(() => screen.UiRequestClose());
            confirm.onClick.AddListener(() => screen.UiConfirmRoomAction());
            cancel.onClick.AddListener(() => screen.UiCancelRoomAction());
            wired = true;
            Refresh();
        }

        private void OpenSettings()
        {
            if (!screen.UiRoomCanManage) return;
            screen.UiPrepareRoomSettings(); settingsModal.SetActive(true);
            nameInput.SetTextWithoutNotify(screen.UiEditRoomName);
            capacityInput.SetTextWithoutNotify(screen.UiEditRoomCapacity);
            passwordInput.SetTextWithoutNotify("");
        }
        private void CloseSettings()
        {
            if (screen.UiProcessing) return;
            settingsModal.SetActive(false); screen.UiEditRoomPassword = ""; passwordInput.SetTextWithoutNotify("");
            UguiViewUtility.ClearSelection(transform);
        }
        internal bool BlocksMovement => !wasVisible || settingsModal.activeSelf || confirmationModal.activeSelf ||
            screen == null || !screen.UiRoomCanAct;
        internal Vector2 ReadMovement()
        {
            if (BlocksMovement || RoomMovementInput.IsEditingText) return Vector2.zero;
            return Vector2.ClampMagnitude(movementButtons.Where(x => x.IsHeld).Aggregate(Vector2.zero, (value, button) => value + button.direction), 1);
        }
        private void EnsureComponents()
            => UguiViewUtility.EnsureComponents(this, ref canvas, ref interaction, ref raycaster);

        private void LateUpdate() => Refresh();
        internal void RefreshState() { if (wired) Refresh(); }
        internal void ResetTransientState()
        {
            UguiViewUtility.ClearSelection(transform);
            settingsModal.SetActive(false); confirmationModal.SetActive(false); mapModal.SetActive(false);
            foreach (var field in new[] {nameInput, capacityInput, passwordInput, chatInput}) field.SetTextWithoutNotify("");
            UguiViewUtility.RemoveMissing(rows, new HashSet<string>());
            UguiViewUtility.RemoveMissing(markers, new HashSet<string>());
            UguiViewUtility.RemoveMissing(messages, new HashSet<string>());
            confirmationText.text = notices.text = settingsStatus.text = "";
            emptyParticipants.gameObject.SetActive(true); emptyChat.gameObject.SetActive(true);
            context = messagesContext = ""; nextLists = 0; showLobbyParticipants = false; wasVisible = false;
        }
        private void Refresh()
        {
            if (screen == null) return;
            EnsureComponents();
            bool visible = screen.UseUguiRoom;
            UguiViewUtility.SetVisible(this, canvas, interaction, raycaster, visible);
            if (!visible)
            {
                if (wasVisible) ResetTransientState();
                return;
            }
            wasVisible = true;
            string current = screen.UiSelfId + "|" + screen.UiActiveRoomId;
            if (current != context)
            {
                context = current; showLobbyParticipants = false; settingsModal.SetActive(false); confirmationModal.SetActive(false); mapModal.SetActive(false);
                screen.UiSelectRoomChat(1); nextLists = 0;
            }
            bool master = screen.UiLocalPlayer?.IsRoomMaster == true;
            if (!master && settingsModal.activeSelf) { settingsModal.SetActive(false); screen.UiEditRoomPassword = ""; passwordInput.SetTextWithoutNotify(""); UguiViewUtility.ClearSelection(transform); }
            bool pending = screen.UiRoomConfirmation;
            if (pending != confirmationModal.activeSelf) UguiViewUtility.ClearSelection(transform);
            confirmationModal.SetActive(pending); confirmationText.text = screen.UiRoomConfirmationText;
            mainInteraction.interactable = mainInteraction.blocksRaycasts = !screen.UiProcessing && !pending && !settingsModal.activeSelf && !mapModal.activeSelf;
            settingsModal.GetComponent<CanvasGroup>().interactable = settingsModal.GetComponent<CanvasGroup>().blocksRaycasts = !pending;
            confirm.interactable = cancel.interactable = !screen.UiProcessing;
            heading.text = screen.UiRoomHeading; summary.text = screen.UiRoomSummary(); connection.text = screen.UiConnection + "\n" + screen.UiRoomConnection;
            notice.text = screen.UiOperationNotice(); operationNoticePanel.SetActive(notice.text.Length > 0);
            status.text = screen.UiStatus;
            ready.interactable = screen.UiRoomCanReady; Caption(ready, screen.UiLocalPlayer?.IsReady == true ? "준비 취소" : "준비");
            phase.gameObject.SetActive(master); phase.interactable = screen.UiRoomCanPhase; Caption(phase, screen.UiLocalPlayer?.RoomIsRunning == true ? "대기 상태로 복귀" : "전원 준비 후 시작");
            settings.gameObject.SetActive(master); settings.interactable = screen.UiRoomCanManage;
            openMap.interactable = screen.UiRoomCanAct;
            leave.interactable = logout.interactable = screen.UiRoomCanAct;
            roomParticipants.interactable = showLobbyParticipants && screen.UiRoomCanAct;
            lobbyParticipants.interactable = !showLobbyParticipants && screen.UiRoomCanAct;
            refreshParticipants.interactable = screen.UiRoomCanAct;
            clearNotices.interactable = screen.UiRoomCanAct;
            roomChat.interactable = screen.UiRoomChatChannel != 1 && screen.UiRoomCanAct;
            lobbyChat.interactable = screen.UiRoomChatChannel != 0 && screen.UiRoomCanAct;
            roomParticipants.GetComponent<UguiSelectionHighlight>()?.SetSelected(!showLobbyParticipants);
            lobbyParticipants.GetComponent<UguiSelectionHighlight>()?.SetSelected(showLobbyParticipants);
            roomChat.GetComponent<UguiSelectionHighlight>()?.SetSelected(screen.UiRoomChatChannel == 1);
            lobbyChat.GetComponent<UguiSelectionHighlight>()?.SetSelected(screen.UiRoomChatChannel == 0);
            chatInput.SetTextWithoutNotify(screen.UiChatInput); chatInput.interactable = sendChat.interactable = screen.UiCanSendRoomScreenChat;
            chatStatus.text = screen.UiRoomChatStatus;
            notices.text = string.IsNullOrEmpty(screen.UiRoomNotices) ? "새로운 방 알림이 없습니다." : screen.UiRoomNotices;
            nameInput.interactable = capacityInput.interactable = saveSettings.interactable = screen.UiRoomCanEdit;
            passwordInput.interactable = entryLock.interactable = clearPassword.interactable = closeRoom.interactable = screen.UiRoomCanManage;
            savePassword.interactable = screen.UiRoomCanManage && screen.UiEditRoomPassword.Length > 0;
            passwordInput.SetTextWithoutNotify(screen.UiEditRoomPassword);
            passwordStatus.text = screen.UiRoomPasswordStatus;
            Caption(entryLock, screen.UiLocalPlayer?.RoomEntryLocked == true ? "새 참가자 입장 열기" : "새 참가자 입장 잠그기");
            closeSettings.interactable = !screen.UiProcessing;
            settingsStatus.text = (screen.UiLocalPlayer?.RoomIsRunning == true ? "진행 중에는 방 이름·최대 인원을 변경할 수 없습니다.\n" : "") + screen.UiStatus;
            foreach (var move in movementButtons) move.GetComponent<Button>().interactable = !BlocksMovement;
            UpdateParticipantActions();
            UpdateMap();
            if (Time.unscaledTime < nextLists) return;
            nextLists = Time.unscaledTime + .15f; UpdateParticipants(); UpdateParticipantActions(); UpdateChat();
        }
        private static void Caption(Button button, string text) => button.GetComponentInChildren<Text>().text = text;
        private void UpdateParticipants()
        {
            var ids = new HashSet<string>(showLobbyParticipants ? screen.UiParticipants.Select(p => p.userId) : screen.UiRoomPlayers.Select(p => p.UserId));
            UguiViewUtility.RemoveMissing(rows, ids); emptyParticipants.gameObject.SetActive(ids.Count == 0);
            participantTitle.text = (showLobbyParticipants ? "현재 로비" : "현재 방") + " 참가자 · " + ids.Count + "명";
            foreach (var id in ids)
            {
                if (string.IsNullOrEmpty(id)) continue;
                if (!rows.TryGetValue(id, out var row))
                {
                    row = Instantiate(participantTemplate, participantsContent); row.name = "Member - " + id; row.gameObject.SetActive(true);
                    row.Find("Actions/Transfer").GetComponent<Button>().onClick.AddListener(() => screen.UiRequestTransfer(id));
                    row.Find("Actions/Kick").GetComponent<Button>().onClick.AddListener(() => screen.UiRequestKick(id)); rows[id] = row;
                }
                var player = Array.Find(screen.UiRoomPlayers, p => p.UserId == id);
                var card = row.GetComponent<RoomParticipantCard>();
                if (card != null)
                {
                    if (showLobbyParticipants) card.RenderLobby(screen.UiParticipantLabel(Array.Find(screen.UiParticipants, p => p.userId == id)), id == screen.UiSelfId);
                    else card.RenderRoom(player, player.UserId == screen.UiLocalPlayer?.RoomMasterUserId);
                    continue;
                }
                row.Find("Details").GetComponent<Text>().text = showLobbyParticipants
                    ? screen.UiParticipantLabel(Array.Find(screen.UiParticipants, p => p.userId == id))
                    : player.UserName + (player.IsOwner ? " (나)" : "") + (player.UserId == screen.UiLocalPlayer?.RoomMasterUserId ? " [방장]" : "") +
                        "\n" + (player.IsReady ? "준비 완료" : "준비 전") + " · " + player.ConnectionStatusLabel + " · " + player.LatencyLabel +
                        "\nX " + player.ServerPosition.x.ToString("F1") + " / Z " + player.ServerPosition.z.ToString("F1");
            }
        }
        private void UpdateParticipantActions()
        {
            foreach (var entry in rows)
            {
                bool manage = !showLobbyParticipants && screen.UiLocalPlayer?.IsRoomMaster == true && entry.Key != screen.UiSelfId;
                entry.Value.Find("Actions").gameObject.SetActive(manage);
                foreach (var button in entry.Value.GetComponentsInChildren<Button>()) button.interactable = screen.UiRoomCanManage;
            }
        }
        private void UpdateMap()
        {
            var players = screen.UiRoomPlayers;
            UguiViewUtility.RemoveMissing(markers, new HashSet<string>(players.Select(p => p.UserId)));
            foreach (var player in players)
            {
                if (string.IsNullOrEmpty(player.UserId)) continue;
                if (!markers.TryGetValue(player.UserId, out var marker))
                { marker = Instantiate(markerTemplate, map); marker.gameObject.SetActive(true); markers[player.UserId] = marker; }
                marker.anchorMin = marker.anchorMax = new Vector2(.1f + .8f * (player.ServerPosition.x / RoomPlayer.ArenaHalfSize + 1) / 2,
                    .1f + .8f * (player.ServerPosition.z / RoomPlayer.ArenaHalfSize + 1) / 2);
                marker.anchoredPosition = Vector2.zero;
                marker.GetComponent<Image>().color = player.IsOwner ? new Color(.15f,.7f,.4f) : new Color(.2f,.5f,1);
                marker.GetComponentInChildren<Text>().text = player.UserName;
            }
        }
        private void UpdateChat()
        {
            string current = context + "|" + screen.UiRoomChatChannel;
            if (messagesContext != current) { UguiViewUtility.RemoveMissing(messages, new HashSet<string>()); messagesContext = current; }
            var displayed = screen.UiRoomScreenMessages;
            bool follow = messages.Count == 0 || chatScroll.verticalNormalizedPosition < .05f;
            bool changed = UguiViewUtility.RemoveMissing(messages, new HashSet<string>(displayed.Select(x => x.id)));
            emptyChat.gameObject.SetActive(displayed.Length == 0);
            foreach (var message in displayed)
            {
                if (messages.ContainsKey(message.id)) continue;
                var row = Instantiate(chatTemplate, chatContent); row.gameObject.SetActive(true);
                row.text = UguiViewUtility.ChatLabel(message); messages[message.id] = row; changed = true;
            }
            if (changed && follow) { Canvas.ForceUpdateCanvases(); chatScroll.verticalNormalizedPosition = 0; }
        }
        private void OnDestroy() { if (runtimeFont != null) Destroy(runtimeFont); }
    }
}
