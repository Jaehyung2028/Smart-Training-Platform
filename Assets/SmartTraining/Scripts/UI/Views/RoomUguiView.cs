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
        public Button changeContent;
        public Text contentLabel;
        private ContentTreePopup contentTree;
        public Canvas canvas;
        public CanvasGroup mainInteraction;
        public Font fontOverride;
        public Text heading, summary, connection, notice, status, participantTitle, emptyParticipants;
        public Text notices, emptyChat, chatStatus, settingsStatus, passwordStatus, confirmationText;
        public Button ready, phase, settings, leave, logout, roomParticipants, lobbyParticipants;
        public Button roomChat, lobbyChat, sendChat, clearNotices;
        public Button saveSettings, savePassword, clearPassword, entryLock, closeRoom, closeSettings, confirm, cancel;
        public InputField nameInput, capacityInput, passwordInput, chatInput;
        public GameObject settingsModal, confirmationModal, operationNoticePanel;
        public RectTransform participantsContent, participantTemplate, chatContent;
        public Text chatTemplate;
        public ScrollRect chatScroll;
        private ApiLobbyScreen screen;
        private CanvasGroup interaction;
        private GraphicRaycaster raycaster;
        private Font runtimeFont;
        private bool wired, wasVisible, showLobbyParticipants;
        private string context = "";
        private float nextLists;
        private readonly Dictionary<string, RectTransform> rows = new();
        private ChatMessageListView chatList;

        public void Bind(ApiLobbyScreen controller)
        {
            screen = controller;
            EnsureComponents();
            if (wired)
            {
                return;
            }

            UguiViewUtility.EnsureEventSystem(transform);
            runtimeFont = UguiViewUtility.ApplyFont(gameObject, fontOverride);
            contentTree = new ContentTreePopup(transform, runtimeFont, content => screen.UiChangeRoomContent(content));
            changeContent.onClick.AddListener(() =>
            {
                if (screen.UiRoomCanEdit)
                {
                    var current = screen.UiRoomContent;
                    contentTree.Show(screen.UiContents, current?.rootContent ?? "", current?.id ?? 0);
                }
            });
            ready.onClick.AddListener(() => screen.UiToggleReady());
            // 테마가 바뀌어도 시작 불가 상태는 버튼 자체와 색상으로 명확히 표시합니다.
            phase.transition = Selectable.Transition.ColorTint;
            var phaseColors = phase.colors;
            phaseColors.disabledColor = new Color32(148, 163, 184, 255);
            phase.colors = phaseColors;
            phase.onClick.AddListener(() => screen.UiTogglePhase());
            settings.onClick.AddListener(OpenSettings);
            closeSettings.onClick.AddListener(CloseSettings);
            leave.onClick.AddListener(() => screen.UiLeaveRoom());
            logout.onClick.AddListener(() => screen.UiRoomLogout());
            roomParticipants.onClick.AddListener(() =>
            {
                if (screen.UiRoomCanAct)
                {
                    showLobbyParticipants = false;
                    nextLists = 0;
                }
            });
            lobbyParticipants.onClick.AddListener(() =>
            {
                if (screen.UiRoomCanAct)
                {
                    showLobbyParticipants = true;
                    nextLists = 0;
                }
            });
            roomChat.onClick.AddListener(() =>
            {
                if (screen.UiRoomCanAct)
                {
                    screen.UiSelectRoomChat(1);
                }
            });
            lobbyChat.onClick.AddListener(() =>
            {
                if (screen.UiRoomCanAct)
                {
                    screen.UiSelectRoomChat(0);
                }
            });
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
            if (!screen.UiRoomCanManage)
            {
                return;
            }

            screen.UiPrepareRoomSettings();
            settingsModal.SetActive(true);
            nameInput.SetTextWithoutNotify(screen.UiEditRoomName);
            capacityInput.SetTextWithoutNotify(screen.UiEditRoomCapacity);
            passwordInput.SetTextWithoutNotify("");
        }


        private void CloseSettings()
        {
            if (screen.UiProcessing)
            {
                return;
            }

            settingsModal.SetActive(false);
            screen.UiEditRoomPassword = "";
            passwordInput.SetTextWithoutNotify("");
            UguiViewUtility.ClearSelection(transform);
        }


        private void EnsureComponents() => UguiViewUtility.EnsureComponents(this, ref canvas, ref interaction, ref raycaster);

        private void LateUpdate() => Refresh();

        internal void RefreshState()
        {
            if (wired)
            {
                Refresh();
            }
        }


        internal void ResetTransientState()
        {
            contentTree?.Close();
            UguiViewUtility.ClearSelection(transform);
            settingsModal.SetActive(false);
            confirmationModal.SetActive(false);
            foreach (var field in new[]
            {
                nameInput,
                capacityInput,
                passwordInput,
                chatInput
            }

            )
            {
                field.SetTextWithoutNotify("");
            }

            UguiViewUtility.RemoveMissing(rows, new HashSet<string>());
            chatList?.Clear();
            confirmationText.text = notices.text = settingsStatus.text = "";
            emptyParticipants.gameObject.SetActive(true);
            emptyChat.gameObject.SetActive(true);
            context = "";
            nextLists = 0;
            showLobbyParticipants = false;
            wasVisible = false;
        }


        private void Refresh()
        {
            if (screen == null)
            {
                return;
            }

            EnsureComponents();
            bool visible = screen.UseUguiRoom && !screen.UiContentSceneActive;
            UguiViewUtility.SetVisible(this, canvas, interaction, raycaster, visible);
            if (!visible)
            {
                if (wasVisible)
                {
                    ResetTransientState();
                }

                return;
            }

            wasVisible = true;
            string current = screen.UiSelfId + "|" + screen.UiActiveRoomId;
            if (current != context)
            {
                context = current;
                showLobbyParticipants = false;
                settingsModal.SetActive(false);
                confirmationModal.SetActive(false);
                screen.UiSelectRoomChat(1);
                nextLists = 0;
            }

            bool master = screen.UiLocalPlayer?.IsRoomMaster == true;
            if (!master && settingsModal.activeSelf)
            {
                settingsModal.SetActive(false);
                screen.UiEditRoomPassword = "";
                passwordInput.SetTextWithoutNotify("");
                UguiViewUtility.ClearSelection(transform);
            }

            contentTree.Refresh(screen.UiContents);
            if (!screen.UiRoomCanEdit)
            {
                contentTree.Close();
            }

            changeContent.gameObject.SetActive(master);
            changeContent.interactable = screen.UiRoomCanEdit;
            contentLabel.text = "콘텐츠: " + (screen.UiRoomContent?.title ?? "확인 중…");
            bool pending = screen.UiRoomConfirmation;
            if (pending != confirmationModal.activeSelf)
            {
                UguiViewUtility.ClearSelection(transform);
            }

            confirmationModal.SetActive(pending);
            confirmationText.text = screen.UiRoomConfirmationText;
            mainInteraction.interactable = mainInteraction.blocksRaycasts = !screen.UiProcessing && !pending && !settingsModal.activeSelf && !contentTree.IsOpen;
            settingsModal.GetComponent<CanvasGroup>().interactable = settingsModal.GetComponent<CanvasGroup>().blocksRaycasts = !pending;
            confirm.interactable = cancel.interactable = !screen.UiProcessing;
            heading.text = screen.UiRoomHeading;
            summary.text = screen.UiRoomSummary();
            connection.text = screen.UiConnection + "\n" + screen.UiRoomConnection;
            notice.text = screen.UiOperationNotice();
            operationNoticePanel.SetActive(notice.text.Length > 0);
            status.text = screen.UiStatus;
            ready.gameObject.SetActive(!master);
            ready.interactable = screen.UiRoomCanReady;
            Caption(ready, screen.UiLocalPlayer?.IsReady == true ? "준비 취소" : "준비");
            phase.gameObject.SetActive(master);
            phase.interactable = screen.UiRoomCanPhase;
            Caption(phase, screen.UiLocalPlayer?.RoomIsRunning == true ? "대기 상태로 복귀" : "시작하기");
            settings.gameObject.SetActive(master);
            settings.interactable = screen.UiRoomCanManage;
            leave.interactable = logout.interactable = screen.UiRoomCanAct;
            roomParticipants.interactable = showLobbyParticipants && screen.UiRoomCanAct;
            lobbyParticipants.interactable = !showLobbyParticipants && screen.UiRoomCanAct;
            clearNotices.interactable = screen.UiRoomCanAct;
            roomChat.interactable = screen.UiRoomChatChannel != 1 && screen.UiRoomCanAct;
            lobbyChat.interactable = screen.UiRoomChatChannel != 0 && screen.UiRoomCanAct;
            roomParticipants.GetComponent<UguiSelectionHighlight>()?.SetSelected(!showLobbyParticipants);
            lobbyParticipants.GetComponent<UguiSelectionHighlight>()?.SetSelected(showLobbyParticipants);
            roomChat.GetComponent<UguiSelectionHighlight>()?.SetSelected(screen.UiRoomChatChannel == 1);
            lobbyChat.GetComponent<UguiSelectionHighlight>()?.SetSelected(screen.UiRoomChatChannel == 0);
            // IME 조합과 커서 위치를 매 프레임 초기화하지 않습니다. 전송 성공·채널 변경의 비움은 반영합니다.
            if (chatInput.text != screen.UiChatInput && (!chatInput.isFocused || screen.UiChatInput.Length == 0))
            {
                chatInput.SetTextWithoutNotify(screen.UiChatInput);
            }

            bool canEditChat = screen.UiCanEditRoomChat && !pending && !settingsModal.activeSelf && !contentTree.IsOpen;
            chatInput.interactable = canEditChat;
            sendChat.interactable = canEditChat && screen.UiCanSendRoomScreenChat;
            chatStatus.text = screen.UiRoomChatStatus;
            notices.text = string.IsNullOrEmpty(screen.UiRoomNotices) ? "새로운 방 알림이 없습니다." : screen.UiRoomNotices;
            nameInput.interactable = capacityInput.interactable = saveSettings.interactable = screen.UiRoomCanEdit;
            passwordInput.interactable = entryLock.interactable = clearPassword.interactable = closeRoom.interactable = screen.UiRoomCanManage;
            savePassword.interactable = screen.UiRoomCanManage && screen.UiEditRoomPassword.Length > 0;
            passwordInput.SetTextWithoutNotify(screen.UiEditRoomPassword);
            passwordStatus.text = screen.UiRoomPasswordStatus;
            Caption(
                entryLock,
                screen.UiLocalPlayer?.RoomEntryLocked == true ? "새 참가자 입장 열기" : "새 참가자 입장 잠그기"
            );
            closeSettings.interactable = !screen.UiProcessing;
            settingsStatus.text = (screen.UiLocalPlayer?.RoomIsRunning == true ? "진행 중에는 방 이름·최대 인원을 변경할 수 없습니다.\n" : "") + screen.UiStatus;
            UpdateParticipantActions();
            if (Time.unscaledTime < nextLists)
            {
                return;
            }

            nextLists = Time.unscaledTime + .15f;
            UpdateParticipants();
            UpdateParticipantActions();
            UpdateChat();
        }


        private static void Caption(Button button, string text) => button.GetComponentInChildren<Text>().text = text;

        private void UpdateParticipants()
        {
            var ids = new HashSet<string>(showLobbyParticipants ? screen.UiParticipants.Select(p => p.userId) : screen.UiRoomPlayers.Select(p => p.UserId));
            UguiViewUtility.RemoveMissing(rows, ids);
            emptyParticipants.gameObject.SetActive(ids.Count == 0);
            participantTitle.text = (showLobbyParticipants ? "현재 로비" : "현재 방") + " 참가자 · " + ids.Count + "명";
            foreach (var id in ids)
            {
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                if (!rows.TryGetValue(id, out var row))
                {
                    row = Instantiate(participantTemplate, participantsContent);
                    row.name = "Member - " + id;
                    row.gameObject.SetActive(true);
                    row.Find("Actions/Transfer").GetComponent<Button>().onClick.AddListener(() => screen.UiRequestTransfer(id));
                    row.Find("Actions/Kick").GetComponent<Button>().onClick.AddListener(() => screen.UiRequestKick(id));
                    rows[id] = row;
                }

                var player = Array.Find(screen.UiRoomPlayers, p => p.UserId == id);
                var card = row.GetComponent<RoomParticipantCard>();
                if (card != null)
                {
                    if (showLobbyParticipants)
                    {
                        card.RenderLobby(
                            screen.UiParticipantLabel(Array.Find(screen.UiParticipants, p => p.userId == id)),
                            id == screen.UiSelfId
                        );
                    }
                    else
                    {
                        card.RenderRoom(player, player.UserId == screen.UiLocalPlayer?.RoomMasterUserId);
                    }

                    continue;
                }

                row.Find("Details").GetComponent<Text>().text = showLobbyParticipants ? screen.UiParticipantLabel(Array.Find(screen.UiParticipants, p => p.userId == id)) : player.UserName + (player.IsOwner ? " (나)" : "") + (player.UserId == screen.UiLocalPlayer?.RoomMasterUserId ? " [방장]" : "") + "\n" + (player.IsReady ? "준비 완료" : "준비 전") + " · " + player.ConnectionStatusLabel + " · " + player.LatencyLabel + "\n" + (player.GetComponent<TrainingProgress>()?.Label ?? "학습 미시작");
            }
        }


        private void UpdateParticipantActions()
        {
            foreach (var entry in rows)
            {
                bool manage = !showLobbyParticipants && screen.UiLocalPlayer?.IsRoomMaster == true && entry.Key != screen.UiSelfId;
                entry.Value.Find("Actions").gameObject.SetActive(manage);
                foreach (var button in entry.Value.GetComponentsInChildren<Button>())
                {
                    button.interactable = screen.UiRoomCanManage;
                }
            }
        }


        private void UpdateChat()
        {
            chatList ??= new ChatMessageListView(chatScroll, () => Instantiate(chatTemplate, chatContent), chatTemplate.color);
            var displayed = screen.UiRoomScreenMessages;
            emptyChat.gameObject.SetActive(displayed.Count == 0);
            chatList.Refresh(displayed, screen.UiSelfId, context + "|" + screen.UiRoomChatChannel);
        }


        private void OnDestroy()
        {
            chatList?.Dispose();
            if (runtimeFont != null)
            {
                Destroy(runtimeFont);
            }
        }
    }
}
