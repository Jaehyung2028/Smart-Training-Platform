# Smart Training 폴더 안내

`Assets/SmartTraining`은 다음과 같이 구분했습니다. 기존 클래스 이름·네임스페이스와 파일의 Unity GUID는 유지했습니다.

```text
SmartTraining/
├─ Scripts/
│  ├─ Client/
│  │  ├─ Controllers/   ApiLobbyScreen과 기능별 partial 파일
│  │  ├─ Api/           ASP.NET 요청과 응답 모델
│  │  └─ Networking/    접속·실시간 알림·연결 상태·주소 설정
│  ├─ Gameplay/
│  │  ├─ Core/          콘텐츠 로딩·씬 연결
│  │  ├─ Room/          방 플레이어
│  │  ├─ Model/         모델 관찰·회전·부품 기능
│  │  ├─ Scenario/      실습 데이터·진행·동기화
│  │  └─ Video/         영상 콘텐츠·동기화
│  ├─ Common/           공통 입력·카메라·미디어·설정
│  ├─ Servers/          Unity 전용 방 서버 실행 코드
│  ├─ Domain/           방 검색·정렬과 알림 기록
│  ├─ UI/
│  │  ├─ Views/         로그인·로비·방 화면과 카드
│  │  ├─ Content/       Common·Model·Scenario·Video별 콘텐츠 UI
│  │  └─ Common/        공통 테마·초기화·선택 표시
│  └─ Diagnostics/      개발용 검증과 에디터 세션 정리
├─ Editor/
│  ├─ Build/            클라이언트·전용 서버 빌드
│  ├─ Builders/         UI 프리팹 생성과 Content/의 콘텐츠 생성기
│  ├─ UI/
│  │  ├─ Designers/     화면별 테마 적용
│  │  └─ SceneTools/    씬 배치·미리보기 창
│  └─ Tests/
│     ├─ UI/           UI 연결·해상도 검사
│     ├─ Integration/  API·서버·종료 연동 검사
│     └─ Features/     접속·채팅·방 관리 기능 검사
├─ Content/
│  ├─ Models/          모델 프리팹과 부품·애니메이션
│  ├─ Videos/          영상 프리팹·클립·자막
│  ├─ Scenarios/       실습 프리팹과 단계 데이터
│  └─ Shared/Materials/ 공용 재질
├─ Resources/           런타임 로딩용 프리팹과 테마
├─ Scenes/              클라이언트·전용 서버 씬
└─ Documentation/
   ├─ README.md         이 안내
   ├─ History/          단계별 구현·확인 기록
   └─ Guides/           코드 정리 등 별도 안내
```

## 자주 수정하는 곳

- 화면 배치와 색상: `Resources/SmartTraining`의 UI 프리팹과 `DefaultUguiTheme`
- 화면 표시 코드: `Scripts/UI/Views`
- 버튼 동작과 API 흐름: `Scripts/Client/Controllers`
- 통신 요청과 모델: `Scripts/Client/Api`
- 테마 적용 도구: `Editor/UI/Designers`
- 자동 빌드 도구: `Editor/Build`

`ApiLobbyScreen`의 partial 파일들은 하나의 클래스를 구성하므로 `Controllers`에 모았습니다. `Editor` 폴더의 스크립트는 계속 에디터 전용으로 컴파일되며 새 어셈블리는 추가하지 않았습니다. `Diagnostics/EditorSessionCleanup.cs`는 기존 코드와 같은 어셈블리를 유지하기 위해 Scripts 아래에 두었습니다.

`Resources`와 `Scenes`는 기존 위치를 유지해 런타임 로딩과 빌드 경로를 바꾸지 않았습니다. 저장소 루트의 `Tests`는 외부 실행 검사·결과·캡처이고, `Editor/Tests`는 Unity 에디터에서 실행하는 검사입니다.

## 최근 UI 안내

- [로그인](History/STEP52-LOGIN-DESIGN.md)
- [콘텐츠 로비 목록](History/STEP53-CATALOG-DESIGN.md)
- [로비 내부](History/STEP54-INTERIOR-DESIGN.md)
- [방 내부](History/STEP55-ROOM-DESIGN.md)

## 실행 파일 만들기

다른 PC에서 실행할 때는 [LAN 접속 안내](Guides/LAN-CONNECTION.md)를 참고합니다.

`Smart Training → Build → Build Windows Test Bundle`로 클라이언트와 전용 서버를 함께 만듭니다. 자세한 순서는 [Windows 테스트 빌드 안내](Guides/BUILD-TEST-BUNDLE.md)를 참고하세요.

기존 파일은 `.meta`와 함께 이동했습니다. 이후에도 Unity 에디터에서 이동하거나 `.meta`를 함께 관리해 씬·프리팹 참조가 유지되도록 해주세요.
