using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace SmartTraining.Editor
{
    public static class FirstPersonCameraChecks
    {
        private sealed class TestInput : IFirstPersonCameraInput
        {
            public bool Looking => false;

            public bool Inspecting { get; set; }

            public Vector2 LookDelta => Vector2.zero;

            public bool Enabled { get; private set; }

            public bool Disposed { get; private set; }


            public void Enable()
            {
                Enabled = true;
            }


            public void Disable()
            {
                Enabled = false;
            }


            public void Dispose()
            {
                Disposed = true;
            }
        }


        [MenuItem("Smart Training/Content/Check First Person Camera")]
        public static void Run()
        {
            var target = new GameObject("Camera Check Target");
            var root = new GameObject("Independent Camera Check", typeof(Camera), typeof(FirstPersonCameraController));
            var input = new TestInput();
            try
            {
                var controller = root.GetComponent<FirstPersonCameraController>();
                bool allowed = true;
                controller.SetInputSource(input);
                controller.Bind(target.transform, () => allowed);
                controller.Configure(80, 1, false);
                Check(input.Enabled && controller.CanInteract, "콘텐츠 없이 입력 연결");
                input.Inspecting = true;
                controller.UpdateView(0.1f);
                Check(
                    controller.ViewCamera.fieldOfView < 80 && controller.ViewCamera.fieldOfView > 60,
                    "응시의 부드러운 확대"
                );
                controller.UpdateView(1);
                Check(Mathf.Abs(controller.ViewCamera.fieldOfView - 60) < 0.01f, "설정 화각 기준 확대");
                input.Inspecting = false;
                controller.UpdateView(1);
                Check(Mathf.Abs(controller.ViewCamera.fieldOfView - 80) < 0.01f, "놓으면 기본 화각 복귀");
                target.transform.position = new Vector3(2, 3, 4);
                controller.UpdateView(0.1f);
                Check(
                    Vector3.Distance(
                    root.transform.position,
                    target.transform.position + Vector3.up * 0.65f
                ) < 0.001f,
                    "추적 대상 변경 반영"
                );
                input.Inspecting = true;
                controller.UpdateView(1);
                allowed = false;
                controller.UpdateView(0);
                Check(
                    !controller.CanInteract && Mathf.Abs(controller.ViewCamera.fieldOfView - 80) < 0.01f,
                    "조작 차단 시 즉시 기본 화각 복귀"
                );
                controller.Configure(100, 1, false);
                Check(controller.ViewCamera.fieldOfView == 100, "외부 설정 적용");
                Check(
                    Vector2.Distance(controller.ToWorldMovement(Vector2.up), Vector2.up) < 0.001f,
                    "초기 시선 기준 이동 변환"
                );
                controller.SetInputSource(input);
                Check(!input.Disposed, "동일 입력 재연결 보존");
                // EditMode에서는 Unity가 PlayMode 수명주기를 호출하지 않으므로 해당 경계를 명시적으로 실행합니다.
                typeof(FirstPersonCameraController).GetMethod(
                    "OnDisable",
                    BindingFlags.Instance | BindingFlags.NonPublic
                ).Invoke(controller, null);
                root.SetActive(false);
                Check(!input.Enabled, "비활성화 시 입력 해제");
                root.SetActive(true);
                typeof(FirstPersonCameraController).GetMethod(
                    "OnEnable",
                    BindingFlags.Instance | BindingFlags.NonPublic
                ).Invoke(controller, null);
                Check(input.Enabled, "재활성화 시 입력 복구");
                UnityEngine.Object.DestroyImmediate(target);
                controller.UpdateView(0);
                Check(
                    !controller.CanInteract && controller.ViewCamera.fieldOfView == 100,
                    "대상 소멸 시 안전한 복귀"
                );
                typeof(FirstPersonCameraController).GetMethod(
                    "OnDisable",
                    BindingFlags.Instance | BindingFlags.NonPublic
                ).Invoke(controller, null);
                typeof(FirstPersonCameraController).GetMethod(
                    "OnDestroy",
                    BindingFlags.Instance | BindingFlags.NonPublic
                ).Invoke(controller, null);
                UnityEngine.Object.DestroyImmediate(root);
                Check(input.Disposed, "종료 시 입력 자원 해제");
                Debug.Log("FIRST PERSON CAMERA CHECKS PASSED (13)");
            }
            finally
            {
                if (root != null)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }

                if (target != null)
                {
                    UnityEngine.Object.DestroyImmediate(target);
                }
            }
        }


        private static void Check(bool condition, string name)
        {
            if (!condition)
            {
                throw new InvalidOperationException(name);
            }
        }
    }
}
