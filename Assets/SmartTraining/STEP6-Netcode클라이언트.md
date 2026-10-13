# Unity STEP 6 — Netcode 클라이언트와 방 참가 기반

전용 방 서버 연동이 이후 단계에서 완료됐습니다. 현재 확인 방법은 `STEP7-전용방서버.md`를 참고하세요.

## 이번 단계
- Netcode for GameObjects 2.12.0을 프로젝트 의존 패키지에 추가합니다.
- Unity Transport 2.6.0은 Netcode의 의존 패키지로 함께 설치됩니다.
- 기존 ApiLobby 씬에 방 참가, 방 퇴장/접속 예약 취소 버튼을 추가합니다.
- 서버에서 받은 예약 토큰으로 Netcode 연결을 시도합니다.
- 실제 접속 승인과 ASP.NET 사용자 접속 확인은 다음 단계의 Unity 전용 서버가 담당합니다.

버전 근거: Unity 6.4 공식 패키지 안내
https://docs.unity3d.com/6000.4/Documentation/Manual/com.unity.netcode.gameobjects.html

## 확인 순서와 현재 한계
1. Unity의 패키지 다운로드와 스크립트 컴파일 완료를 기다립니다.
2. ApiLobby 씬 Play → 로그인 → 로비 입장 → 콘텐츠 선택 → 방 생성.
3. 방 목록에서 방을 선택하면 '선택한 방에 참가' 버튼이 표시됩니다.
4. 현재는 Unity 방 서버 실행 파일이 없으므로 참가 시 서버 초기화 오류가 정상입니다.
5. 이 오류로 로그인이 풀리거나 pendingRoomId가 남지 않는지 확인합니다.
6. Play 종료 후 서버 사용자 정리 완료 메시지와 같은 닉네임 재로그인도 확인합니다.

아직 일반 방 참가가 끝까지 동작하는 상태는 아닙니다.
다음 작업: Unity 전용 서버 시작 → /RoomHub 등록 → ValidateConnection → Netcode 승인 → ConfirmConnection → 종료 통지.
ASP.NET 설정의 RoomServer/Smart Training Room.exe 파일 생성과 서버 등록 코드가 필요합니다.
이번 단계의 로컬 UDP 연결 검증은 그 등록 기능을 대신하지 않습니다.

## 변경 파일과 함수
Packages/manifest.json — com.unity.netcode.gameobjects 2.12.0 의존 추가. 기존 패키지 항목은 유지.
Scripts/ApiModels.cs — RoomMembershipRequest, ReservedRoom, JoinRoomResponse, RoomConnectionData 추가.
Scripts/TrainingApiClient.cs — JoinRoom(), LeaveRoom() 추가.
- 참가 API는 서버 준비 대기 최대 설정 300초에 맞춰 요청 제한 310초를 사용합니다.
- 다른 API는 기존 15초 제한을 유지합니다.
Scripts/RoomNetworkClient.cs — Connect(), Dispose().
- 주소, 포트, 예약 만료를 검사하고 호스트 이름은 IP로 조회합니다.
- NetworkManager와 UnityTransport를 런타임에 생성합니다. Inspector 설정은 필요하지 않습니다.
- NetworkConfig.ConnectionData에 UTF-8 JSON {roomId,userId,admissionToken}을 전달합니다.
- 사용자 인증 accessToken을 Netcode 승인 데이터에 넣지 않습니다.
- 클라이언트 연결은 최대 15초 또는 남은 예약 기한까지 대기합니다.
- 승인 거부/연결 실패 시 연결을 종료하고 호출자에게 오류를 반환합니다.
Scripts/NetworkShutdownCleanup.cs — Begin(), Update().
- NetworkManager 종료 완료 후 객체를 파괴하여 Transport의 이미 해제된 메모리 접근을 방지합니다.
Scripts/ApiLobbyScreen.cs — JoinRoom(), LeaveRoom(), Logout(), Update().
- 참가 실패 시 내 정보로 남은 예약을 확인하고 서버 퇴장/예약 취소를 요청합니다.
- 네트워크 단절 시 서버 소속 정리를 요청합니다.
- 로그인 인증이 없어지거나 Play가 끝나면 Netcode 연결도 종료합니다.
- Netcode 연결 완료와 ASP.NET의 접속 확인 완료를 별도로 표시합니다.
Editor/NetcodeLoopbackChecks.cs — 로컬 UDP 연결·승인 데이터·거부·만료·종료 검증.
Editor/ApiSmokeTest.cs — 전용 서버 부재 오류와 예약 잔류 여부 검증을 추가했습니다.

## 검증 결과
설치된 Unity 라이브러리와 Netcode 참조로 런타임 빌드 성공.
별도 Unity 검증 프로젝트에서 실제 API + Netcode UDP 전체 44개 검증 통과.
실제 로컬 Netcode 서버/클라이언트의 승인 데이터 전달과 연결 성공을 확인했습니다.
승인 거부, 만료 예약 거부, 반복 연결 종료, 런타임 예외 없음도 확인했습니다.
이전 로그인/로비/방/Play 종료 정리 검증도 통과했습니다.
ASP.NET이 실행한 Unity 전용 서버와 끝까지 연동하는 검증은 다음 단계입니다.
서버 코드 및 기존 씬 파일은 이번 단계에 변경하지 않았습니다.
# 후속 구현 완료

전용 방 서버 실행·등록과 실제 참가 연결이 추가됐습니다. 현재 확인 방법은 `STEP7-전용방서버.md`를 참고하세요.

