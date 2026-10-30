using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

namespace SmartTraining
{
    public sealed partial class ApiLobbyScreen
    {
        private LobbyInteriorUguiView interiorView;
        private string uiCreatedRoomId = "";

        internal bool UseUguiInterior => interiorView != null &&
            isActiveAndEnabled &&
            UiLoggedIn &&
            string.IsNullOrEmpty(lobbyJoinProgress) &&
            (!string.IsNullOrEmpty(roomJoinProgress) ||
            (!string.IsNullOrEmpty(api.User.currentLobbyId) &&
            network?.IsConnected != true));

        internal string UiCurrentLobby => api?.User?.currentLobbyId ?? "";

        internal string UiCurrentLobbyName => api?.User?.currentLobbyName ?? "";

        internal RoomSummary[] UiRoomList => GetDisplayedRooms();

        internal RoomSummary UiRoom => selectedRoom;

        internal ContentSummary[] UiContents => contents;

        internal string UiContentId => selectedContentId;

        internal int UiContentNodeId => selectedContentNodeId;

        internal LobbyParticipant[] UiParticipants => lobbyParticipants;

        internal bool UiParticipantsLoaded => participantsLoaded;

        internal string UiSelfId => api?.User?.id ?? "";

        internal IReadOnlyList<LobbyChatMessage> UiChatMessages => chatMessages;

        internal string UiChatInput { get => chatInput; set => chatInput = value; }

        internal string UiChatStatus => UiProcessing ? UiStatus : UiChatConnected ? chatStatus : "로비 채팅 연결 중…";

        internal bool UiCanChat => UiChatConnected && !UiProcessing;

        internal bool UiChatConnected => UiLoggedIn &&
            !liveConnecting &&
            lobbyUpdates != null &&
            lobbyUpdates.Failure == null &&
            liveLobbyId == UiCurrentLobby;

        internal string UiSearch { get => roomSearch; set => roomSearch = value; }

        internal bool UiAvailableOnly => availableRoomsOnly;

        internal int UiProtection => (int)roomProtectionFilter;

        internal int UiSort => (int)roomSort;

        internal string UiRoomPassword { get => joinRoomPassword; set => joinRoomPassword = value; }

        internal string UiCreateName { get => roomName; set => roomName = value; }

        internal string UiCreateCapacity { get => maxUsersText; set => maxUsersText = value; }

        internal string UiCreatePassword { get => createRoomPassword; set => createRoomPassword = value; }

        internal string UiCreatedRoomId => uiCreatedRoomId;

        internal string UiRoomProgress => roomJoinProgress;

        internal bool UiCanCancelRoom => roomJoinCanCancel && !roomJoinCancelRequested;

        internal bool UiHasRoomCleanup => UiLoggedIn &&
            (!string.IsNullOrEmpty(api.User.currentRoomId) ||
            !string.IsNullOrEmpty(api.User.pendingRoomId) ||
            !string.IsNullOrEmpty(networkRoomId));

        internal bool UiHasReconnect => !string.IsNullOrEmpty(reconnectRoomId);

        internal string UiReconnectPassword { get => reconnectPassword; set => reconnectPassword = value; }

        internal bool UiCanUseRooms => UiLoggedIn &&
            !UiProcessing &&
            !string.IsNullOrEmpty(UiCurrentLobby) &&
            !UiHasRoomCleanup &&
            network?.IsConnected != true;

        internal bool UiCanJoinRoom => UiCanUseRooms &&
            selectedRoom != null &&
            Array.Exists(UiRoomList, x => x.id == selectedRoom.id) &&
            RoomBrowser.CanJoin(selectedRoom);


        internal void UiChooseRoom(string id)
        {
            if (UiCanUseRooms)
            {
                RunOperation(SelectRoom(id), "방 정보를 확인하고 있습니다.");
            }
        }


        internal void UiJoinRoom()
        {
            if (UiCanJoinRoom)
            {
                RunOperation(JoinRoom(selectedRoom.id), "방에 입장하고 있습니다.");
            }
        }


        internal void UiLeaveCurrentLobby()
        {
            if (UiLoggedIn && !string.IsNullOrEmpty(UiCurrentLobby) && !UiProcessing && !UiHasRoomCleanup)
            {
                RunOperation(LeaveLobby(), "로비에서 나가고 있습니다.");
            }
        }


        internal void UiClearRoom()
        {
            if (!UiProcessing && UiHasRoomCleanup)
            {
                ForgetReconnect();
                RunOperation(LeaveRoom("남은 방 접속 정보를 정리했습니다."), "남은 방 접속 정보를 정리하고 있습니다.");
            }
        }


        internal void UiReconnectRoom()
        {
            if (UiCanUseRooms && UiHasReconnect)
            {
                RunOperation(ReconnectRoom(), "방에 다시 접속하고 있습니다.");
            }
        }


        internal void UiForgetRoom()
        {
            if (!UiProcessing)
            {
                ForgetReconnect();
            }
        }


        internal void UiCancelRoom() => RequestCancelRoomJoin();

        internal void UiResetFilters()
        {
            if (!UiProcessing)
            {
                ResetRoomBrowser();
            }
        }


        internal void UiToggleAvailable()
        {
            if (!UiProcessing)
            {
                availableRoomsOnly = !availableRoomsOnly;
            }
        }


        internal void UiCycleProtection()
        {
            if (!UiProcessing)
            {
                roomProtectionFilter = (RoomProtectionFilter)(((int)roomProtectionFilter + 1) % 3);
            }
        }


        internal void UiCycleSort()
        {
            if (!UiProcessing)
            {
                roomSort = (RoomSortOrder)(((int)roomSort + 1) % 2);
            }
        }


        // 자식이 있는 항목은 분류이고, 자식이 없는 항목만 방에서 사용할 실제 콘텐츠입니다.
        private static bool IsSelectableContent(ContentSummary content)
        {
            return content != null && (content.children == null || content.children.Length == 0);
        }


        internal void UiPrepareCreate()
        {
            uiCreatedRoomId = "";
            createRoomPassword = "";
            if (!Array.Exists(
                contents,
                x => x.rootContent == selectedContentId && x.id == selectedContentNodeId && IsSelectableContent(x)
            ))
            {
                var first = contents.FirstOrDefault(IsSelectableContent);
                selectedContentId = first?.rootContent ?? "";
                selectedContentNodeId = first?.id ?? 0;
            }
        }


        // 현재 목록에 있는 최하위 콘텐츠만 선택할 수 있습니다.
        internal void UiSelectContent(string rootContent, int nodeId)
        {
            if (!UiCanUseRooms ||
                !Array.Exists(
                contents,
                item => item.rootContent == rootContent &&
                item.id == nodeId &&
                IsSelectableContent(item)
            ))
            {
                return;
            }

            selectedContentId = rootContent;
            selectedContentNodeId = nodeId;
        }


        internal void UiSubmitCreate()
        {
            if (UiCanUseRooms &&
                Array.Exists(
                contents,
                x => x.rootContent == selectedContentId &&
                x.id == selectedContentNodeId &&
                IsSelectableContent(x)
            ))
            {
                RunOperation(CreateRoomForUi(), "방을 생성하고 있습니다.");
            }
        }


        private IEnumerator CreateRoomForUi()
        {
            uiCreatedRoomId = "";
            yield return CreateRoom();
            if (!string.IsNullOrEmpty(uiCreatedRoomId))
            {
                ResetRoomBrowser();
            }
        }


        internal void UiViewParticipant(string id)
        {
            if (UiCanUseRooms)
            {
                RunOperation(ViewParticipantRoom(id), "참가자의 방을 확인하고 있습니다.");
            }
        }


        internal void UiSendChat()
        {
            if (UiCanChat)
            {
                chatChannel = 0;
                RunOperation(SendLobbyChat(), "채팅을 전송하고 있습니다.");
            }
        }


        internal string UiParticipantLabel(LobbyParticipant user) => user.nickname + (user.userId == UiSelfId ? " (나)" : "") + "\n" + ParticipantStateLabel(user);

        internal string UiOperationNotice()
        {
            var lobby = Array.Find(lobbies, x => x.id == UiCurrentLobby);
            if (lobby == null || !lobby.isMaintenance)
            {
                return operationError;
            }

            string text = lobby.operatingMode == "scheduled" ? "종료 예정" : "점검 중 · 기존 참가자는 계속 이용할 수 있습니다.";
            if (lobby.operatingMode == "scheduled" && DateTimeOffset.TryParse(lobby.shutdownAtUtc, out var at))
            {
                text += " · " + at.ToLocalTime().ToString("HH:mm:ss") + " · 남은 " + Math.Max(
                    0,
                    (long)Math.Ceiling((at - DateTimeOffset.UtcNow).TotalSeconds)
                ) + "초";
            }

            string notice = text + "\n" + lobby.operatingReason;
            return string.IsNullOrEmpty(operationError) ? notice : notice + "\n" + operationError;
        }


        internal string UiRoomDetail()
        {
            var room = selectedRoom;
            if (room == null)
            {
                return "참가할 방을 선택해주세요.";
            }

            return room.name + " / " + room.contentTitle + "\n접속 " + room.connectedUsers + "/" + room.maxUsers + " · 예약 " + room.reservedUsers + " · 빈자리 " + room.availableSlots + "\n" + (room.isRunning ? "진행 중" : room.isEntryLocked ? "입장 잠김" : RoomStateLabel(room.state)) + (room.hasPassword ? " · 비밀번호 필요" : " · 공개 방");
        }
    }
}
