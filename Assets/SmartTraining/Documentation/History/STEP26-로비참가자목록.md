# STEP26 — 로비 참가자 목록

현재 입장한 로비의 참가자 닉네임과 접속 상태를 로비 화면 및 방 화면에 표시합니다. 기존 SignalR 자동 갱신 연결을 사용합니다.

## 구현 위치

- 서버 `GET /api/Lobby/{lobbyId}/participants`, `LobbyController.GetParticipants()` — 인증된 현재 로비 참가자만 조회. 미인증 401, 다른 로비 403, 없는 로비 404.
- 서버 `RoomService.GetLobbyParticipants()` — 만료 예약 정리 후 회원·예약·접속 확정 정보를 함께 조회하고 별도 응답 객체를 생성.
- 서버 `LobbyParticipantResponse` — 사용자 ID·닉네임·접속 상태·방 이름·방장 여부·진행 여부만 응답. 인증/입장/서버 토큰과 서버 주소는 제외.
- 서버 `LobbyRoomUpdates.CollectChanges()` — 참가자 스냅샷도 비교. 인원수가 같아도 사용자가 교체되면 갱신.
- Unity `TrainingApiClient.GetLobbyParticipants()` — 참가자 조회.
- Unity `ApiLobbyScreen.Participants.cs`의 `DrawLobbyParticipants()`, `LoadLobbyParticipants()`, `SyncParticipantsContext()` — 표시, 갱신, 로비 변경/로그아웃 시 목록 정리.

## 표시 상태

- `lobby`: 로비 대기.
- `reserved`: 방 접속 예약. 실제 접속으로 집계하지 않음.
- `connected`: 방 접속 중. 방 이름·방장 여부·대기/진행 상태도 표시.
- 자신의 닉네임 옆에 `(나)` 표시. 닉네임 기준 정렬.

입장·퇴장·로그아웃·예약·실접속·연결 끊김·방장 위임·방 이름/진행 상태 변경은 자동 갱신합니다. 서버가 약 1초 간격으로 변화를 모으므로 화면 반영에는 통신 시간과 처리 중인 작업에 따른 지연이 있을 수 있습니다.

다른 로비 목록을 살펴봐도 참가자 목록은 현재 소속 로비를 표시합니다. 현재 로비를 떠나거나 로그아웃하면 이전 닉네임 목록을 삭제합니다. 자동 갱신 연결이 끊기면 마지막 조회 정보를 표시한다는 안내를 표시하고 재연결 후 다시 조회합니다. 수동 새로고침 버튼도 제공합니다.

## 직접 확인

1. ASP.NET 서버를 재시작하고 Unity 에디터와 클라이언트 빌드를 서로 다른 닉네임으로 실행합니다.
2. 같은 로비에 입장하면 `현재 로비 참가자`에 두 닉네임이 자동 표시되는지 확인합니다.
3. 한 명이 방에 참가하면 다른 화면에서 `방 접속 중`과 방 이름·방장 표시가 갱신되는지 확인합니다.
4. 방 접속 예약만 된 사용자는 `방 접속 예약`으로 표시되는지 확인합니다.
5. 방 퇴장·강퇴·연결 끊김으로 로비에 돌아오면 `로비 대기`로 바뀌는지 확인합니다.
6. 방장을 위임하면 참가자 목록의 방장 표시가 바뀌는지 확인합니다.
7. 로그아웃하면 다른 사용자 화면의 목록에서 해당 닉네임이 사라지고, 로그아웃한 화면의 목록이 지워지는지 확인합니다.

자동 검증: 서버 `Tests/Checks/LobbyParticipantsChecks.cs`, `ApiContractChecks.cs`; Unity `Editor/LobbyParticipantsChecks.cs` 및 기존 멀티 접속 통합 검증.
