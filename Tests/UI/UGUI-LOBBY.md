> 업데이트: 로비 내부도 uGUI로 전환했습니다. 현재 범위와 편집 방법은 [로비 내부 전환 안내](UGUI-LOBBY-INTERIOR.md)를 참고해주세요. 아래 내용은 로그인·목록 전환 단계의 기록입니다.

# uGUI 전환 1단계 — 로그인·콘텐츠 로비 목록

## 확인 방법
Unity가 스크립트를 다시 읽은 뒤 기존 `Assets/SmartTraining/Scenes/ApiLobby.unity`를 실행합니다. 로그인·로비 목록은 Canvas로 표시합니다. 로비에 입장하면 기존 방 관리·채팅 화면으로 연결하고, 로비에서 퇴장하면 uGUI 목록으로 돌아옵니다.

이번 전환 범위는 서버 주소·닉네임 입력, 주소 저장·초기화, 서버 연결 확인, 로그인·로그아웃, 콘텐츠 로비 목록과 선택, 점검·예약 종료 표시, 새 입장 제한, 입장 진행·취소와 실패 재시도 안내입니다. 로비 내부의 방 생성·검색·참가자·채팅 및 방 내부 UI는 다음 전환 대상이며 현재는 기존 OnGUI 화면을 사용합니다.

## 에디터에서 수정하기
메뉴 **Smart Training → UI → Open Login and Lobby Prefab**을 선택하거나 다음 프리팹을 엽니다.

`Assets/SmartTraining/Resources/SmartTraining/LobbyUI.prefab`

- `Background / Main Panel`: 배경·주 패널 색, 여백, 크기
- `Login Panel`: 서버 주소·닉네임 입력창과 로그인 버튼
- `Lobby Browser Panel / Lobby Scroll / Viewport / Content / Lobby Row Template`: 동적 로비 행의 기본 디자인. 템플릿은 비활성 상태로 유지합니다.
- `Lobby Join Progress`: 입장 진행·취소
- `Lobby Entry Notice`: 실패 안내·재시도·닫기
- 루트의 `LobbyUguiView`: 연결된 버튼·텍스트·입력창 및 선택적 `Font Override`

색·폰트 크기·레이아웃은 이 프리팹에서 편집합니다. 실행 중 Hierarchy의 복제본에만 적용한 변경은 종료 시 사라지므로 프리팹을 직접 편집해주세요. 버튼 기능은 런타임에 코드로 연결하므로 Inspector의 On Click에 같은 동작을 중복 추가하지 않습니다.

프리팹을 지웠다면 **Create Login and Lobby UI (if missing)** 메뉴로 다시 만들 수 있습니다. 빌드에서도 누락 시 생성하며 기존 프리팹의 디자인을 덮어쓰지 않습니다. 기존 씬·URP 설정과 패키지 설정은 변경하지 않았습니다. 원본 프로젝트에 이미 설치된 `com.unity.ugui` 2.0.0과 Input System을 사용합니다.

Canvas는 `Scale With Screen Size`(1280×800 기준)로 크기를 조정합니다. 1280×800과 960×720에서 실제 텍스트·버튼 배치를 확인했습니다. 한글은 Windows에 설치된 맑은 고딕을 사용하며 폰트 파일을 프로젝트에 복사하지 않습니다. 필요하면 `Font Override`에 프로젝트의 Font 에셋을 연결할 수 있습니다.

## 주요 코드
- `ApiLobbyScreen.Ugui.cs`: `EnsureUgui()` 프리팹 로드·바인딩, `UseUgui` 전환 범위, `UiLogin()`·`UiSelect()`·`UiEnter()` 기존 동작 연결
- `LobbyUguiView.cs`: `Bind()` 버튼·입력 이벤트, `Refresh()` 상태 표시, `UpdateRows()` 동적 목록, `ApplyFont()` 한글 폰트
- `ApiLobbyScreen.cs`: `OnEnable()`에서 uGUI 준비, `OnGUI()`에서 현재 화면에 맞게 표시 전환
- `Editor/LobbyUguiBuilder.cs`: `EnsureAssets()` 편집 가능한 프리팹 생성, `OpenPrefab()` 에디터 메뉴
- `Editor/RoomClientBuild.cs`: 빌드 시 프리팹 존재 확인

EventSystem이 없을 때 InputSystemUIInputModule을 준비하고 기존 EventSystem이 있으면 재사용합니다. 로비 내부 화면으로 전환되면 CanvasGroup과 GraphicRaycaster도 비활성화해 숨겨진 버튼이 마우스·키보드 입력을 받지 않게 합니다. 처리 중에는 버튼을 제한하고 입장 취소만 허용합니다. 자동 갱신은 입력창의 포커스를 매번 초기화하지 않습니다.

## 검증
실제 서버와 uGUI 버튼 콜백·입력창을 사용한 연동 검사 13개를 통과했습니다. 화면 요소의 중앙에서 GraphicRaycaster가 해당 컨트롤을 찾는지도 확인했습니다. 로그인·동적 행 선택·점검 자동 반영·입장 진행·취소·기존 화면 전환·작은 해상도·로그아웃·재로그인을 포함합니다. 새 UI의 런타임 예외는 없었습니다.

기존 종료 예약·방 접속·다른 로비 유지·서버 재시작 흐름 21개도 통과했습니다. Unity 클라이언트와 전용 방 서버 빌드를 완료했습니다. 서버 API 소스는 변경하지 않았습니다.

`Tests/UI/LOGIN-1280.png`, `Tests/UI/LOBBIES-960.png`는 Unity Canvas를 렌더링한 검증 이미지입니다. `MultiClientProbe`의 uGUI 명령·화면 검사는 개발 빌드와 에디터에서만 포함되며 일반 실행에서 자동 시작하지 않습니다.

Unity 프로젝트 폴더에서 재검증합니다. ASP.NET 빌드와 Unity 개발 실행 파일이 먼저 준비되어 있어야 합니다. 포트 51456의 독립 테스트 서버·이력 파일을 사용하고 자신이 시작한 프로세스만 정리합니다.

```powershell
& '.\Tests\MultiClient\Run-UguiLobby.ps1' -ServerProject 'C:\Users\yafa7\Desktop\Projects\Smart Training Server\Smart Training Server\Smart Training Server' -ClientExecutable '.\Builds\Client\Smart Training Client.exe' -EvidenceRoot "$env:TEMP\SmartTrainingUguiChecks"
```
