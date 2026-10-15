# Unity STEP 3 — 콘텐츠 선택과 방 생성·조회

## 확인 순서
1. 기존 ASP.NET 서버(http 프로필)를 실행합니다.
2. Unity의 Assets/SmartTraining/Scenes/ApiLobby.unity 씬에서 Play를 누릅니다.
3. 로그인 → 로비 생성 또는 선택 → 선택한 로비에 입장을 누릅니다.
4. 콘텐츠 제목 버튼을 눌러 사용할 루트 콘텐츠를 선택합니다.
   하위 단계는 제목으로 표시하며 방 콘텐츠 ID로 선택하지 않습니다.
5. 방 이름과 최대 인원을 입력하고 선택한 콘텐츠로 방 생성을 누릅니다.
   서버 기본 설정은 이름 2~30자, 최대 인원 1~8명입니다.
6. 방 목록에 생성한 방이 나타나고 선택된 방의 콘텐츠와 서버 상태가 표시됩니다.
7. 방 목록 새로고침으로 접속/예약/남은 자리 수를 조회합니다.
   방을 누르면 서버에서 상세 정보를 다시 조회합니다.
8. 로그아웃하면 생성자 소유의 빈 방은 기존 서버 정리 기능으로 제거됩니다.

## 정상 표시
방 생성은 로비에 방 정보를 등록하는 단계입니다. 실제 접속 예약이나 Unity 서버 실행은 아닙니다.
접속 0/최대인원, 예약 0, 남은 자리=최대인원, 서버 실행 대기가 정상입니다.
방이 남아 있으면 마지막 사용자가 로비에서 퇴장해도 로비가 유지됩니다.
생성한 빈 방을 정리하려면 이번 단계에서는 로그아웃을 사용합니다.
실제 방 참가·예약 취소·Netcode 연결과 별도 방 삭제 UI는 이번 단계에 추가하지 않았습니다.

## 변경 파일과 함수
Scripts/Client/Api/ApiModels.cs:
- CreateRoomRequest: lobbyId, roomName, contentId, maxUsers. 사용자 ID를 보내지 않습니다.
- CreateRoomResponse: 성공 여부와 생성한 방 ID를 읽습니다.
- RoomSummary, RoomList: 방 목록/상세 DTO와 배열 처리.
Scripts/Client/Api/TrainingApiClient.cs:
- CreateRoom(): POST /api/Room/create. contentId는 선택한 루트의 rootContent입니다.
- GetRooms(): GET /api/Lobby/{lobbyId}/rooms.
- GetRoom(): GET /api/Lobby/{lobbyId}/rooms/{roomId}.
- 기존 토큰 자동 갱신, Bearer 인증, 15초 제한, 오류 메시지 처리를 재사용합니다.
Scripts/Client/Controllers/ApiLobbyScreen.cs:
- CreateRoom(): 인원 숫자 검사 후 생성하고 서버 상태를 다시 조회합니다.
- LoadRooms(), SelectRoom(): 방 목록과 상세 조회.
- LoadLobbyData(): 선택한 로비의 콘텐츠와 방 목록을 순서대로 조회.
- ClearLobbyData(): 로비 변경·로그아웃·인증 실패 시 다른 로비의 선택/방/콘텐츠를 비웁니다.
- RoomStateLabel(): 서버 상태 번호를 화면 문구로 표시합니다.
- OnGUI(): 콘텐츠 선택 버튼과 방 생성 입력·목록·상세 표시를 추가했습니다.
Editor/ApiSmokeTest.cs:
- 방 생성·조회와 오류 응답, 로그아웃 후 생성한 빈 방 정리 검증 추가.

## 검증
Unity 설치 라이브러리로 런타임 빌드 성공(오류/경고 0개).
같은 코드·씬의 별도 Unity 검증 프로젝트와 실제 임시 ASP.NET 서버에서 전체 31개 검증 통과.
로비 미가입 생성 거부, 미등록 콘텐츠 거부, 인원 제한, 생성 ID,
목록과 상세 JSON, 콘텐츠/방장/상태/인원 필드, 생성만으로는 접속/예약되지 않음,
로비 방 개수, 다른 로비로 상세 조회 거부, 방이 남은 로비 유지,
로그아웃 후 소유한 빈 방·로비 정리를 확인했습니다.
이전 로그인/갱신/로비/콘텐츠 검증도 함께 통과했습니다.
원본 Play 화면과 버튼 배치는 위 순서로 확인해주세요.
서버 코드, 씬 파일, 패키지 설정은 변경하지 않았습니다.