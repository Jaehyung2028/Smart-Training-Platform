using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SmartTraining
{
    // Shared view setup; admission, permissions and networking stay in ApiLobbyScreen.
    internal static class UguiViewUtility
    {
        internal static void EnsureComponents(Component view, ref Canvas canvas, ref CanvasGroup interaction, ref GraphicRaycaster raycaster)
        {
            // Unity objects require Unity's null comparison, including missing/destroyed components.
            if (canvas == null) canvas = view.GetComponent<Canvas>();
            if (interaction == null) interaction = view.GetComponent<CanvasGroup>();
            if (interaction == null) interaction = view.gameObject.AddComponent<CanvasGroup>();
            if (raycaster == null) raycaster = view.GetComponent<GraphicRaycaster>();
        }

        internal static void SetVisible(Component view, Canvas canvas, CanvasGroup interaction, GraphicRaycaster raycaster, bool visible)
        {
            canvas.enabled = visible;
            interaction.interactable = interaction.blocksRaycasts = visible;
            raycaster.enabled = visible;
            if (!visible) ClearSelection(view.transform);
        }

        internal static void ClearSelection(Transform root)
        {
            var selected = EventSystem.current?.currentSelectedGameObject;
            if (selected != null && selected.transform.IsChildOf(root)) EventSystem.current.SetSelectedGameObject(null);
        }

        internal static void EnsureEventSystem(Transform parent)
        {
            if (EventSystem.current != null) return;
            var events = new GameObject("UI Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(parent, false);
        }

        // Return only a font owned by the view so OnDestroy never destroys a supplied font asset.
        internal static Font ApplyFont(GameObject root, Font fontOverride)
        {
            Font font = fontOverride, ownedFont = null;
            if (font == null)
            {
                var installed = new HashSet<string>(Font.GetOSInstalledFontNames(), StringComparer.OrdinalIgnoreCase);
                foreach (var name in new[] { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR" })
                    if (installed.Contains(name)) { font = ownedFont = Font.CreateDynamicFontFromOSFont(name, 20); break; }
            }
            if (font != null) foreach (var label in root.GetComponentsInChildren<Text>(true)) label.font = font;
            return ownedFont;
        }

        internal static string ChatLabel(LobbyChatMessage message)
        {
            string time = DateTimeOffset.TryParse(message.sentAt, out var sent) ? sent.ToLocalTime().ToString("HH:mm:ss") : "";
            return "[" + time + "] " + message.nickname + ": " + message.text;
        }

        internal static bool RemoveMissing<T>(Dictionary<string,T> items, HashSet<string> ids) where T : Component
        {
            bool changed = false;
            foreach (var id in items.Keys.Where(id => !ids.Contains(id)).ToArray())
            {
                items[id].gameObject.SetActive(false);
                UnityEngine.Object.Destroy(items[id].gameObject);
                items.Remove(id); changed = true;
            }
            return changed;
        }
    }
}
