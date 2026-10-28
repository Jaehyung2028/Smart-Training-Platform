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
        private readonly List<(string ToolId, string Title, Button Button, Text Label)> buttons = new();
        private ScenarioContent scenario;
        private ScenarioDefinition definition;

        public ScenarioToolMenu(Transform section, Font font)
        {
            root = section.gameObject;
            this.font = font;
            CreateLabel("공구 선택 (개인)", 24);
            selection = CreateLabel("내 공구: 없음", 28);
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
                AddTool("", "선택 안 함");
                foreach (var tool in definition.Tools)
                {
                    AddTool(tool.Id, tool.Title);
                }
            }

            selection.text = "내 공구: " + definition.GetToolTitle(current.SelectedTool);
            foreach (var item in buttons)
            {
                bool selected = item.ToolId == current.SelectedTool;
                item.Label.text = (selected ? "선택됨 · " : "") + item.Title;
                item.Button.GetComponent<Image>().color = selected ? new Color32(37, 99, 235, 255) : new Color32(51, 65, 85, 255);
                item.Button.interactable = canInteract && current.CanInteract && !current.IsComplete;
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
            button.transform.SetParent(root.transform, false);
            button.targetGraphic = button.GetComponent<Image>();
            UguiVisualStyle.Round(button.GetComponent<Image>());
            button.GetComponent<LayoutElement>().preferredHeight = 36;
            var label = new GameObject("Tool Name", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(button.transform, false);
            label.font = font;
            label.fontSize = 18;
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
            buttons.Add((toolId, title, button, label));
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
