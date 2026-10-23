using Unity.Netcode;
using UnityEngine;

namespace SmartTraining
{
    public sealed class NetworkShutdownCleanup : MonoBehaviour
    {
        private NetworkManager manager;
        public void Begin(NetworkManager value)
        {
            manager = value;
            manager.Shutdown(true);
        }
        private void Update()
        {
            if (manager == null || (!manager.IsListening && !manager.ShutdownInProgress))
                Destroy(gameObject);
        }
    }
}
