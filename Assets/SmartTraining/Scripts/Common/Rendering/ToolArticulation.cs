using UnityEngine;

namespace SmartTraining
{
    // 도구의 회전부·집게만 제어하는 표시 모듈입니다. 작업 규칙이나 네트워크를 조회하지 않습니다.
    public sealed class ToolArticulation : MonoBehaviour
    {
        [SerializeField]
        private Transform spinningPart;
        [SerializeField]
        private Vector3 spinningAxis = Vector3.forward;
        [SerializeField]
        private Transform leftJaw, rightJaw;
        [SerializeField]
        private Vector3 jawAxis = Vector3.up;
        private Quaternion spinRest, leftRest, rightRest;
        private float previousSpin, previousGrip;
        private bool initialized;
        public float SpinAngle => previousSpin;
        public float GripAmount => previousGrip;

        private void Awake()
        {
            spinRest = spinningPart != null ? spinningPart.localRotation : Quaternion.identity;
            leftRest = leftJaw != null ? leftJaw.localRotation : Quaternion.identity;
            rightRest = rightJaw != null ? rightJaw.localRotation : Quaternion.identity;
        }


        public void SetState(float spinAngle, float grip)
        {
            if (initialized && spinAngle == previousSpin && grip == previousGrip)
            {
                return;
            }

            initialized = true;
            previousSpin = spinAngle;
            previousGrip = grip;
            if (spinningPart != null)
            {
                spinningPart.localRotation = spinRest * Quaternion.AngleAxis(spinAngle, spinningAxis);
            }

            float opening = 8 * (1 - Mathf.Clamp01(grip));
            if (leftJaw != null)
            {
                leftJaw.localRotation = leftRest * Quaternion.AngleAxis(opening, jawAxis);
            }

            if (rightJaw != null)
            {
                rightJaw.localRotation = rightRest * Quaternion.AngleAxis(-opening, jawAxis);
            }
        }
    }
}
