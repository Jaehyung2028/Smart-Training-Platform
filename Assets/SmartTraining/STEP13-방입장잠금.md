# 방장 입장 잠금

방장은 새 입장을 잠그거나 다시 열 수 있습니다. 이미 접속한 참가자의 연결과 이동은 유지됩니다.
방 목록과 방 안 화면에 현재 잠금 상태가 표시됩니다.

## 확인 순서
1. ASP.NET과 Unity Play를 종료한 뒤 ASP.NET을 다시 빌드·실행합니다.
2. Unity ApiLobby 씬에서 방에 참가하고 방장 화면의 `새 참가자 입장 잠그기`를 누릅니다.
3. 다른 닉네임으로 `Builds/Client/Smart Training Client.exe`를 실행합니다. 방 목록을 새로고침하면 `입장 잠김`이 표시되고 참가 버튼이 비활성화됩니다.
4. 방 안의 참가자는 이동과 통신을 계속할 수 있습니다.
5. 방장이 `새 참가자 입장 열기`를 누르면 새로운 예약으로 참가할 수 있습니다.
6. 잠긴 상태에서 방장을 넘기거나 방장이 나갑니다. 잠금 상태는 유지되며 새 방장이 열 수 있습니다.

## 예약·재접속 규칙
- 잠금 시점에 아직 접속이 확정되지 않은 입장 예약은 취소됩니다.
- 이전에 발급된 예약 토큰으로 접속 승인을 받거나 입장을 확정할 수 없습니다. 방을 연 후 새 예약을 받아야 합니다.
- 잠긴 방에서 나갔다 다시 들어가는 사용자도 새 입장 요청이므로 잠금 해제 후 참가해야 합니다.
- 방 목록은 새로고침으로 갱신합니다. 오래된 목록에서 참가를 시도해도 서버가 최신 잠금 상태를 검사합니다.
- 실제 접속 확정과 잠금 처리는 같은 멤버십 잠금에서 처리합니다. 먼저 접속이 확정되면 기존 참가자로 남고, 먼저 방이 잠기면 접속을 거절합니다.
- 전용 서버가 종료되어 모든 접속이 초기화되면 잠금을 해제합니다. 방장이 없는 방을 다시 시작할 수 있게 하기 위한 처리입니다.

## 함수별 변경
- ASP.NET `RoomController.SetRoomEntryLock`, `RoomService.SetRoomEntryLock`: `POST /api/Room/entry-lock`, 현재 방장 확인과 예약 취소.
- ASP.NET `RoomService.JoinRoom`, `ValidateConnection`, `ConfirmConnection`: 예약·전용 서버 접속 승인·접속 확정 단계에서 잠금 검사.
- ASP.NET `RoomService.GetRoomByLobby`, `GetRoomMaster`: 방 목록과 전용 서버에 잠금 상태 제공.
- Unity `RoomDedicatedServer.SynchronizeRoomMaster`, `RoomPlayer.SetRoomEntryLockServer`: 서버에서 모든 참가자에게 상태 동기화.
- Unity `TrainingApiClient.SetRoomEntryLock`, `ApiLobbyScreen.SetRoomEntryLock`: 방장 버튼과 표시 갱신 대기.
- Unity `ApiLobbyScreen.OnGUI`, `DrawRoom`: 방 목록·방 내부 상태 표시 및 참가 버튼 조건.

요청 예: `{ "lobbyId": "로비 ID", "roomId": "방 ID", "isEntryLocked": true }`
`isEntryLocked`는 반드시 명시합니다. 현재 방장만 변경할 수 있고, 이전 방장은 위임 직후 변경 권한을 잃습니다.
전용 서버와 클라이언트의 네트워크 상태 구조가 함께 갱신되므로 두 빌드 모두 새 버전을 사용해야 합니다.
