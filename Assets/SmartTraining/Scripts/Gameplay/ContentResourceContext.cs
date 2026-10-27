using System;
using System.Collections;
using UnityEngine;

namespace SmartTraining
{
    // 기본 콘텐츠 프리팹의 부모에 제공됩니다. 콘텐츠 스크립트는 Start 이후 부모에서 찾아 추가 로딩에 사용합니다.
    // 생성·해제만 담당하며 공구 소유권, 영상 시간, 실습 단계의 멀티 동기화는 별도로 구현합니다.
    public sealed class ContentResourceContext : MonoBehaviour
    {
        private ContentAssetScope assets;

        public string ContentType { get; private set; }

        internal void Initialize(ContentAssetScope assets, string contentType)
        {
            this.assets = assets;
            ContentType = contentType;
        }

        public IEnumerator LoadAdditional<T>(string assetId, Action<T> loaded, Action<string> failed) where T : UnityEngine.Object
        {
            if (assets == null)
            {
                failed?.Invoke("콘텐츠 리소스가 아직 초기화되지 않았습니다.");
                yield break;
            }

            yield return assets.LoadAsset(assetId, loaded, failed);
        }

        public IEnumerator InstantiateAdditional(string assetId, Transform parent, Action<GameObject> loaded, Action<string> failed)
        {
            if (assets == null)
            {
                failed?.Invoke("콘텐츠 리소스가 아직 초기화되지 않았습니다.");
                yield break;
            }

            yield return assets.Instantiate(assetId, parent != null ? parent : transform, loaded, failed);
        }
    }
}
