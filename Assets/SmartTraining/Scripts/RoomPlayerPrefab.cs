using System;
using Unity.Netcode;
using UnityEngine;

namespace SmartTraining
{
    public static class RoomPlayerPrefab
    {
        public static GameObject Register(NetworkManager manager)
        {
            var prefab = Resources.Load<GameObject>("SmartTraining/RoomPlayer");
            if (prefab == null || prefab.GetComponent<NetworkObject>() == null || prefab.GetComponent<RoomPlayer>() == null)
                throw new InvalidOperationException("RoomPlayer 네트워크 프리팹을 찾을 수 없습니다.");
            manager.AddNetworkPrefab(prefab);
            return prefab;
        }
    }
}

