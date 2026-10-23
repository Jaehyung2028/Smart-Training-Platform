# Windows 테스트 빌드 만들기

Unity 메뉴 **Smart Training → Build → Build Windows Test Bundle**을 실행합니다. `TrainingTestBundleBuild.Build()`가 전용 서버와 Development 클라이언트를 차례로 빌드합니다. Windows Dedicated Server 빌드 모듈이 설치되어 있어야 합니다.

결과는 `Builds/TestBundles/날짜-고유번호/`에 생성됩니다. 매번 새 폴더를 사용하므로 기존 실행 파일을 덮어쓰지 않습니다.

```text
날짜-고유번호/
├─ Client/                 클라이언트 실행 파일과 필요한 파일
├─ RoomServer/             방 전용 서버 실행 파일과 필요한 파일
├─ RUNNING.txt             실행 순서와 API 연결 안내
└─ build-manifest.json     두 빌드가 완료된 경우에만 생성되는 결과 정보
```

## 실행 순서

1. ASP.NET 서버가 새 `RoomServer/Smart Training Room.exe`를 사용하도록 `Room:ServerExecutablePath`를 설정합니다. 기존 기본 경로를 유지한다면 API 실행 폴더의 `RoomServer`를 새 폴더 전체로 교체합니다. 실행 중인 전용 서버가 없는 상태에서 교체해주세요.
2. ASP.NET 서버를 실행합니다. Unity 전용 서버는 입장 요청을 받으면 기존 온디맨드 방식으로 실행됩니다.
3. `Client/Smart Training Client.exe`를 실행하고 API 서버 주소를 입력합니다. 다른 PC에서는 서버 PC의 주소를 사용합니다.
4. 클라이언트를 두 개 실행할 때는 서로 다른 닉네임으로 같은 로비·방에 접속합니다.


실행 파일 하나만 복사하면 동작하지 않습니다. `_Data`, `UnityPlayer.dll` 등 같은 폴더에 생성된 파일들을 함께 보관합니다. 이 메뉴는 API 프로젝트의 설정이나 실행 중인 서버를 자동 변경하지 않습니다.

## 빌드 동작

- 기존 `RoomServerBuild.Build()`와 `RoomClientBuild.Build()`를 재사용합니다. 개별 빌드 메뉴도 유지합니다.
- 성공하면 결과 폴더를 열고 `build-manifest.json`을 작성합니다. 실패한 빌드는 완료 표시를 만들지 않고 `build-error.txt`에 오류를 남깁니다.
- 임시 출력 경로 환경 변수와 HTTP 설정은 작업 종료 시 원래 값으로 복원합니다.
- `SMART_TRAINING_TEST_BUNDLE_ROOT`로 결과 상위 폴더를 지정할 수 있습니다. 자동 실행은 Unity의 `-executeMethod SmartTraining.Editor.TrainingTestBundleBuild.Build`를 사용합니다.

현재 클라이언트는 기존과 같은 Development Build입니다. 정식 서비스 배포 설정과 코드 서명·설치 프로그램은 별도 작업 대상입니다.
