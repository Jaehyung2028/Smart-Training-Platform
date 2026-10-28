using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace SmartTraining
{
    // 전용 서버의 시나리오 자원 수명주기입니다. 방 서버는 시작 여부와 확정된 에셋 주소만 전달합니다.
    public sealed class ScenarioServerSession : MonoBehaviour
    {
        private NetworkManager manager;
        private GameObject networkPrefab;
        private SharedScenario shared;
        private ContentAssetScope assets;
        private string activeAsset = "";

        public void Initialize(NetworkManager manager, GameObject networkPrefab)
        {
            this.manager = manager;
            this.networkPrefab = networkPrefab;
        }


        public void Synchronize(bool running, string assetId)
        {
            string desired = running ? assetId ?? "" : "";
            if (desired == activeAsset)
            {
                return;
            }

            Clear();
            activeAsset = desired;
            if (running && desired.Length > 0)
            {
                assets = new ContentAssetScope();
                StartCoroutine(Load(assets, desired));
            }
        }


        private IEnumerator Load(ContentAssetScope scope, string assetId)
        {
            yield return scope.LoadAsset<GameObject>(assetId, prefab =>
            {
                // 취소된 로딩은 ContentAssetScope가 콜백을 실행하지 않습니다.
                if (!prefab.TryGetComponent<ScenarioContent>(out var source) || !source.TryInitialize(out _))
                {
                    Debug.LogError("서버가 시나리오 원본을 확인하지 못했습니다: " + assetId);
                    return;
                }

                shared = Instantiate(networkPrefab).GetComponent<SharedScenario>();
                shared.InitializeServer(source);
                shared.NetworkObject.Spawn();
            }, Debug.LogError);
        }


        private void Clear()
        {
            if (shared != null)
            {
                if (shared.IsSpawned && manager != null && manager.IsListening)
                {
                    shared.NetworkObject.Despawn();
                }
                else
                {
                    Destroy(shared.gameObject);
                }

                shared = null;
            }

            assets?.Dispose();
            assets = null;
            activeAsset = "";
        }


        private void OnDisable()
        {
            Clear();
        }
    }
}
