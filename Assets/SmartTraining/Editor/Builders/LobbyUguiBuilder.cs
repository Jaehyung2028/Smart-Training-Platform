using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SmartTraining.Editor
{
    public static class LobbyUguiBuilder
    {
        public const string AssetPath = "Assets/SmartTraining/Resources/SmartTraining/LobbyUI.prefab";
        private static readonly Color Ink = new Color(.12f, .18f, .29f), Blue = new Color(.15f, .36f, .77f);
        [MenuItem("Smart Training/UI/Open Login and Lobby Prefab")]
        public static void OpenPrefab()
        {
            EnsureAssets();
            AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath));
        }
        [MenuItem("Smart Training/UI/Create Login and Lobby UI (if missing)")]
        public static void EnsureAssets()
        {
            if (AssetDatabase.LoadAssetAtPath<LobbyUguiView>(AssetPath) != null)
            {
                EnsureRootInteraction(AssetPath);
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            var root = new GameObject("LobbyUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(LobbyUguiView));
            try
            {
                var view = root.GetComponent<LobbyUguiView>();
                view.canvas = root.GetComponent<Canvas>();
                view.canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                view.canvas.sortingOrder = 20;
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 800);
                scaler.matchWidthOrHeight = .5f;
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                var background = Node("Background", root.transform);
                Stretch(background);
                background.gameObject.AddComponent<Image>().color = new Color(.055f, .09f, .16f);
                var card = Node("Main Panel", background);
                card.anchorMin = new Vector2(.08f, .04f);
                card.anchorMax = new Vector2(.92f, .96f);
                card.offsetMin = card.offsetMax = Vector2.zero;
                card.gameObject.AddComponent<Image>().color = new Color(.96f, .97f, .99f);
                Vertical(card, 18, 12);
                Label("SMART TRAINING", card, 28, 42);
                view.connectionText = Label("로그인 후 서버에 연결됩니다.", card, 16, 26);
                view.loginPanel = Panel("Login Panel", card);
                Vertical(view.loginPanel.transform, 0, 9);
                Label("서버에 연결하고 훈련을 시작하세요", view.loginPanel.transform, 21, 34);
                Label("서버 주소", view.loginPanel.transform, 16, 24);
                view.addressInput = Input("Server Address", view.loginPanel.transform, "http://localhost:5229", ServerAddress.MaxLength);
                var addressActions = Row("Address Actions", view.loginPanel.transform);
                view.saveButton = Button("주소 저장", addressActions);
                view.resetButton = Button("기본 주소로 초기화", addressActions);
                view.addressNotice = Label("", view.loginPanel.transform, 14, 42);
                Label("닉네임", view.loginPanel.transform, 16, 24);
                view.nicknameInput = Input("Nickname", view.loginPanel.transform, "닉네임을 입력해주세요", 20);
                var loginActions = Row("Login Actions", view.loginPanel.transform);
                view.healthButton = Button("서버 연결 확인", loginActions);
                view.loginButton = Button("로그인", loginActions);
                view.lobbyPanel = Panel("Lobby Browser Panel", card);
                Vertical(view.lobbyPanel.transform, 0, 8);
                Flex(view.lobbyPanel);
                view.userText = Label("콘텐츠 로비", view.lobbyPanel.transform, 22, 34);
                var actions = Row("Lobby Actions", view.lobbyPanel.transform);
                view.refreshButton = Button("목록 새로고침", actions);
                view.reconnectButton = Button("알림 다시 연결", actions);
                view.logoutButton = Button("로그아웃", actions);
                var scroll = Node("Lobby Scroll", view.lobbyPanel.transform);
                Flex(scroll.gameObject);
                scroll.gameObject.AddComponent<Image>().color = Color.white;
                var viewport = Node("Viewport", scroll);
                Stretch(viewport);
                viewport.gameObject.AddComponent<Image>().color = Color.white;
                viewport.gameObject.AddComponent<RectMask2D>();
                var content = Node("Content", viewport);
                content.anchorMin = new Vector2(0, 1);
                content.anchorMax = Vector2.one;
                content.pivot = new Vector2(.5f, 1);
                content.sizeDelta = Vector2.zero;
                Vertical(content, 5, 6);
                content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var sr = scroll.gameObject.AddComponent<ScrollRect>();
                sr.viewport = viewport;
                sr.content = content;
                sr.horizontal = false;
                sr.movementType = ScrollRect.MovementType.Clamped;
                sr.scrollSensitivity = 28;
                view.lobbyContent = content;
                view.emptyText = Label("조회된 로비가 없습니다.", content, 18, 60);
                view.lobbyRowTemplate = Button("로비 이름\n상태 · 사용자 · 방", content);
                view.lobbyRowTemplate.name = "Lobby Row Template";
                view.lobbyRowTemplate.GetComponent<LayoutElement>().preferredHeight = 72;
                view.lobbyRowTemplate.gameObject.SetActive(false);
                view.selectedDetail = Label("목록에서 로비를 선택해주세요.", view.lobbyPanel.transform, 17, 110);
                view.enterButton = Button("선택한 로비에 입장", view.lobbyPanel.transform);
                view.progressPanel = Panel("Lobby Join Progress", card);
                Vertical(view.progressPanel.transform, 10, 12);
                Flex(view.progressPanel);
                Label("로비 입장 중", view.progressPanel.transform, 24, 40);
                view.progressText = Label("로비 입장을 요청하고 있습니다…", view.progressPanel.transform, 20, 130);
                view.cancelButton = Button("로비 입장 취소", view.progressPanel.transform);
                view.failurePanel = Panel("Lobby Entry Notice", content);
                Vertical(view.failurePanel.transform, 8, 6);
                view.failureText = Label("", view.failurePanel.transform, 17, 64);
                var failureActions = Row("Failure Actions", view.failurePanel.transform);
                view.retryButton = Button("같은 로비에 다시 입장", failureActions);
                view.closeFailureButton = Button("안내 닫기", failureActions);
                view.failurePanel.transform.SetAsFirstSibling();
                view.statusText = Label("서버 연결을 확인한 뒤 로그인해주세요.", card, 16, 66);
                view.lobbyPanel.SetActive(false);
                view.progressPanel.SetActive(false);
                view.failurePanel.SetActive(false);
                UguiLoginDesigner.Apply(root, UguiLoginDesigner.LoadTheme());
                UguiCatalogDesigner.Apply(root, UguiLoginDesigner.LoadTheme());
                PrefabUtility.SaveAsPrefabAsset(root, AssetPath);
                AssetDatabase.SaveAssets();
                Debug.Log("UGUI PREFAB CREATED: " + AssetPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        internal static void EnsureRootInteraction(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponent<CanvasGroup>() == null)
                {
                    root.AddComponent<CanvasGroup>();

                }
                // RequireComponent may already have restored it while loading the old prefab.
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        internal static RectTransform Node(string name, Transform parent)
        {
            var node = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            node.SetParent(parent, false);
            return node;
        }
        internal static void Stretch(RectTransform node)
        {
            node.anchorMin = Vector2.zero;
            node.anchorMax = Vector2.one;
            node.offsetMin = node.offsetMax = Vector2.zero;
        }
        internal static void Height(GameObject node, float value)
        {
            var layout = node.AddComponent<LayoutElement>();
            layout.minHeight = value;
            layout.preferredHeight = value;
            layout.flexibleHeight = 0;
        }
        internal static void Flex(GameObject node)
        {
            var layout = node.AddComponent<LayoutElement>();
            layout.minHeight = 100;
            layout.flexibleHeight = 1;
        }
        internal static void Vertical(Transform node, int padding, int spacing)
        {
            var layout = node.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.spacing = spacing;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
        }
        internal static GameObject Panel(string name, Transform parent) => Node(name, parent).gameObject;
        internal static Transform Row(string name, Transform parent)
        {
            var row = Node(name, parent);
            Height(row.gameObject, 44);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            return row;
        }
        internal static Text Label(string text, Transform parent, int size, int height)
        {
            var node = Node(text.Length > 30 ? "Label" : text, parent);
            var label = node.gameObject.AddComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = text;
            label.fontSize = size;
            label.color = Ink;
            label.supportRichText = false;
            label.raycastTarget = false;
            label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            Height(node.gameObject, height);
            return label;
        }
        internal static Button Button(string title, Transform parent)
        {
            var node = Node(title, parent);
            var image = node.gameObject.AddComponent<Image>();
            image.color = Blue;
            var button = node.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(.8f, .88f, 1);
            colors.disabledColor = new Color(.55f, .6f, .68f, .7f);
            button.colors = colors;
            Height(node.gameObject, 44);
            var text = Label(title, node, 18, 30);
            UnityEngine.Object.DestroyImmediate(text.GetComponent<LayoutElement>());
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(12, 4);
            text.rectTransform.offsetMax = new Vector2(-12, -4);
            text.color = Color.white;
            return button;
        }
        internal static InputField Input(string name, Transform parent, string hint, int limit)
        {
            var node = Node(name, parent);
            Height(node.gameObject, 44);
            node.gameObject.AddComponent<Image>().color = Color.white;
            var input = node.gameObject.AddComponent<InputField>();
            var text = Label("", node, 20, 40);
            var placeholder = Label(hint, node, 18, 40);
            foreach (var label in new[] { text, placeholder })
            {
                UnityEngine.Object.DestroyImmediate(label.GetComponent<LayoutElement>());
                Stretch(label.rectTransform);
                label.rectTransform.offsetMin = new Vector2(12, 5);
                label.rectTransform.offsetMax = new Vector2(-12, -5);
            }
            placeholder.color = new Color(.45f, .5f, .6f);
            input.textComponent = text;
            input.placeholder = placeholder;
            input.characterLimit = limit;
            return input;
        }
    }
}
