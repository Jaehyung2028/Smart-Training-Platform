# uGUI 전환 후 코드 정리

## 제거한 코드

- `ApiLobbyScreen.OnGUI()`와 각 partial 파일의 `Draw...()` 메서드: 로그인·로비 목록·로비 내부·방·입장 진행·채팅·확인창은 모두 기존 uGUI 화면에서 표시합니다. 기존 GUI로 돌아가는 표시 경로도 제거했습니다.
- 전용 스크롤 위치, 편집 여부, 키보드 포커스 플래그, GUI 이동 버튼의 마지막 입력 시간 등 GUI 전용 상태와 대입문.
- 호출하지 않던 `ApiLobbyScreen.CreateLobby()`와 로비 이름 입력 변수: 실제 로비는 현재 서버 catalog.json에서 조회합니다. 다른 검사 코드에서 사용하는 `TrainingApiClient.CreateLobby()` 호환 메서드는 유지했습니다.
- `RoomMovementView.cs`: 예전 GUI 전용 위치·이동 표시. 현재 `RoomUguiView.UpdateMap()`과 `RoomMoveButton`을 사용합니다.
- `ApiLobbyScreen.Shutdown.cs`: 표시 메서드를 제거한 뒤 남은 입장 제한 판정 하나는 `ApiLobbyScreen.Ugui.cs`에 합쳤습니다. 종료 예정·점검 기능은 유지합니다.

## 합친 코드

`Scripts/UI/Common/UguiViewUtility.cs`에서 세 화면이 공통 처리를 사용합니다.

- `EnsureComponents()`: Canvas·CanvasGroup·GraphicRaycaster 참조 확인과 CanvasGroup 복구. Unity의 null 비교를 사용합니다.
- `SetVisible()`, `ClearSelection()`: Canvas 표시와 입력·레이캐스트 차단, 숨긴 화면의 선택 해제.
- `EnsureEventSystem()`: 기존 EventSystem 재사용 또는 최초 생성.
- `ApplyFont()`: 한글 폰트 선택과 적용. 화면이 생성한 런타임 폰트만 반환해 화면 종료 시 정리하고, 지정한 폰트 에셋은 파괴하지 않습니다.
- `ChatLabel()`, `RemoveMissing()`: 채팅 표시 형식과 사라진 목록 행 정리.

`LobbyUguiView`, `LobbyInteriorUguiView`, `RoomUguiView`는 현재 프리팹의 직렬화된 참조를 유지하며 이 공통 함수를 호출합니다. 프리팹 디자인이나 Inspector 연결을 다시 만들 필요가 없습니다.

`ApiLobbyScreen.Ugui.cs`의 `EnsureUgui()`는 `EnsureView<T>()`로 화면 탐색·Resources 로드·인스턴스 생성을 통일합니다. 누락된 프리팹은 Console에 리소스 경로를 표시합니다.

`ApiLobbyScreen.Update()`는 uGUI 이동 버튼과 키보드 입력만 읽습니다. `RoomMovementInput.Read()`는 uGUI 입력창 편집 중 키보드 이동을 차단합니다.

## 유지한 동작과 검증

로그인·인증 세션 종료·알림 재연결·방 목록 자동 갱신·입장 예약·취소·재접속·방장 권한·준비·진행 상태·채팅·퇴장 동작은 기존 서비스와 RPC를 사용합니다. ASP.NET 서버 소스와 catalog.json, Unity 패키지·씬·프리팹 배치는 변경하지 않았습니다.

삭제된 GUI 전용 필드를 확인하던 `Editor/ParticipantRoomChecks.cs`, `Editor/ServerConnectionChecks.cs`의 검사는 현재 방 선택·입력 초기화 상태를 확인하도록 조정했습니다.

클라이언트 빌드, 에디터의 CanvasGroup 초기화·복구·숨김 입력 차단 검사와 독립 클라이언트 두 개를 이용한 기존 검사들을 실행합니다. 결과는 프로젝트 루트의 `Tests/UI/CLEANUP-*.txt`에 기록합니다.

## 직접 확인

1. Unity에서 컴파일 완료 후 기존 ApiLobby 씬을 플레이합니다.
2. 로그인 → 콘텐츠 로비 입장 → 방 생성 → 두 클라이언트 입장을 확인합니다.
3. 준비·시작·대기 복귀, 방장 설정·위임·강퇴, 채팅 전환을 확인합니다.
4. 방에서 나가기·재접속·로그아웃 후 올바른 uGUI 화면으로 이동하는지 확인합니다.

예전 GUI 표시 코드를 제거했으므로 정상 프리팹이 있는 현재 구성에서는 uGUI만 표시됩니다.
