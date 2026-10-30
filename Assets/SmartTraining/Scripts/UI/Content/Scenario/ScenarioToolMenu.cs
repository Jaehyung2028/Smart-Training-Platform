using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SmartTraining
{
    // 기존 기능 메뉴 안의 개인 공구 버튼 목록입니다. 이름과 선택 상태를 항상 텍스트로 표시합니다.
    internal sealed class ScenarioToolMenu
    {
        private readonly GameObject root;
        private readonly Text selection;
        private readonly Transform toolContainer;
        private string displayedTool;
        private readonly List<(string ToolId, string Title, Button Button, Text Label, Image Background)> buttons = new();
        private ScenarioContent scenario;
        private ScenarioDefinition definition;

        public ScenarioToolMenu(Transform section, Font font)
        {
            var view = UiPrefab.Instantiate("ScenarioToolMenu", section);
            root = view.Get<GameObject>("root");
            selection = view.Get<Text>("selection");
            toolContainer = view.Get<Transform>("toolContainer");
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
                    UiPrefab.Destroy(item.Button.gameObject);
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
            var prefab = UiPrefab.Instantiate("ScenarioToolRow", toolContainer);
            var button = prefab.GetComponent<Button>();
            button.name = "Scenario Tool " + toolId;
            var label = prefab.Get<Text>("Tool Name:Text");
            label.text = title;
            button.onClick.AddListener(() =>
            {
                if (button.IsInteractable())
                {
                    scenario?.SelectTool(toolId);
                }
            });
            buttons.Add((toolId, title, button, label, button.GetComponent<Image>()));
        }
    }
}
