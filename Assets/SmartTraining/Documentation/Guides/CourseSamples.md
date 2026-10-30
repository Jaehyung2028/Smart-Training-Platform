# 교육과정 샘플 목록

서버 catalog.json이 목록의 원본입니다. 교육과정 3개에 모델·영상·실습 9개, 협동 실습 3개 (2인 권장 2개·4인 권장 1개)를 구성하여 총 12개가 실행됩니다. 분류 항목은 선택할 수 없고 최하위 항목만 방 콘텐츠가 됩니다.

## 트리 구조

```text
장비 기초 교육 (샘플)
  전동 드릴 구조 둘러보기 [model]
  사용 전 학습
    장비 사용 순서 안내 영상 [video]
    현장 연습
      작업 전 공구 점검 실습 [scenario]
  협동 정비 실습
    협동 장비 접점 정비 (2인) [scenario]
작업 안전 교육 (샘플)
  안전 이론
    작업 전 안전 수칙 영상 [video]
  안전 장비와 작업
    보호 장비 구조 둘러보기 [model]
    공구별 체결·타격 실습 [scenario]
  협동 복구 실습
    협동 방호 덮개 복구 (2인) [scenario]
비상 대응 교육 (샘플)
  비상 대응 순서 안내 영상 [video]
  현장 대응
    장비 이해
      소화기 구조 둘러보기 [model]
    대피 준비
      대피 전 안전 확인 실습 [scenario]
  협동 복구 실습
    협동 비상 유도등 복구 (4인) [scenario]
```

## 실행 리소스

| 콘텐츠 | 타입 | Addressables 주소 |
|---|---|---|
| 전동 드릴 구조 둘러보기 | model | `content/model/training-drill` |
| 장비 사용 순서 안내 영상 | video | `content/video/equipment-guide` |
| 작업 전 공구 점검 실습 | scenario | `content/scenario/tool-check` |
| 협동 장비 접점 정비 (2인) | scenario | `content/scenario/cooperative-maintenance` |
| 작업 전 안전 수칙 영상 | video | `content/video/safety-intro` |
| 보호 장비 구조 둘러보기 | model | `content/model/safety-equipment` |
| 공구별 체결·타격 실습 | scenario | `content/scenario/tool-assembly` |
| 비상 대응 순서 안내 영상 | video | `content/video/emergency-guide` |
| 소화기 구조 둘러보기 | model | `content/model/extinguisher` |
| 대피 전 안전 확인 실습 | scenario | `content/scenario/evacuation-check` |
| 협동 방호 덮개 복구 (2인) | scenario | `content/scenario/cooperative-safety` |
| 협동 비상 유도등 복구 (4인) | scenario | `content/scenario/cooperative-emergency` |

기존 드릴 모델·안전 영상·볼트 실습은 기존 폴더와 주소를 유지합니다. 추가 샘플 6개와 관련 클립·자막·재질·시나리오 데이터는 `Content`에 있습니다.

`SampleCourseBuilder.Prepare()`는 Addressables에 등록된 기존 프리팹과 연결을 확인합니다. 샘플 생성기는 제거했으며 배치·모델·단계는 직접 편집합니다. [프리팹 편집 안내](PrefabEditing.md)를 참고합니다.

추가 모델은 기본 도형으로 구성된 관찰용 모형입니다. 보호 장비와 소화기의 부품 설명을 포함하며 공유 회전·선택·반투명 표시를 재사용합니다. 세 모델 모두 5초 분해 클립과 단계 안내를 가지며 분해 재생·일시정지·재조립·슬라이더를 공유합니다. 보호 장비는 모자·챙·좌우 렌즈·연결부, 소화기는 안전핀·손잡이·호스·받침·용기를 순서대로 분리합니다. 교육용 구조 표현이며 실제 장비의 정비 절차를 뜻하지 않습니다. 클립과 Controller는 각 모델 폴더에서 편집할 수 있습니다.

추가 영상은 주제별 12초 안내 영상과 한국어 자막입니다. 실제 촬영물이 아닌 샘플입니다.

여섯 시나리오·협동 실습에는 공구 작업과 공구 없이 확인하는 단계를 함께 배치했습니다. 일반 실습 3개와 협동 실습 3개에서 각각 8종 공구가 모두 사용됩니다. 단계와 공구 배분은 [공구 실습 구성](WorkplaceTools.md)을 참고합니다.

카탈로그 변경 후 ASP.NET 서버를 재시작하고 Unity 서버·클라이언트를 최신 Addressables와 함께 빌드합니다.

추가 샘플은 Content/Models, Videos, Scenarios 아래에서 샘플 이름으로 찾습니다. 공용 재질은 Content/Shared/Materials에 있습니다.

협동 샘플의 역할·진행 순서와 편집 위치는 [협동 정비 실습](CooperativeMaintenance.md)을 참고합니다.
