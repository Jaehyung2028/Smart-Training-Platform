using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static SmartTraining.Editor.LobbyUguiBuilder;

namespace SmartTraining.Editor
{
    public static class RoomUguiBuilder
    {
        public const string AssetPath = "Assets/SmartTraining/Resources/SmartTraining/RoomUI.prefab";

        [MenuItem("Smart Training/UI/Open Room Prefab")]
        public static void OpenPrefab()
        {
            EnsureAssets();
            AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath));
        }

        [MenuItem("Smart Training/UI/Create Room UI (if missing)")]
        public static void EnsureAssets()
        {
            if (AssetDatabase.LoadAssetAtPath<RoomUguiView>(AssetPath) != null)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
            var root = new GameObject(
                "RoomUI",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(CanvasGroup),
                typeof(RoomUguiView)
            );
            try
            {
                var ui = root.GetComponent<RoomUguiView>();
                ui.canvas = root.GetComponent<Canvas>();
                ui.canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                ui.canvas.sortingOrder = 22;
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 800);
                scaler.matchWidthOrHeight = .5f;
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                var background = Node("Background", root.transform);
                Stretch(background);
                background.gameObject.AddComponent<Image>().color = new Color(.055f, .09f, .16f);
                var main = Node("Main Panel", background);
                Anchors(main, new Vector2(.025f, .025f), new Vector2(.975f, .975f));
                main.gameObject.AddComponent<Image>().color = new Color(.96f, .97f, .99f);
                Vertical(main, 12, 6);
                ui.mainInteraction = main.gameObject.AddComponent<CanvasGroup>();
                ui.heading = Label("훈련방", main, 24, 32);
                ui.summary = Label("대기 중 · 접속 0/4 · 준비 0/0\n방장 확인 중…", main, 17, 48);
                ui.connection = Label("", main, 14, 38);
                var operationScroll = Scroll("Operating Notice", main, out var operationContent);
                SetHeight(operationScroll.gameObject, 44);
                ui.operationNoticePanel = operationScroll.gameObject;
                ui.notice = Label("", operationContent, 15, 44);
                Object.DestroyImmediate(ui.notice.GetComponent<LayoutElement>());
                var actions = CompactRow("Room Actions", main);
                ui.ready = SmallButton("준비", actions);
                ui.phase = SmallButton("전원 준비 후 시작", actions);
                ui.settings = SmallButton("방장 설정", actions);
                var body = Row("Body", main);
                Flex(body.gameObject);
                body.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
                var left = Node("Participants Panel", body);
                Vertical(left, 0, 5);
                var ll = left.gameObject.AddComponent<LayoutElement>();
                ll.flexibleWidth = 1;
                ll.minWidth = 280;
                var tabs = CompactRow("Participant Tabs", left);
                ui.roomParticipants = SmallButton("현재 방", tabs);
                ui.lobbyParticipants = SmallButton("현재 로비", tabs);
                ui.participantTitle = Label("현재 방 참가자", left, 18, 26);
                var list = Scroll("Participant List", left, out ui.participantsContent);
                Flex(list.gameObject);
                list.GetComponent<LayoutElement>().minHeight = 80;
                ui.emptyParticipants = Label("참가자 정보를 기다리는 중…", ui.participantsContent, 16, 48);
                ui.participantTemplate = Node("Participant Template", ui.participantsContent);
                Vertical(ui.participantTemplate, 6, 4);
                ui.participantTemplate.gameObject.AddComponent<Image>().color = new Color(.93f, .95f, .98f);
                ui.participantTemplate.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var detail = Label("참가자\n준비 상태 · 연결 지연\n위치", ui.participantTemplate, 16, 70);
                detail.name = "Details";
                var rowActions = CompactRow("Actions", ui.participantTemplate);
                var transfer = SmallButton("방장 위임", rowActions);
                transfer.name = "Transfer";
                var kick = SmallButton("강퇴", rowActions);
                kick.name = "Kick";
                ui.participantTemplate.gameObject.SetActive(false);
                var participantFooter = CompactRow("Participant Footer", left);
                ui.refreshParticipants = SmallButton("로비 명단 새로고침", participantFooter);
                var right = Node("Chat and Movement", body);
                Vertical(right, 0, 5);
                var rl = right.gameObject.AddComponent<LayoutElement>();
                rl.flexibleWidth = 1;
                rl.minWidth = 340;
                var channels = CompactRow("Chat Channels", right);
                ui.roomChat = SmallButton("방 채팅", channels);
                ui.lobbyChat = SmallButton("로비 채팅", channels);
                ui.chatScroll = Scroll("Chat Messages", right, out ui.chatContent);
                Flex(ui.chatScroll.gameObject);
                ui.chatScroll.GetComponent<LayoutElement>().minHeight = 80;
                ui.emptyChat = Label("아직 수신한 메시지가 없습니다.", ui.chatContent, 16, 40);
                ui.chatTemplate = Label("Message", ui.chatContent, 16, 40);
                ui.chatTemplate.name = "Message Template";
                Object.DestroyImmediate(ui.chatTemplate.GetComponent<LayoutElement>());
                ui.chatTemplate.gameObject.SetActive(false);
                var compose = CompactRow("Compose", right);
                compose.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
                ui.chatInput = Input("Chat Message", compose, "메시지 입력 (200자 이내)", 200);
                SetHeight(ui.chatInput.gameObject, 36);
                ui.chatInput.GetComponent<LayoutElement>().flexibleWidth = 1;
                ui.chatInput.textComponent.fontSize = 16;
                ui.sendChat = SmallButton("전송", compose);
                Width(ui.sendChat.gameObject, 68);
                ui.chatStatus = Label("채팅 연결 중…", right, 14, 24);
                ui.openMap = SmallButton("위치·이동 보기", participantFooter);
                ui.mapModal = Overlay("Movement Map", root.transform, out var mapCard, new Vector2(.18f, .12f), new Vector2(.82f, .88f));
                Label("참가자 위치·이동", mapCard, 24, 34);
                var movement = Row("Movement", mapCard);
                Flex(movement.gameObject);
                movement.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
                ui.map = Node("Position Map", movement);
                ui.map.gameObject.AddComponent<Image>().color = new Color(.85f, .89f, .94f);
                var mapLayout = ui.map.gameObject.AddComponent<LayoutElement>();
                mapLayout.minWidth = 220;
                mapLayout.flexibleWidth = 2;
                ui.markerTemplate = Node("Marker Template", ui.map);
                ui.markerTemplate.anchorMin = ui.markerTemplate.anchorMax = new Vector2(.5f, .5f);
                ui.markerTemplate.sizeDelta = new Vector2(12, 12);
                ui.markerTemplate.gameObject.AddComponent<Image>().raycastTarget = false;
                var markerLabel = Label("이름", ui.markerTemplate, 12, 24);
                Object.DestroyImmediate(markerLabel.GetComponent<LayoutElement>());
                markerLabel.rectTransform.anchorMin = markerLabel.rectTransform.anchorMax = new Vector2(.5f, 1);
                markerLabel.rectTransform.pivot = new Vector2(.5f, 0);
                markerLabel.rectTransform.sizeDelta = new Vector2(120, 22);
                markerLabel.alignment = TextAnchor.MiddleCenter;
                ui.markerTemplate.gameObject.SetActive(false);
                var controls = Node("Movement Controls", movement);
                Vertical(controls, 0, 4);
                controls.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                Label("위치 확인 · 초록: 나 / 파랑: 참가자\nWASD·방향키 또는 버튼 길게 누르기", controls, 13, 44);
                var upRow = CompactRow("Forward", controls);
                var up = Move("앞 ↑", upRow, Vector2.up);
                var bottom = CompactRow("Directions", controls);
                var back = Move("뒤 ↓", bottom, Vector2.down);
                var west = Move("←", bottom, Vector2.left);
                var east = Move("→", bottom, Vector2.right);
                ui.movementButtons = new[]
                {
                    up,
                    back,
                    west,
                    east
                };
                ui.closeMap = Button("위치 창 닫기", mapCard);
                ui.mapModal.SetActive(false);
                var noticeHeader = CompactRow("Notice Header", main);
                Label("방 알림 · 최근 20개", noticeHeader, 16, 30);
                ui.clearNotices = SmallButton("알림 지우기", noticeHeader);
                Width(ui.clearNotices.gameObject, 130);
                var noticesScroll = Scroll("Notices", main, out var noticesContent);
                SetHeight(noticesScroll.gameObject, 62);
                ui.notices = Label("새로운 방 알림이 없습니다.", noticesContent, 14, 40);
                Object.DestroyImmediate(ui.notices.GetComponent<LayoutElement>());
                ui.status = Label("", main, 16, 42);
                var footer = CompactRow("Footer", main);
                ui.leave = SmallButton("방에서 나가기", footer);
                ui.logout = SmallButton("로그아웃", footer);
                ui.settingsModal = Overlay("Room Settings", root.transform, out var settingsCard, new Vector2(.15f, .08f), new Vector2(.85f, .92f));
                ui.settingsModal.AddComponent<CanvasGroup>();
                Label("방장 설정", settingsCard, 24, 34);
                var settingsScroll = Scroll("Settings Scroll", settingsCard, out var settingsContent);
                Flex(settingsScroll.gameObject);
                Label("방 이름", settingsContent, 16, 24);
                ui.nameInput = Input("Room Name", settingsContent, "방 이름 (기본 2~30자)", 30);
                Label("최대 인원", settingsContent, 16, 24);
                ui.capacityInput = Input("Capacity", settingsContent, "최대 인원 (기본 1~8명)", 3);
                ui.capacityInput.contentType = InputField.ContentType.IntegerNumber;
                ui.saveSettings = Button("방 이름·최대 인원 적용", settingsContent);
                Label("접속·예약 인원보다 정원을 줄일 수 없습니다.", settingsContent, 15, 28);
                ui.passwordStatus = Label("방 비밀번호", settingsContent, 16, 28);
                ui.passwordInput = Input("Password", settingsContent, "새 비밀번호 (4~32자)", 32);
                ui.passwordInput.contentType = InputField.ContentType.Password;
                ui.savePassword = Button("비밀번호 설정·변경", settingsContent);
                ui.clearPassword = Button("비밀번호 해제 · 공개 방으로 변경", settingsContent);
                ui.entryLock = Button("새 참가자 입장 잠그기", settingsContent);
                ui.closeRoom = Button("방 닫기 · 참가자 전원 로비로 이동", settingsContent);
                ui.settingsStatus = Label("", settingsCard, 16, 66);
                ui.closeSettings = Button("설정 창 닫기", settingsCard);
                ui.settingsModal.SetActive(false);
                ui.confirmationModal = Overlay(
                    "Room Action Confirmation",
                    root.transform,
                    out var confirmCard,
                    new Vector2(.18f, .22f),
                    new Vector2(.82f, .78f)
                );
                Label("방 관리 요청 확인", confirmCard, 24, 38);
                ui.confirmationText = Label("", confirmCard, 18, 140);
                var confirmActions = Row("Confirmation Actions", confirmCard);
                ui.confirm = Button("확인 후 실행", confirmActions);
                ui.cancel = Button("취소", confirmActions);
                ui.confirmationModal.SetActive(false);
                UguiRoomDesigner.Apply(root, UguiLoginDesigner.LoadTheme());
                PrefabUtility.SaveAsPrefabAsset(root, AssetPath);
                AssetDatabase.SaveAssets();
                Debug.Log("UGUI ROOM PREFAB CREATED: " + AssetPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static RoomMoveButton Move(string text, Transform parent, Vector2 direction)
        {
            var button = SmallButton(text, parent);
            var hold = button.gameObject.AddComponent<RoomMoveButton>();
            hold.direction = direction;
            return hold;
        }

        private static void Anchors(RectTransform node, Vector2 min, Vector2 max)
        {
            node.anchorMin = min;
            node.anchorMax = max;
            node.offsetMin = node.offsetMax = Vector2.zero;
        }

        private static void SetHeight(GameObject node, float height)
        {
            var layout = node.GetComponent<LayoutElement>();
            if (layout == null)
            {
                layout = node.AddComponent<LayoutElement>();
            }

            layout.minHeight = layout.preferredHeight = height;
            layout.flexibleHeight = 0;
        }

        private static Transform CompactRow(string name, Transform parent)
        {
            var row = Row(name, parent);
            SetHeight(row.gameObject, 36);
            return row;
        }

        private static Button SmallButton(string name, Transform parent)
        {
            var button = Button(name, parent);
            SetHeight(button.gameObject, 36);
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
            var root = Node(name, parent);
            root.gameObject.AddComponent<Image>().color = Color.white;
            var viewport = Node("Viewport", root);
            Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            content = Node("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1);
            content.sizeDelta = Vector2.zero;
            Vertical(content, 4, 4);
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = root.gameObject.AddComponent<ScrollRect>();
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
            Vertical(card, 16, 8);
            return overlay.gameObject;
        }
    }
}
