# 로비 입장 진행 표시와 취소

로비에 입장할 때 별도 화면으로 로비 이름, 진행 단계, 경과 시간, 취소 버튼을 표시합니다.

- 1/2: 로비 서버 준비와 입장 승인을 기다립니다. 이미 실행 중인 로비는 입장 요청 중이라고 표시합니다.
- 2/2: 서버가 입장을 승인한 뒤 내 정보와 로비 정보를 불러옵니다.
- 취소: 진행 중인 입장 HTTP 요청을 중단하고, 별도의 로비 퇴장 요청으로 남은 입장 대기 또는 이미 확정된 입장을 정리합니다. 내 정보에서 대상 로비의 확정·대기 정보를 확인한 뒤 목록으로 돌아옵니다.

취소 중에는 중복 취소를 막습니다. 마지막 대기자라면 서버가 로비 프로그램을 종료하고, 다른 접속 사용자나 입장 대기자가 있으면 유지합니다. 로그인은 유지하므로 다시 입장할 수 있습니다.

서버에 연결되지 않아 취소를 확인할 수 없는 경우에는 성공했다고 표시하지 않습니다. 내 정보를 새로고침해 남은 입장 정보를 확인하고, 확정 입장은 기존 로비 퇴장 버튼으로, 대기 중인 입장은 새 `로비 입장 대기 취소` 버튼으로 정리할 수 있습니다.

## 변경 함수

- `ApiLobbyScreen.JoinLobby()` — 진행 상태와 취소 가능한 입장 흐름
- `DrawLobbyJoinProgress()` — 진행 화면·경과 시간·취소 버튼
- `RequestCancelLobbyJoin()` — 취소 요청과 중복 취소 방지
- `CancelLobbyEntry()` — 취소 후 확정·대기 정보 정리와 서버 확인
- `ClearLobbyJoinProgress()` — 성공·실패·취소·화면 종료 시 상태 초기화
- `TrainingApiClient.JoinLobby()` / `ChangeLobby()` — 선택적인 취소 조건을 HTTP 요청에 전달

서버에는 기존 로비 입장 취소와 마지막 사용자 종료 처리를 사용하며, 이번 작업에서는 서버 서비스 코드를 변경하지 않았습니다. Unity 클라이언트와 서버 실행 파일을 다시 빌드했습니다.

## 자동 검증

`Tests/MultiClient/Run-LobbyEntryCancellation.ps1`로 실제 Unity 클라이언트 두 개를 실행해 10개 검사를 통과했습니다. 최초 입장 취소, 취소 후 재시도, 입장 승인 직후 취소, 다른 사용자와 서버 유지, 실행 파일 누락 오류, 실패 후 다른 로비 입장, 마지막 퇴장을 확인했습니다.

입장 승인 직후의 경합은 개발 빌드의 명시적인 테스트 실행 모드에서만 대기를 걸어 재현합니다. 일반 실행에서는 테스트 대기를 사용하지 않습니다. 기존 방 입장 취소 검사도 독립 클라이언트 3개로 통과했습니다.

```powershell
& '.\Tests\MultiClient\Run-LobbyEntryCancellation.ps1' -ServerProject 'C:\Users\yafa7\Desktop\Projects\Smart Training Server\Smart Training Server\Smart Training Server' -ClientExecutable '.\Builds\Client\Smart Training Client.exe' -EvidenceRoot "$env:TEMP\SmartTrainingLobbyEntryChecks"
```

## 수동 확인

1. ASP.NET 서버와 Unity 에디터를 다시 실행하고 로그인합니다.
2. 비어 있는 로비에 입장해서 진행 상태와 경과 시간이 표시되는지 확인합니다.
3. 준비 중 `로비 입장 취소`를 누르면 로그인 상태로 목록에 돌아오는지 확인합니다.
4. 같은 로비에 다시 들어가 방 생성·참가가 가능한지 확인합니다.
5. 다른 사용자가 남아 있는 로비에서 입장을 취소해도 기존 사용자의 접속이 유지되는지 확인합니다.

서버 준비가 빠르면 취소 버튼이 짧게 보일 수 있습니다. 위 자동 검사는 입장 승인 직후에도 취소하는 상황을 재현합니다.
