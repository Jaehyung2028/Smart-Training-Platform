# UI 스타일 다듬기

기존 로그인·로비·방 기능과 프리팹 연결을 유지하면서 버튼·카드·입력칸의 스타일을 통일했습니다.

- 밝은 배경과 흰색 카드, 파란 주요 버튼으로 화면의 역할을 구분합니다.
- RoundedSurface.png를 9-slice Sprite로 사용해 서로 다른 크기의 카드와 버튼에 동일한 모서리를 적용합니다.
- 대기실의 시작·방장 설정 버튼은 상단, 퇴장·로그아웃은 우측 하단에 배치합니다.
- 참가자 카드에서 이름·연결 상태와 학습 결과를 나누며 완료 결과는 초록색으로 표시합니다.
- 콘텐츠 HUD의 메뉴·조작 패널에도 같은 모서리 스타일을 사용합니다. 학습 진행은 별도 카드에 표시하고 영상 조작창 위로 배치합니다.

수정 위치:

- Editor/UI/Designers/UguiScreenPolish.cs: 전체 화면의 스타일과 배치 적용
- Scripts/UI/Common/UguiVisualStyle.cs: 화면 로직과 독립된 공통 모서리 표현
- Resources/SmartTraining/DefaultUguiTheme.asset: 기본 색상
- Resources/SmartTraining/UI/RoundedSurface.png: 모서리 리소스
- Scripts/UI/Views/RoomParticipantCard.cs: 참가자 학습 결과 표현
- Scripts/UI/Content/ContentHudController.cs: 콘텐츠 진행 카드

Unity 메뉴 Smart Training → UI → Polish All Screens는 이번 디자인 설정을 다시 적용합니다. 개별 프리팹과 씬 연결은 유지하며 실제 실행 UI는 기존 프리팹을 사용합니다.

방 내부의 로비 명단은 SignalR 변경 알림으로 자동 갱신합니다. 수동 명단 새로고침과 이전 위치·이동 팝업은 제거했습니다. 콘텐츠 내의 1인칭 이동은 공통 입력·카메라 모듈을 그대로 사용합니다.

대기 중 방장은 방 상단의 콘텐츠 변경 버튼으로 같은 교육과정의 최하위 콘텐츠를 선택할 수 있습니다. 방과 참가자는 유지하며, 변경 시 전원의 준비와 이전 콘텐츠의 학습 결과를 초기화합니다. 접속 예약 중이거나 진행 중이면 변경할 수 없습니다.
