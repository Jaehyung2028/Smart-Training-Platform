using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SmartTraining.Editor
{
    public static class UguiInteriorDesigner
    {
        [MenuItem("Smart Training/UI/Apply Lobby Interior Theme")]
        public static void ApplyInteriorTheme()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            var root = PrefabUtility.LoadPrefabContents(LobbyInteriorUguiBuilder.InteriorPath);
            try
            {
                Apply(root, UguiLoginDesigner.LoadTheme());
                PrefabUtility.SaveAsPrefabAsset(root, LobbyInteriorUguiBuilder.InteriorPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("INTERIOR THEME APPLIED: existing room and chat actions preserved");
        }

        public static void Apply(GameObject root, UguiTheme theme)
        {
            var ui = root.GetComponent<LobbyInteriorUguiView>();
            root.transform.Find("Background").GetComponent<Image>().color = theme.background;
            ui.mainInteraction.GetComponent<Image>().color = theme.surface;
            foreach (var text in root.GetComponentsInChildren<Text>(true))
                text.color = theme.ink;
            ui.heading.fontStyle = FontStyle.Bold;
            ui.heading.fontSize = 24;
            foreach (var text in new[] { ui.connection, ui.status, ui.chatStatus, ui.emptyRooms, ui.emptyParticipants, ui.emptyChat })
                text.color = theme.muted;
            ui.participantTitle.fontStyle = ui.roomCount.fontStyle = FontStyle.Bold;
            ui.notice.color = new Color32(146, 64, 14, 255);
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                bool primary = button == ui.create || button == ui.join || button == ui.submitCreate || button == ui.sendChat || button == ui.reconnect;
                bool row = button == ui.roomTemplate || button == ui.participantTemplate;
                button.GetComponent<Image>().color = Color.white;
                var colors = button.colors;
                colors.normalColor = primary ? theme.accent : row ? Color.white : theme.secondary;
                colors.highlightedColor = primary ? theme.accentHover : row ? new Color32(239, 246, 255, 255) : theme.line;
                colors.selectedColor = colors.highlightedColor;
                colors.pressedColor = primary ? theme.accentPressed : new Color32(203, 213, 225, 255);
                colors.disabledColor = theme.line;
                colors.fadeDuration = .12f;
                button.colors = colors;
                var text = button.GetComponentInChildren<Text>();
                text.color = primary ? Color.white : theme.ink;
                if (!row)
                    text.fontStyle = primary ? FontStyle.Bold : FontStyle.Normal;
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
                group.padding = new RectOffset(8, 8, 8, 8);
                group.spacing = 8;
            }
            var selection = ui.roomTemplate.GetComponent<UguiSelectionHighlight>() ?? ui.roomTemplate.gameObject.AddComponent<UguiSelectionHighlight>();
            selection.border = Border(ui.roomTemplate.gameObject, theme.line);
            selection.normalColor = theme.line;
            selection.selectedColor = theme.accent;
            selection.SetSelected(false);
            Border(ui.participantTemplate.gameObject, theme.line);
            var height = ui.roomTemplate.GetComponent<LayoutElement>();
            height.minHeight = height.preferredHeight = 76;
            ui.roomTemplate.GetComponentInChildren<Text>().fontSize = 16;
            foreach (var filter in new[] { ui.available, ui.protection, ui.sort, ui.resetFilters })
                filter.GetComponentInChildren<Text>().fontSize = 14;
            foreach (var modal in new[] { ui.createModal, ui.progressModal })
            {
                modal.GetComponent<Image>().color = new Color(0.03f, .06f, .12f, .72f);
                modal.transform.Find("Card").GetComponent<Image>().color = theme.surface;
            }
        }
        private static Outline Border(GameObject root, Color color)
        {
            var outline = root.GetComponent<Outline>() ?? root.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(1, -1);
            return outline;
        }
    }
}
