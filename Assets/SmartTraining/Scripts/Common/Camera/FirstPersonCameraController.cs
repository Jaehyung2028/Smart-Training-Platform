using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SmartTraining
{
    // 어떤 콘텐츠 씬에서도 재사용하는 로컬 카메라입니다. 대상 Transform과 조작 허용 조건만 전달받습니다.
    // 콘텐츠 세션의 모델 상호작용보다 먼저 카메라를 갱신하되 플레이어의 Update 이동 뒤에 추적합니다.
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class FirstPersonCameraController : MonoBehaviour
    {
        [SerializeField]
        private Vector3 eyeOffset = new(0, 0.65f, 0);
        [SerializeField, Range(1, 89)]
        private float pitchLimit = 85;
        [SerializeField, Range(0.1f, 1)]
        private float inspectionRatio = 0.75f;

        private Camera viewCamera;
        private IFirstPersonCameraInput input;
        private Transform followTarget;
        private Func<bool> inputAllowed;
        private float yaw, pitch;
        private float zoom = 1;
        private float fieldOfView = 60;
        private float sensitivity = 1;
        private bool invertY;
        private int lookInputStartsAtFrame;

        public Camera ViewCamera => viewCamera;
        public float Yaw => yaw;
        public bool CanInteract => isActiveAndEnabled && followTarget != null &&
            Application.isFocused && (inputAllowed?.Invoke() ?? true);

        private void Awake()
        {
            EnsureInitialized();
        }

        // 비활성 프리팹에 먼저 설정·입력을 연결하는 경우에도 Awake 실행 순서에 의존하지 않습니다.
        private void EnsureInitialized()
        {
            if (viewCamera == null)
            {
                viewCamera = GetComponent<Camera>();
                viewCamera.nearClipPlane = 0.05f;
                viewCamera.clearFlags = CameraClearFlags.SolidColor;
                viewCamera.backgroundColor = new Color32(148, 163, 184, 255);
            }
            input ??= new FirstPersonCameraInput();
        }

        private void OnEnable()
        {
            EnsureInitialized();
            input.Enable();
            lookInputStartsAtFrame = Time.frameCount + 2;
        }

        // 카메라가 입력 객체의 활성화·해제를 소유합니다. 키 설정·다른 입력 장치 구현도 교체할 수 있습니다.
        public void SetInputSource(IFirstPersonCameraInput source)
        {
            EnsureInitialized();
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            if (ReferenceEquals(input, source))
            {
                return;
            }
            input?.Disable();
            input?.Dispose();
            input = source;
            if (isActiveAndEnabled)
            {
                input.Enable();
            }
        }

        // 씬이나 사용 측의 타입 대신 추적 대상과 조작 허용 조건만 연결합니다.
        public void Bind(Transform target, Func<bool> canUseInput = null)
        {
            EnsureInitialized();
            followTarget = target;
            inputAllowed = canUseInput;
            yaw = pitch = 0;
            ResetZoom();
            lookInputStartsAtFrame = Time.frameCount + 2;
        }

        // 설정 저장은 사용 측에서 담당하고, 이 모듈은 현재 설정값만 적용합니다.
        public void Configure(float baseFieldOfView, float lookSensitivity, bool invertVertical)
        {
            EnsureInitialized();
            fieldOfView = Mathf.Clamp(baseFieldOfView, 30, 120);
            sensitivity = Mathf.Max(0, lookSensitivity);
            invertY = invertVertical;
            viewCamera.fieldOfView = fieldOfView * zoom;
        }

        private void LateUpdate()
        {
            UpdateView(Time.unscaledDeltaTime);
        }

        // Unity 갱신은 위에서 연결하고 실제 카메라 처리는 독립적으로 실행할 수 있게 둡니다.
        public void UpdateView(float deltaTime)
        {
            bool canLook = CanInteract;
            UpdateZoom(canLook, deltaTime);
            if (followTarget == null)
            {
                return;
            }
            if (canLook && Time.frameCount >= lookInputStartsAtFrame && input.Looking)
            {
                var delta = input.LookDelta;
                float vertical = invertY ? delta.y : -delta.y;
                yaw = Mathf.Repeat(yaw + delta.x * 0.12f * sensitivity, 360);
                pitch = Mathf.Clamp(pitch + vertical * 0.12f * sensitivity, -pitchLimit, pitchLimit);
            }
            transform.SetPositionAndRotation(followTarget.position + eyeOffset, Quaternion.Euler(pitch, yaw, 0));
        }

        private void UpdateZoom(bool canLook, float deltaTime)
        {
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            float target = canLook && !overUi && input.Inspecting ? inspectionRatio : 1;
            // 메뉴·포커스 상실 시 즉시 복귀하고, 일반 누르기·놓기는 부드럽게 전환합니다.
            zoom = canLook ? Mathf.Lerp(zoom, target, 1 - Mathf.Exp(-14 * Mathf.Max(0, deltaTime))) : 1;
            if (Mathf.Abs(zoom - target) < 0.001f)
            {
                zoom = target;
            }
            viewCamera.fieldOfView = fieldOfView * zoom;
        }

        // 전달받은 이동 입력을 시선 방향으로 변환합니다. 이동 자체와 네트워크 전송은 담당하지 않습니다.
        public Vector2 ToWorldMovement(Vector2 movement)
        {
            var world = Quaternion.Euler(0, yaw, 0) * new Vector3(movement.x, 0, movement.y);
            return new Vector2(world.x, world.z);
        }

        private void ResetZoom()
        {
            zoom = 1;
            if (viewCamera != null)
            {
                viewCamera.fieldOfView = fieldOfView;
            }
        }

        private void OnDisable()
        {
            input?.Disable();
            ResetZoom();
        }

        private void OnDestroy()
        {
            input?.Dispose();
            input = null;
            inputAllowed = null;
            followTarget = null;
        }
    }
}
