using UnityEngine;

namespace SmartTraining
{
    // 클릭과 드래그를 구분합니다. 짧은 클릭은 설명만 열고 드래그를 시작한 경우에만 모델 조작권을 요청합니다.
    public sealed class ModelPointerGesture
    {
        public const float DragThreshold = 8f;
        public const float ClickDuration = 0.35f;
        private Vector2 origin;
        private float startedAt;

        public bool IsPressed { get; private set; }
        public bool IsDragging { get; private set; }

        public void Begin(Vector2 position, float time)
        {
            origin = position;
            startedAt = time;
            IsPressed = true;
            IsDragging = false;
        }

        public void Update(Vector2 position)
        {
            if (IsPressed && (position - origin).sqrMagnitude > DragThreshold * DragThreshold)
            {
                IsDragging = true;
            }
        }

        public bool End(Vector2 position, float time)
        {
            Update(position);
            bool clicked = IsPressed && !IsDragging && time - startedAt <= ClickDuration;
            Cancel();
            return clicked;
        }

        public void Cancel()
        {
            IsPressed = false;
            IsDragging = false;
        }
    }
}
