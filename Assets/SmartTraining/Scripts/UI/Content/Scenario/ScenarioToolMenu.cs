using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SmartTraining
{
    // 기존 기능 메뉴 안의 개인 공구 버튼 목록입니다. 이름과 선택 상태를 항상 텍스트로 표시합니다.
    internal sealed class ScenarioToolMenu
    {
        private readonly GameObject root;
        private readonly Font font;
        private readonly Text selection;
        private readonly Transform toolContainer;
        private string displayedTool;
        private readonly List<(string ToolId, string Title, Button Button, Text Label, Image Background)> buttons = new();
        private ScenarioContent scenario;
        private ScenarioDefinition definition;

        public ScenarioToolMenu(Transform section, Font font)
        {
            root = section.gameObject;
            this.font = font;
            CreateLabel("공구 선택 (개인)", 24);
            selection = CreateLabel("내 공구: 없음", 28);
            var viewport = new GameObject(
                "Tool List Scroll",
                typeof(RectTransform),
                typeof(Image),
                typeof(RectMask2D),
                typeof(ScrollRect),
                typeof(LayoutElement)
            );
            viewport.transform.SetParent(root.transform, false);
            viewport.GetComponent<LayoutElement>().preferredHeight = 126;
            viewport.GetComponent<Image>().color = new Color32(15, 23, 42, 255);
            var content = new GameObject(
                "Tool List Content",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter)
            );
            content.transform.SetParent(viewport.transform, false);
            var rect = content.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.up;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.up;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = new Vector2(-14, 0);
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 6;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            toolContainer = content.transform;
            var scroll = viewport.GetComponent<ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = rect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 24;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            var track = new GameObject("Tool Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            track.transform.SetParent(viewport.transform, false);
            var trackRect = track.GetComponent<RectTransform>();
            trackRect.anchorMin = Vector2.right;
            trackRect.anchorMax = Vector2.one;
            trackRect.offsetMin = new Vector2(-10, 0);
            trackRect.offsetMax = Vector2.zero;
            track.GetComponent<Image>().color = new Color32(30, 41, 59, 255);
            var handle = new GameObject("Tool Scroll Handle", typeof(RectTransform), typeof(Image));
            handle.transform.SetParent(track.transform, false);
            handle.GetComponent<Image>().color = new Color32(100, 116, 139, 255);
            var handleRect = handle.GetComponent<RectTransform>();
            handleRect.anchorMin = Vector2.zero;
            handleRect.anchorMax = Vector2.one;
            handleRect.offsetMin = handleRect.offsetMax = Vector2.zero;
            var scrollbar = track.GetComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handle.GetComponent<RectTransform>();
            scrollbar.targetGraphic = handle.GetComponent<Image>();
            scroll.verticalScrollbar = scrollbar;
        }


        public void Refresh(ScenarioContent current, bool visible, bool canInteract)
        {
            scenario = current;
            root.SetActive(visible && current != null && current.Definition.Tools.Count > 0);
            if (!root.activeSelf)
            {
                return;
            }

            if (definition != current.Definition)
            {
                foreach (var item in buttons)
                {
                    item.Button.gameObject.SetActive(false);
                    Object.Destroy(item.Button.gameObject);
                }

                buttons.Clear();
                definition = current.Definition;
                displayedTool = null;
                AddTool("", "선택 안 함");
                foreach (var tool in definition.Tools)
                {
                    AddTool(tool.Id, tool.Title);
                }
            }

            bool changed = displayedTool != current.SelectedTool;
            if (changed)
            {
                displayedTool = current.SelectedTool;
                selection.text = "내 공구: " + definition.GetToolTitle(displayedTool);
            }

            foreach (var item in buttons)
            {
                if (changed)
                {
                    bool selected = item.ToolId == displayedTool;
                    item.Label.text = (selected ? "선택됨 · " : "") + item.Title;
                    item.Background.color = selected ? new Color32(37, 99, 235, 255) : new Color32(51, 65, 85, 255);
                }

                item.Button.interactable = canInteract && current.CanSelectTool;
            }
        }


        private void AddTool(string toolId, string title)
        {
            var button = new GameObject(
                "Scenario Tool " + toolId,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement)
            ).GetComponent<Button>();
            button.transform.SetParent(toolContainer, false);
            button.targetGraphic = button.GetComponent<Image>();
            UguiVisualStyle.Round(button.GetComponent<Image>());
            button.GetComponent<LayoutElement>().preferredHeight = 36;
            var label = new GameObject("Tool Name", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(button.transform, false);
            label.font = font;
            label.fontSize = 16;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 12;
            label.resizeTextMaxSize = 16;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            label.supportRichText = false;
            label.text = title;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(4, 2);
            label.rectTransform.offsetMax = new Vector2(-4, -2);
            button.onClick.AddListener(() =>
            {
                if (button.IsInteractable())
                {
                    scenario?.SelectTool(toolId);
                }
            });
            buttons.Add((toolId, title, button, label, button.GetComponent<Image>()));
        }


        private Text CreateLabel(string text, float height)
        {
            var label = new GameObject(text, typeof(RectTransform), typeof(Text), typeof(LayoutElement)).GetComponent<Text>();
            label.transform.SetParent(root.transform, false);
            label.font = font;
            label.fontSize = 17;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            label.supportRichText = false;
            label.text = text;
            label.GetComponent<LayoutElement>().preferredHeight = height;
            return label;
        }
    }
}
