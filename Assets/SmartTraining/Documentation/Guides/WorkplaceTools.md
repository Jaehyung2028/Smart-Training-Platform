# 공구·점검 혼합 실습

별도 공구 8종 종합 실습은 제거했습니다. 기존 일반 시나리오 3개와 협동 콘텐츠 3개에 공구 작업을 분배합니다. 모든 콘텐츠는 8종 공구를 메뉴에서 고를 수 있으며, 각 콘텐츠의 작업에는 3~5종 공구와 무공구 점검이 섞입니다. 일반·협동 콘텐츠를 각각 모두 진행하면 두 묶음 모두 8종 공구를 사용합니다.

| 콘텐츠 | 실제 사용하는 공구 | 무공구 단계 | 전체 단계 |
|---|---|---|---|
| 작업 전 공구 점검 실습 | 콤비네이션 렌치, 플라이어, 전동 드릴, 망치 | 3 | 7 |
| 공구별 체결·타격 실습 | 콤비네이션 렌치, 십자 드라이버, 망치, 파이프 렌치, 톱 | 2 | 9 |
| 대피 전 안전 확인 실습 | 도끼, 톱, 파이프 렌치, 망치 | 3 | 7 |
| 협동 장비 접점 정비 | 콤비네이션 렌치, 플라이어, 전동 드릴, 십자 드라이버 | 3 | 8 |
| 협동 방호 덮개 복구 | 파이프 렌치, 망치, 전동 드릴, 톱 | 3 | 7 |
| 협동 비상 유도등 복구 | 도끼, 톱, 플라이어, 십자 드라이버 | 5 | 10 |

## 단계와 편집 위치

### 작업 전 공구 점검 실습

1. 전원 차단 확인 — 공구 없음
2. 점검 덮개 볼트 풀기 — 콤비네이션 렌치
3. 고정 클립 제거 — 플라이어
4. 공구 외관 확인 — 공구 없음
5. 시험 판재 가공 — 전동 드릴
6. 시험 핀 고정 — 망치
7. 작업 구역 최종 확인 — 공구 없음

데이터: `Content/Scenarios/ToolCheck/ToolCheck.asset`, 배치: `Content/Scenarios/ToolCheck/ToolCheck.prefab`

### 공구별 체결·타격 실습

1. 볼트 풀기 — 콤비네이션 렌치
2. 볼트 다시 체결 — 콤비네이션 렌치
3. 나사 풀기 — 십자 드라이버
4. 나사 다시 체결 — 십자 드라이버
5. 핀 고정 — 망치
6. 핀 고정 상태 확인 — 공구 없음
7. 배관 연결부 풀기 — 파이프 렌치
8. 보강 판재 자르기 — 톱
9. 전체 작업 결과 확인 — 공구 없음

데이터: `Content/Scenarios/ToolAssembly.asset`, 배치: `Content/Scenarios/ToolAssembly.prefab`

### 대피 전 안전 확인 실습

1. 비상 알림 확인 — 공구 없음
2. 훈련용 목재 장애물 분리 — 도끼
3. 통로 판재 자르기 — 톱
4. 대피 방향 확인 — 공구 없음
5. 훈련용 배관 연결부 풀기 — 파이프 렌치
6. 유도 표지 핀 고정 — 망치
7. 집결 장소 최종 확인 — 공구 없음

데이터: `Content/Scenarios/EvacuationCheck/EvacuationCheck.asset`, 배치: `Content/Scenarios/EvacuationCheck/EvacuationCheck.prefab`

### 협동 장비 접점 정비

1. 고장 표시 확인 — 공구 없음
2. 덮개 볼트 풀기 — 콤비네이션 렌치
3. 접점 고정 클립 제거 — 플라이어
4. 불량 접점 교체 — 공구 없음
5. 보강판 구멍 가공 — 전동 드릴
6. 보강판 점검 나사 풀기 — 십자 드라이버
7. 보강판 점검 나사 체결 — 십자 드라이버
8. 공동 작동 확인 — 공구 없음

데이터: `Content/Scenarios/CooperativeMaintenance/CooperativeMaintenance.asset`, 배치: `Content/Scenarios/CooperativeMaintenance/CooperativeMaintenance.prefab`

### 협동 방호 덮개 복구

1. 방호 덮개 이상 확인 — 공구 없음
2. 보조 배관 연결부 풀기 — 파이프 렌치
3. 덮개 고정 핀 장착 — 망치
4. 방호 덮개 닫기 — 공구 없음
5. 보강판 구멍 가공 — 전동 드릴
6. 보호 판재 길이 조정 — 톱
7. 덮개 확인 스위치 점검 — 공구 없음

데이터: `Content/Scenarios/CooperativeSafety/CooperativeSafety.asset`, 배치: `Content/Scenarios/CooperativeSafety/CooperativeSafety.prefab`

### 협동 비상 유도등 복구

1. 복구 순서 확인 — 공구 없음
2. 훈련용 전원 차단 확인 — 공구 없음
3. 비상 유도등 상태 확인 — 공구 없음
4. 목재 장애물 분리 — 도끼
5. 보호 판재 자르기 — 톱
6. 고정 클립 제거 — 플라이어
7. 배터리 덮개 나사 풀기 — 십자 드라이버
8. 훈련용 배터리 교체 — 공구 없음
9. 배터리 덮개 나사 체결 — 십자 드라이버
10. 비상 유도등 점등 확인 — 공구 없음

데이터: `Content/Scenarios/CooperativeEmergency/CooperativeEmergency.asset`, 배치: `Content/Scenarios/CooperativeEmergency/CooperativeEmergency.prefab`

## 공통 모듈

- 작업 모션: `ScenarioWorkMotion`, 공구의 접촉점·작업 각도는 각 데이터의 Tools에서 편집
- 역할·개인 공구 선택·작업 검사는 기존 `ScenarioProgress`와 `SharedScenario`를 재사용

이번 구성이 적용된 샘플은 이후 빌드에서 덮어쓰지 않습니다. 공구 원본 에셋과 공용 장착 프리팹은 유지합니다. 가상 작업대의 모형을 사용하는 기능 샘플이며 실제 현장의 작업 절차를 의미하지 않습니다.

외형·단계 편집과 새 연결 방식은 [프리팹 편집 안내](PrefabEditing.md)를 참고합니다.
