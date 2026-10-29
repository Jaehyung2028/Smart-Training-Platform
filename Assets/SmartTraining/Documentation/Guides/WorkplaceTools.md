# Workplace Tools 실습

장비 기초 교육 → 공구 8종 종합 실습을 선택합니다. 여덟 공구를 한 번씩 사용하는 작업과 마지막 결과 확인으로 총 9단계입니다. 기존 볼트·나사·망치 실습과 세 협동 실습도 새 공구 모델을 사용합니다. 공구가 필요한 시나리오의 기능 메뉴에는 8종 목록과 선택 해제가 표시되며 휠·스크롤바로 이동할 수 있습니다.

| 원본 모델 | 공구 ID | 표시 이름 | 종합 실습의 작업·모션 |
|---|---|---|---|
| Wrench_Combination | spanner | 콤비네이션 렌치 | 볼트 풀기: 볼트와 공구 회전·상승 |
| Screwdriver_Cross | screwdriver | 십자 드라이버 | 나사 풀기: 축 회전·상승 |
| Hammer_01 | hammer | 망치 | 핀 고정: 세 번 타격 |
| Axe | axe | 도끼 | 목재 쪼개기: 두 번 내려치기, 작업 후 두 조각 |
| Drill | drill | 전동 드릴 | 구멍 가공: 회전부 회전·전진·진동, 작업 후 구멍 |
| Pipe_Wrench | pipe-wrench | 파이프 렌치 | 배관 연결부 풀기: 연결부와 공구 회전 |
| Pliers | pliers | 플라이어 | 클립 뽑기: 양쪽 집게 닫힘·당기기 |
| Saw | saw | 톱 | 판재 자르기: 앞뒤 왕복, 작업 후 판재 분리 |

공구 선택은 개인별 상태이며, 서버가 현재 단계의 공구·대상·시간·작업자를 확인합니다. 손·공구·작업 대상과 가공 결과는 공동 진행 상태에 따라 표시합니다. 공구 동작은 서버 작업 진행률을 기준으로 위치·회전을 계산하므로 매 프레임 자세 RPC를 추가하지 않습니다. 취소하면 미완료 상태로 돌아가고, 다시 실습하면 공구 선택과 가공 결과도 초기화됩니다.

## 모델과 장착점

원본은 `Assets/Workplace Tools`에 두고, 손잡이 위치·크기를 맞춘 장착용 프리팹은 `Assets/SmartTraining/Content/Scenarios/Tools/Workplace`에 있습니다. 원본 모델·재질을 재사용하며 장착 모델의 Collider는 작업 클릭을 가로채지 않도록 비활성화합니다.

- 공구 프리팹의 `Work Contact`: 기본 접촉점 기준 표시
- 각 ScenarioDefinition의 `Tools → Work Contact Point`: 해당 콘텐츠에서 실제 사용할 접촉점
- `Tools → Work Rotation`: 작업 대상에 맞추는 공구 방향
- 작업 대상의 `Work Anchor`: 손과 공구가 접근할 위치·방향
- `ToolArticulation`: 드릴 회전부와 플라이어 집게 참조

교체 후 생성된 에셋·개별 작업 각도의 편집은 보존합니다. 공구 목록 전체를 확장하려면 `WorkplaceToolsBuilder`의 모델·ID·표시 이름 목록을 함께 수정합니다.

## 코드·데이터 위치

- 모델 생성·목록 연결: `WorkplaceToolsBuilder.Prepare()`, `ApplyCatalog()`
- 종합 실습 데이터·작업대: `Content/Scenarios/WorkplaceTools`, `WorkplaceToolsContentBuilder.Prepare()`
- Addressables 주소: `content/scenario/workplace-tools`
- 공구 선택 UI: `ScenarioToolMenu`
- 손·공구 작업 자세 연결: `ScenarioEquipmentView.RefreshPose()`
- 공구별 동작 계산: `ScenarioWorkMotion.ToolPose()`, `SpinAngle()`, `GripAmount()`
- 회전부·집게 표시: 공통 모듈 `ToolArticulation.SetState()`
- 단계별 가공 결과: `ScenarioStageVisuals`

이 모션은 가상 작업대에서 손·공구와 작업 결과를 표현하는 절차형 모션입니다. 원본 도구 에셋의 메시를 사용하며, 실제 장비 운용·가공 절차의 안내 자료는 아닙니다.
