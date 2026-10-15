# uGUI 전환 2단계 — 로비 내부

## 확인 방법
기존 `ApiLobby.unity` 씬을 실행하고 로그인 → 콘텐츠 로비 선택 → 입장합니다. 로비 내부도 Canvas로 표시합니다. 방에 참가하면 기존 방 내부 화면으로 이동하고, 방에서 나오면 uGUI 로비 내부로 돌아옵니다. 로비 퇴장은 콘텐츠 목록으로, 로그아웃은 로그인 화면으로 이동합니다.

현재 로그인·콘텐츠 로비 목록·로비 내부·방 입장 진행은 uGUI입니다. 방 내부의 준비·방장 권한·방 채팅과 훈련 화면은 기존 OnGUI를 유지하며 다음 전환 대상입니다.

## 제공 기능
- 방 목록·선택·상세와 비밀번호 입장. 서버 생성·삭제·진행 상태를 실시간 반영합니다.
- 이름 검색, 입장 가능 여부, 공개·비밀번호 필터, 최근 생성·빈자리 순 정렬. 필터 버튼을 누르면 다음 값으로 전환하며 초기화로 모두 해제합니다.
- 새 방 만들기 창: 이름·최대 인원·선택적 비밀번호·콘텐츠. 콘텐츠 버튼은 등록된 루트 콘텐츠를 순환하며 서버 검증과 기존 생성 API를 사용합니다. 성공하면 창을 닫고 검색 조건을 초기화해 새 방을 선택합니다. 실패하면 입력을 유지하고 사유를 표시합니다.
- 참가자의 로비 대기·방 예약·방 접속·방장·진행 상태. 방 접속 중인 참가자 행을 누르면 해당 방을 찾아 상세를 보여줍니다. 바로 입장하지 않으며 비밀번호와 입장 제한을 확인할 수 있습니다.
- 로비 채팅: 최근 수신 100개, 200자 입력, 자동 줄바꿈·스크롤. 마크업은 서식으로 해석하지 않고 원문으로 표시합니다. 자동 갱신은 작성 중인 초안을 지우지 않습니다.
- 방 입장 진행·취소, 남은 접속 정리, 연결 끊김 후 비밀번호 재입력·재접속.
- 점검 사유·종료 예정 시각·남은 초 표시, 알림 재연결·전체 목록 새로고침.

입장만 차단한 점검 중에도 기존 로비 참가자의 방 이용은 유지합니다. 예약 종료 시각에 소속이 정리되면 기존 로직에 따라 콘텐츠 목록으로 복귀합니다. 숨겨진 Canvas의 입력은 차단하며 생성·진행 창이 열려 있는 동안 뒤쪽 버튼도 차단합니다.

## 화면 편집
**Smart Training → UI → Open Lobby Interior Prefab** 메뉴 또는 아래 에셋을 엽니다.

`Assets/SmartTraining/Resources/SmartTraining/LobbyInteriorUI.prefab`

- `Background / Main Panel / Header`: 로비 이름·사용자·연결 및 운영 안내
- `Lobby Body / Room Browser`: 검색·필터·방 목록·선택 상세·입장·재접속
- `Lobby Body / Participants and Chat`: 참가자 목록·채팅
- `Create Room Modal`: 방 생성 창
- `Room Join Progress`: 방 접속 진행·취소
- 루트 `LobbyInteriorUguiView`: 연결된 버튼·입력창·목록 템플릿·선택적 Font Override

색·글자 크기·배치와 행 템플릿을 프리팹에서 수정할 수 있습니다. 템플릿은 비활성 상태로 두고, 참조 연결은 유지해주세요. 버튼 기능은 코드로 연결하므로 Inspector On Click에 같은 동작을 중복 등록하지 않습니다. 생성 메뉴와 빌드는 에셋이 없을 때만 만들며 기존 디자인을 덮어쓰지 않습니다. 이전 로그인 프리팹·씬·URP·패키지 설정은 변경하지 않았습니다.

1280×800, 960×720에서 Unity Canvas 렌더링과 컨트롤 중앙의 GraphicRaycaster 결과를 확인했습니다. Windows의 맑은 고딕을 사용하며 폰트 파일을 복사하지 않습니다. 다른 Font 에셋을 쓰려면 루트 Font Override에 연결합니다.

## 수정 파일과 함수
- `ApiLobbyScreen.UguiInterior.cs`: `UseUguiInterior`, `UiSubmitCreate()`, `UiJoinRoom()`, `UiViewParticipant()`, `UiSendChat()` — 기존 서비스 동작과 새 화면 연결
- `LobbyInteriorUguiView.cs`: `Bind()`, `Refresh()`, `OpenCreate()`, `UpdateRooms()`, `UpdateParticipants()`, `UpdateChat()` — UI 이벤트·상태·목록·창 관리
- `Editor/LobbyInteriorUguiBuilder.cs`: `EnsureAssets()`, `OpenPrefab()` — 별도 프리팹 생성·편집 메뉴
- `ApiLobbyScreen.Ugui.cs`: `EnsureUgui()`에서 로비 내부 프리팹도 준비
- `ApiLobbyScreen.cs`: `OnGUI()` 표시 분기, `LoadState()` 현재 로비 기준 조회, 생성 성공 식별자 전달
- `ApiLobbyScreen.RoomConnection.cs`: 개발용 입장 취소 검증 지점의 범위와 초기화. 일반 실행은 테스트 중지 플래그를 켜지 않습니다.
- `Editor/LobbyUguiBuilder.cs`, `RoomClientBuild.cs`: 기존 생성 도구 재사용 및 빌드 시 새 프리팹 확인

서버 API 소스는 변경하지 않았습니다. 방 내부 권한·인증·예약·연결 정책은 기존 코드에 연결합니다.

## 검증
실제 ASP.NET과 Unity 클라이언트 2개로 로비 내부 uGUI 검사 20개 통과. 방 생성 실패·성공, 공개·비밀번호 방, 자동 갱신, 검색·필터·정렬, 참가자 방 찾기, 채팅 원문, 점검·예약 종료 안내와 초안 유지, 잘못된 비밀번호, 접속 취소, Netcode 2인 접속·재접속, 방 삭제·퇴장·로그아웃을 포함합니다. UI 런타임 예외 없이 완료했습니다.

기존 로그인 uGUI 검사 13개와 멀티·종료 예약 검사 21개도 통과했습니다. 클라이언트·전용 방 서버 빌드를 완료했습니다. 개발용 `MultiClientProbe` 검사와 캡처는 명시적으로 실행한 개발 빌드에서만 시작합니다.

검증 이미지는 `Tests/UI/INTERIOR-1280.png`, `INTERIOR-960.png`, `CREATE-ROOM-MODAL.png`에 보관했습니다.

Unity 프로젝트 폴더에서 다음과 같이 다시 검사할 수 있습니다. 별도 포트 51458과 독립 운영 이력 파일을 사용하며 자신이 시작한 프로세스만 종료합니다. 먼저 서버와 Unity 개발 실행 파일을 빌드해주세요.

```powershell
& '.\Tests\MultiClient\Run-UguiInterior.ps1' -ServerProject 'C:\Users\yafa7\Desktop\Projects\Smart Training Server\Smart Training Server\Smart Training Server' -ClientExecutable '.\Builds\Client\Smart Training Client.exe' -EvidenceRoot "$env:TEMP\SmartTrainingInteriorChecks"
```
