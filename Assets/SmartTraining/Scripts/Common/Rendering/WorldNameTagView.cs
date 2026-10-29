using System;
using UnityEngine;
using UnityEngine.UI;

namespace SmartTraining
{
    // 네트워크·플레이어 타입에 의존하지 않는 월드 이름표입니다. 카메라와 텍스트는 사용 측에서 전달합니다.
    public sealed class WorldNameTagView : IDisposable
    {
        private readonly GameObject root;
        private readonly Text label;
        private readonly Text detailLabel;
        private readonly RectTransform rect;
        private readonly Canvas canvas;
        private readonly Font font;
        private string displayedName, displayedDetail;

        public WorldNameTagView(Transform parent)
        {
            root = new GameObject("Player Name Tag", typeof(RectTransform), typeof(Canvas), typeof(Image));
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0, 1.35f, 0);
            root.transform.localScale = Vector3.one * .006f;
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(260, 42);
            var background = root.GetComponent<Image>();
            background.color = new Color32(15, 23, 42, 210);
            background.raycastTarget = false;
            font = Font.CreateDynamicFontFromOSFont("Malgun Gothic", 24);
            label = new GameObject("Player Name", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(root.transform, false);
            var textRect = label.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8, 2);
            textRect.offsetMax = new Vector2(-8, -2);
            label.font = font;
            label.fontSize = 24;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 12;
            label.resizeTextMaxSize = 24;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.supportRichText = false;
            label.raycastTarget = false;
            detailLabel = new GameObject("Player Detail", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            detailLabel.transform.SetParent(root.transform, false);
            detailLabel.font = font;
            detailLabel.fontSize = 18;
            detailLabel.alignment = TextAnchor.MiddleCenter;
            detailLabel.color = new Color32(125, 211, 252, 255);
            detailLabel.supportRichText = false;
            detailLabel.raycastTarget = false;
            detailLabel.rectTransform.anchorMin = Vector2.zero;
            detailLabel.rectTransform.anchorMax = Vector2.right;
            detailLabel.rectTransform.offsetMin = new Vector2(8, 2);
            detailLabel.rectTransform.offsetMax = new Vector2(-8, 26);
            root.SetActive(false);
        }


        public void Refresh(string displayName, Camera viewCamera, string detail = "")
        {
            bool visible = viewCamera != null && !string.IsNullOrEmpty(displayName);
            root.SetActive(visible);
            if (!visible)
            {
                return;
            }

            if (displayedName != displayName || displayedDetail != detail)
            {
                displayedName = displayName;
                displayedDetail = detail;
                label.text = displayName;
                bool hasDetail = !string.IsNullOrEmpty(detail);
                int lines = 1;
                if (hasDetail)
                {
                    foreach (char character in detail)
                    {
                        if (character == '\n')
                        {
                            lines++;
                        }
                    }
                }

                float detailHeight = hasDetail ? Mathf.Min(lines, 4) * 24 : 0;
                rect.sizeDelta = new Vector2(260, 42 + detailHeight);
                label.rectTransform.offsetMin = new Vector2(8, 2 + detailHeight);
                detailLabel.rectTransform.offsetMax = new Vector2(-8, 2 + detailHeight);
                detailLabel.gameObject.SetActive(hasDetail);
                detailLabel.text = detail;
            }

            canvas.worldCamera = viewCamera;
            // 아바타의 회전과 별개로 각 화면의 관찰 카메라를 향합니다.
            root.transform.rotation = viewCamera.transform.rotation;
        }


        public void Dispose()
        {
            root.SetActive(false);
            UnityEngine.Object.Destroy(root);
            UnityEngine.Object.Destroy(font);
        }
    }
}
