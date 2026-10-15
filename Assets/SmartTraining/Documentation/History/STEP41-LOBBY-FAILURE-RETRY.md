# 로비 입장 실패 안내와 재시도

로비 서버를 준비하지 못한 원인을 구분해서 안내하고, 로그인 상태로 같은 로비에 다시 입장할 수 있게 했습니다.

| 상황 | 사용자 안내 |
| --- | --- |
| 실행 파일 또는 디렉터리 누락 | 로비 서버 실행 파일을 찾지 못했으며 서버 관리자에게 설정 확인 요청 |
| 그 밖의 실행 실패 | 실행하지 못했음을 알리고 재시도·관리자 문의 안내 |
| 준비 제한 시간 초과 | 준비 시간이 초과됐음을 알리고 같은 로비 재입장 안내 |
| 준비 중 프로그램 종료·연결 해제 | 연결이 끊겼음을 알리고 같은 로비 재입장 안내 |

서버 내부 경로와 예외 상세 내용을 사용자 화면에 보내지 않습니다. 관리자가 확인할 상세 오류는 서버 로그에 남습니다.

## 실패 화면과 재시도

실패 후 로비 목록에 대상 로비와 오류 안내를 표시하며 `같은 로비에 다시 입장`, `안내 닫기` 버튼을 제공합니다. 다른 로비를 선택해 입장할 수도 있습니다.

재시도 전 서버의 내 정보를 확인합니다. 이미 대상 로비에 입장했다면 목록을 새로고침하고, 대상 로비 입장 대기가 남아 있다면 먼저 정리합니다. 다른 로비에 들어갔거나 다른 로비의 입장이 대기 중이라면 먼저 그 정보를 정리하도록 안내합니다. 같은 요청을 중복해서 보내지 않습니다.

성공, 새 로비 입장, 로그아웃 시 이전 실패와 재시도 대상은 초기화합니다. 취소 요청을 일반 실행 실패로 표시하지 않으며 기존 입장 취소 동작은 유지합니다.

## 주요 변경 함수

- 서버 `LobbyService.StartLobby()` — 실행 예외 종류에 따른 안내 생성
- 서버 `JoinCatalogLobby()` / `ResetLobby()` / `MaintainLobbyServers()` — 요청별 시작 결과로 실패 원인 전달 및 대기 정리
- 서버의 `StartupResult` — 같은 시작을 기다리는 여러 요청이 정리·재시작 후에도 원래 실패 원인을 받도록 결과 보존
- Unity `HandleLobbyJoinFailure()` / `DrawLobbyJoinFailure()` — 실패 원인·대상 로비·재시도 버튼 표시
- Unity `RetryLobbyJoin()` — 내 정보 확인 후 같은 로비 재입장
- Unity `ClearLobbyJoinFailure()` — 이전 실패 초기화

## 검증

- 서버 검사 515개 통과: 누락 파일, 일반 실행 실패, 시간 초과, 동시 대기자에게 같은 실패 전달, 실패 후 같은 로비 재입장 포함
- 실제 Unity 클라이언트 검사 9개 통과: 실행 파일 누락·복구 후 동일 로비 재시도, 시간 초과, 준비 중 프로세스 종료, 다른 점유 로비 유지, 실패 패널 초기화
- 기존 로비 입장 취소 검사 10개 통과
- ASP.NET과 Unity 클라이언트·서버 빌드 완료

기존 조회 검사의 콘텐츠가 하나라는 가정도 제거해 실제 카탈로그의 예시 3개를 기준으로 검사할 수 있게 했습니다.

## 재실행

Unity 프로젝트 폴더의 PowerShell에서 실행합니다. 실행 파일 누락과 복구는 검사 전용 복사본에서 재현하며 원본 실행 파일이나 카탈로그를 변경하지 않습니다. 준비 완료를 보내지 않는 .NET 테스트 프로그램으로 시간 초과와 프로세스 종료를 재현합니다.

```powershell
& '.\Tests\MultiClient\Run-LobbyFailureRetry.ps1' -ServerProject 'C:\Users\yafa7\Desktop\Projects\Smart Training Server\Smart Training Server\Smart Training Server' -ClientExecutable '.\Builds\Client\Smart Training Client.exe' -EvidenceRoot "$env:TEMP\SmartTrainingLobbyFailureChecks"
```

테스트 프로그램 소스는 `Tests/Fixtures/SilentLobbyWorker`에 있습니다. 검사 실행 시 .NET 8 SDK로 빌드하며 API 포트 51435와 검사 서버가 실행한 프로세스만 사용합니다. 결과는 `Tests/MultiClient/LOBBY-FAILURE-RETRY-RESULTS.txt`에 기록했습니다.

## 수동 확인

ASP.NET 서버와 Unity 에디터를 다시 실행합니다. 로비 실행에 문제가 발생하면 일반 처리 문구 대신 실패 원인과 재시도 버튼이 표시됩니다. 서버 설정을 바로잡은 후 같은 로비 재시도에 성공하면 안내가 사라지고 기존 방 생성·참가 화면으로 이동합니다. 정상 서버에서는 실패 안내가 표시되지 않습니다.
