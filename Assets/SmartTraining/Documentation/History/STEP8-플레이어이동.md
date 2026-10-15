# 플레이어 생성과 이동 동기화

방 참가가 확정되면 사용자별 `RoomPlayer` 네트워크 오브젝트를 생성합니다.
ASP.NET에서 확인한 닉네임과 방 인덱스로 이름·시작 위치를 지정합니다.
다른 클라이언트가 늦게 참가해도 기존 참가자의 현재 위치를 받습니다.

## 확인 순서
1. Unity Play와 실행 중인 ASP.NET을 종료한 후 ASP.NET을 다시 빌드하고 시작합니다. 기존 HTTPS 프로필을 사용할 수 있습니다.
2. Unity에서 `Assets/SmartTraining/Scenes/ApiLobby.unity`를 열고 Play → 로그인 → 로비/방 참가를 진행합니다.
3. 방에 접속하면 플레이어 위치 지도가 표시됩니다. 내 플레이어는 초록색, 다른 플레이어는 파란색입니다.
4. Game 화면을 클릭하고 WASD 또는 방향키로 이동합니다. 화면의 이동 버튼을 누르고 있어도 움직입니다.
5. 두 명으로 확인하려면 Unity 프로젝트의 `Builds/Client/Smart Training Client.exe`를 실행하고 다른 닉네임으로 같은 로비·방에 참가합니다.
6. 두 화면에서 닉네임과 위치가 일치하는지, 한 명이 나가면 다른 화면에서도 해당 플레이어가 사라지는지 확인합니다.

창을 전환하면 이전 창의 이동 입력을 멈춥니다. 다른 참가자의 이동 수신은 계속됩니다.
실행 파일은 `_Data`, `MonoBleedingEdge`, DLL 등 같은 폴더의 런타임 파일이 함께 있어야 합니다.
기본 훈련 영역은 X/Z 각각 -12~12, 이동 속도는 초당 4입니다.
입력이 0.3초 이상 도착하지 않으면 서버에서 이동을 멈춥니다.
현재는 평면 이동과 영역 경계를 구현했습니다. 장애물 충돌·점프·애니메이션·훈련 행동은 이후 단계입니다.

## 함수별 변경
- ASP.NET `RoomService.ConfirmConnection`: 성공 응답에 서버가 관리하는 사용자 ID·닉네임·방 인덱스를 포함합니다. 기존 success/message 계약은 유지됩니다.
- `RoomDedicatedServer.Confirm`: ASP.NET 참가 확정 후 `SpawnAsPlayerObject`로 플레이어 생성. 접속 종료 시 Netcode가 소유 플레이어를 제거합니다.
- `RoomPlayer.InitializeServer`, `OnNetworkSpawn`: 식별 정보와 시작 위치 설정.
- `RoomPlayer.MoveRpc`: 소유자만 방향 입력을 전송할 수 있습니다. 비정상 숫자와 큰 입력을 검사합니다.
- `RoomPlayer.ServerTick`: 서버가 속도·경계를 적용해 위치를 계산하고 NetworkVariable로 동기화합니다.
- `RoomNetworkClient.LocalPlayer`, `Players`, `SendMovement`: 내 플레이어/전체 참가자 조회 및 초당 최대 20회 입력 전달.
- `RoomMovementInput.Read`: 원본 프로젝트의 Input System과 WASD/방향키 지원.
- `RoomMovementView.Draw`, `ApiLobbyScreen.DrawRoom`: 위치 지도, 닉네임, 이동 버튼과 퇴장 화면.
- `RoomPlayerPrefab.Register`: 클라이언트/서버가 동일한 Resources 프리팹을 등록합니다.

## 다시 빌드
`Smart Training > Build Windows Room Server`: 전용 서버 빌드.
`Smart Training > Build Windows Test Client`: 테스트 클라이언트 빌드.
플레이어 또는 네트워크 코드를 수정하면 서버와 클라이언트를 함께 다시 빌드합니다.
프리팹은 이미 추가했으므로 별도 Inspector 연결은 필요 없습니다.
전용 서버 빌드의 전체 런타임 폴더를 ASP.NET 프로젝트 `RoomServer/`에 복사하고 ASP.NET을 다시 빌드합니다.

구현은 NGO 2.12의 소유자 RPC와 서버 쓰기 권한의 NetworkVariable을 사용합니다.
RPC 공식 문서: https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.12/manual/advanced-topics/message-system/rpc.html
