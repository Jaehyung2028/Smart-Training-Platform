# Unity 첫 연동 — 로그인과 로비 목록

## 실행
1. ASP.NET 서버를 http 프로필로 실행합니다. 기본 HTTP 주소는 http://localhost:5229입니다.
2. Unity에서 Assets/SmartTraining/Scenes/ApiLobby.unity를 열고 Play를 누릅니다.
3. 서버 연결 확인 → 닉네임 입력 → 로그인 순서로 진행합니다.
4. 내 정보와 로비 목록을 조회합니다. 로비가 없으면 빈 목록이 정상입니다.
5. 새로고침 또는 로그아웃으로 상태를 확인합니다.
다른 PC의 서버를 사용할 때는 화면의 서버 주소를 해당 PC의 주소로 변경합니다.
localhost는 Unity가 실행 중인 PC를 뜻합니다.

## 파일과 함수
Scripts/Client/Api/ApiModels.cs: Unity JsonUtility용 public 필드 DTO. 서버의 camelCase JSON과 일치합니다.
Scripts/Client/Api/TrainingApiClient.cs: CheckHealth(), Login(), GetMe(), GetLobbies(), Logout().
WithSession(): 인증 요청 전에 토큰 만료 30초 이내면 refresh 요청 후 새 토큰을 사용합니다.
Send(): UnityWebRequest, 15초 제한, Bearer 인증, JSON 오류 처리, 401 세션 제거.
로그인 토큰은 메모리에만 보관하며 화면·로그·PlayerPrefs에 기록하지 않습니다.
요청은 순차 처리합니다. 호출자는 IEnumerator를 코루틴으로 실행합니다.
Scripts/Client/Controllers/ApiLobbyScreen.cs: OnGUI() 첫 연동 화면, Login(), LoadState(), CheckHealth().
요청 중 버튼을 비활성화합니다. 씬을 닫으면 요청을 중단하고 로컬 세션을 지웁니다.
Scenes/ApiLobby.unity: 화면 스크립트가 연결된 전용 씬. 기존 SampleScene은 유지합니다.
Editor/ApiSmokeTest.cs: Unity 에디터 배치 실행으로 실제 API·JsonUtility 응답을 검증합니다.

## 사용 API
GET /api/health
POST /api/Auth/login
GET /api/Auth/me
GET /api/Lobby
POST /api/Auth/refresh
POST /api/Auth/logout

## 범위
이번 단계는 API 연동 확인용 간단한 IMGUI 화면입니다.
로비 생성·가입 버튼, 콘텐츠 선택, 방 생성·참가, Netcode는 다음 단계입니다.
Netcode 패키지와 기존 패키지 설정은 변경하지 않았습니다.
서버 코드 변경은 없습니다.
에디터의 Play 종료 후에도 비동기로 서버 정리를 완료하도록 보완했습니다. STEP5-에디터종료보완.md를 참고하세요.
비정상 종료 시 기존 서버 세션 만료 정리 기능이 처리합니다.
HTTP는 로컬 개발 확인용 주소입니다. HTTPS 인증서 검증을 우회하는 코드는 없습니다.
## 검증 결과
Unity 6000.4.7f1의 설치된 라이브러리로 런타임 코드 빌드 성공(오류 0개).
같은 코드와 씬을 별도 최소 검증 프로젝트에서 Unity 에디터 배치 모드로 실행했습니다.
실제 ASP.NET 테스트 서버와 씬 로드·연결·로그인·내 정보·로비 항목·자동 갱신·로그아웃·비로그인 거부·서버 오류 총 9개 검증이 통과했습니다.
원본 프로젝트의 배치 실행은 시작 직후 종료되어 구체적인 원인을 확인하지 못했습니다.
따라서 원본의 전체 패키지 환경과 Play 화면·버튼 표시를 검증 완료한 것은 아닙니다.
Unity에서 ApiLobby 씬을 열고 Play로 최종 확인해주세요. 테스트 서버는 검증 후 종료했습니다.