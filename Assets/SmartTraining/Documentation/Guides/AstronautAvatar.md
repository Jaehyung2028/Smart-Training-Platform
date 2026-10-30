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

`RoomPlayer.prefab`에서 모델·Animator·이름표를 직접 연결합니다. 아바타 자동 생성기는 제거했습니다. `PlayerAvatarView.Show()`, `Refresh()`, `Hide()`는 전달받은 모델과 Animator만 제어하며 씬·플레이어 타입이나 네트워크를 조회하지 않습니다. `RoomPlayer.LateUpdate()`가 동기화 위치 갱신 뒤 표시 모듈을 갱신합니다.

## 플레이어 위치·회전 동기화

RoomPlayer 프리팹의 NetworkTransform은 Server 권한으로 위치 XYZ와 회전 Y를 동기화합니다. Interpolate와 Use Unreliable Deltas가 켜져 있으며 크기는 동기화하지 않습니다.

RoomNetworkClient.SendMovement는 방향과 ViewYaw를 같은 MoveRpc 요청에 담아 초당 최대 20번 전송합니다. RoomPlayer.FixedUpdate가 입력 크기·속도·영역·입력 유실을 검사해 Rigidbody의 속도와 회전을 적용합니다. 벽·바닥 충돌은 서버 물리 엔진이 계산하고, 전송·보간은 NetworkRigidbody와 NetworkTransform에 맡깁니다. 시작과 대기 복귀는 서버 Teleport로 초기 위치와 회전을 복원합니다.

클라이언트의 직접 NetworkTransform.SetState 요청은 OnClientRequestChange에서 현재 서버 좌표로 돌려보내 이동 검사를 우회하지 못하게 합니다. Position 속성은 서버에서는 판정 좌표, 클라이언트에서는 보간된 표시 좌표입니다.

PlayerAvatarView는 동기화된 이동 거리로 Animator의 Speed를 갱신합니다. Idle·Walk·Run을 위한 별도 NetworkAnimator나 중복 애니메이션 패킷은 사용하지 않습니다.
