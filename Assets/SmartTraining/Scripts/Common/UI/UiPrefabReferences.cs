using System;
using System.Collections.Generic;
using UnityEngine;

namespace SmartTraining
{
    // 프리팹의 표시 요소를 연결합니다. 계층 이름을 바꿔도 직렬화된 참조로 바인딩합니다.
    public sealed class UiPrefabReferences : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            public string key;
            public UnityEngine.Object value;
        }


        [SerializeField]
        private Entry[] entries = Array.Empty<Entry>();
        [SerializeField]
        private string instanceName;
        [SerializeField]
        private string templateName;

        public string TemplateName => templateName;

        public string InstanceName => instanceName;


        private Dictionary<string, UnityEngine.Object> lookup;

        public T Get<T>(string key)
            where T : UnityEngine.Object
        {
            if (lookup == null)
            {
                lookup = new Dictionary<string, UnityEngine.Object>(StringComparer.Ordinal);
                foreach (var entry in entries)
                {
                    lookup[entry.key] = entry.value;
                }
            }

            if (lookup.TryGetValue(key, out var value) && value is T result)
            {
                return result;
            }

            throw new InvalidOperationException(name + " 프리팹의 UI 참조를 확인해주세요: " + key);
        }
    }
}
