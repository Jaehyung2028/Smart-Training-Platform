using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static SmartTraining.Editor.LobbyUguiBuilder;

namespace SmartTraining.Editor
{
    public static class UguiLoginDesigner
    {
        public const string ThemePath = "Assets/SmartTraining/Resources/SmartTraining/DefaultUguiTheme.asset";

        public static UguiTheme LoadTheme()
        {
            var theme = AssetDatabase.LoadAssetAtPath<UguiTheme>(ThemePath);
            if (theme != null)
                return theme;
            theme = ScriptableObject.CreateInstance<UguiTheme>();
            AssetDatabase.CreateAsset(theme, ThemePath);
            return theme;
        }

        [MenuItem("Smart Training/UI/Apply Login Theme")]
        public static void ApplyLoginTheme()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            var root = PrefabUtility.LoadPrefabContents(AssetPath);
            try
            {
                Apply(root, LoadTheme());
                PrefabUtility.SaveAsPrefabAsset(root, AssetPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("LOGIN THEME APPLIED: existing inputs and action references preserved");
        }

        // Explicit editor action. Runtime never replaces a designer's prefab edits.
        public static void Apply(GameObject root, UguiTheme theme)
        {
            var ui = root.GetComponent<LobbyUguiView>();
            var background = root.transform.Find("Background");
            var card = background.Find("Main Panel");
            background.GetComponent<Image>().color = theme.background;
            card.GetComponent<Image>().color = theme.surface;
            var heading = card.GetChild(0).GetComponent<Text>();
            heading.text = "SMART TRAINING";
            heading.fontSize = 24;
            heading.color = theme.ink;
            ui.connectionText.fontSize = 14;
            ui.connectionText.color = theme.muted;
            ui.statusText.fontSize = 14;
            ui.statusText.color = theme.muted;

            var panel = ui.loginPanel.transform;
            var form = panel.Find("Login Form") as RectTransform;
            if (form == null)
            {
                // Move existing controls rather than recreating them: scene wiring and serialized IDs survive.
                var children = new System.Collections.Generic.List<Transform>();
                foreach (Transform child in panel)
                    children.Add(child);
                Object.DestroyImmediate(panel.GetComponent<VerticalLayoutGroup>());
                var columns = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
                columns.spacing = 32;
                columns.childControlWidth = columns.childControlHeight = true;
                columns.childForceExpandWidth = false;
                columns.childForceExpandHeight = false;
                columns.childAlignment = TextAnchor.UpperLeft;
                var hero = Node("Login Introduction", panel);
                Vertical(hero, 0, 14);
                var heroSize = hero.gameObject.AddComponent<LayoutElement>();
                heroSize.minWidth = 280;
                heroSize.flexibleWidth = 1;
                var eyebrow = Label("CONNECTED TRAINING PLATFORM", hero, 12, 30);
                eyebrow.color = theme.accent;
                var title = Label("훈련의 시작,\n하나의 연결에서.", hero, 38, 130);
                title.fontStyle = FontStyle.Bold;
                title.alignment = TextAnchor.UpperLeft;
                var description = Label("서버 주소와 닉네임을 입력하면\n콘텐츠 로비에 접속할 수 있습니다.", hero, 17, 85);
                description.color = theme.muted;
                description.alignment = TextAnchor.UpperLeft;
                var steps = Label("01  서버 주소 확인\n02  닉네임 입력\n03  콘텐츠 로비 선택", hero, 16, 100);
                steps.color = theme.muted;
                steps.lineSpacing = 1.35f;
                form = Node("Login Form", panel);
                Vertical(form, 16, 9);
                var formSize = form.gameObject.AddComponent<LayoutElement>();
                formSize.minWidth = 500;
                formSize.flexibleWidth = 1;
                form.gameObject.AddComponent<Image>().color = theme.field;
                foreach (var child in children)
                    child.SetParent(form, false);
            }

            var formHeading = form.GetChild(0).GetComponent<Text>();
            form.GetComponent<Image>().color = theme.field;
            var introduction = panel.Find("Login Introduction");
            foreach (var text in introduction.GetComponentsInChildren<Text>(true))
                text.color = theme.muted;
            introduction.GetChild(0).GetComponent<Text>().color = theme.accent;
            introduction.GetChild(1).GetComponent<Text>().color = theme.ink;
            formHeading.text = "서버 접속";
            formHeading.fontSize = 24;
            formHeading.fontStyle = FontStyle.Bold;
            formHeading.color = theme.ink;
            foreach (var label in form.GetComponentsInChildren<Text>(true))
                label.color = theme.ink;
            foreach (var input in new[] { ui.addressInput, ui.nicknameInput })
            {
                input.GetComponent<Image>().color = theme.surface;
                var outline = input.GetComponent<Outline>() ?? input.gameObject.AddComponent<Outline>();
                outline.effectColor = theme.line;
                outline.effectDistance = new Vector2(1, -1);
                input.textComponent.fontSize = theme.bodySize;
                input.textComponent.color = theme.ink;
                var placeholder = (Text)input.placeholder;
                placeholder.fontSize = theme.bodySize;
                placeholder.color = theme.muted;
                SetHeight(input.gameObject, theme.controlHeight);
                var colors = input.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = Color.white;
                colors.selectedColor = new Color(.90f, .94f, 1);
                colors.disabledColor = new Color(.9f, .91f, .93f);
                input.colors = colors;
            }
            ui.addressNotice.fontSize = 13;
            ui.addressNotice.color = theme.muted;
            foreach (var button in new[] { ui.saveButton, ui.resetButton, ui.healthButton, ui.loginButton })
            {
                bool primary = button == ui.loginButton;
                button.GetComponent<Image>().color = Color.white;
                var colors = button.colors;
                colors.normalColor = primary ? theme.accent : theme.secondary;
                colors.highlightedColor = primary ? theme.accentHover : new Color32(203, 213, 225, 255);
                colors.selectedColor = colors.highlightedColor;
                colors.pressedColor = primary ? theme.accentPressed : new Color32(148, 163, 184, 255);
                colors.disabledColor = new Color32(203, 213, 225, 255);
                colors.fadeDuration = .12f;
                button.colors = colors;
                var text = button.GetComponentInChildren<Text>();
                text.color = primary ? Color.white : theme.ink;
                text.fontSize = theme.bodySize;
                text.fontStyle = primary ? FontStyle.Bold : FontStyle.Normal;
                SetHeight(button.gameObject, theme.controlHeight);
                SetHeight(button.transform.parent.gameObject, theme.controlHeight);
            }
        }

        private static void SetHeight(GameObject target, float height)
        {
            var element = target.GetComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = height;
        }
    }
}
