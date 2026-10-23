using System;
using System.Collections;

namespace SmartTraining
{
    public sealed partial class ApiLobbyScreen
    {

        private IEnumerator ViewParticipantRoom(string userId)
        {
            var source = api;
            if (source?.IsLoggedIn != true)
            {
                status = "먼저 로그인해주세요.";
                yield break;
            }
            bool loaded = false;
            yield return source.GetMe(_ => loaded = true, Failure);
            if (!loaded || source != api)
                yield break;
            if (network?.IsConnected == true || source.User.currentRoomId.Length > 0 || source.User.pendingRoomId.Length > 0)
            {
                status = "현재 방에서 나가거나 접속 예약을 취소한 뒤 다른 참가자의 방을 찾아주세요.";
                yield break;
            }
            var lobby = source.User.currentLobbyId;
            if (string.IsNullOrEmpty(lobby))
            {
                status = "현재 로비에 입장한 뒤 사용해주세요.";
                yield break;
            }
            selectedRoom = null;
            joinRoomPassword = "";
            LobbyParticipant[] participants = null;
            yield return source.GetLobbyParticipants(lobby, items => participants = items, Failure);
            if (participants == null || source != api || source.User.currentLobbyId != lobby)
                yield break;
            var target = Array.Find(participants, item => item.userId == userId);
            if (target == null)
            {
                status = "해당 참가자가 현재 로비를 떠났습니다. 목록을 새로고침해주세요.";
                yield break;
            }
            if (target.connectionState != "connected" || string.IsNullOrEmpty(target.roomId))
            {
                status = "해당 참가자는 현재 방에 접속 중이 아닙니다.";
                yield break;
            }

            ContentSummary[] contentItems = null;
            RoomSummary[] roomItems = null;
            RoomSummary detail = null;
            yield return source.GetContents(lobby, items => contentItems = items, Failure);
            if (contentItems == null)
                yield break;
            yield return source.GetRooms(lobby, items => roomItems = items, Failure);
            if (roomItems == null)
                yield break;
            if (!Array.Exists(roomItems, item => item.id == target.roomId))
            {
                status = "해당 방이 닫혔습니다. 참가자 목록을 새로고침해주세요.";
                yield break;
            }
            yield return source.GetRoom(lobby, target.roomId, item => detail = item, Failure);
            if (detail == null)
                yield break;
            participants = null;
            yield return source.GetLobbyParticipants(lobby, items => participants = items, Failure);
            if (participants == null || source != api || source.User.currentLobbyId != lobby)
                yield break;
            var current = Array.Find(participants, item => item.userId == userId);
            if (current == null || current.connectionState != "connected" || current.roomId != detail.id)
            {
                status = "해당 참가자의 위치가 변경되었습니다. 목록에서 다시 선택해주세요.";
                yield break;
            }

            selectedLobbyId = lobby;
            ClearLobbyData();
            ResetRoomBrowser();
            contents = contentItems;
            rooms = roomItems;
            selectedRoom = detail;
            selectedContentId = detail.contentId;
            int index = Array.FindIndex(rooms, item => item.id == detail.id);
            if (index >= 0)
                rooms[index] = detail;
            SyncParticipantsContext();
            lobbyParticipants = participants;
            participantsLoaded = true;

            status = ParticipantRoomViewStatus(detail);
        }
        private static string ParticipantRoomViewStatus(RoomSummary room)
        {
            if (room.isRunning)
                return "방을 찾았습니다. 진행 중인 방에는 새로 입장할 수 없습니다.";
            if (room.isEntryLocked)
                return "방을 찾았습니다. 방장이 새 참가자의 입장을 잠갔습니다.";
            if (room.availableSlots <= 0)
                return "방을 찾았습니다. 현재 남은 자리가 없습니다.";
            if (!RoomBrowser.CanJoin(room))
                return "방을 찾았습니다. 현재 방 서버 상태에서는 참가할 수 없습니다.";
            return room.hasPassword ? "방을 찾았습니다. 비밀번호를 입력한 뒤 참가 버튼을 눌러주세요." : "방을 찾았습니다. 참가 버튼을 눌러 입장할 수 있습니다.";
        }
    }
}
