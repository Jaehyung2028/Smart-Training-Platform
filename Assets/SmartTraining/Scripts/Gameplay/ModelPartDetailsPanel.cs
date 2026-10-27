using System;
using UnityEngine;
using UnityEngine.UI;

namespace SmartTraining
{
    // 부품 설명은 각 참가자의 HUD에서만 표시합니다. 창을 열어도 이동과 모델 회전을 중지하지 않습니다.
    public sealed class ModelPartDetailsPanel
    {
        private readonly GameObject root;
        private readonly Text title;
        private readonly Text description;
        private readonly ScrollRect scroll;
        private int displayedVersion = -1;

        public ModelPartDetailsPanel(Transform parent, Font font, Action close)
        {
            root = new GameObject("Model Part Details", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.up;
            rect.anchoredPosition = new Vector2(24, -132);
            rect.sizeDelta = new Vector2(380, 280);
            root.GetComponent<Image>().color = new Color32(15, 23, 42, 245);
            var layout = root.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 16, 16);
            layout.spacing = 12;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var header = new GameObject("Header", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            header.transform.SetParent(root.transform, false);
            header.GetComponent<LayoutElement>().preferredHeight = 36;
            var row = header.GetComponent<HorizontalLayoutGroup>();
            row.spacing = 8;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = false;
            title = CreateLabel("Part Title", header.transform, font, 22);
            title.alignment = TextAnchor.MiddleLeft;
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 16;
            title.resizeTextMaxSize = 22;
            title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            var closeObject = new GameObject("Close Part Details", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            closeObject.transform.SetParent(header.transform, false);
            closeObject.GetComponent<LayoutElement>().preferredWidth = 64;
            closeObject.GetComponent<Image>().color = new Color32(51, 65, 85, 255);
            var closeButton = closeObject.GetComponent<Button>();
            closeButton.targetGraphic = closeObject.GetComponent<Image>();
            closeButton.onClick.AddListener(() => close());
            var closeText = CreateLabel("Close Label", closeObject.transform, font, 18);
            closeText.text = "닫기";
            closeText.alignment = TextAnchor.MiddleCenter;
            Stretch(closeText.rectTransform);

            var scrollObject = new GameObject("Description Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(LayoutElement));
            scrollObject.transform.SetParent(root.transform, false);
            scrollObject.GetComponent<LayoutElement>().preferredHeight = 200;
            scrollObject.GetComponent<Image>().color = Color.clear;
            scroll = scrollObject.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24;
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewport.transform.SetParent(scrollObject.transform, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            Stretch(viewportRect);
            description = CreateLabel("Part Description", viewport.transform, font, 18);
            description.alignment = TextAnchor.UpperLeft;
            description.color = new Color32(203, 213, 225, 255);
            description.horizontalOverflow = HorizontalWrapMode.Wrap;
            description.verticalOverflow = VerticalWrapMode.Overflow;
            description.lineSpacing = 1.1f;
            var contentRect = description.rectTransform;
            contentRect.anchorMin = Vector2.up;
            contentRect.anchorMax = Vector2.one;
            contentRect.pivot = Vector2.up;
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;
            description.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            root.SetActive(false);
        }

        internal void Refresh(ModelViewer viewer, bool visible)
        {
            bool show = visible && viewer != null && viewer.SelectedPart != null;
            root.SetActive(show);
            if (!show)
            {
                return;
            }
            title.text = viewer.SelectedPartTitle;
            description.text = viewer.SelectedPartDescription;
            if (displayedVersion != viewer.SelectionVersion)
            {
                displayedVersion = viewer.SelectionVersion;
                scroll.StopMovement();
                scroll.content.anchoredPosition = Vector2.zero;
            }
        }

        private static Text CreateLabel(string name, Transform parent, Font font, int size)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(parent, false);
            text.font = font;
            text.fontSize = size;
            text.color = Color.white;
            text.supportRichText = false;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
