using UnityEngine;
using UnityEngine.UI;

namespace SmartTraining
{
    // 화면 종류나 콘텐츠 로직을 모르는 공통 표현 도구입니다. 기존 버튼 이벤트와 색상 상태는 유지합니다.
    public static class UguiVisualStyle
    {
        private static Sprite rounded;

        public static void Round(Image image)
        {
            if (image == null)
            {
                return;
            }

            if (rounded == null)
            {
                rounded = Resources.Load<Sprite>("SmartTraining/UI/RoundedSurface");
            }

            if (rounded != null)
            {
                image.sprite = rounded;
                image.type = Image.Type.Sliced;
            }
        }


        public static void PolishDark(Transform root)
        {
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                string name = image.name;
                if (image.GetComponent<Button>() != null ||
                    name.Contains("Panel") ||
                    name.Contains("Controls") ||
                    name.Contains("Card") ||
                    name == "Content Action List" ||
                    name.Contains("Background"))
                {
                    Round(image);
                }
            }

            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                var colors = button.colors;
                colors.fadeDuration = .14f;
                button.colors = colors;
                var label = button.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.fontSize = Mathf.Clamp(label.fontSize, 14, 17);
                }
            }
        }
    }
}
