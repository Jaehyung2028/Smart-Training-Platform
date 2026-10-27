# 통신 코드와 라이브러리의 역할

## 서버 이벤트를 등록하는 곳

`RoomHubConnection.RegisterEvents()`에서 Microsoft 라이브러리의 `connection.On<T>()`로 등록합니다.
서버가 보내는 이름과 인수 개수·형식은 기존과 같습니다.

```csharp
connection.On<string>("LobbyChat", json => Enqueue(() => LobbyChat?.Invoke(json)));
```

SignalR 수신은 Unity 메인 스레드가 아닐 수 있으므로 화면 변경은 큐에 넣습니다.
`Tick()`은 큐에 담긴 화면 콜백만 실행합니다.
핸드셰이크·WebSocket 메시지 조립·요청 ID·요청 대응 사전·핑 타이머는 직접 관리하지 않습니다.

`ApiLobbyScreen.ConnectLobbyRooms()`는 수신한 이벤트를 로비 갱신·채팅 처리와 연결합니다.
전용 서버의 `RoomDedicatedServer`도 같은 연결 어댑터로 등록·강퇴·접속 보고를 처리합니다.
프로젝트의 재접속 로직이 세션 재확인과 재구독을 담당하므로 SDK 자동 재접속을 중복으로 활성화하지 않습니다.
요청 시간 초과 시 서버 처리 여부가 불명확한 변경 요청을 자동 재전송하지 않고 연결을 닫아 실제 상태를 재조회합니다.

## JSON

`TrainingJson`에 `System.Text.Json` 설정을 모읍니다.
Unity DTO의 public 필드를 유지하려고 `IncludeFields`를 활성화합니다.
목록 응답은 `RoomSummary[]`, `ContentSummary[]`처럼 배열로 바로 읽습니다.
과거 JsonUtility의 최상위 배열 제한을 우회하던 목록 래퍼 클래스는 필요하지 않습니다.
에디터 상태 저장·Unity UI 미리보기 등 Unity 객체를 다루는 곳은 네이티브 JsonUtility를 유지합니다.

## 라이브러리로 대체한 부분과 유지한 부분

| 영역 | 처리 주체 | 남겨 둔 프로젝트 코드 |
|---|---|---|
| SignalR 프로토콜·연결 유지·요청 응답 | Microsoft SignalR Client | 메인 스레드 전달, 요청 제한 시간, 기존 재접속 흐름 연결 |
| HTTP 요청 | UnityWebRequest | 로그인 토큰 첨부, 세션 갱신, 사용자 취소 |
| HTTP·채팅 JSON과 배열 | System.Text.Json | DTO 필드 설정과 공통 형식 오류 변환 |
| 플레이어 네트워크 | Unity Netcode·Unity Transport | 준비·방장·이동 허용 규칙 |
| 콘텐츠 로딩 | Unity Addressables | 한 콘텐츠 실행에서 사용한 리소스 수명 관리 |
| 개인 설정 | Unity PlayerPrefs | 저장·취소·기본값 규칙 |
| 인증 헤더 구문 | .NET AuthenticationHeaderValue | 유효한 세션과 로비 소속 검사 |
| 비밀번호·토큰 암호 연산 | .NET 암호화 API | 방 비밀번호 규칙과 세션 만료 정책 |
| 프로세스·네트워크 조회 | .NET Process·NetworkInterface | 포트 배정과 실행한 방 서버의 종료 정리 |
| Windows 자식 프로세스 일괄 종료 | Windows Job Object | OS API 연결과 핸들 정리 |

방장·예약·준비·운영 상태 등은 프로젝트의 업무 규칙입니다.
이를 라이브러리로 바꾸기 위해 인증 방식이나 서버 API를 변경하지 않습니다.
ASP.NET 공통 오류 필터도 클라이언트가 사용하는 기존 오류 JSON을 유지하기 위해 남깁니다.

## 라이브러리 파일 관리

Microsoft SignalR 8.0.33과 종속 DLL은 `Assets/Plugins/SignalR`에 있습니다.
`Tools/SignalRDependencies/SignalRDependencies.csproj`와 `packages.lock.json`으로 버전·종속성을 고정합니다.
`Sync-UnityLibraries.ps1`은 고정한 패키지를 복원해 Unity용 DLL을 다시 배치합니다.
Unity 런타임이 이미 제공하는 System.Memory·Buffers·Tasks.Extensions·Numerics.Vectors는 중복으로 가져오지 않습니다.
`link.xml`은 JSON 리플렉션에 필요한 데이터와 SignalR 관련 코드를 보존합니다.
현재 프로젝트의 주 실행 대상은 Windows 에디터·Standalone·전용 서버입니다.
