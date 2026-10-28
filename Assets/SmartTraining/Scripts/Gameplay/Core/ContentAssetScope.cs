using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace SmartTraining
{
    // 콘텐츠 한 번의 실행에서 불러온 기본·추가 에셋을 함께 소유합니다.
    // 프리팹 인스턴스를 먼저 제거한 뒤 로드 핸들을 반환하며, 로딩 중 취소돼도 콜백으로 다시 생성하지 않습니다.
    public sealed class ContentAssetScope : IDisposable
    {
        private readonly List<AsyncOperationHandle> handles = new();

        private readonly List<GameObject> instances = new();

        private bool disposed;

        public IEnumerator LoadAsset<T>(string assetId, Action<T> loaded, Action<string> failed) where T : UnityEngine.Object
        {
            if (disposed)
            {
                yield break;
            }

            if (string.IsNullOrWhiteSpace(assetId))
            {
                failed?.Invoke("불러올 콘텐츠 에셋 주소가 없습니다.");
                yield break;
            }

            var handle = Addressables.LoadAssetAsync<T>(assetId);
            handles.Add(handle);
            yield return handle;
            if (disposed)
            {
                yield break;
            }

            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
            {
                failed?.Invoke($"콘텐츠 에셋을 불러오지 못했습니다: {assetId}");
                yield break;
            }

            loaded?.Invoke(handle.Result);
        }

        public IEnumerator Instantiate(string assetId, Transform parent, Action<GameObject> loaded, Action<string> failed)
        {
            yield return LoadAsset<GameObject>(assetId, prefab =>
            {
                var instance = UnityEngine.Object.Instantiate(prefab, parent, false);
                instances.Add(instance);
                loaded?.Invoke(instance);
            }, failed);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            for (int i = instances.Count - 1; i >= 0; i--)
            {
                if (instances[i] != null)
                {
                    instances[i].SetActive(false);
                    UnityEngine.Object.Destroy(instances[i]);
                }
            }

            instances.Clear();
            for (int i = handles.Count - 1; i >= 0; i--)
            {
                if (handles[i].IsValid())
                {
                    Addressables.Release(handles[i]);
                }
            }

            handles.Clear();
        }
    }
}
