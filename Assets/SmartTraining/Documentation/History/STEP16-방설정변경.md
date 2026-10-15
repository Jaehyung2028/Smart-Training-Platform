# STEP16 — 방장 전용 방 설정

## 변경한 함수
- `RoomService.UpdateRoomSettings`: 현재 실제 참가 중인 방장만 방 이름·정원 변경. 이름 길이·중복 정책 및 정원 범위 확인. 접속+유효 예약 인원 아래로 축소 금지. 실패 시 이름·정원 모두 유지.
- `RoomController.UpdateRoomSettings`: `POST /api/Room/settings`. 인증된 사용자로 권한 확인.
- `RoomService.GetRoomMaster`: 등록된 전용 서버에 최신 방 이름·정원도 전달.
- `RoomDedicatedServer.SynchronizeRoomMaster`, `RoomPlayer.SetRoomSettingsServer`: 기존 주기 조회와 Netcode 상태로 이름·정원 동기화.
- `TrainingApiClient.UpdateRoomSettings`, `ApiLobbyScreen.UpdateRoomSettings`: 편집 화면 및 API 요청, 실제 동기화 결과 확인.

## 확인 방법
1. ASP.NET을 재시작하고 Unity ApiLobby 씬을 Play합니다.
2. 두 참가자가 같은 방에 접속합니다. 방장에게만 **방 이름·최대 인원 편집** 버튼이 표시됩니다.
3. 이름을 바꾸고 정원을 2로 적용합니다. 양쪽 화면의 방 이름과 접속/최대 인원 표시가 바뀌는지 확인합니다.
4. 두 명 접속 중 정원을 1로 줄이면 거절되고 이름·정원은 유지됩니다.
5. 정원을 늘리면 새 참가자를 받을 수 있습니다. 미완료 예약도 자리로 계산하므로 예약자 자리를 없애는 축소는 거절됩니다.
6. 준비 상태·위치·현재 연결·방장 역할은 설정 변경으로 초기화되지 않습니다.
7. 방장 위임 후 새 방장이 편집할 수 있고 이전 방장에게는 버튼이 사라집니다.
8. 로비로 돌아가 방 목록을 새로고침하면 최신 이름과 정원이 표시됩니다.

기본 서버 설정은 이름 2~30자, 정원 1~8명입니다. 준비 상태와 달리 방 설정은 ASP.NET에서 관리하고 전용 서버는 화면 표시용 값을 동기화합니다. 현재 화면의 입력 길이는 기본 설정을 기준으로 합니다. GitHub 업로드는 별도 요청 시 진행합니다.
