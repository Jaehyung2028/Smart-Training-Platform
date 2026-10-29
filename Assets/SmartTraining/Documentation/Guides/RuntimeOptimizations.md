# 런타임 최적화

실행 중 자주 반복되는 생성·검색·문자열 변환·배치 계산을 줄입니다. 네트워크 권한이나 콘텐츠 판정, 채팅 전송 형식은 유지합니다.

| 대상 | 적용 기법 | 코드 |
|---|---|---|
| 로비·대기실·콘텐츠 채팅 | Unity `ObjectPool<Text>`로 행 재사용. 대기 행 최대 100개, 화면 종료 시 풀 해제 | `ChatMessageListView.Refresh()`, `Clear()`, `Dispose()` |
| 채팅 데이터 전달 | `IReadOnlyList`로 읽기 전용 목록 전달. 매 프레임 `ToArray()` 복사 제거 | `ApiLobbyScreen.UiChatMessages`, `UiRoomScreenMessages`, `UiContentRoomMessages` |
| 참가자 검색 | 같은 프레임의 명단 조회 공유. 인원이 같으면 배열 재사용. 연결 종료 시 캐시 제거 | `RoomNetworkClient.Players`, `Dispose()` |
| HUD 참가자 상태 | 인원 변화 때만 배열 재할당, 값 구조체를 기존 버퍼에 기록 | `ContentSceneSession.GetParticipantStates()` |
| 참가자 목록 UI | 이름·방장·연결 상태가 바뀔 때만 정렬·문구 갱신 | `ContentParticipantState.Equals()`, `ContentParticipantPanel.Refresh()` |
| 네트워크 문자열 | FixedString 값이 변경될 때만 관리 문자열로 변환 | `RoomPlayer.UserId`, `UserName`, `RoomMasterUserId` |
| 역할 문구 | 현재 네 역할의 16가지 조합을 사전 계산해 한 줄·여러 줄 표시 재사용 | `ScenarioRoles.Label()` |
| 역할 메뉴·이름표 | 참조 캐시와 변경 감지. 이름표의 카메라 방향은 매 프레임 유지 | `ScenarioRoleMenu.RefreshRole()`, `WorldNameTagView.Refresh()` |
| 채팅 배치 | 변경 시 해당 목록만 배치 계산. 전체 Canvas 강제 갱신 제거 | `LayoutRebuilder.ForceRebuildLayoutImmediate()` |

## 수명과 정확성

채팅 행은 반환할 때 비활성화하고 이전 텍스트를 비웁니다. 다시 꺼낼 때 문구·본인 메시지 색상·순서를 설정합니다. 방·채널이 바뀌면 활성 행을 먼저 반환하고 새 대화를 표시합니다. 화면이 종료되면 활성 행과 대기 풀을 함께 정리합니다.

풀은 화면마다 소유하며 전역으로 공유하지 않습니다. 현재 수신 이력 상한과 같은 100개를 대기 상한으로 사용합니다. 적게 쓰는 화면을 위해 미리 100개를 만들지 않고 실제 표시할 때 필요한 수만 생성합니다.

참가자 캐시는 프레임, 네트워크 오브젝트 개수, 플레이어의 Spawn 상태를 확인합니다. 퇴장·재접속·방장 변경이 있을 때 최신 정보를 다시 반영합니다. 공통 표시 모듈에는 콘텐츠나 씬을 조회하는 기능을 추가하지 않습니다.

## 확인 범위

`Smart Training/Content/Check Runtime Optimizations`에서 풀의 행 재사용·방 전환·본인 색상 재설정·재사용 배열의 참가자 변경 감지와 역할 문자열 할당을 확인합니다.

편집기 검사에서는 메시지 500개를 100개 이력으로 순환할 때 생성 행이 100개를 유지했습니다. 예열 후 변경 없는 채팅 목록 1,000회 갱신과 역할 문자열 20,000회 조회 구간의 추가 관리 메모리 할당은 각각 0바이트였습니다. 전체 HUD·게임의 할당량이나 FPS 측정값을 뜻하지 않습니다. 실제 장면의 전체 비용은 Unity Profiler에서 CPU Usage·GC Alloc·UI Layout을 확인합니다.
