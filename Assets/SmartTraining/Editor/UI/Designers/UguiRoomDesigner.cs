using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static SmartTraining.Editor.LobbyUguiBuilder;

namespace SmartTraining.Editor
{
    public static class UguiRoomDesigner
    {
        [MenuItem("Smart Training/UI/Apply Room Theme")]
        public static void ApplyRoomTheme()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            var root = PrefabUtility.LoadPrefabContents(RoomUguiBuilder.AssetPath);
            try
            {
                Apply(root, UguiLoginDesigner.LoadTheme());
                PrefabUtility.SaveAsPrefabAsset(root, RoomUguiBuilder.AssetPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("ROOM THEME APPLIED: existing permissions and action references preserved");
        }

        public static void Apply(GameObject root, UguiTheme theme)
        {
            var ui = root.GetComponent<RoomUguiView>();
            root.transform.Find("Background").GetComponent<Image>().color = theme.background;
            ui.mainInteraction.GetComponent<Image>().color = theme.surface;
            foreach (var text in root.GetComponentsInChildren<Text>(true))
                text.color = theme.ink;
            ui.heading.fontStyle = FontStyle.Bold;
            ui.heading.fontSize = 24;
            ui.participantTitle.fontStyle = FontStyle.Bold;
            foreach (var text in new[] { ui.connection, ui.status, ui.chatStatus, ui.notices, ui.emptyChat, ui.emptyParticipants })
                text.color = theme.muted;
            ui.notice.color = new Color32(146, 64, 14, 255);
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                bool primary = button == ui.ready || button == ui.phase || button == ui.sendChat || button == ui.saveSettings || button == ui.savePassword || button == ui.confirm;
                bool danger = button == ui.closeRoom || button.name == "Kick";
                button.GetComponent<Image>().color = Color.white;
                var colors = button.colors;
                colors.normalColor = primary ? theme.accent : danger ? new Color32(254, 226, 226, 255) : theme.secondary;
                colors.highlightedColor = primary ? theme.accentHover : danger ? new Color32(254, 202, 202, 255) : theme.line;
                colors.selectedColor = colors.highlightedColor;
                colors.pressedColor = primary ? theme.accentPressed : new Color32(203, 213, 225, 255);
                colors.disabledColor = theme.line;
                colors.fadeDuration = .12f;
                button.colors = colors;
                var label = button.GetComponentInChildren<Text>();
                label.color = primary ? Color.white : danger ? new Color32(153, 27, 27, 255) : theme.ink;
                label.fontSize = theme.bodySize;
                label.fontStyle = primary ? FontStyle.Bold : FontStyle.Normal;
            }
            foreach (var tab in new[] { ui.roomParticipants, ui.lobbyParticipants, ui.roomChat, ui.lobbyChat })
            {
                var highlight = tab.GetComponent<UguiSelectionHighlight>() ?? tab.gameObject.AddComponent<UguiSelectionHighlight>();
                highlight.border = Border(tab.gameObject, theme.line);
                highlight.normalColor = theme.line;
                highlight.selectedColor = theme.accent;
            }
            foreach (var input in root.GetComponentsInChildren<InputField>(true))
            {
                input.GetComponent<Image>().color = theme.field;
                Border(input.gameObject, theme.line);
                input.textComponent.color = theme.ink;
                ((Text)input.placeholder).color = theme.muted;
            }
            foreach (var scroll in root.GetComponentsInChildren<ScrollRect>(true))
            {
                scroll.GetComponent<Image>().color = theme.field;
                var viewport = scroll.viewport.GetComponent<Image>();
                if (viewport != null)
                    viewport.color = theme.field;
                Border(scroll.gameObject, theme.line);
                var group = scroll.content.GetComponent<VerticalLayoutGroup>();
                if (group != null)
                {
                    group.padding = new RectOffset(8, 8, 8, 8);
                    group.spacing = 8;
                }
            }
            var card = ui.participantTemplate.GetComponent<RoomParticipantCard>();
            if (card == null)
            {
                card = ui.participantTemplate.gameObject.AddComponent<RoomParticipantCard>();
                card.details = ui.participantTemplate.Find("Details").GetComponent<Text>();
                var status = Node("Participant Status", ui.participantTemplate);
                status.SetAsFirstSibling();
                Height(status.gameObject, 24);
                var group = status.gameObject.AddComponent<HorizontalLayoutGroup>();
                group.spacing = 8;
                group.childControlWidth = group.childControlHeight = true;
                group.childForceExpandWidth = true;
                card.statusRow = status.gameObject;
                card.role = Label("참가자", status, 14, 24);
                card.readiness = Label("준비 전", status, 14, 24);
                card.readiness.alignment = TextAnchor.MiddleRight;
                card.role.fontStyle = card.readiness.fontStyle = FontStyle.Bold;
            }
            card.ink = theme.ink;
            card.muted = theme.muted;
            card.accent = theme.accent;
            card.line = theme.line;
            card.border = Border(ui.participantTemplate.gameObject, theme.line);
            card.role.color = theme.accent;
            card.readiness.color = theme.muted;
            ui.participantTemplate.GetComponent<Image>().color = theme.field;
            foreach (var modal in new[] { ui.settingsModal, ui.confirmationModal, ui.mapModal })
            {
                modal.GetComponent<Image>().color = new Color(.03f, .06f, .12f, .72f);
                modal.transform.Find("Card").GetComponent<Image>().color = theme.surface;
            }
            ui.map.GetComponent<Image>().color = theme.secondary;
        }
        private static Outline Border(GameObject root, Color color)
        {
            var border = root.GetComponent<Outline>() ?? root.AddComponent<Outline>();
            border.effectColor = color;
            border.effectDistance = new Vector2(1, -1);
            return border;
        }
    }
}
