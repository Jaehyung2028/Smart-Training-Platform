using System;
using UnityEngine;

namespace SmartTraining
{
    // 전달받은 손·아이템 프리팹의 표시 수명만 관리합니다. 선택 판정과 동기화는 호출 측의 역할입니다.
    public sealed class HeldItemView : IDisposable
    {
        private readonly HandheldItemRig rig;
        private GameObject item;
        private GameObject currentPrefab;
        private ToolArticulation articulation;

        public HeldItemView(HandheldItemRig prefab, string name)
        {
            rig = UnityEngine.Object.Instantiate(prefab);
            rig.name = name;
        }


        public void Attach(Transform parent, Vector3 position, Quaternion rotation)
        {
            rig.transform.SetParent(parent, false);
            rig.transform.localPosition = position;
            rig.transform.localRotation = rotation;
        }


        public Vector3 Position => rig.transform.position;

        public Quaternion Rotation => rig.transform.rotation;


        public void SetPose(Vector3 position, Quaternion rotation)
        {
            rig.transform.SetPositionAndRotation(position, rotation);
        }


        // 아이템의 접촉점이 지정한 월드 위치·방향에 맞도록 손 루트의 자세를 계산합니다.
        // Grip과 모델의 배치가 달라도 콘텐츠 측에서 보정 수치를 다시 계산할 필요가 없습니다.
        public void GetContactPose(
            Vector3 localContactPoint,
            Vector3 worldPoint,
            Quaternion worldRotation,
            out Vector3 position,
            out Quaternion rotation
        )
        {
            var rigTransform = rig.transform;
            Quaternion itemRotation = Quaternion.Inverse(rigTransform.rotation) * item.transform.rotation;
            rotation = worldRotation * Quaternion.Inverse(itemRotation);
            Vector3 point = rigTransform.InverseTransformPoint(item.transform.TransformPoint(localContactPoint));
            position = worldPoint - rotation * Vector3.Scale(rigTransform.lossyScale, point);
        }


        public void SetItem(GameObject prefab)
        {
            if (currentPrefab == prefab)
            {
                return;
            }

            if (item != null)
            {
                item.SetActive(false);
                UnityEngine.Object.Destroy(item);
            }

            articulation = null;
            currentPrefab = prefab;
            item = prefab != null ? UnityEngine.Object.Instantiate(prefab, rig.Grip, false) : null;
            if (item != null)
            {
                item.name = prefab.name;
                articulation = item.GetComponent<ToolArticulation>();
                // 장착용 모델이 실습 클릭이나 이동 충돌을 가로채지 않도록 합니다.
                foreach (var collider in item.GetComponentsInChildren<Collider>(true))
                {
                    collider.enabled = false;
                }
            }
        }


        public void SetArticulation(float spinAngle, float grip)
        {
            articulation?.SetState(spinAngle, grip);
        }


        public void Dispose()
        {
            if (rig != null)
            {
                rig.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(rig.gameObject);
            }
        }
    }
}
