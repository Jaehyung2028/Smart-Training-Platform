# RoomPlayer 우주비행사 표시

`Resources/SmartTraining/RoomPlayer.prefab` 아래에 배치한 `Astronaut`를 사용합니다. 기존 네트워크 참가자·이동·시선 방향·이름표·역할 표시 구조를 유지합니다.

## 표시와 애니메이션

- 대기실과 전용 서버에서는 모델을 비활성화합니다.
- 콘텐츠가 시작되면 다른 참가자의 전신을 표시합니다. 본인 1인칭 전신은 카메라를 가리지 않도록 숨깁니다.
- 모델을 매번 생성·파괴하지 않고 프리팹 소유 자식을 표시·숨김으로 재사용합니다.
- 동기화된 위치의 실제 변화량으로 이동 속도를 구하므로 애니메이션용 RPC를 추가하지 않습니다.
- `Speed` 파라미터를 부드럽게 갱신합니다. 위치 초기화처럼 큰 좌표 보정은 이동 애니메이션으로 취급하지 않습니다.
- Root Motion을 끄고 기존 서버 이동만 위치를 변경합니다.

`Stylized_Astronaut/Character/Astronaut.fbx`의 반복 클립 `Idle`, `Walk`, `Run`을 사용합니다. 달리기 외의 점프·공중 동작은 이동 기능과 연결되어 있지 않으므로 이번 이동 트리에 포함하지 않습니다.

## 편집 위치

- 모델 참조: `RoomPlayer`의 `Avatar Root`, `Avatar Animator`
- Animator: `Content/Characters/RoomPlayerAstronaut.controller`
- Blend Tree: `Locomotion` → `Idle Walk Run`
- 속도 기준: Idle 0, Walk 2, Run 4 (기존 서버 최대 이동 속도 4에 맞춤)
- 원본 클립·모델·재질: `Assets/Stylized_Astronaut`

최초 연결 때 모델 경계의 높이를 1.8로 맞추고 발바닥을 기존 플레이어 중심의 Y=-1에 정렬합니다. 이후 프리팹에서 편집한 모델 배치는 보존합니다. 원본 에셋의 데모 이동·카메라 스크립트를 네트워크 플레이어에 붙일 필요가 없습니다.

`RoomPlayerAvatarBuilder.Configure()`는 표시 참조와 초기 설정을 연결합니다. `PlayerAvatarView.Show()`, `Refresh()`, `Hide()`는 전달받은 모델과 Animator만 제어하며 씬·플레이어 타입이나 네트워크를 조회하지 않습니다. `RoomPlayer.LateUpdate()`가 동기화 위치 갱신 뒤 표시 모듈을 갱신합니다.
