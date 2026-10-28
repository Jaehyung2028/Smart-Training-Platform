# Unity 코드 작성 규칙

- 재사용하는 입력·카메라 기능은 `Scripts/Common`의 독립 모듈로 작성합니다. 콘텐츠별 동작은 해당 콘텐츠에 둡니다.
- 공통 모듈은 ApiLobbyScreen, RoomPlayer, ContentSceneSession, 콘텐츠 유형, 씬 경로를 참조하지 않습니다. 필요한 값·Transform·콜백을 전달받습니다.
- ContentSceneSession은 카메라 모듈 생성·연결·설정 전달·정리를 맡고, 시점 입력·회전·응시·화각 계산을 직접 구현하지 않습니다.
- 이미 있는 Unity Input System, Netcode, SignalR, Addressables 기능을 우선 사용합니다. 전역 관리자나 중복 구현을 무조건 추가하지 않습니다.
- C#은 블록 namespace, Allman 중괄호, 공백 4칸, CRLF를 사용하며 의도를 설명하는 한국어 주석을 작성합니다.
- 기존 사용자 변경을 보존하고, 수정한 스크립트·주요 함수와 확인한 동작을 사용자에게 알립니다. Git 커밋·푸시는 요청받았을 때만 합니다.

- 콘텐츠 HUD·메뉴·설정 편집은 ContentHudController가 담당합니다. ContentSceneSession에서 UGUI를 직접 생성하거나 설정을 저장하지 않습니다.
- RoomPlayer는 씬 상태를 직접 조회하거나 메시·재질을 생성하지 않습니다. 표시 조건은 소유 측에서 전달하고 PlayerAvatarView를 사용합니다.
- 영상의 로컬 디코딩·화면·소리 처리는 VideoSurfacePlayer, 방 시계·권한 연결은 VideoContentPlayer에 둡니다.
- 이동 입력에서 UGUI 포커스를 조회하지 않습니다. 화면 계층에서 입력 차단 여부를 판단합니다.
