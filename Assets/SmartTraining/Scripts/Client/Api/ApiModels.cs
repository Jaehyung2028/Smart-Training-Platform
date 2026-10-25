using System;
using System.Collections.Generic;

namespace SmartTraining
{
    // JsonUtility는 프로퍼티 대신 JSON 이름과 동일한 public 필드를 사용합니다.
    [Serializable]
    public class ApiError
    {
        public bool success;

        public string message;

        public string errorCode;

        public string traceId;
    }

    // 닉네임 로그인 입력입니다. 본인 사용자 ID는 서버에서 발급합니다.
    [Serializable]
    public class LoginRequest
    {
        public string userName;
    }

    // 로그인 또는 토큰 갱신 결과입니다. 이후 요청에 사용할 토큰과 사용자 상태를 받습니다.
    [Serializable]
    public class LoginResponse
    {
        public bool success;

        public string message;

        public string accessToken;

        public string expiresAtUtc;

        public CurrentUser user;
    }

    // /me에서 받은 사용자 소속입니다. 접속 확정 방과 입장 예약 중인 방을 구분합니다.
    [Serializable]
    public class CurrentUser
    {
        public string id;

        public string name;

        public string currentLobbyId;

        public string currentLobbyName;

        public string currentRoomId;

        public string currentRoomName;

        // 서버에는 예약되어 있지만 아직 실제 접속이 확정되지 않은 방입니다.
        public string pendingRoomId;

        public int lobbyIndex;

        public int roomIndex;
    }

    // 로비 카드에 표시할 요약입니다. 운영 상태와 사용자·방 개수를 포함합니다.
    [Serializable]
    public class LobbySummary
    {
        public string id;

        public string name;

        public string createdAt;

        public string contentId;

        public string maintenanceReason;

        public string maintenanceChangedAtUtc;

        public int userCount;

        public int roomCount;

        public int contentCount;

        public bool isMaintenance;

        public long maintenanceRevision;

        public string shutdownAtUtc;

        public string shutdownReason;

        public string shutdownState;

        public string operatingMode;

        public string operatingReason;

        public long operatingRevision;

        public long shutdownRevision;
    }

    // JsonUtility가 최상위 JSON 배열을 읽을 수 있도록 items로 감싼 형식입니다.
    [Serializable]
    public class LobbyList
    {
        public LobbySummary[] items;
    }

    // 로비 참가자의 대기·방 접속 상태와 방장 여부를 표시합니다.
    [Serializable]
    public class LobbyParticipant
    {
        public string userId;

        public string nickname;

        public string connectionState;

        public string roomId;

        public string roomName;

        public bool isRoomMaster;

        public bool roomIsRunning;
    }

    // 로비 참가자 배열을 JsonUtility로 읽기 위한 래퍼입니다.
    [Serializable]
    public class LobbyParticipantList
    {
        public LobbyParticipant[] items;
    }

    // 서버의 { status: ok } JSON을 읽는 클라이언트 형식입니다. 서버에 같은 이름의 클래스가 필요한 것은 아닙니다.
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

    // 카탈로그 노드입니다. rootContent는 트리, id는 같은 트리 안의 노드를 구분합니다.
    [Serializable]
    public class ContentSummary
    {
        // 콘텐츠 트리 전체의 문자열 ID입니다. 고정 로비 ID에도 사용합니다.
        public string rootContent;

        public string title;

        public int id;

        public int parentId;

        public int depth;

        // 하위 노드입니다. 펼침·접기 UI에서도 이 부모·자식 관계를 사용합니다.
        public ContentSummary[] children;

        // API는 트리를 반환합니다. 선택 UI에서는 부모 다음에 자식을 배치한 목록으로 사용합니다.
        public static ContentSummary[] Flatten(ContentSummary[] roots)
        {
            var result = new List<ContentSummary>();
            foreach (var root in roots ?? Array.Empty<ContentSummary>())
            {
                AddToList(root, result);
            }

            return result.ToArray();
        }

        private static void AddToList(ContentSummary content, List<ContentSummary> result)
        {
            if (content == null)
            {
                return;
            }

            result.Add(content);
            foreach (var child in content.children ?? Array.Empty<ContentSummary>())
            {
                AddToList(child, result);
            }
        }
    }

    // 콘텐츠 트리 배열을 JSON에서 읽기 위한 래퍼입니다.
    [Serializable]
    public class ContentList
    {
        public ContentSummary[] items;
    }

    // 방 생성 입력입니다. 콘텐츠 루트와 선택 노드 ID를 함께 전달합니다.
    [Serializable]
    public class CreateRoomRequest
    {
        public string lobbyId;

        public string roomName;

        public string contentId;

        public string password;

        public int maxUsers;

        // contentId 루트 안에서 선택한 노드의 숫자 ID입니다.
        public int contentNodeId;
    }

    // 생성한 방 ID를 받아 바로 입장 요청으로 이어갈 때 사용합니다.
    [Serializable]
    public class CreateRoomResponse
    {
        public bool success;

        public RoomSummary room;
    }

    // 방 목록과 상세 화면에서 사용하는 공개 정보입니다. 입장 토큰은 포함하지 않습니다.
    [Serializable]
    public class RoomSummary
    {
        public string id;

        public string lobbyId;

        public string name;

        public string contentId;

        public string contentTitle;

        public string masterUserId;

        public string createdAt;

        public int maxUsers;

        public int connectedUsers;

        public int reservedUsers;

        public int availableSlots;

        public int state;

        // contentId 루트 안에서 선택한 노드의 숫자 ID입니다.
        public int contentNodeId;

        public bool isEntryLocked;

        public bool hasPassword;

        public bool isRunning;
    }

    // 방 요약 배열을 JSON에서 읽기 위한 래퍼입니다.
    [Serializable]
    public class RoomList
    {
        public RoomSummary[] items;
    }

    // 로비·방 ID는 작업할 장소입니다. 요청자 신원은 본문이 아닌 인증 헤더에서 전달됩니다.
    [Serializable]
    public class RoomMembershipRequest
    {
        public string lobbyId;

        public string roomId;
    }

    // 입장할 방과 비밀번호를 전달합니다. 공개 방에서는 비밀번호를 생략할 수 있습니다.
    [Serializable]
    public class JoinRoomRequest
    {
        public string lobbyId;

        public string roomId;

        public string password;
    }

    // 방장용 비밀번호 변경 입력입니다. 빈 문자열은 해제이며 입장 요청과 의미가 다릅니다.
    [Serializable]
    public class UpdateRoomPasswordRequest
    {
        public string lobbyId;

        public string roomId;

        public string password;
    }

    // 예약한 방의 접속 주소와 포트입니다. HTTP API 주소와 구분합니다.
    [Serializable]
    public class ReservedRoom
    {
        public string id;

        public string lobbyId;

        public string name;

        public string ipAddress;

        public int port;
    }

    // 입장 예약 결과입니다. 이 성공만으로 Netcode 연결이 완료된 것은 아닙니다.
    [Serializable]
    public class JoinRoomResponse
    {
        public bool success;

        public ReservedRoom room;

        public string admissionToken;

        // 토큰 또는 예약의 UTC 만료 시각입니다. 어떤 응답인지에 따라 대상을 구분합니다.
        public string expiresAtUtc;
    }

    // Netcode 접속 승인 데이터입니다. 일반 HTTP 본문과 달리 예약 토큰에 연결된 참가자를 식별하는 userId가 필요합니다.
    [Serializable]
    public class RoomConnectionData
    {
        public string roomId;

        public string userId;

        // 방 입장 예약을 증명하는 토큰입니다. 로그인용 accessToken과 다릅니다.
        public string admissionToken;
    }

    // 전용 서버와 클라이언트가 읽는 Hub 응답입니다. 호출 종류에 필요한 필드를 사용합니다.
    [Serializable]
    public class RoomHubResult : ApiError
    {
        public string userId;

        public string userName;

        public string masterUserId;

        public string roomName;

        public int roomIndex;

        public int maxUsers;

        public bool isEntryLocked;

        public bool isRunning;

        public long phaseRevision;

        // 방 채팅 구독 전에 생성된 메시지를 제외하기 위한 기준 번호입니다.
        public long chatSequence;
    }

    // 방장이 변경할 방 이름과 정원입니다.
    [Serializable]
    public class UpdateRoomSettingsRequest
    {
        public string lobbyId;

        public string roomId;

        public string roomName;

        public int maxUsers;
    }

    // targetUserId는 요청자 본인이 아니라 위임·강퇴할 상대입니다. 서버가 요청자의 방장 권한과 대상 소속을 확인합니다.
    [Serializable]
    public class RoomParticipantRequest
    {
        public string lobbyId;

        public string roomId;

        public string targetUserId;
    }

    // 방장의 신규 입장 잠금 설정입니다.
    [Serializable]
    public class SetRoomEntryLockRequest
    {
        public string lobbyId;

        public string roomId;

        public bool isEntryLocked;
    }
}
