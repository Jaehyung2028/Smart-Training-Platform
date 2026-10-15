# 로비 점검 모드

## 사용 방법
1. ASP.NET 서버를 실행하고 서버 PC에서 `http://localhost:5229/operations`를 엽니다.
2. 로비 행의 **점검 설정**을 누르고 사유(1~200자)를 입력한 후 적용합니다.
3. Unity 로비 목록에 **점검 중**과 사유가 자동 반영되고 새 로비 입장 버튼이 비활성화됩니다. 직접 API로 요청해도 서버가 거절합니다.
4. **점검 해제**에서 해제 사유를 입력하고 적용하면 다시 입장할 수 있습니다.

이미 로비에 들어온 사용자의 방 생성·입장·준비·시작·대기 복귀·방 재접속은 유지합니다. 점검 설정 전에 접수된 로비 입장 대기도 완료할 수 있습니다. 마지막 참가자가 나가면 기존 정책대로 로비 프로그램을 종료하지만 점검 상태는 남습니다. 점검 전환 자체는 Unity 프로그램을 시작하거나 종료하지 않습니다.

## 저장과 요청 제어
`Data/operations-history.json`에 로비별 상태·사유·UTC 변경 시각·설정 버전을 저장합니다. 모드 변경 시 즉시 저장하고 서버 재시작 때 복원합니다. 기존 버전 1 이력 파일도 읽습니다. 파일 저장을 끄거나 저장에 실패하면 현재 실행에는 적용하되 운영 화면에 저장 경고를 표시합니다. 테스트용 `SMART_TRAINING_SERVER_PROBE=1`도 파일 저장을 끕니다.

`POST /api/operations/lobbies/{lobbyId}/maintenance`는 서버 PC의 루프백 요청만 허용합니다. JSON 본문에 `enabled`, `reason`, `expectedRevision`이 필요하고 `X-Operations-Request: 1` 헤더가 필요합니다. 다른 웹사이트 Origin 및 외부 Host는 거절합니다. 일반 사용자 토큰만으로 원격 운영 권한을 얻지 않습니다. 별도 운영자 로그인 기능은 없습니다.

설정 버전이 다르면 409를 반환하여 오래된 화면이 새 설정을 덮어쓰지 못하게 합니다. 이 경우 화면의 설정 입력을 닫고 최신 상태를 표시하므로 다시 선택해주세요. 완전히 같은 상태·사유의 반복 요청은 중복 이력을 만들지 않습니다. 점검 기록은 프로그램 실행·실패 이력과 분리됩니다.

## 주요 코드
- `Services/Lobby/LobbyService.Maintenance.cs`: `SetMaintenance()` — 카탈로그·사유 검증, 상태 변경 및 즉시 저장
- `Services/Lobby/LobbyService.Catalog.cs`: `JoinCatalogLobby()` — 새 입장 차단
- `Services/Operations/ServerOperationsRegistry.Maintenance.cs`: `GetMaintenance()`, `SetMaintenance()` — 버전 검사, 시각·사유·이력 관리
- `Services/Operations/ServerOperationsRegistry.Persistence.cs`: 저장·복원
- `_Controllers/OperationsController.cs`: 로컬 변경 API
- `Services/Operations/OperationsPage.cs`: 사유 입력, 적용·해제, 자동 갱신 중 입력 유지
- Unity `ApiModels.cs`, `ApiLobbyScreen.cs`, `ApiLobbyScreen.LobbyConnection.cs`: 점검 상태 표시와 새 입장·재시도 제한

## 검증
서버 검사 589개 통과(점검 관련 30개 포함). 실제 ASP.NET과 Unity 클라이언트 3개로 18개 항목을 통과했습니다. 새 입장 차단, 다른 로비 입장, 기존 방의 준비·시작·재접속, 해제 후 참가, 마지막 퇴장 시 프로그램 종료, 실제 ASP.NET 재시작 후 복원, 접근 제한을 포함합니다. 운영 화면에서 사유 필수 입력·한글 사유·설정·해제·갱신 시 입력 유지·문자열 안전 표시를 확인했고 브라우저 오류는 없었습니다. Unity 클라이언트와 방 서버 빌드를 통과했습니다.

솔루션 폴더에서 다음과 같이 재검증합니다. 별도 포트 51450과 독립 이력 파일을 사용하며 자신이 시작한 프로세스만 종료합니다. 기존 서버 기록과 카탈로그는 변경하지 않습니다. 실행 전에 ASP.NET 프로젝트와 Unity 개발 실행 파일을 빌드해주세요.

```powershell
& '.\Tests\Operations\Run-Maintenance.ps1' -ServerProject '.\Smart Training Server' -ClientExecutable 'C:\Users\yafa7\Desktop\Projects\Smart Training Platform\Builds\Client\Smart Training Client.exe' -EvidenceRoot "$env:TEMP\SmartTrainingMaintenanceChecks"
```