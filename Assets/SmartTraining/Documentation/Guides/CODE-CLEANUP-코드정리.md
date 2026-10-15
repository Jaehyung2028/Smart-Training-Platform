# 코드 정리

현재 멀티 접속·방 관리 기능을 유지하면서 사용되지 않는 경로와 중복 코드를 정리했습니다.

## ASP.NET
- LobbyHub와 Unity 로비 서버 실행·준비 대기·연결 종료 경로를 제거했습니다. 실제 로비는 ASP.NET의 LobbyService와 LobbyUpdatesHub로 관리합니다.
- LobbyService.JoinLobbyCore()를 동기 처리로 정리했습니다. 외부 JoinLobby()의 Task 반환 규약과 회원 관리 잠금은 유지합니다.
- LobbyInfo에서 미사용 로비 서버 상태·IP·포트 필드를 제거했습니다. 로비 생성 응답에도 이 세 필드가 사라집니다. 로비 및 방 목록에 필요한 데이터는 유지합니다.
- 범용 ServerConnectionService와 ServerConnectionInfo·ServerType을 RoomServerConnectionRegistry로 통합했습니다.
- RoomHub.GetRoomId()는 등록된 방 ID를 조회하고, 기존의 서버 비밀키·예약 토큰 검증은 RoomService가 계속 담당합니다.
- 실제로 사용하지 않던 PasswordMaxLength와 Simulation 설정을 제거했습니다. 비밀번호 제한은 기존 4~32자를 그대로 사용합니다.
- 사용되지 않는 RoomService.OnDisconnected(), TryGet(), GetAll()과 LobbyService.TryGet()을 제거했습니다.
- 자동 검증에 사용하는 LobbyService.GetAll()과 종료·로그아웃 검증 스크립트는 유지합니다.

## Unity
- ApiLobbyScreen.Room.cs: DrawRoom(), 준비 상태, 방장 위임·강퇴, 방 설정·비밀번호·진행 상태·방 닫기 처리를 분리했습니다.
- ApiLobbyScreen.RoomConnection.cs: JoinRoom(), LeaveRoom(), Logout(), HandleConnectionLost(), ReconnectRoom()을 분리했습니다.
- DisposeRoomConnection(): 퇴장·로그아웃·종료·화면 초기화에 반복되던 연결 정리를 한 함수로 모았습니다.
- 기존 함수 이름, 화면 동작, 참가자 알림, 실제 게임 연결의 동작은 유지합니다.

## 확인
서버와 Unity를 다시 실행하고 로그인, 로비·방 입장, 준비, 방장 위임, 퇴장·재접속, 로그아웃을 확인하세요.
기존처럼 http://localhost:5229 또는 http://서버IP:5229를 사용하며 Builds/Client의 단일 개발 클라이언트를 실행합니다.
