using System;

namespace SmartTraining
{
    // JsonUtility는 프로퍼티 대신 JSON 이름과 동일한 public 필드를 사용합니다.
    [Serializable] public class ApiError { public bool success; public string message; public string errorCode; public string traceId; }
    [Serializable] public class LoginRequest { public string userName; }
    [Serializable] public class LoginResponse { public bool success; public string message; public string accessToken; public string expiresAtUtc; public CurrentUser user; }
    [Serializable] public class CurrentUser
    {
        public string id, name, currentLobbyId, currentLobbyName, currentRoomId, currentRoomName, pendingRoomId;
        public int lobbyIndex, roomIndex;
    }
    [Serializable] public class LobbySummary { public string id, name, createdAt; public int userCount, roomCount, contentCount; }
    [Serializable] public class LobbyList { public LobbySummary[] items; }
    [Serializable] public class HealthResponse { public string status; }
    [Serializable] public class CreateLobbyRequest { public string lobbyName; }
    [Serializable] public class LobbyMembershipRequest { public string lobbyId; }
    [Serializable] public class CreateLobbyResponse { public bool success; public LobbySummary lobby; }
    [Serializable] public class ContentSummary
    {
        public string rootContent, title;
        public int id, parentId, depth;
        public ContentSummary[] children;
    }
    [Serializable] public class ContentList { public ContentSummary[] items; }
    [Serializable] public class CreateRoomRequest { public string lobbyId, roomName, contentId; public int maxUsers; }
    [Serializable] public class CreateRoomResponse { public bool success; public RoomSummary room; }
    [Serializable] public class RoomSummary
    {
        public string id, lobbyId, name, contentId, contentTitle, masterUserId, createdAt;
        public int maxUsers, connectedUsers, reservedUsers, availableSlots, state;
        public bool isEntryLocked;
    }
    [Serializable] public class RoomList { public RoomSummary[] items; }
    [Serializable] public class RoomMembershipRequest { public string lobbyId, roomId; }
    [Serializable] public class ReservedRoom { public string id, lobbyId, name, ipAddress; public int port; }
    [Serializable] public class JoinRoomResponse { public bool success; public ReservedRoom room; public string admissionToken, expiresAtUtc; }
    [Serializable] public class RoomConnectionData { public string roomId, userId, admissionToken; }
    [Serializable] public class RoomHubResult : ApiError { public string userId, userName, masterUserId; public int roomIndex; public bool isEntryLocked; }
    [Serializable] public class TransferRoomMasterRequest { public string lobbyId, roomId, targetUserId; }
    [Serializable] public class KickRoomUserRequest { public string lobbyId, roomId, targetUserId; }
    [Serializable] public class SetRoomEntryLockRequest { public string lobbyId, roomId; public bool isEntryLocked; }
}
