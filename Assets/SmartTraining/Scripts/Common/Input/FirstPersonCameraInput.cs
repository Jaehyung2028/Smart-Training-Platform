using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SmartTraining
{
    // 입력 구현을 교체해도 카메라의 시점·확대 코드를 바꾸지 않도록 하는 공통 계약입니다.
    public interface IFirstPersonCameraInput : IDisposable
    {
        bool Looking { get; }
        bool Inspecting { get; }
        Vector2 LookDelta { get; }
        void Enable();
        void Disable();
    }

    // 입력 장치와 바인딩만 담당합니다. 콘텐츠·카메라·네트워크 상태를 알지 않습니다.
    public sealed class FirstPersonCameraInput : IFirstPersonCameraInput
    {
        private readonly InputActionMap actions = new("First Person Camera");
        private readonly InputAction lookHeld;
        private readonly InputAction lookDelta;
        private readonly InputAction inspectHeld;

        public FirstPersonCameraInput()
        {
            lookHeld = actions.AddAction("Look Held", InputActionType.Button, "<Mouse>/rightButton");
            lookDelta = actions.AddAction("Look Delta", InputActionType.Value, "<Mouse>/delta");
            inspectHeld = actions.AddAction("Inspect Held", InputActionType.Button, "<Mouse>/middleButton");
            lookHeld.wantsInitialStateCheck = inspectHeld.wantsInitialStateCheck = true;
        }

        public bool Looking => lookHeld.IsPressed();
        public bool Inspecting => inspectHeld.IsPressed();
        public Vector2 LookDelta => lookDelta.ReadValue<Vector2>();

        public void Enable()
        {
            actions.Enable();
        }

        public void Disable()
        {
            actions.Disable();
        }

        public void Dispose()
        {
            actions.Dispose();
        }
    }
}
