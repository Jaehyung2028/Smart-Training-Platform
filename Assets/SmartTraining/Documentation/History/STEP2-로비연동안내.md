# Unity STEP 2 — 로비 생성·입장·퇴장과 콘텐츠 조회

## 확인 순서
1. ASP.NET 서버를 기존 http 프로필로 실행합니다.
2. Unity에서 Assets/SmartTraining/Scenes/ApiLobby.unity를 열고 Play를 누릅니다.
3. 로그인 후 새 로비 이름(현재 서버 기본값 2~10자)을 입력하고 로비 생성을 누릅니다.
4. 새 로비가 목록에 나타나며 선택되고 콘텐츠와 하위 단계가 표시됩니다.
5. 선택한 로비에 입장을 누르면 현재 로비 이름과 사용자 1명을 확인할 수 있습니다.
6. 현재 로비에서 퇴장을 누르면 현재 로비가 없음으로 바뀝니다.
   사용자와 방이 모두 없는 로비는 서버에서 삭제되어 목록에서도 사라집니다.
7. 같은 이름의 로비를 다시 생성하면 서버 중복 오류가 표시되고 로그인은 유지됩니다.
   이 오류는 기존 로비가 남아 있을 때 확인합니다.

## 변경 파일과 함수
- Scripts/Client/Api/ApiModels.cs: CreateLobbyRequest, LobbyMembershipRequest, CreateLobbyResponse,
  ContentSummary, ContentList 추가. 요청에는 사용자 ID를 넣지 않습니다.
- Scripts/Client/Api/TrainingApiClient.cs: CreateLobby(), JoinLobby(), LeaveLobby(), GetContents(), ChangeLobby() 추가.
  기존 인증 토큰 갱신과 서버 JSON 오류 처리를 그대로 사용합니다.
- Scripts/Client/Controllers/ApiLobbyScreen.cs: CreateLobby(), SelectLobby(), LoadContents(), JoinLobby(), LeaveLobby(),
  DrawContent(), ClearView() 추가. LoadState()와 OnGUI()에 로비 선택·입장·퇴장 표시를 연결합니다.
- Editor/ApiSmokeTest.cs: 실제 로비 상태 변경과 콘텐츠 트리 검증 추가.

## 화면 동작
로비 생성과 로비 입장은 별도 동작입니다. 생성 후에는 선택만 됩니다.
다른 로비로 이동하려면 현재 로비에서 먼저 퇴장합니다.
입장 후와 퇴장 후에는 서버의 내 정보·목록·콘텐츠를 다시 조회합니다.
요청 중 버튼은 비활성화하여 중복 요청을 방지합니다.
전체 화면을 스크롤할 수 있어 콘텐츠가 많아도 아래 항목을 확인할 수 있습니다.
로그아웃·인증 실패·화면 종료 시 선택한 로비와 콘텐츠도 비웁니다.
콘텐츠는 rootContent, id, parentId, depth, title, children으로 읽고 제목 트리를 표시합니다.
방 생성에 사용할 rootContent 선택 버튼은 다음 단계입니다.
로비 소속 여부는 currentLobbyId로 판별합니다. 기존 lobbyIndex는 입장 여부 판별에 사용하지 않습니다.

## API
POST /api/Lobby/create — {"lobbyName":"훈련로비"}
POST /api/Lobby/join — {"lobbyId":"..."}
POST /api/Lobby/leave — {"lobbyId":"..."}
GET /api/Lobby/{lobbyId}/contents

## 검증과 범위
Unity 설치 라이브러리 기준 런타임 빌드 성공: 오류/경고 0개.
같은 씬·코드의 별도 Unity 검증 프로젝트와 실제 임시 ASP.NET 서버에서 19개 검증 통과.
로비 생성, 생성 목록, 중복 이름 오류, 콘텐츠 트리와 루트 ID,
입장 후 내 정보·사용자 수, 중복 입장 거부, 퇴장 후 상태·빈 로비 정리,
삭제된 로비의 콘텐츠 오류, 기존 인증·갱신·로그아웃을 확인했습니다.
원본 Unity 프로젝트의 Play 화면과 버튼 배치는 사용자가 위 순서로 확인해야 합니다.
이번 단계는 서버 코드·패키지·씬 파일을 변경하지 않습니다.
방 생성/참가와 Netcode 실제 접속은 아직 추가하지 않았습니다.