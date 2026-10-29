using UnityEngine;

namespace SmartTraining
{
    // 손 모델과 아이템의 장착 지점만 정의합니다. 콘텐츠나 네트워크 상태는 알지 못합니다.
    public sealed class HandheldItemRig : MonoBehaviour
    {
        [SerializeField]
        private Transform grip;

        public Transform Grip => grip;
    }
}
