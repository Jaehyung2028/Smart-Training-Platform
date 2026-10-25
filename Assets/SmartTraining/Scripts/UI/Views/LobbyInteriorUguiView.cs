using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SmartTraining
{
    [RequireComponent(typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster))]
    public sealed class LobbyInteriorUguiView : MonoBehaviour
    {
        public Canvas canvas;

        public CanvasGroup mainInteraction;

        public Font fontOverride;

        public Text heading, connection, notice, status, roomCount, roomDetail, participantTitle, emptyRooms, emptyParticipants, emptyChat, chatStatus;

        public InputField search, joinPassword, chatInput, reconnectPassword, createName, createCapacity, createPassword;

        public Button refresh, leave, create, join, available, protection, sort, resetFilters, sendChat, notifications, logout, clearRoom, reconnect, forgetReconnect;

        public Button createContent, submitCreate, closeCreate, cancelJoin;

        public GameObject createModal, progressModal, reconnectPanel;

        public Text createStatus, progress;

        public RectTransform roomsContent, participantsContent, chatContent;

        public Button roomTemplate, participantTemplate;

        public Text chatTemplate;

        public ScrollRect chatScroll;

        private ApiLobbyScreen screen;

        private ContentTreePopup contentTree;

        private CanvasGroup interaction;

        private GraphicRaycaster raycaster;

        private Font runtimeFont;

        private bool wired, submitting;

        private string context = "";

        private float nextLists;

        private readonly Dictionary<string, Button> roomRows = new(), participantRows = new();

        private readonly Dictionary<string, Text> chatRows = new();

        // Controller와 입력 필드·버튼을 연결합니다. 중복 Bind로 클릭 이벤트가 여러 번 등록되지 않게 wired를 확인합니다.
        public void Bind(ApiLobbyScreen controller)
        {
            screen = controller;
            EnsureComponents();
            if (wired)
            {
                return;
            }

            wired = true;
            UguiViewUtility.EnsureEventSystem(transform);
            runtimeFont = UguiViewUtility.ApplyFont(gameObject, fontOverride);
            search.onValueChanged.AddListener(value =>
            {
                screen.UiSearch = value;
                nextLists = 0;
            });
            joinPassword.onValueChanged.AddListener(value => screen.UiRoomPassword = value);
            chatInput.onValueChanged.AddListener(value => screen.UiChatInput = value);
            reconnectPassword.onValueChanged.AddListener(value => screen.UiReconnectPassword = value);
            createName.onValueChanged.AddListener(value => screen.UiCreateName = value);
            createCapacity.onValueChanged.AddListener(value => screen.UiCreateCapacity = value);
            createPassword.onValueChanged.AddListener(value => screen.UiCreatePassword = value);
            refresh.onClick.AddListener(() => screen.UiRefresh());
            leave.onClick.AddListener(() => screen.UiLeaveCurrentLobby());
            create.onClick.AddListener(OpenCreate);
            closeCreate.onClick.AddListener(CloseCreate);
            contentTree = new ContentTreePopup(
                createModal.transform,
                createContent.GetComponentInChildren<Text>().font,
                content => screen.UiSelectContent(content.rootContent, content.id)
            );
            createContent.onClick.AddListener(() =>
            {
                if (screen.UiCanUseRooms)
                {
                    contentTree.Show(screen.UiContents, screen.UiContentId, screen.UiContentNodeId);
                }
            });
            submitCreate.onClick.AddListener(() =>
            {
                if (!screen.UiCanUseRooms)
                {
                    return;
                }

                submitting = true;
                screen.UiSubmitCreate();
            });
            join.onClick.AddListener(() => screen.UiJoinRoom());
            available.onClick.AddListener(() =>
            {
                screen.UiToggleAvailable();
                nextLists = 0;
            });
            protection.onClick.AddListener(() =>
            {
                screen.UiCycleProtection();
                nextLists = 0;
            });
            sort.onClick.AddListener(() =>
            {
                screen.UiCycleSort();
                nextLists = 0;
            });
            resetFilters.onClick.AddListener(() =>
            {
                screen.UiResetFilters();
                nextLists = 0;
            });
            sendChat.onClick.AddListener(() => screen.UiSendChat());
            notifications.onClick.AddListener(() => screen.UiReconnectNotifications());
            logout.onClick.AddListener(() => screen.UiLogout());
            clearRoom.onClick.AddListener(() => screen.UiClearRoom());
            reconnect.onClick.AddListener(() => screen.UiReconnectRoom());
            forgetReconnect.onClick.AddListener(() => screen.UiForgetRoom());
            cancelJoin.onClick.AddListener(() => screen.UiCancelRoom());
            Refresh();
        }

        // 현재 콘텐츠 선택을 준비하고 방 생성 입력 창을 엽니다.
        private void OpenCreate()
        {
            if (!screen.UiCanUseRooms || screen.UiContents.Length == 0)
            {
                return;
            }

            screen.UiPrepareCreate();
            submitting = false;
            createModal.SetActive(true);
            createName.SetTextWithoutNotify(screen.UiCreateName);
            createCapacity.SetTextWithoutNotify(screen.UiCreateCapacity);
            createPassword.SetTextWithoutNotify("");
            EventSystem.current?.SetSelectedGameObject(createName.gameObject);
        }

        // 처리 중이 아니면 생성 창과 트리 창을 닫고 비밀번호 입력을 지웁니다.
        private void CloseCreate()
        {
            if (screen.UiProcessing)
            {
                return;
            }

            contentTree?.Close();
            createModal.SetActive(false);
            screen.UiCreatePassword = "";
            createPassword.SetTextWithoutNotify("");
            EventSystem.current?.SetSelectedGameObject(null);
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

        // 로그아웃·로비 변경 때 임시 입력과 생성된 목록 행을 정리합니다.
        internal void ResetTransientState()
        {
            UguiViewUtility.ClearSelection(transform);
            contentTree?.Close();
            createModal.SetActive(false);
            progressModal.SetActive(false);
            reconnectPanel.SetActive(false);
            submitting = false;
            context = "";
            nextLists = 0;
            foreach (var field in new[]
            {
                joinPassword,
                reconnectPassword,
                createPassword,
                createName,
                createCapacity,
                chatInput,
                search
            }

            )
            {
                field.SetTextWithoutNotify("");
            }

            UguiViewUtility.RemoveMissing(roomRows, new HashSet<string>());
            UguiViewUtility.RemoveMissing(participantRows, new HashSet<string>());
            UguiViewUtility.RemoveMissing(chatRows, new HashSet<string>());
            roomDetail.text = progress.text = createStatus.text = "";
        }

        // Controller 상태를 화면에 반영합니다. 처리 중 입력과 모달 뒤쪽 조작도 제한합니다.
        private void Refresh()
        {
            if (screen == null)
            {
                return;
            }

            EnsureComponents();
            string currentContext = screen.UiLoggedIn && !string.IsNullOrEmpty(screen.UiCurrentLobby) ? screen.UiSelfId + "|" + screen.UiCurrentLobby : "";
            if (context.Length > 0 && context != currentContext)
            {
                ResetTransientState();
            }

            bool visible = screen.UseUguiInterior;
            UguiViewUtility.SetVisible(this, canvas, interaction, raycaster, visible);
            if (!visible)
            {
                contentTree?.Close();
                createModal.SetActive(false);
                progressModal.SetActive(false);
                reconnectPanel.SetActive(false);
                screen.UiCreatePassword = "";
                foreach (var field in new[]
                {
                    createPassword,
                    joinPassword,
                    reconnectPassword
                }

                )
                {
                    field.SetTextWithoutNotify("");
                }

                return;
            }

            if (context != currentContext)
            {
                context = currentContext;
                contentTree?.Close();
                createModal.SetActive(false);
                submitting = false;
                screen.UiCreatePassword = "";
                screen.UiResetFilters();
                nextLists = 0;
            }

            contentTree?.Refresh(screen.UiContents);
            if (screen.UiProcessing)
            {
                contentTree?.Close();
            }

            bool busy = screen.UiProcessing, joining = !string.IsNullOrEmpty(screen.UiRoomProgress);
            progressModal.SetActive(joining);
            mainInteraction.interactable = mainInteraction.blocksRaycasts = !busy && !joining && !createModal.activeSelf;
            if (submitting && !busy)
            {
                submitting = false;
                if (!string.IsNullOrEmpty(screen.UiCreatedRoomId))
                {
                    contentTree?.Close();
                    createModal.SetActive(false);
                    createPassword.SetTextWithoutNotify("");
                    nextLists = 0;
                }
            }

            heading.text = screen.UiCurrentLobbyName + "  /  " + screen.UiUser;
            connection.text = screen.UiConnection;
            notice.text = screen.UiOperationNotice();
            notice.gameObject.SetActive(notice.text.Length > 0);
            status.text = screen.UiStatus;
            roomDetail.text = screen.UiRoomDetail();
            search.SetTextWithoutNotify(screen.UiSearch);
            search.interactable = !busy;
            available.GetComponentInChildren<Text>().text = screen.UiAvailableOnly ? "입장 가능만: 켜짐" : "입장 가능만: 꺼짐";
            protection.GetComponentInChildren<Text>().text = new[]
            {
                "전체 방",
                "공개 방",
                "비밀번호 방"
            }[screen.UiProtection];
            sort.GetComponentInChildren<Text>().text = screen.UiSort == 0 ? "최근 생성순" : "빈자리 많은 순";
            foreach (var button in new[]
            {
                refresh,
                available,
                protection,
                sort,
                resetFilters,
                notifications
            }

            )
            {
                button.interactable = !busy;
            }

            leave.interactable = !busy && !screen.UiHasRoomCleanup;
            create.interactable = screen.UiCanUseRooms && screen.UiContents.Length > 0;
            join.interactable = screen.UiCanJoinRoom;
            logout.interactable = !busy;
            joinPassword.gameObject.SetActive(screen.UiRoom?.hasPassword == true);
            joinPassword.SetTextWithoutNotify(screen.UiRoomPassword);
            joinPassword.interactable = !busy;
            clearRoom.gameObject.SetActive(screen.UiHasRoomCleanup);
            clearRoom.interactable = !busy;
            reconnectPanel.SetActive(screen.UiHasReconnect);
            reconnectPassword.SetTextWithoutNotify(screen.UiReconnectPassword);
            reconnectPassword.interactable = !busy;
            reconnect.interactable = screen.UiCanUseRooms;
            forgetReconnect.interactable = !busy;
            chatInput.SetTextWithoutNotify(screen.UiChatInput);
            chatInput.interactable = sendChat.interactable = screen.UiCanChat;
            chatStatus.text = screen.UiChatStatus;
            progress.text = screen.UiRoomProgress;
            cancelJoin.interactable = screen.UiCanCancelRoom;
            createStatus.text = screen.UiStatus;
            var content = Array.Find(screen.UiContents, x => x.rootContent == screen.UiContentId && x.id == screen.UiContentNodeId);
            createContent.GetComponentInChildren<Text>().text = "콘텐츠 선택: " + (content == null ? "없음" : (content.depth > 0 ? "하위 콘텐츠 · " : "") + content.title);
            bool choosingContent = contentTree?.IsOpen == true;
            foreach (var field in new[]
            {
                createName,
                createCapacity,
                createPassword
            }

            )
            {
                field.interactable = !busy && !choosingContent;
            }

            createContent.interactable = closeCreate.interactable = !busy && !choosingContent;
            submitCreate.interactable = screen.UiCanUseRooms && content != null && !choosingContent;
            if (Time.realtimeSinceStartup < nextLists)
            {
                return;
            }

            nextLists = Time.realtimeSinceStartup + .15f;
            UpdateRooms();
            UpdateParticipants();
            UpdateChat();
        }

        // 현재 방 목록과 행 ID를 비교해 필요한 행을 만들고 사라진 행을 제거합니다.
        private void UpdateRooms()
        {
            var displayed = screen.UiRoomList;
            var ids = new HashSet<string>(displayed.Select(x => x.id));
            UguiViewUtility.RemoveMissing(roomRows, ids);
            emptyRooms.gameObject.SetActive(displayed.Length == 0);
            roomCount.text = "방 목록 · " + displayed.Length + "개 표시";
            int index = 1;
            foreach (var room in displayed)
            {
                if (!roomRows.TryGetValue(room.id, out var row))
                {
                    string id = room.id;
                    row = Instantiate(roomTemplate, roomsContent);
                    row.name = "Room - " + id;
                    row.gameObject.SetActive(true);
                    row.onClick.AddListener(() => screen.UiChooseRoom(id));
                    roomRows[id] = row;
                }

                row.transform.SetSiblingIndex(index++);
                row.interactable = screen.UiCanUseRooms;
                row.GetComponent<UguiSelectionHighlight>()?.SetSelected(screen.UiRoom?.id == room.id);
                row.GetComponentInChildren<Text>().text = (screen.UiRoom?.id == room.id ? "● " : "") + room.name + (room.hasPassword ? " [비밀번호]" : "") + "\n접속 " + room.connectedUsers + "/" + room.maxUsers + " · 예약 " + room.reservedUsers + " · 빈자리 " + room.availableSlots + " · " + (room.isRunning ? "진행 중" : room.isEntryLocked ? "입장 잠김" : "대기 중");
            }
        }

        // 참가자 소속과 연결 상태를 참가자 행에 반영합니다.
        private void UpdateParticipants()
        {
            var users = screen.UiParticipants;
            UguiViewUtility.RemoveMissing(participantRows, new HashSet<string>(users.Select(x => x.userId)));
            participantTitle.text = "참가자 · " + users.Length + "명";
            emptyParticipants.gameObject.SetActive(users.Length == 0);
            emptyParticipants.text = screen.UiParticipantsLoaded ? "표시할 참가자가 없습니다." : "참가자 목록을 불러오는 중…";
            foreach (var user in users)
            {
                if (!participantRows.TryGetValue(user.userId, out var row))
                {
                    string id = user.userId;
                    row = Instantiate(participantTemplate, participantsContent);
                    row.name = "Participant - " + id;
                    row.gameObject.SetActive(true);
                    row.onClick.AddListener(() => screen.UiViewParticipant(id));
                    participantRows[id] = row;
                }

                row.GetComponentInChildren<Text>().text = screen.UiParticipantLabel(user) + (user.connectionState == "connected" ? "\n클릭하여 방 보기" : "");
                row.interactable = screen.UiCanUseRooms && user.connectionState == "connected";
            }
        }

        // 새 채팅 행을 추가하고 사용자가 아래쪽을 보고 있을 때만 최신 메시지로 스크롤합니다.
        private void UpdateChat()
        {
            var messages = screen.UiChatMessages;
            bool follow = chatRows.Count == 0 || chatScroll.verticalNormalizedPosition < .05f;
            bool changed = UguiViewUtility.RemoveMissing(chatRows, new HashSet<string>(messages.Select(x => x.id)));
            emptyChat.gameObject.SetActive(messages.Length == 0);
            foreach (var message in messages)
            {
                if (chatRows.ContainsKey(message.id))
                {
                    continue;
                }

                var row = Instantiate(chatTemplate, chatContent);
                row.gameObject.SetActive(true);
                row.text = UguiViewUtility.ChatLabel(message);
                chatRows[message.id] = row;
                changed = true;
            }

            if (changed && follow)
            {
                Canvas.ForceUpdateCanvases();
                chatScroll.verticalNormalizedPosition = 0;
            }
        }

        private void OnDestroy()
        {
            if (runtimeFont != null)
            {
                Destroy(runtimeFont);
            }
        }
    }
}
