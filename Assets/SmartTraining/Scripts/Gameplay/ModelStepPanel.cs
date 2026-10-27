using System;
using UnityEngine;
using UnityEngine.UI;

namespace SmartTraining
{
    // 재생 위치에 맞는 공통 안내입니다. 직접 선택한 부품의 설명 창과 독립적으로 표시합니다.
    public sealed class ModelStepPanel
    {
        private readonly GameObject root;
        private readonly Text title, description;
        private readonly Button previous, next;
        private readonly ScrollRect scroll;
        private ModelViewer viewer;
        private Camera camera;
        private readonly Action beforeMove;
        private int displayedIndex = -2;

        public ModelStepPanel(Transform parent, Font font, Action beforeMove)
        {
            this.beforeMove = beforeMove;
            root = new GameObject("Model Step Guide", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(24, 24);
            rect.sizeDelta = new Vector2(380, 210);
            root.GetComponent<Image>().color = new Color32(15, 23, 42, 245);
            var layout = root.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 12, 12);
            layout.spacing = 8;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            title = Label("Step Title", root.transform, font, 19);
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 30;
            var viewport = new GameObject("Step Description Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(LayoutElement));
            viewport.transform.SetParent(root.transform, false);
            viewport.GetComponent<Image>().color = Color.clear;
            viewport.GetComponent<LayoutElement>().preferredHeight = 92;
            scroll = viewport.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.scrollSensitivity = 24;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            var mask = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            mask.SetParent(viewport.transform, false);
            mask.anchorMin = Vector2.zero;
            mask.anchorMax = Vector2.one;
            mask.offsetMin = mask.offsetMax = Vector2.zero;
            description = Label("Step Description", mask, font, 17);
            description.rectTransform.anchorMin = Vector2.up;
            description.rectTransform.anchorMax = Vector2.one;
            description.rectTransform.pivot = Vector2.up;
            description.rectTransform.sizeDelta = Vector2.zero;
            description.horizontalOverflow = HorizontalWrapMode.Wrap;
            description.verticalOverflow = VerticalWrapMode.Overflow;
            description.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = mask;
            scroll.content = description.rectTransform;
            var row = new GameObject("Step Navigation", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(root.transform, false);
            row.GetComponent<LayoutElement>().preferredHeight = 40;
            var buttons = row.GetComponent<HorizontalLayoutGroup>();
            buttons.spacing = 8;
            buttons.childControlWidth = buttons.childControlHeight = true;
            buttons.childForceExpandWidth = buttons.childForceExpandHeight = true;
            previous = Button("Previous Model Step", "이전 단계", row.transform, font, -1);
            next = Button("Next Model Step", "다음 단계", row.transform, font, 1);
            root.SetActive(false);
        }

        internal void Refresh(ModelViewer current, Camera currentCamera, bool visible, bool canInteract)
        {
            bool show = visible && current != null && current.StepCount > 0;
            root.SetActive(show);
            viewer = current;
            camera = currentCamera;
            if (!show)
            {
                displayedIndex = -2;
                return;
            }
            var step = viewer.CurrentStep;
            int index = viewer.CurrentStepIndex;
            title.text = step != null ? (index + 1) + " / " + viewer.StepCount + " · " + step.Title : "단계 안내";
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 14;
            title.resizeTextMaxSize = 19;
            description.text = step != null ? step.Description : "다음 단계 버튼으로 안내를 시작하세요.";
            if (displayedIndex != index)
            {
                displayedIndex = index;
                scroll.StopMovement();
                description.rectTransform.anchoredPosition = Vector2.zero;
            }
            previous.interactable = canInteract && viewer.CanMoveStep(camera, -1);
            next.interactable = canInteract && viewer.CanMoveStep(camera, 1);
        }

        private Button Button(string name, string caption, Transform parent, Font font, int direction)
        {
            var button = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement)).GetComponent<Button>();
            button.transform.SetParent(parent, false);
            button.GetComponent<LayoutElement>().flexibleWidth = 1;
            button.GetComponent<Image>().color = new Color32(51, 65, 85, 255);
            button.targetGraphic = button.GetComponent<Image>();
            var text = Label("Label", button.transform, font, 17);
            text.text = caption;
            text.alignment = TextAnchor.MiddleCenter;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            button.onClick.AddListener(() =>
            {
                if (button.IsInteractable())
                {
                    beforeMove();
                    viewer?.MoveStep(camera, direction);
                }
            });
            return button;
        }

        private static Text Label(string name, Transform parent, Font font, int size)
        {
            var label = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(parent, false);
            label.font = font;
            label.fontSize = size;
            label.color = Color.white;
            label.supportRichText = false;
            label.raycastTarget = false;
            return label;
        }
    }
}
