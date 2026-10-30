# 프리팹과 씬에서 편집하기

콘텐츠 외형과 UI는 미리 저장된 씬·프리팹에서 편집합니다. 빌드할 때 샘플 도형·작업대·애니메이션·단계를 다시 만들지 않습니다.

## 편집 위치

| 대상 | 위치 |
|---|---|
| 공통 맵·벽·바닥 충돌 | `Assets/SmartTraining/Resources/SmartTraining/TrainingEnvironment.prefab` |
| 콘텐츠 씬 조명·후처리 | `Assets/SmartTraining/Scenes/TrainingContent.unity` |
| 모델 콘텐츠 | `Assets/SmartTraining/Content/Models` |
| 실습·협동 장비와 작업 대상 | `Assets/SmartTraining/Content/Scenarios`의 각 콘텐츠 `.prefab` |
| 실습 단계·공구·역할 조건 | 같은 콘텐츠의 `.asset` (ScenarioDefinition) |
| 공구와 손 | `Assets/SmartTraining/Content/Scenarios/Tools` |
| 플레이어·Animator·이름표 연결 | `Assets/SmartTraining/Resources/SmartTraining/RoomPlayer.prefab` |
| 이름표 | 같은 폴더의 `Player Name Tag.prefab` |
| 1인칭 카메라 기본 구성 | 같은 폴더의 `FirstPersonCamera.prefab` |
| 로그인·로비·방 화면 | 같은 폴더의 `LobbyUI`, `LobbyInteriorUI`, `RoomUI` 프리팹 |
| 콘텐츠 HUD와 추가 UI | `Assets/SmartTraining/Resources/SmartTraining/UI/Templates` |

## 씬에 미리 배치된 공통 요소

- `ApiLobby.unity`: 기존 로그인·로비·방 화면, 교육과정 드롭다운, 콘텐츠 선택창, EventSystem, ContentSceneSession이 배치되어 있습니다.
- `TrainingContent.unity / Content Scene`: `FirstPersonCamera`, `ContentHudController`, `Content Resources`를 미리 배치했습니다.
- HUD 아래에는 설정·기능 메뉴·채팅·참가자·부품 목록/설명·실습 안내/결과 패널이 연결되어 있습니다. Inspector의 ContentSceneLayout이 카메라·HUD·콘텐츠 기준점을 연결합니다.
- 선택한 콘텐츠는 `Content Resources`의 자식으로 불러옵니다. 이 Transform을 옮기면 콘텐츠 배치 기준점을 조정할 수 있습니다.
- 카메라와 HUD는 입장 준비 완료 후 활성화합니다. 편집할 때는 Hierarchy에서 켜서 확인하고 실행 전에는 원래 비활성 상태로 돌려놓습니다.
- 채팅 메시지·목록 행, 콘텐츠 종류별 기능 버튼·공구/역할 메뉴, 선택 콘텐츠, 네트워크 플레이어는 실행 데이터에 따라 달라지므로 프리팹을 필요할 때 복제합니다.
- 누락된 콘텐츠 씬 요소를 다시 배치하려면 TrainingContent 씬에서 `Smart Training > Content > Place Missing Content Scene Objects`를 실행합니다. 빌드 시 배치를 다시 만들지 않습니다.

## UI 프리팹

`ContentHudController`는 상단 도구 모음·메뉴·고정 표시를, 나머지는 각 기능 패널을 담습니다. 패널 프리팹의 바깥 RectTransform은 화면 크기를 전달하는 용도이고, 실제 배치는 그 안의 표시 패널에서 편집할 수 있습니다.

- `ContentActionMenu`: 우측 하단 기능 메뉴
- `ClientSettingsPanel`: 감도·시야각·음량과 저장·취소
- `VideoControlPanel`, `VideoSubtitles`: 영상 조작 메뉴와 자막
- `ScenarioControlPanel`, `ScenarioResultPanel`: 실습 진행·결과
- `ScenarioToolMenu`, `ScenarioRoleMenu`: 개인 공구·협동 역할
- `ContentChatPanel`, `ContentParticipantPanel`: 방 채팅·참가자
- `ModelPartListPanel`, `ModelPartDetailsPanel`: 부품 트리·설명
- `ContentTreePopup`, `CourseSelection`, `CoursePopup`: 콘텐츠 선택창·교육과정 드롭다운
- `*Row`, `ContentChatMessage`: 데이터 개수만큼 복제하는 목록 행
- `ActionButton`, `ActionLabel`, `ActionSlider`, `ActionSection`, `ModelPlaybackControls`: 기능 메뉴의 반복 조작 요소
- `EventSystem`: 씬에 이벤트 시스템이 없을 때 사용하는 입력 프리팹

`UiPrefabReferences` 컴포넌트의 Entries가 표시 요소를 연결합니다. 오브젝트 이름을 바꿔도 연결된 참조는 유지됩니다. 기존 Key는 유지하고, Text나 Button을 다른 오브젝트로 교체했다면 해당 Value를 새 컴포넌트에 연결합니다. `UiPrefab.Instantiate()`는 저장된 프리팹을 복제하며, 각 컨트롤러는 데이터 갱신·이벤트·권한 표시만 처리합니다.

화면보다 큰 패널은 화면에 맞춰 줄어듭니다. 실습 작업 중 추가 안내, 역할 줄 수, 목록 들여쓰기·스크롤처럼 데이터에 따라 변하는 배치는 코드에서 조절합니다. 버튼의 선택·오류·방장 상태 색상도 상태에 따라 갱신됩니다.

## 맵과 충돌

TrainingContent와 RoomServer 씬은 같은 TrainingEnvironment 프리팹을 참조합니다. 기존 `_Level`의 배치·모델·재질을 유지하면서 부족한 MeshCollider를 추가했습니다. 맵을 고칠 때는 이 프리팹에서 수정하거나, 씬 인스턴스에서 바꾼 내용을 프리팹에 Apply해야 전용 서버에도 같은 벽·바닥이 적용됩니다. 새 벽·바닥 모델에도 Collider를 연결합니다.

RoomPlayer는 Rigidbody·CapsuleCollider·NetworkRigidbody를 사용합니다. 서버 FixedUpdate가 수평 속도와 회전을 적용하고 중력·벽·바닥 충돌은 물리 엔진이 계산합니다. 클라이언트의 Rigidbody는 NetworkRigidbody가 Kinematic으로 관리하며 결과를 NetworkTransform으로 표시합니다. 캡슐 반지름은 0.32, 높이는 1.8이고 벽에 달라붙지 않도록 PlayerPhysics 재질을 사용합니다.

## 모델 교체

시나리오 작업 대상의 `ScenarioObject.Id`는 단계의 TargetId와 일치해야 합니다. 모델 메시를 교체할 때 이 대상 컴포넌트와 WorkAnchor를 유지하고, 작업 접촉점을 새 모델에 맞춥니다. 가공 전후 모델은 `ScenarioStageVisuals`에 연결하고 전환할 완료 단계 번호를 지정합니다.

모델 둘러보기의 부품에는 `ModelPartInfo`와 선택용 Collider를 지정합니다. 콘텐츠 루트의 `ModelViewer`·`ScenarioContent`·`VideoContentPlayer` 연결을 유지합니다. 각 콘텐츠 프리팹을 Addressables에 등록하고, 서버 `catalog.json`의 assetId를 같은 Address로 연결합니다.

`SampleCourseBuilder.Prepare()`와 기존 준비 진입점은 저장된 프리팹·공유 상태 참조를 확인하는 용도로만 남겨 두었습니다. 배치를 수정한 뒤 최신 콘텐츠가 들어간 클라이언트·방 서버를 함께 빌드합니다.

## 한글 폰트

편집기와 빌드에서 같은 한글 표시를 위해 `Assets/SmartTraining/Fonts/NotoSansKR-Regular.otf`를 연결했습니다. 원본은 [Noto CJK](https://github.com/notofonts/noto-cjk/tree/main/Sans/SubsetOTF/KR)이며 라이선스는 같은 폴더의 `OFL.txt`에 있습니다. 다른 폰트로 교체하려면 프리팹 Text의 Font를 변경합니다.