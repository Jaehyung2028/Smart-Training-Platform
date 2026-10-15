# 로비 종료 예약

## 사용 방법
1. ASP.NET과 Unity를 다시 실행합니다. 서버 PC에서 `http://localhost:5229/operations`를 엽니다.
2. 대상 로비의 **종료 예약**에서 시각과 사유(1~200자)를 입력하고 적용합니다. 시각은 브라우저의 현지 시각으로 입력하며 현재 이후부터 30일 이내만 허용합니다.
3. 예약 즉시 새 로비 입장을 차단합니다. Unity의 로비 목록과 현재 로비·방 화면에서 종료 시각, 남은 초, 사유를 표시합니다. 이미 접수된 입장과 기존 방 이용은 종료 시각까지 유지합니다.
4. 예약 시각에 해당 로비의 모든 방·접속 예약·로비 입장 대기를 정리하고 로비 및 방 프로그램을 종료합니다. 로그인은 유지하고 참가자는 로비 목록으로 돌아갑니다. 다른 로비는 계속 이용할 수 있습니다.
5. 종료 전 **종료 예약 취소**, 종료 후 **종료 해제**에서 사유를 입력하면 차단을 해제합니다. 별도로 켜둔 점검 모드는 유지되므로 필요하면 점검도 해제합니다.

예약 시각을 변경하려면 기존 예약을 취소한 뒤 새로 지정합니다. 예약·취소·실행은 각각 시각과 사유를 기록하며 오래된 화면의 요청은 409로 거절하고 최신 상태를 표시합니다. 반복 적용 버튼은 요청 중 비활성화합니다. 자동 갱신은 작성 중인 입력을 지우지 않습니다.

## 실행 및 복원 정책
- 종료 검사는 서버에서 1초마다 수행합니다. 프로세스 정리와 클라이언트 알림에 시간이 걸릴 수 있어 화면 복귀가 모든 PC에서 정확히 같은 순간에 완료되지는 않습니다.
- Unity의 남은 시간은 클라이언트 시계를 이용한 안내입니다. 실제 종료 판단은 ASP.NET 서버의 UTC 시각으로 합니다.
- 종료 후에는 차단을 유지하여 접속 요청이나 복구 감시가 로비를 다시 실행하지 못하게 합니다. 명시적으로 종료 해제하면 다시 온디맨드로 실행합니다.
- `Data/operations-history.json`에 종료 예약·완료 상태를 저장합니다. 재시작 후 미래 예약을 이어서 수행하고, 이미 시각이 지난 예약은 시작 시 처리합니다. 과거 사용자·프로그램은 복원하지 않습니다.
- 파일 저장이 꺼져 있거나 실패하면 현재 실행에만 적용될 수 있으므로 운영 화면에 경고를 표시합니다. 개발용 `SMART_TRAINING_SERVER_PROBE=1`도 파일 저장을 끕니다.
- 강제 종료 당시 저장되지 않은 최신 실행 이력은 누락될 수 있습니다. 예약 자체는 변경 응답 전에 즉시 저장을 시도합니다.

## 주요 코드
- `Services/Operations/LobbyShutdownService.cs`: `SetShutdown()` 검증·저장, `ExecuteDue()` 기한 도래 처리, `LobbyShutdownWorker` 주기 실행
- `Services/Operations/ServerOperationsRegistry.Shutdown.cs`: 예약 버전·이벤트 관리, 중복 요청 제어
- `Services/Lobby/LobbyService.Shutdown.cs`: `EvacuateLobby()` 소속·입장 대기 정리
- `Services/Room/RoomService.LobbyShutdown.cs`: `CloseLobbyRooms()` 해당 로비의 방 서버·접속·예약 정리
- `Services/Lobby/LobbyService.Catalog.cs`: `JoinCatalogLobby()` 새 입장 차단
- `_Controllers/OperationsController.cs`: `SetShutdown()` 로컬 변경 API
- `Services/Operations/OperationsPage.cs`: 시각·사유 입력, 예약 취소·종료 해제
- Unity `ApiLobbyScreen.Shutdown.cs`: `DrawShutdownNotice()`, `ShutdownBlocksEntry()` 안내·입장 제한
- Unity `ApiLobbyScreen.Live.cs`: 종료로 소속이 바뀌면 상태를 다시 조회하고 목록으로 복귀

`POST /api/operations/lobbies/{lobbyId}/shutdown`의 JSON은 `atUtc`(UTC 시각 또는 해제용 null), `reason`, `expectedRevision`입니다. 서버 PC의 루프백 주소와 `X-Operations-Request: 1` 헤더가 필요합니다. 외부 Host·다른 웹사이트 Origin은 거절하고 일반 사용자 인증만으로 운영 권한을 부여하지 않습니다. 이 API는 별도 관리자 계정이 있는 원격 관리 기능이 아닙니다.

## 검증
서버 검사 623개 통과(종료 예약 34개 포함). 실제 ASP.NET과 Unity 클라이언트 3개로 연동 검사 19개 통과: 안내 자동 갱신, 신규 입장 차단, 예약 취소 후 입장, 기존 PID 유지, 방·로비 참가자의 목록 복귀와 로그인 유지, 다른 로비 유지, 종료 해제 후 온디맨드 재시작, 실제 서버 재시작 후 미래 예약·완료 상태 복원을 확인했습니다. 준비 중 로비 입장 대기, 방 입장 예약, 늦은 서버 등록·접속 확인도 서버 검사에서 검증했습니다. 계획된 종료는 실패로 기록하지 않습니다.

Unity 클라이언트·방 서버 빌드와 운영 페이지의 필수 입력·예약·취소·갱신 중 입력 유지 검증을 통과했습니다. 브라우저 오류는 없었습니다.

솔루션 폴더에서 다음과 같이 다시 실행합니다. 먼저 ASP.NET과 Unity 개발 실행 파일을 빌드해주세요. 별도 포트 51452와 독립 이력 파일을 사용하며 자신이 실행한 프로세스만 종료합니다.

```powershell
& '.\Tests\Operations\Run-Shutdown.ps1' -ServerProject '.\Smart Training Server' -ClientExecutable 'C:\Users\yafa7\Desktop\Projects\Smart Training Platform\Builds\Client\Smart Training Client.exe' -EvidenceRoot "$env:TEMP\SmartTrainingShutdownChecks"
```
