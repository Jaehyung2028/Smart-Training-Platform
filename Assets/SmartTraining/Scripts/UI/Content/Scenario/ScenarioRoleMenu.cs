using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SmartTraining
{
    // 협동 콘텐츠에서만 표시하는 역할 선택입니다. 실제 배정과 단계 권한은 서버가 결정합니다.
    internal sealed class ScenarioRoleMenu
    {
        private readonly GameObject root;
        private readonly Text status;

        private sealed class RoleButton
        {
            public Button Button;
            public Text Label;
            public Image Background;
            public string Participant;
            public bool Selected, Assigned, Initialized;
        }


        private readonly Dictionary<ScenarioRole, RoleButton> buttons = new();
        private ScenarioContent current;

        public ScenarioRoleMenu(Transform parent, Font font)
        {
            root = parent.gameObject;
            status = new GameObject(
                "Scenario Roles",
                typeof(RectTransform),
                typeof(Text),
                typeof(LayoutElement)
            ).GetComponent<Text>();
            status.transform.SetParent(parent, false);
            status.font = font;
            status.fontSize = 13;
            status.color = Color.white;
            status.supportRichText = false;
            status.GetComponent<LayoutElement>().preferredHeight = 36;
            foreach (var role in ScenarioRoles.Enumerate(ScenarioRole.Inspector | ScenarioRole.Technician | ScenarioRole.SafetyOfficer | ScenarioRole.Coordinator))
            {
                buttons.Add(role, Create(parent, font, role));
            }
        }


        public void Refresh(ScenarioContent scenario, bool visible, bool canInteract)
        {
            current = scenario;
            root.SetActive(visible && current != null && current.Definition.RequiresRoles);
            if (!root.activeSelf)
            {
                return;
            }

            status.text = "역할당 한 명 · 빈 역할 겸임 가능\n작업 중인 사람은 완료 후 추가 선택";
            foreach (var item in buttons)
            {
                bool used = (current.Definition.Roles & item.Key) == item.Key;
                item.Value.Button.gameObject.SetActive(used);
                if (used)
                {
                    RefreshRole(item.Value, item.Key, canInteract);
                }
            }
        }


        private void RefreshRole(RoleButton view, ScenarioRole role, bool canInteract)
        {
            bool selected = (current.SelectedRoles & role) == role;
            bool assigned = current.IsRoleAssigned(role);
            view.Button.interactable = canInteract && !current.CanCancelWork && !current.IsComplete && !assigned;
            string name = assigned ? current.RoleParticipantName(role) : "";
            if (!view.Initialized || view.Selected != selected || view.Assigned != assigned || view.Participant != name)
            {
                view.Initialized = true;
                view.Selected = selected;
                view.Assigned = assigned;
                view.Participant = name;
                view.Label.text = ScenarioRoles.Label(role) + "\n" + (assigned ? name + (selected ? " (나)" : " · 담당 중") : "선택하기");
                view.Background.color = selected ? new Color32(37, 99, 235, 255) : assigned ? new Color32(71, 85, 105, 255) : new Color32(51, 65, 85, 255);
            }
        }


        private RoleButton Create(Transform parent, Font font, ScenarioRole role)
        {
            var button = new GameObject(
                "Scenario Role " + role,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement)
            ).GetComponent<Button>();
            button.transform.SetParent(parent, false);
            button.targetGraphic = button.GetComponent<Image>();
            button.GetComponent<LayoutElement>().preferredHeight = 56;
            var label = new GameObject("Role Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(button.transform, false);
            label.font = font;
            label.fontSize = 15;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 11;
            label.resizeTextMaxSize = 15;
            label.supportRichText = false;
            label.color = Color.white;
            label.text = ScenarioRoles.Label(role) + " 선택";
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
            button.onClick.AddListener(() =>
            {
                if (button.IsInteractable())
                {
                    current?.SelectRole(role);
                }
            });
            return new RoleButton
            {
                Button = button,
                Label = label,
                Background = button.GetComponent<Image>()
            };
        }
    }
}
