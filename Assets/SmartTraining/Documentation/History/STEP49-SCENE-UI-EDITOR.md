# 씬 UI 배치와 에디터 미리보기

## 시작 방법

1. 플레이를 종료하고 `Assets/SmartTraining/Scenes/ApiLobby.unity`를 엽니다.
2. **Smart Training → UI → Scene UI Editor**를 선택합니다.
3. 편집 창에서 로그인·콘텐츠 로비 목록·로비 내부·방 내부 또는 각 창을 선택합니다.
4. **Game** 탭에서 결과를 확인하고, **Hierarchy → ApiLobbyScreen → UI Workspace** 아래의 오브젝트를 선택해 Inspector에서 위치·크기·색상을 수정합니다.
5. 씬을 저장하고 플레이에서 실제 접속·버튼 동작을 확인합니다.

이미 씬에 세 프리팹과 EventSystem을 배치했습니다. 현재 열어둔 씬에 Workspace가 아직 보이지 않는다면 저장한 씬을 다시 열거나 편집 창의 **현재 씬에 UI 배치·연결**을 누릅니다. 배치 도구는 기존 화면을 재사용하므로 같은 UI를 다시 만들지 않습니다. 도구는 씬을 자동 저장하지 않습니다.

## 편집 범위

- `LobbyUI`: 로그인·콘텐츠 목록·로비 입장 진행
- `LobbyInteriorUI`: 방 목록·참가자·로비 채팅·방 생성·방 입장 진행
- `RoomUI`: 참가자·준비·방장 권한·채팅·방 설정·관리 확인·위치 창
- `UI Event System`: 실제 플레이의 uGUI 입력 처리
- Workspace의 `UguiSceneLayout`: 컨트롤러와 세 화면, EventSystem 참조

세 화면은 원래 프리팹과 연결된 인스턴스입니다. 씬에서 바꾼 배치는 씬의 Override로 남습니다. 공통 원본 디자인을 바꾸려면 기존 Open Login and Lobby Prefab / Open Lobby Interior Prefab / Open Room Prefab 메뉴에서 프리팹을 편집합니다. 자동 배치는 프리팹 원본을 재생성하지 않습니다.

오브젝트 루트는 활성화한 상태로 유지합니다. 실제 실행에서는 Canvas와 CanvasGroup으로 표시·입력을 전환합니다. UI 버튼의 기능은 플레이 중 `Bind()`가 연결하므로 Inspector On Click에 같은 기능을 중복 등록할 필요는 없습니다.

## 미리보기와 실제 실행

미리보기는 서버 접속이나 실제 참가자 생성 없이 기본 배치와 창을 확인합니다. 채팅·참가자 목록의 실시간 데이터와 버튼 동작은 플레이에서 확인합니다.

한 번에 하나의 화면만 표시하며 버튼의 레이캐스트는 미리보기 동안 차단합니다. 한글 확인을 위해 임시 폰트를 사용하지만 원래 폰트 연결은 복구합니다. 미리보기 중 수정한 위치·색상·글자 크기 같은 디자인은 유지합니다.

씬 저장·플레이 진입·빌드·스크립트 재로드·Undo/Redo·씬 닫기·편집 창 닫기 전에 미리보기를 복구합니다. 임시 확인 문구와 폰트가 씬·빌드에 저장되지 않도록 처리했습니다.

실제 실행은 `ApiLobbyScreen.EnsureUgui()`가 씬의 연결을 우선 사용합니다. 씬에 배치한 화면이 없으면 기존 Resources 로딩을 사용합니다.

## 연결 검사

편집 창의 **버튼·입력창 연결 검사** 또는 Workspace Inspector에서 결과를 확인합니다.

- 필수 버튼·입력창·텍스트·목록 참조 누락
- 다른 화면 오브젝트를 잘못 연결한 경우
- 이동 버튼 배열의 비어 있는 항목
- Canvas·CanvasGroup·GraphicRaycaster와 Missing Script
- 중복 화면·EventSystem·컨트롤러와 비활성 화면 루트

선택적인 `Font Override`는 비워둘 수 있습니다. 오류에는 `RoomUI.ready`, `movementButtons[0]`처럼 실제 필드명을 표시합니다. Workspace가 있는 씬을 빌드할 때도 필수 연결 오류를 검사합니다.

## 파일과 함수

- `Scripts/UI/Common/UguiSceneLayout.cs`: 씬에 배치한 화면과 입력 시스템 참조
- `Scripts/Client/Controllers/ApiLobbyScreen.Ugui.cs`: `EnsureUgui()` — 씬 참조 우선 연결, 기존 로딩 유지
- `Editor/UguiSceneDesigner.cs`: `Install()`, `Validate()`, `OpenWindow()` — 배치·연결 검사·메뉴
- `Editor/UguiScenePreview.cs`: `Show()`, `Restore()` — 임시 표시와 복구, 빌드 전 검사
- `Editor/UguiSceneWindow.cs`: 편집 창과 Workspace Inspector
- `Editor/UguiSceneChecks.cs`: 격리된 배치 모드에서 미리보기·저장·디자인 보존·오류 검출 검사
- `Editor/RoomClientBuild.cs`: `Build()` 시작 시 미리보기 복구
- `Scripts/Diagnostics/MultiClientProbe.cs`: 개발 검사에서 씬 UI 재사용과 중복 여부를 확인

기존 카메라·조명·URP 설정과 씬의 다른 오브젝트 데이터는 유지했습니다. 기존 프리팹 원본, 패키지, ASP.NET 서버 소스는 수정하지 않았습니다.

## 검증 결과

에디터 검사에서는 열 가지 화면·창 전환, 저장 시 복구, 디자인 수정과 프리팹 연결 유지, 잘못된 연결 검출을 확인합니다. 실제 클라이언트 검사에서는 로그인부터 방장 위임·강퇴·재접속·퇴장까지 기존 동작과 세 화면·하나의 EventSystem 재사용을 확인합니다.

결과는 프로젝트 루트 `Tests/UI/SCENE-UI-*.txt`에 기록합니다. 재실행 도구는 `Tests/MultiClient/Run-SceneUi.ps1`이며 기존 도구처럼 `-ServerProject`, `-ClientExecutable`, `-EvidenceRoot`에 실제 경로를 지정합니다. 검사 API 포트는 51462입니다.
