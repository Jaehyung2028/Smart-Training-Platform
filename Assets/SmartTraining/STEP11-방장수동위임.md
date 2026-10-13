# 방장 수동 위임

방장이 접속 중인 다른 참가자에게 방장을 직접 넘길 수 있습니다.
기존 자동 위임과 함께 동작하며, 수동 위임 때문에 재접속하거나 전용 서버를 다시 실행하지 않습니다.

## 확인 순서
1. ASP.NET과 Unity Play를 종료한 뒤 ASP.NET을 빌드·실행합니다.
2. Unity ApiLobby 씬과 `Builds/Client/Smart Training Client.exe`에서 서로 다른 닉네임으로 같은 방에 참가합니다.
3. 방장 화면에만 보이는 `참가자이름에게 방장 넘기기` 버튼을 누릅니다.
4. 약 1초 후 두 화면에서 방장 표시가 선택한 참가자로 바뀝니다.
5. 이전 방장에게는 위임 버튼이 사라지고 새 방장에게 버튼이 나타납니다. 새 방장은 이전 방장에게 다시 넘길 수도 있습니다.
6. 두 참가자가 이동하거나 새 방장이 퇴장해 봅니다. 이동 동기화와 남은 참가자에게 자동 위임이 계속 동작해야 합니다.

버튼 클릭으로 즉시 위임하며, 서버 응답과 표시 갱신을 기다리는 동안 중복 버튼 입력을 막습니다.
위임 대상이 퇴장했거나 방장이 먼저 바뀌었다면 서버의 오류 메시지를 표시합니다.

## 권한 규칙
- 로그인한 현재 방장만 위임할 수 있습니다. 요청자의 ID는 인증 토큰에서 가져옵니다.
- 대상은 같은 방에 실제 접속이 확정된 참가자여야 합니다. 예약자·다른 방 사용자·없는 사용자에게는 위임하지 않습니다.
- 위임 후 이전 방장은 즉시 위임 권한을 잃습니다. 클라이언트 표시 갱신을 기다리는 중에도 서버는 최신 방장을 기준으로 검사합니다.
- 같은 방의 접속 인원과 각 플레이어 소유자는 유지됩니다.

## 함수별 변경
- ASP.NET `RoomController.TransferRoomMaster`: `POST /api/Room/transfer-master`, 로그인 사용자 식별과 결과 반환.
- ASP.NET `RoomService.TransferRoomMaster`: 멤버십 잠금 안에서 방장 권한·대상 접속 상태 검사 및 방장 변경.
- Unity `TrainingApiClient.TransferRoomMaster`: 인증된 위임 요청 전달.
- Unity `ApiLobbyScreen.DrawRoom`, `TransferRoomMaster`: 방장만 보는 대상별 버튼과 상태 표시 갱신 대기.
- 기존 `RoomDedicatedServer.SynchronizeRoomMaster`: 변경된 방장을 모든 참가자에게 동기화.

요청 예: `{ "lobbyId": "로비 ID", "roomId": "방 ID", "targetUserId": "대상 사용자 ID" }`
네트워크 접속과 방장 기능에 집중하며 훈련 콘텐츠는 추가하지 않았습니다.
