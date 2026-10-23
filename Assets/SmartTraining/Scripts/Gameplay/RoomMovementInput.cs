using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace SmartTraining
{
    public static class RoomMovementInput
    {
        internal static bool IsEditingText
        {
            get
            {
                var selected = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
                return selected != null && selected.TryGetComponent<UnityEngine.UI.InputField>(out var field) && field.isFocused;
            }
        }
        public static Vector2 Read()
        {
            if (!Application.isFocused || IsEditingText)
                return Vector2.zero;
            var value = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            var keys = Keyboard.current;
            if (keys == null) return value;
            if (keys.aKey.isPressed || keys.leftArrowKey.isPressed) value.x--;
            if (keys.dKey.isPressed || keys.rightArrowKey.isPressed) value.x++;
            if (keys.sKey.isPressed || keys.downArrowKey.isPressed) value.y--;
            if (keys.wKey.isPressed || keys.upArrowKey.isPressed) value.y++;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) value.x--;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) value.x++;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) value.y--;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) value.y++;
#endif
            return Vector2.ClampMagnitude(value, 1);
        }
    }
}
