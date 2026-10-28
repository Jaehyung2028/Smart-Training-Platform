# 모듈 역할과 의존 기준

프로젝트가 직접 작성한 Unity `Assets/SmartTraining` 및 ASP.NET 프로젝트의 C#을 대상으로 폴더·선언·의존 참조와 주요 실행 흐름을 점검했습니다. 패키지·빌드 출력·생성물은 리팩터링 대상에서 제외합니다.

## 분리한 책임

| 기능 | 담당 모듈 | 연결 역할 |
|---|---|---|
| 카메라 입력·시점·응시 | Common/Input/FirstPersonCameraInput, Common/Camera/FirstPersonCameraController | 씬 세션이 대상과 허용 조건 전달 |
| 키보드 이동 | Common/Input/PlayerMovementInput | 화면이 텍스트 입력 여부를 확인한 뒤 사용 |
| 개인 설정 저장·편집 취소 | Common/Settings/ClientPreferences | 기존 PlayerPrefs 키 보존 |
| 설정 패널 | Common/UI/ClientSettingsPanel | 적용·저장·취소 콜백 전달 |
| 콘텐츠 HUD·메뉴·Esc·설정 편집 | UI/Content/ContentHudController | 세션은 ContentHudState와 ContentHudActions 전달 |
| 아바타 메시·재질·표시 | Common/Rendering/PlayerAvatarView | RoomPlayer는 네트워크 상태, 씬 세션은 표시 가능 여부 결정 |
| 로컬 영상 디코딩·화면·소리·위치 보정 | Common/Media/VideoSurfacePlayer | VideoContentPlayer가 공유 시계 값 전달 |
| SRT 자막 해석 | Common/Media/VideoSubtitles | 표시 측이 시간으로 문장 조회 |
| 채팅 데이터·UI 표현 | Client/Api/ApiModels, UI/Chat/ChatMessagePresentation | 화면과 공통 UGUI 유틸에서 데이터·표시 규칙 분리 |
| 인증 헤더·사용자 Claim 해석 | ASP.NET Services/Auth/UserAuthentication | Controller·인증 Handler·로비 구독이 재사용 |

ContentSceneSession은 씬·Addressables 수명주기와 기능 연결을 담당합니다. UI를 직접 생성하거나 설정 저장·키 입력·화각·영상 디코딩을 처리하지 않습니다.
RoomPlayer는 특정 콘텐츠 씬의 정적 상태를 조회하거나 메시·재질을 만들지 않습니다. SetPresentationVisible()로 표시 조건을 전달받아 표시 모듈을 연결합니다.
VideoContentPlayer는 프리팹의 직렬화 필드를 유지하고 방의 공유 시계·권한과 로컬 표시 모듈을 연결합니다. 로컬 VideoSurfacePlayer는 방·플레이어·네트워크 타입을 참조하지 않습니다.

## 유지한 의존성

- ApiLobbyScreen은 로그인·로비·방의 클라이언트 흐름을 연결하는 프로젝트 전용 조정자입니다. HTTP·SignalR·Netcode의 실제 처리는 별도 클라이언트에 둡니다. partial 파일 분리는 독립 모듈 분리로 간주하지 않습니다.
- ModelViewer는 모델 콘텐츠의 상호작용 조정자이고, 부품 트리·강조·투명도·분해 애니메이션 처리는 각 전용 모듈에 둡니다. SharedModel과 SharedVideo의 참가자 검사는 네트워크 권한 경계라서 서버 쪽에 유지합니다.
- RoomService의 예약·정원·방장·소속 변경은 같은 멤버십 잠금에서 일관성을 지켜야 합니다. 이 처리들을 파일 크기만으로 별도 저장소에 분산하지 않습니다.
- UserLifecycleService는 사용자·로비·방 정리 순서와 알림을 조정합니다. 네트워크 송신은 잠금 밖에서 수행합니다.
- 운영 서비스의 상태·변경 버전·저장 기록은 같은 운영 상태를 다룹니다. 조회용 DTO와 웹 페이지 표현은 별도 계층을 유지합니다.
- ContentAssetScope, TrainingJson, RoomHubConnection, ProcessHelper, RoomPassword 등은 이미 라이브러리·자원 수명주기·도메인별 역할로 구분되어 있어 유지합니다.

## 이후 코드 추가 기준

1. 다른 콘텐츠·씬에서도 쓸 기능은 Common에 두고 필요한 값·Transform·콜백만 받습니다.
2. Common에서 ApiLobbyScreen·ContentSceneSession·RoomPlayer·씬 경로·콘텐츠 종류를 참조하지 않습니다.
3. UI는 상태와 요청 콜백을 받고, 네트워크·세션 저장소를 직접 조작하지 않습니다.
4. 프로젝트 전용 조정자는 모듈 연결과 처리 순서를 맡으며 입력 해석·렌더링·파일 파싱을 직접 구현하지 않습니다.
5. 기존 프리팹 필드·GUID·설정 저장 키·API JSON·권한 검사는 구조 정리 중에도 보존합니다.

## 시나리오 실습

ScenarioProgress는 단계 규칙, SharedScenario는 서버 권한·거리 판정과 동기화, ScenarioServerSession은 원본 로드·해제를 담당합니다. ScenarioContent는 로컬 클릭 연결, ScenarioObjectView는 색상·체결 표현, ScenarioControlPanel은 안내 UI를 맡습니다. 공통 카메라·이동 입력은 기존 Common 모듈을 그대로 사용합니다. 자세한 흐름은 ScenarioPractice.md를 참고합니다.
