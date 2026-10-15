# uGUI 전환 3단계 — 방 내부

## 실행과 확인

기존 `Assets/SmartTraining/Scenes/ApiLobby.unity`를 실행합니다. 로그인 → 콘텐츠 로비 입장 → 방 생성·참가 순서로 진행하면 방 내부도 uGUI로 표시됩니다. 별도 씬 설정은 필요하지 않습니다.

두 클라이언트에서 다음 순서로 확인합니다.

1. 방 이름·접속 인원·최대 인원·방장·내 역할·참가자의 준비 및 연결 상태를 확인합니다.
2. 한 명만 준비했을 때 시작할 수 없는지 확인하고, 전원 준비 후 방장이 시작합니다. 대기 복귀 시 준비 상태는 초기화됩니다.
3. 방장 설정에서 방 이름·정원을 변경합니다. 접속·예약 인원보다 작은 정원은 서버가 거절하고, 진행 중에는 이름·정원 변경을 막습니다.
4. 비밀번호 설정·해제, 입장 잠금·해제를 확인합니다. 비밀번호 입력은 마스킹되고 저장 성공이나 창 닫기·퇴장 시 지웁니다.
5. 방 채팅·로비 채팅을 전환해 확인합니다. 채팅은 최근 수신 100개이며 마크업을 원문으로 표시합니다. 채널 전환 시 작성 중인 메시지는 비웁니다.
6. 참가자 행의 방장 위임·강퇴를 누르면 확인창이 열립니다. 취소하면 적용하지 않으며, 실행 전 대상·방·방장 상태를 기존 로직으로 재검증합니다.
7. 방장 연결 끊김 시 자동 위임, 로비 재접속 버튼으로 복귀, 방에서 나가기·로그아웃·방 닫기를 확인합니다.

설정·확인·위치 창이 열리면 뒤쪽 버튼의 입력을 차단합니다. 채팅·설정 입력 중에는 WASD·방향키 이동을 차단합니다. 설정·확인창에서도 이동을 차단합니다. 위치 창은 기존 위치 확인·이동 기능을 옮긴 것으로 새 훈련 콘텐츠가 아닙니다.

## 프리팹 편집

메뉴 **Smart Training → UI → Open Room Prefab** 또는 `Assets/SmartTraining/Resources/SmartTraining/RoomUI.prefab`을 엽니다.

- `Background/Main Panel`: 방 정보, 준비·시작, 참가자 목록, 채팅, 알림, 퇴장
- `Room Settings`: 방 이름·정원·비밀번호·입장 잠금·방 닫기
- `Room Action Confirmation`: 방장 위임·강퇴·방 닫기 확인
- `Movement Map`: 참가자 위치·이동 버튼
- 루트 `RoomUguiView`: 연결된 UI 참조, 선택적 `Font Override`

`Participant Template`, `Message Template`, `Marker Template`은 비활성 템플릿으로 유지하고 참조를 연결한 상태로 디자인을 수정합니다. 버튼 기능은 `Bind()`에서 연결하므로 Inspector의 On Click에 같은 기능을 중복 등록하지 않습니다. 생성 메뉴와 빌드는 새 방 프리팹이 없을 때만 생성합니다.

한글 폰트는 기존 화면과 같은 방식으로 운영체제에서 찾습니다. 플랫폼에 맞는 폰트 에셋을 직접 연결할 수도 있습니다. 폰트 파일은 포함하지 않았습니다.

## 코드와 함수

- `Scripts/RoomUguiView.cs`: `Bind()`, `Refresh()`, `EnsureComponents()`, `UpdateParticipants()`, `UpdateChat()`, `UpdateMap()`, `ReadMovement()` — 화면·모달·동기화 상태·이동 입력
- `Scripts/ApiLobbyScreen.UguiRoom.cs`: `UseUguiRoom`, `UiToggleReady()`, `UiTogglePhase()`, `UiSaveRoomSettings()`, `UiRequestTransfer()`, `UiRequestKick()`, `UiConfirmRoomAction()`, `UiSendRoomScreenChat()`, `UiLeaveRoom()` — 기존 API·RPC·확인 절차에 연결
- `Scripts/ApiLobbyScreen.Ugui.cs`: `EnsureUgui()` — 방 프리팹을 로드하고 연결
- `Scripts/ApiLobbyScreen.cs`: `OnGUI()` — uGUI 방 표시 중 기존 화면 중복 표시 방지, `Update()` — 이동 입력 연결·차단
- `Scripts/RoomMoveButton.cs`: 포인터 누름·해제·영역 이탈·비활성화·포커스 이탈 시 이동 버튼 상태 처리
- `Scripts/RoomMovementInput.cs`: `IsEditingText`, `Read()` — uGUI 입력창 편집 중 키보드 이동 차단
- `Editor/RoomUguiBuilder.cs`: `EnsureAssets()`, `OpenPrefab()` — 프리팹 생성과 편집 메뉴
- `Editor/RoomClientBuild.cs`: `Build()` — 방 프리팹 포함 확인
- `Editor/CanvasGroupRegression.cs`: `Run()` — 세 프리팹의 반복 바인딩·파괴된 컴포넌트 참조 복구 검사
- `Scripts/MultiClientProbe.cs`: 개발 빌드에서 명시적으로 켠 검사에만 사용. UI 갱신 후 상태를 수집하고, 테스트 명령 파일 교체와 겹치는 읽기는 재시도합니다.

ASP.NET API·인증·접속 예약·방장 권한 정책은 기존 구현을 사용합니다. 서버 코드·catalog.json·패키지·씬·렌더 파이프라인은 이번 UI 작업에서 변경하지 않았습니다.

## 자동 검사

`Tests/MultiClient/Run-UguiRoom.ps1`은 독립된 API와 두 클라이언트를 실행합니다. 개발 클라이언트를 빌드한 뒤 다음 세 인자를 실제 경로로 지정해 실행합니다.

```powershell
./Tests/MultiClient/Run-UguiRoom.ps1 `
  -ServerProject 'C:/Users/yafa7/Desktop/Projects/Smart Training Server/Smart Training Server/Smart Training Server' `
  -ClientExecutable 'C:/Users/yafa7/Desktop/Projects/Smart Training Platform/Builds/Client/Smart Training Client.exe' `
  -EvidenceRoot 'C:/Users/yafa7/Desktop/Projects/Smart Training Platform/TestResults/RoomUI'
```

API 포트는 51460입니다. 검사에서 시작한 프로세스만 정리하며 실제 개발 서버는 종료하지 않습니다. 운영 이력은 별도 결과 폴더에 저장합니다. 결과 텍스트와 Canvas 렌더 이미지는 `Tests/UI/ROOM-*`에 정리했습니다. 실제 Unity Editor에서의 플레이 확인은 별도로 진행해주세요.
