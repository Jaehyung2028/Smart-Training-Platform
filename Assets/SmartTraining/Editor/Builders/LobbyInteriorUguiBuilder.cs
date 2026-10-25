using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static SmartTraining.Editor.LobbyUguiBuilder;

namespace SmartTraining.Editor
{
    public static class LobbyInteriorUguiBuilder
    {
        public const string InteriorPath = "Assets/SmartTraining/Resources/SmartTraining/LobbyInteriorUI.prefab";

        [MenuItem("Smart Training/UI/Open Lobby Interior Prefab")]
        public static void OpenPrefab()
        {
            EnsureAssets();
            AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<GameObject>(InteriorPath));
        }

        [MenuItem("Smart Training/UI/Create Lobby Interior UI (if missing)")]
        public static void EnsureAssets()
        {
            if (AssetDatabase.LoadAssetAtPath<LobbyInteriorUguiView>(InteriorPath) != null)
            {
                EnsureRootInteraction(InteriorPath);
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(InteriorPath));
            var root = new GameObject(
                "LobbyInteriorUI",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(LobbyInteriorUguiView)
            );
            try
            {
                var ui = root.GetComponent<LobbyInteriorUguiView>();
                ui.canvas = root.GetComponent<Canvas>();
                ui.canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                ui.canvas.sortingOrder = 21;
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 800);
                scaler.matchWidthOrHeight = .5f;
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                var background = Node("Background", root.transform);
                Stretch(background);
                background.gameObject.AddComponent<Image>().color = new Color(.055f, .09f, .16f);
                var main = Node("Main Panel", background);
                Anchors(main, new Vector2(.03f, .03f), new Vector2(.97f, .97f));
                main.gameObject.AddComponent<Image>().color = new Color(.96f, .97f, .99f);
                Vertical(main, 12, 6);
                ui.mainInteraction = main.gameObject.AddComponent<CanvasGroup>();
                var header = CompactRow("Header", main);
                header.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
                ui.heading = Label("콘텐츠 로비", header, 24, 36);
                ui.heading.GetComponent<LayoutElement>().flexibleWidth = 1;
                ui.notifications = SmallButton("알림 다시 연결", header);
                Width(ui.notifications.gameObject, 160);
                ui.logout = SmallButton("로그아웃", header);
                Width(ui.logout.gameObject, 110);
                ui.connection = Label("서버 알림 연결", main, 15, 24);
                ui.notice = Label("운영 안내", main, 16, 44);
                ui.notice.GetComponent<LayoutElement>().minHeight = 24;
                ui.notice.GetComponent<LayoutElement>().preferredHeight = -1;
                ui.notice.gameObject.SetActive(false);
                var body = Node("Lobby Body", main);
                Flex(body.gameObject);
                var columns = body.gameObject.AddComponent<HorizontalLayoutGroup>();
                columns.spacing = 14;
                columns.childControlHeight = columns.childControlWidth = true;
                columns.childForceExpandHeight = true;
                columns.childForceExpandWidth = false;
                var left = Node("Room Browser", body);
                Vertical(left, 0, 6);
                var ll = left.gameObject.AddComponent<LayoutElement>();
                ll.flexibleWidth = 2;
                ll.minWidth = 530;
                var right = Node("Participants and Chat", body);
                Vertical(right, 0, 6);
                var rl = right.gameObject.AddComponent<LayoutElement>();
                rl.flexibleWidth = 1;
                rl.minWidth = 300;
                var roomActions = CompactRow("Room Actions", left);
                ui.create = SmallButton("새 방 만들기", roomActions);
                ui.refresh = SmallButton("목록 새로고침", roomActions);
                ui.leave = SmallButton("로비 퇴장", roomActions);
                ui.search = Input("Room Name Search", left, "방 이름 검색", 60);
                Compact(ui.search.gameObject);
                var filters = CompactRow("Room Filters", left);
                ui.available = SmallButton("입장 가능만: 꺼짐", filters);
                ui.protection = SmallButton("전체 방", filters);
                ui.sort = SmallButton("최근 생성순", filters);
                ui.resetFilters = SmallButton("초기화", filters);
                foreach (var text in filters.GetComponentsInChildren<Text>())
                {
                    text.fontSize = 15;
                }

                ui.roomCount = Label("방 목록", left, 17, 24);
                var roomScroll = Scroll("Room List", left, out ui.roomsContent);
                Flex(roomScroll.gameObject);
                ui.emptyRooms = Label("생성된 방이 없거나 검색 조건에 맞는 방이 없습니다.", ui.roomsContent, 16, 70);
                ui.roomTemplate = Button("방 이름\n접속 · 예약 · 빈자리", ui.roomsContent);
                ui.roomTemplate.name = "Room Row Template";
                SetHeight(ui.roomTemplate.gameObject, 64);
                ui.roomTemplate.GetComponentInChildren<Text>().fontSize = 16;
                ui.roomTemplate.gameObject.SetActive(false);
                ui.roomDetail = Label("참가할 방을 선택해주세요.", left, 16, 70);
                ui.joinPassword = Input("Join Password", left, "선택한 방의 비밀번호", 32);
                Compact(ui.joinPassword.gameObject);
                ui.joinPassword.contentType = InputField.ContentType.Password;
                ui.joinPassword.gameObject.SetActive(false);
                var joinActions = CompactRow("Join Actions", left);
                ui.join = SmallButton("선택한 방에 참가", joinActions);
                ui.clearRoom = SmallButton("남은 방 접속 정리", joinActions);
                ui.clearRoom.gameObject.SetActive(false);
                ui.reconnectPanel = Panel("Reconnect Panel", left);
                Vertical(ui.reconnectPanel.transform, 0, 4);
                ui.reconnectPassword = Input("Reconnect Password", ui.reconnectPanel.transform, "이전 방 비밀번호 (필요한 경우)", 32);
                Compact(ui.reconnectPassword.gameObject);
                ui.reconnectPassword.contentType = InputField.ContentType.Password;
                var reconnectActions = CompactRow("Reconnect Actions", ui.reconnectPanel.transform);
                ui.reconnect = SmallButton("이전 방 재접속", reconnectActions);
                ui.forgetReconnect = SmallButton("재접속 대상 지우기", reconnectActions);
                ui.reconnectPanel.SetActive(false);
                ui.participantTitle = Label("참가자", right, 18, 24);
                var participantScroll = Scroll("Participant List", right, out ui.participantsContent);
                Flex(participantScroll.gameObject);
                ui.emptyParticipants = Label("참가자 목록을 불러오는 중…", ui.participantsContent, 16, 60);
                ui.participantTemplate = Button("참가자\n위치 · 방 보기", ui.participantsContent);
                ui.participantTemplate.name = "Participant Row Template";
                SetHeight(ui.participantTemplate.gameObject, 80);
                ui.participantTemplate.GetComponentInChildren<Text>().fontSize = 15;
                ui.participantTemplate.gameObject.SetActive(false);
                Label("로비 채팅", right, 18, 24);
                ui.chatScroll = Scroll("Lobby Chat", right, out ui.chatContent);
                Flex(ui.chatScroll.gameObject);
                ui.emptyChat = Label("아직 수신한 메시지가 없습니다.", ui.chatContent, 15, 44);
                ui.chatTemplate = Label("Message", ui.chatContent, 15, 40);
                Object.DestroyImmediate(ui.chatTemplate.GetComponent<LayoutElement>());
                ui.chatTemplate.name = "Chat Message Template";
                ui.chatTemplate.gameObject.SetActive(false);
                var chatActions = CompactRow("Chat Compose", right);
                chatActions.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
                ui.chatInput = Input("Chat Message", chatActions, "메시지 입력 (200자 이내)", 200);
                Compact(ui.chatInput.gameObject);
                ui.chatInput.GetComponent<LayoutElement>().flexibleWidth = 1;
                ui.chatInput.textComponent.fontSize = 16;
                ui.sendChat = SmallButton("전송", chatActions);
                Width(ui.sendChat.gameObject, 65);
                ui.chatStatus = Label("", right, 14, 34);
                ui.status = Label("", main, 16, 52);
                ui.createModal = Overlay("Create Room Modal", root.transform, out var createCard, new Vector2(.16f, .12f), new Vector2(.84f, .88f));
                Label("새 방 만들기", createCard, 24, 34);
                Label("방 이름", createCard, 16, 22);
                ui.createName = Input("Create Room Name", createCard, "방 이름 (기본 2~30자)", 30);
                Label("최대 인원", createCard, 16, 22);
                ui.createCapacity = Input("Create Capacity", createCard, "최대 인원 (기본 1~8명)", 3);
                ui.createCapacity.contentType = InputField.ContentType.IntegerNumber;
                Label("비밀번호 (선택)", createCard, 16, 22);
                ui.createPassword = Input("Create Password", createCard, "선택 사항 (기본 4~32자)", 32);
                ui.createPassword.contentType = InputField.ContentType.Password;
                ui.createContent = Button("콘텐츠 선택", createCard);
                ui.createStatus = Label("", createCard, 16, 60);
                var createActions = Row("Create Actions", createCard);
                ui.submitCreate = Button("방 생성", createActions);
                ui.closeCreate = Button("닫기", createActions);
                ui.createModal.SetActive(false);
                ui.progressModal = Overlay(
                    "Room Join Progress",
                    root.transform,
                    out var progressCard,
                    new Vector2(.14f, .30f),
                    new Vector2(.86f, .70f)
                );
                Label("방 입장 중", progressCard, 24, 36);
                ui.progress = Label("방 서버와 연결을 확인하고 있습니다…", progressCard, 18, 110);
                ui.cancelJoin = Button("방 입장 취소", progressCard);
                Label("접속 확인 후 방 화면으로 이동합니다.", progressCard, 16, 28);
                ui.progressModal.SetActive(false);
                UguiInteriorDesigner.Apply(root, UguiLoginDesigner.LoadTheme());
                PrefabUtility.SaveAsPrefabAsset(root, InteriorPath);
                AssetDatabase.SaveAssets();
                Debug.Log("UGUI INTERIOR PREFAB CREATED: " + InteriorPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void Anchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void SetHeight(GameObject node, float height)
        {
            var layout = node.GetComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = height;
            layout.flexibleHeight = 0;
        }

        private static void Compact(GameObject node) => SetHeight(node, 36);

        private static Transform CompactRow(string name, Transform parent)
        {
            var row = Row(name, parent);
            Compact(row.gameObject);
            return row;
        }

        private static Button SmallButton(string name, Transform parent)
        {
            var button = Button(name, parent);
            Compact(button.gameObject);
            button.GetComponentInChildren<Text>().fontSize = 16;
            return button;
        }

        private static void Width(GameObject node, float width)
        {
            var layout = node.GetComponent<LayoutElement>();
            layout.minWidth = layout.preferredWidth = width;
            layout.flexibleWidth = 0;
        }

        private static ScrollRect Scroll(string name, Transform parent, out RectTransform content)
        {
            var node = Node(name, parent);
            node.gameObject.AddComponent<Image>().color = Color.white;
            var viewport = Node("Viewport", node);
            Stretch(viewport);
            viewport.gameObject.AddComponent<Image>().color = Color.white;
            viewport.gameObject.AddComponent<RectMask2D>();
            content = Node("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = Vector2.zero;
            Vertical(content, 4, 4);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = node.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28;
            return scroll;
        }

        private static GameObject Overlay(string name, Transform parent, out RectTransform card, Vector2 min, Vector2 max)
        {
            var overlay = Node(name, parent);
            Stretch(overlay);
            overlay.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .65f);
            card = Node("Card", overlay);
            Anchors(card, min, max);
            card.gameObject.AddComponent<Image>().color = new Color(.96f, .97f, .99f);
            Vertical(card, 18, 8);
            return overlay.gameObject;
        }
    }
}
