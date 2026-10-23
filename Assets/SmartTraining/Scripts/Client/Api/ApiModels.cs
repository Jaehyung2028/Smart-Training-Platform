using System;

namespace SmartTraining
{
    // JsonUtility는 프로퍼티 대신 JSON 이름과 동일한 public 필드를 사용합니다.
    [Serializable]
    public class ApiError
    {
        public bool success; public string message; public string errorCode; public string traceId;
    }
    [Serializable]
    public class LoginRequest
    {
        public string userName;
    }
    [Serializable]
    public class LoginResponse
    {
        public bool success; public string message; public string accessToken; public string expiresAtUtc; public CurrentUser user;
    }
    [Serializable]
    public class CurrentUser
    {
        public string id, name, currentLobbyId, currentLobbyName, currentRoomId, currentRoomName, pendingRoomId;
        public int lobbyIndex, roomIndex;
    }
    [Serializable]
    public class LobbySummary
    {
        public string id, name, createdAt, contentId, maintenanceReason, maintenanceChangedAtUtc; public int userCount, roomCount, contentCount; public bool isMaintenance; public long maintenanceRevision; public string shutdownAtUtc, shutdownReason, shutdownState, operatingMode, operatingReason; public long operatingRevision; public long shutdownRevision;
    }
    [Serializable]
    public class LobbyList
    {
        public LobbySummary[] items;
    }
    [Serializable]
    public class LobbyParticipant
    {
        public string userId, nickname, connectionState, roomId, roomName;
        public bool isRoomMaster, roomIsRunning;
    }
    [Serializable]
    public class LobbyParticipantList
    {
        public LobbyParticipant[] items;
    }
    [Serializable]
    public class HealthResponse
    {
        public string status;
    }
    // 일반 HTTP 요청에는 본인 userId를 넣지 않습니다. 서버가 Authorization 토큰으로 요청자를 확인합니다.
    [Serializable]
    public class LobbyMembershipRequest
    {
        public string lobbyId;
    }
    [Serializable]
    public class ContentSummary
    {
        public string rootContent, title;
        public int id, parentId, depth;
        public ContentSummary[] children;
    }
    [Serializable]
    public class ContentList
    {
        public ContentSummary[] items;
    }
    [Serializable]
    public class CreateRoomRequest
    {
        public string lobbyId, roomName, contentId, password; public int maxUsers;
    }
    [Serializable]
    public class CreateRoomResponse
    {
        public bool success; public RoomSummary room;
    }
    [Serializable]
    public class RoomSummary
    {
        public string id, lobbyId, name, contentId, contentTitle, masterUserId, createdAt;
        public int maxUsers, connectedUsers, reservedUsers, availableSlots, state;
        public bool isEntryLocked;
        public bool hasPassword;
        public bool isRunning;
    }
    [Serializable]
    public class RoomList
    {
        public RoomSummary[] items;
    }
    // 로비·방 ID는 작업할 장소입니다. 요청자 신원은 본문이 아닌 인증 헤더에서 전달됩니다.
    [Serializable]
    public class RoomMembershipRequest
    {
        public string lobbyId, roomId;
    }
    [Serializable]
    public class JoinRoomRequest
    {
        public string lobbyId, roomId, password;
    }
    [Serializable]
    public class UpdateRoomPasswordRequest
    {
        public string lobbyId, roomId, password;
    }
    [Serializable]
    public class ReservedRoom
    {
        public string id, lobbyId, name, ipAddress; public int port;
    }
    [Serializable]
    public class JoinRoomResponse
    {
        public bool success; public ReservedRoom room; public string admissionToken, expiresAtUtc;
    }
    // Netcode 접속 승인 데이터입니다. 일반 HTTP 본문과 달리 예약 토큰에 연결된 참가자를 식별하는 userId가 필요합니다.
    [Serializable]
    public class RoomConnectionData
    {
        public string roomId, userId, admissionToken;
    }
    [Serializable]
    public class RoomHubResult : ApiError
    {
        public string userId, userName, masterUserId, roomName; public int roomIndex, maxUsers; public bool isEntryLocked, isRunning; public long phaseRevision, chatSequence;
    }
    [Serializable]
    public class UpdateRoomSettingsRequest
    {
        public string lobbyId, roomId, roomName; public int maxUsers;
    }
    // targetUserId는 요청자 본인이 아니라 위임·강퇴할 상대입니다. 서버가 요청자의 방장 권한과 대상 소속을 확인합니다.
    [Serializable]
    public class RoomParticipantRequest
    {
        public string lobbyId, roomId, targetUserId;
    }
    [Serializable]
    public class SetRoomEntryLockRequest
    {
        public string lobbyId, roomId; public bool isEntryLocked;
    }
}
