using System;
using UnityEditor;
using Unity.Netcode.Components;
using UnityEngine;

namespace SmartTraining.Editor
{
    public static class RoomPlayerPrefabBuilder
    {
        // 사용자 모델·Animator·이름표 참조를 그대로 사용합니다. 빌드 중 재생성하거나 재배치하지 않습니다.
        public static void Create()
        {
            const string path = "Assets/SmartTraining/Resources/SmartTraining/RoomPlayer.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var transform = prefab != null ? prefab.GetComponent<NetworkTransform>() : null;
            var body = prefab != null ? prefab.GetComponent<Rigidbody>() : null;
            var physics = prefab != null ? prefab.GetComponent<NetworkRigidbody>() : null;
            var capsule = prefab != null ? prefab.GetComponent<CapsuleCollider>() : null;
            if (body == null ||
                physics == null ||
                capsule == null ||
                capsule.isTrigger ||
                !physics.AutoUpdateKinematicState ||
                !physics.UseRigidBodyForMotion ||
                prefab?.GetComponent<RoomPlayer>() == null ||
                transform == null ||
                transform.AuthorityMode != NetworkTransform.AuthorityModes.Server ||
                !transform.Interpolate ||
                !transform.SyncPositionX ||
                !transform.SyncPositionY ||
                !transform.SyncPositionZ ||
                transform.SyncRotAngleX ||
                !transform.SyncRotAngleY ||
                transform.SyncRotAngleZ ||
                transform.SyncScaleX ||
                transform.SyncScaleY ||
                transform.SyncScaleZ)
            {
                throw new InvalidOperationException("RoomPlayer 프리팹의 Rigidbody·CapsuleCollider·NetworkRigidbody와 서버 권한 NetworkTransform 설정을 확인해주세요: " + path);
            }
        }
    }
}
