# 여러 로비의 독립 동작 검증

서버의 실제 카탈로그에 있는 `sample-training`, `safety-training`, `emergency-training` 로비에서 각각 방을 생성했습니다. 실제 Unity 클라이언트 3개와 Unity 로비 프로그램 3개, Netcode 방 서버 3개를 사용해 37개 검사를 통과했습니다.

## 확인한 동작

- 로비마다 별도 방 ID, 방 서버 프로세스와 UDP 접속 주소를 사용합니다.
- 각 방의 참가자 목록에는 해당 방 사용자만 표시됩니다. 준비·진행 상태가 다른 로비에 전달되지 않습니다.
- 로비별 방 조회와 클라이언트의 자동 갱신 목록에는 해당 로비의 방·참가자만 포함됩니다.
- 다른 로비 ID와 방 ID를 섞은 입장 요청은 거절됩니다.
- 한 방의 Netcode 입장 예약과 예약 취소가 다른 로비의 접속·예약 수에 영향을 주지 않습니다. 해당 로비의 방·참가자 목록에 자동으로 반영됩니다.
- 진행 중인 방이 있는 로비 프로그램을 강제 종료해도 방 접속과 진행 상태는 유지됩니다. 해당 로비 프로그램만 재실행됩니다.
- 한 방 서버를 강제 종료하면 해당 참가자만 자신의 로비로 돌아갑니다. 로그인은 유지되고 해당 방에 재접속해 방 서버를 다시 실행할 수 있습니다. 다른 로비와 방 프로세스는 유지됩니다.
- 마지막 참가자가 방과 로비에서 퇴장하면 해당 방·로비 프로그램만 종료됩니다. 다른 클라이언트의 로비 목록에서 사용자 수·방 개수·서버 상태가 자동으로 갱신됩니다.
- 모든 참가자가 로그아웃하면 테스트 서버가 실행한 Unity 프로그램은 모두 종료되고, 카탈로그의 빈 로비 3개는 목록에 남습니다.

검사에서 운영 서버 서비스나 일반 클라이언트 동작을 수정해야 하는 문제는 발견되지 않았습니다. 이번 변경은 반복 실행할 검사와 개발용 관측 정보를 추가한 것입니다.

## 추가·변경한 코드

- `Tests/MultiClient/Run-LobbyIsolation.ps1` — 동시 접속, 예약 취소, 개별 프로세스 강제 종료, 재접속, 자동 갱신 검증
- 스크립트 `Worker()` — 테스트가 시작한 ASP.NET 프로세스의 자식 중 지정한 로비·방 프로그램만 식별
- 스크립트 `Check-Connection()` / `Wait-Player()` — 갱신 중인 상태 파일을 고려해 유효한 접속 정보를 기다리고 검사
- `MultiClientProbe.Update()` — 테스트 스냅샷에 선택 로비, 실제 로비·방·참가자 목록 추가

`MultiClientProbe`는 에디터·개발 빌드에서 명시적으로 `--smarttraining-probe`를 지정할 때만 실행됩니다. 일반 사용자 실행에 새 기능이나 테스트 프로그램이 추가되는 것은 아닙니다.

## 재실행

Unity 프로젝트 폴더에서 PowerShell로 실행합니다. ASP.NET 프로젝트는 먼저 빌드해야 하며 Unity 클라이언트는 현재 개발 빌드를 사용합니다.

```powershell
& '.\Tests\MultiClient\Run-LobbyIsolation.ps1' -ServerProject 'C:\Users\yafa7\Desktop\Projects\Smart Training Server\Smart Training Server\Smart Training Server' -ClientExecutable '.\Builds\Client\Smart Training Client.exe' -EvidenceRoot "$env:TEMP\SmartTrainingLobbyIsolationChecks"
```

이 검사는 전용 API 포트 51430을 사용합니다. 원본 카탈로그를 편집하지 않고, 테스트 서버가 실행한 로비·방 프로세스만 종료합니다. 실제 카탈로그에는 위 3개 예시 ID가 필요합니다. 테스트 실행 중 서버·플레이어 로그와 상태 스냅샷은 지정한 증거 폴더에 남습니다.

통과 결과는 `Tests/MultiClient/LOBBY-ISOLATION-RESULTS.txt`에 정리했습니다.
