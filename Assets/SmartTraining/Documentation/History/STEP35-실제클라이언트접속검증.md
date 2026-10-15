# 실제 클라이언트 여러 개로 접속 검증

## 실제로 확인한 범위
Unity 개발 클라이언트 실행 파일을 서로 다른 프로세스 3개로 실행하고 ASP.NET 및 전용 Unity 방 서버와 연결했습니다. 같은 PC의 로컬 주소에서 실행한 자동 검사이며, 클라이언트는 창을 표시하지 않는 batchmode로 실행했습니다.

`MultiClientProbe`는 일반 실행에서는 활성화되지 않습니다. 개발 빌드에서 `--smarttraining-probe` 인자가 있을 때만 로그인 화면의 기존 함수들을 호출하고, Netcode 참가자·방장·준비·진행 상태와 화면 안내 데이터를 기록합니다. 버튼을 직접 클릭하거나 화면 배치를 확인하는 검사는 아닙니다. 별도 PC의 LAN 검증은 이번 새 검사 범위에 포함하지 않았습니다.

## 발견하고 수정한 문제
클라이언트 프로세스를 강제 종료하면 전용 서버가 포트를 열어 둔 채 UDP 수신을 멈추는 상황을 반복 재현했습니다. 남은 참가자는 방장 자동 위임을 잠시 수신했지만 이후 연결이 끊겼고, 새 참가자도 연결하지 못했습니다.

Unity Transport 2.7.2의 `UDPNetworkInterface.ReceiveJob.Execute()`가 실패한 Baselib 수신 완료의 버퍼를 반환하지 않았습니다. Windows의 ICMP 오류가 반복되면 수신 버퍼가 고갈됩니다. Unity 개발팀이 확인한 수정과 동일하게 실패 분기에서 `ReceiveQueue.ReleaseBuffer(bufferIndex)`를 호출했습니다.

근거: [Unity 개발팀 확인 및 수정 설명](https://discussions.unity.com/t/unity-transport-6-6-0-udp-host-stops-receiving-after-abrupt-client-disconnect-on-windows/1736023).

수정은 `Packages/com.unity.transport`에 기존 2.7.2 패키지를 임베드하여 유지합니다. 캐시 안의 파일을 직접 수정하는 방식이 아니며, 원본 라이선스 및 패키지 메타데이터를 보존했습니다. 패키지 내부 `SMART-TRAINING-PATCH.md`에 수정 범위와 향후 제거 조건을 적었습니다. 버전과 클라이언트/서버의 통신 규약은 유지합니다.

## 통과한 실제 프로세스 검사: 13개
- 서로 다른 프로세스 3개의 로그인
- 최초 참가자의 서버 접속 확정 및 방장 정보 수신
- 마지막 한 자리에 두 클라이언트가 참가 시 한 명만 성공
- 정원 초과 클라이언트에 오류 안내 및 예약 정리
- 두 참가자의 동일한 참가자 목록 수신
- 준비 및 진행 중 상태 동기화
- 진행 중 방의 새 입장 거부 및 예약 정리
- 대기 복귀와 준비 상태 초기화
- 실제 Transport 연결 종료 시 로그인 유지 및 재접속 대상 생성
- 재접속 시 사용자 ID 유지 및 참가자 중복 방지
- 방장 프로세스 강제 종료 후 남은 참가자에게 방장 위임
- 강제 종료 후 새 참가자 입장 및 두 클라이언트의 방장 일치
- 마지막 참가자 로그아웃 후 API 방 목록에서 빈 방 제거

## 변경 파일과 함수
- `Packages/com.unity.transport/Runtime/UDPNetworkInterface.cs`: `ReceiveJob.Execute()`의 실패한 수신 버퍼 반환
- `Assets/SmartTraining/Scripts/Diagnostics/MultiClientProbe.cs`: `StartIfRequested()`, `Update()`로 독립 프로세스 검증 지원. 릴리스 빌드에서는 제외됩니다.
- `Tests/MultiClient/Run-IndependentClients.ps1`: 독립 서버와 클라이언트 3개 실행, 명령·상태·로그 기록, 테스트에서 만든 프로세스만 종료
- `Packages/packages-lock.json`: Transport 항목을 임베드 패키지로 반영

ASP.NET의 방 관리 로직은 변경하지 않았습니다. 전용 서버와 개발 클라이언트 실행 파일은 수정된 Transport로 다시 빌드했습니다.

## 다시 실행하기
서버를 먼저 빌드하여 최신 방 서버 실행 파일이 API 출력 폴더에 복사되게 합니다. PowerShell에서 아래 경로는 본인의 저장소 경로에 맞게 지정합니다.

```powershell
dotnet build 'C:\Users\yafa7\Desktop\Projects\Smart Training Server\Smart Training Server\Smart Training Server\Smart Training Server.csproj'
powershell -NoProfile -ExecutionPolicy Bypass -File 'C:\Users\yafa7\Desktop\Projects\Smart Training Platform\Tests\MultiClient\Run-IndependentClients.ps1' -ServerProject 'C:\Users\yafa7\Desktop\Projects\Smart Training Server\Smart Training Server\Smart Training Server' -ClientExecutable 'C:\Users\yafa7\Desktop\Projects\Smart Training Platform\Builds\Client\Smart Training Client.exe' -EvidenceRoot "$env:TEMP\SmartTrainingMultiClientChecks"
```

검사용 API는 51420 포트를 사용합니다. 기존 5229 서버와 별도로 실행합니다. 로그와 상태 기록은 지정한 EvidenceRoot 아래 날짜별 폴더에 보관됩니다.

직접 화면으로 확인하려면 일반 개발 클라이언트와 에디터를 실행해 방장 프로세스 종료 후 자동 위임, 남은 참가자의 연결 유지, 새 참가자의 입장을 확인하세요. Unity가 프로젝트 내 패키지 변경을 읽고 컴파일을 끝낸 후 실행해야 합니다.
