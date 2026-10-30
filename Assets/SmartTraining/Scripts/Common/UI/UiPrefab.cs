using System;
using System.Collections.Generic;
using UnityEngine;

namespace SmartTraining
{
    // 표시 오브젝트는 저장된 프리팹에서 복제합니다. 배치·색상·글꼴은 프리팹에서 편집합니다.
    public static class UiPrefab
    {
        private static readonly Dictionary<string, UiPrefabReferences> templates = new();

        // 항상 존재하는 패널은 씬·부모 프리팹에 미리 연결된 인스턴스를 사용합니다.
        // 동적 목록 항목과 콘텐츠별 조작 요소만 Instantiate로 복제합니다.
        public static UiPrefabReferences Bind(string name, Transform parent)
        {
            foreach (Transform child in parent)
            {
                if (child.TryGetComponent<UiPrefabReferences>(out var view) && view.TemplateName == name)
                {
                    return view;
                }
            }

            throw new InvalidOperationException(parent.name + " 아래에 UI 프리팹을 미리 배치해주세요: " + name);
        }


        public static UiPrefabReferences Instantiate(string name, Transform parent)
        {
            if (!templates.TryGetValue(name, out var prefab) || prefab == null)
            {
                prefab = Resources.Load<UiPrefabReferences>("SmartTraining/UI/Templates/" + name);
                if (prefab == null)
                {
                    throw new InvalidOperationException("UI 프리팹을 찾을 수 없습니다: " + name);
                }

                templates[name] = prefab;
            }

            var instance = UnityEngine.Object.Instantiate(prefab, parent, false);
            instance.name = string.IsNullOrEmpty(prefab.InstanceName) ? prefab.name : prefab.InstanceName;
            return instance;
        }


        // 편집기 검사에서는 즉시 정리하고 플레이 중에는 프레임 종료 시 정리합니다.
        public static void Destroy(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
