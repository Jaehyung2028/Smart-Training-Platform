using System;
using UnityEditor;
using UnityEngine;

namespace SmartTraining.Editor
{
    // 미리 편집한 프리팹의 연결만 확인합니다. 빌드 중 모델·배치·데이터를 생성하거나 수정하지 않습니다.
    public static class VideoContentBuilder
    {
        public const string PrefabPath = "Assets/SmartTraining/Content/Videos/SafetyVideo.prefab";
        public const string AssetAddress = "content/video/safety-intro";

        public static void Prepare()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)?.GetComponent<VideoContentPlayer>() == null)
            {
                throw new InvalidOperationException("콘텐츠 프리팹과 VideoContentPlayer 연결을 확인해주세요: " + PrefabPath);
            }

            PrepareNetworkPrefab();
        }


        public static void PrepareNetworkPrefab()
        {
            const string path = "Assets/SmartTraining/Resources/SmartTraining/SharedVideo.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path)?.GetComponent<SharedVideo>() == null)
            {
                throw new InvalidOperationException("공유 상태 프리팹을 확인해주세요: " + path);
            }
        }
    }
}
