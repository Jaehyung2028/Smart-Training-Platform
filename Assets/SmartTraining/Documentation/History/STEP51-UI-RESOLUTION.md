# UGUI 해상도 대응

## 수정한 부분

- `LobbyUI`, `LobbyInteriorUI`, `RoomUI`의 `CanvasScaler`는 `Scale With Screen Size`, 기준 해상도 `1280 × 800`, `Screen Match Mode = Expand`를 사용합니다. 가로·세로 비율이 달라도 기준 배치에 필요한 공간을 확보합니다.
- `LobbyInteriorUI`의 로비 채팅 입력칸 높이를 부모 줄과 동일한 36으로 맞췄습니다.
- `LobbyUI`의 `Lobby Entry Notice`를 `Lobby Scroll/Viewport/Content`의 첫 번째 항목으로 옮겼습니다. 입장 오류 안내가 떠도 목록 아래의 상세 안내·입장 버튼 공간을 밀어내지 않습니다. 안내와 목록은 함께 스크롤합니다.
- `LobbyUguiView.Refresh()`는 새로운 오류 안내가 표시되면 목록을 위로 스크롤합니다. `displayedFailure`로 안내가 바뀐 경우만 처리하므로 사용자가 목록을 스크롤하는 것을 계속 방해하지 않습니다.
- 세 `UguiBuilder.EnsureAssets()`의 신규 생성 설정도 동일하게 반영했습니다. 기존 프리팹을 삭제하거나 새로 만들 필요는 없습니다.

색상·글꼴·버튼 구성·서버 통신은 유지했습니다. 작은 창에서는 전체 UI도 축소됩니다. 이번 변경은 데스크톱 창 크기와 화면 비율 대응이며 모바일 전용 화면 구성은 포함하지 않습니다.

## 직접 확인

1. Unity Game 창의 해상도를 `800 × 600`, `1280 × 720`, `1920 × 1080`으로 바꿔 로그인·로비·방 화면을 확인합니다.
2. `1600 × 600` 같은 넓고 낮은 비율에서도 방의 참가자·채팅 목록과 아래 퇴장 버튼이 겹치지 않는지 확인합니다.
3. 새 방 만들기, 방장 설정, 위치 창을 열어 입력칸과 닫기 버튼을 확인합니다. 방장 설정 내용은 스크롤로 이동합니다.
4. 입장 오류가 발생하면 목록 위에 안내가 나타나고 닫기·재시도 영역을 스크롤로 볼 수 있는지 확인합니다.
5. UI를 수정할 때는 기존 `Smart Training → UI → Scene UI Editor`를 사용합니다. `CanvasScaler`와 이번에 옮긴 안내 위치도 Inspector와 Hierarchy에서 확인할 수 있습니다.

## 자동 검사

- `UguiResolutionChecks.Run()` : 별도 복사 프로젝트의 배치 모드에서 6개 해상도 × 14개 화면 상태를 계산합니다. 일반 패널 자식의 넘침과 스크롤 영역 축소를 검사하며 씬을 저장하지 않습니다.
- `Tests/MultiClient/Run-UguiResolution.ps1` : 빌드된 ASP.NET 프로젝트 경로 `-ServerProject`, Development Build 실행 파일 `-ClientExecutable`, 결과 폴더 `-EvidenceRoot`를 지정합니다. 독립 API 포트 51465를 사용합니다. 실제 창 크기를 바꾸며 입력 도달 여부와 화면 캡처를 확인합니다. 오류 안내 배치만 테스트 명령으로 재현하며 서버 입장 로직은 변경하지 않습니다.

검사는 실제 창에서 수행하는 시각적 확인을 보조합니다. 사용 중인 모니터의 글자 가독성과 원하는 디자인은 별도로 확인해주세요.
