using System;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace SmartTraining.Editor
{
    // Addressables에 연결된 콘텐츠를 확인하는 빌드 진입점입니다. 표시·단계·애니메이션을 덮어쓰지 않습니다.
    public static class SampleCourseBuilder
    {
        public const string DirectoryPath = "Assets/SmartTraining/Content";

        [MenuItem("Smart Training/Content/Check Registered Content Prefabs")]
        public static void Prepare()
        {
            ModelContentBuilder.Prepare();
            VideoContentBuilder.Prepare();
            ScenarioContentBuilder.Prepare();
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                throw new InvalidOperationException("Addressables에 콘텐츠 프리팹을 등록해주세요.");
            }

            var entries = settings.groups.Where(group => group != null).SelectMany(group => group.entries).Where(entry => entry.address.StartsWith("content/", StringComparison.Ordinal)).ToArray();
            foreach (var entry in entries)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.AssetPath);
                if (prefab == null)
                {
                    throw new InvalidOperationException("콘텐츠 프리팹을 찾을 수 없습니다: " + entry.address);
                }

                var scenario = prefab.GetComponent<ScenarioContent>();
                if (scenario != null && !scenario.TryInitialize(out var error))
                {
                    throw new InvalidOperationException(entry.address + ": " + error);
                }
            }

            Debug.Log("REGISTERED CONTENT PREFABS READY: " + entries.Length);
        }
    }
}
