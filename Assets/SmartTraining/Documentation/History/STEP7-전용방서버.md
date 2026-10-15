# 전용 방 서버 연결

이제 방 참가 시 ASP.NET이 Windows 전용 Unity 서버를 실행합니다.
서버가 `/RoomHub`에 등록한 뒤 참가 예약을 반환하고, 클라이언트는 Netcode로 접속합니다.

## 확인 방법
1. ASP.NET을 `http` 또는 `https` 실행 프로필로 시작합니다. 기본 HTTP 주소는 `http://localhost:5229`입니다.
2. Unity에서 `Assets/SmartTraining/Scenes/ApiLobby.unity`를 열고 Play를 누릅니다.
3. 로그인 → 로비 생성/참가 → 콘텐츠 선택 → 방 생성 → 방 선택 → `선택한 방에 참가`를 누릅니다.
4. `방에 접속했습니다.`와 연결 인원 1명/예약 인원 0명을 확인합니다.
5. 방 나가기 또는 Play 종료 후 다시 로그인해 사용자·방 상태가 정리됐는지 확인합니다.

`RoomServer.unity`는 ASP.NET이 실행하는 서버용 씬입니다. 에디터에서 직접 Play할 필요가 없습니다.
플레이어 생성과 이동 동기화는 후속 단계에서 추가됐습니다. `STEP8-플레이어이동.md`를 참고하세요.

## 수정한 함수
- `RoomDedicatedServer.Start`: UDP 포트 열기 → RoomHub 연결 → Register.
- `RoomDedicatedServer.Approve`, `Validate`: 참가 토큰을 ASP.NET에서 검증한 후 접속 승인.
- `Connected`, `Confirm`: 실제 접속을 ASP.NET에 반영.
- `Disconnected`, `NotifyDisconnected`: 네트워크 종료 시 참가 정보 정리.
- `DisconnectUser`: 로그아웃한 사용자의 Netcode 연결 종료.
- `RoomHubConnection.Connect`, `Invoke`, `Tick`: JSON SignalR 통신, 응답 처리 및 연결 감시.
- `ApiLobbyScreen.JoinRoom`: Netcode 접속 후 ASP.NET의 참가 확정까지 확인.
- ASP.NET `ProcessHelper.LaunchUnityServer`: 서버 주소·포트 전달, 환경변수로 등록 토큰 전달, 방별 로그 기록.

## 설정 및 다시 빌드
ASP.NET `appsettings.json`의 `Room:ServerApiBaseUrl`은 전용 서버가 접속할 API 주소입니다.
ASP.NET 포트를 바꾸면 이 값도 바꿉니다. Unity 화면의 Server URL도 실제 API 주소로 맞춥니다.
HTTPS 프로필에서도 같은 컴퓨터의 방 서버는 기본 로컬 HTTP 주소로 `/RoomHub`에 연결합니다.
이 로컬 연결은 HTTPS로 전환하지 않습니다. 일반 API 및 원격 방 서버 연결에는 기존 HTTPS 전환을 유지합니다.
`Room:AdvertisedAddress`는 클라이언트에 알려줄 전용 서버 주소이며, API 주소와 별개입니다.
다른 컴퓨터에서 접속하려면 도달 가능한 주소와 UDP 방 포트의 방화벽 설정이 필요합니다.

`Smart Training > Build Windows Room Server` 메뉴로 서버를 다시 빌드할 수 있습니다.
Windows Build Support의 Dedicated Server 지원이 필요합니다.
기본 빌드 위치는 Unity 프로젝트의 `Builds/RoomServer/`입니다.
실행 파일 하나만 복사하지 말고 해당 폴더의 전체 런타임 파일을
ASP.NET 프로젝트의 `RoomServer/`에 복사한 다음 ASP.NET을 다시 빌드합니다.
`.csproj`가 해당 폴더를 빌드·게시 출력에 포함합니다.
기본 실행 경로는 ASP.NET 실행 파일 옆의 `RoomServer/Smart Training Room.exe`입니다.
실행 중인 방 서버는 ASP.NET에서 관리하므로 빌드/교체 전 ASP.NET 실행을 종료합니다.

방 로그는 실행 폴더의 `room-{방 ID}.log`에 기록됩니다. 등록 토큰과 참가 토큰은 로그에 출력하지 않습니다.
서버 제어 연결이 끊기면 방 서버를 종료하고 기존 ASP.NET 감시 기능이 참가 상태를 정리합니다.

SignalR 통신 형식: https://github.com/dotnet/aspnetcore/blob/main/src/SignalR/docs/specs/HubProtocol.md
