using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SmartTraining
{
    [RequireComponent(typeof(Button))]
    public sealed class RoomMoveButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public Vector2 direction;
        public bool IsHeld
        {
            get; private set;
        }
        private int pointerId;
        public void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left || !GetComponent<Button>().IsInteractable())
                return;
            IsHeld = true;
            pointerId = data.pointerId;
        }
        public void OnPointerUp(PointerEventData data)
        {
            if (data.pointerId == pointerId)
                IsHeld = false;
        }
        public void OnPointerExit(PointerEventData data)
        {
            if (data.pointerId == pointerId)
                IsHeld = false;
        }
        private void OnDisable() => IsHeld = false;
        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
                IsHeld = false;
        }
    }
}
