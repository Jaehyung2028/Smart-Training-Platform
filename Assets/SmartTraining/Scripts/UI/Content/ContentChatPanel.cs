using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SmartTraining
{
    // 기존 방 채팅의 표시·초안만 담당합니다. 구독·전송·권한 판단은 전달받은 상태와 콜백을 사용합니다.
    internal sealed class ContentChatPanel
    {
        private readonly GameObject root;
        private readonly RectTransform content;
        private readonly ScrollRect scroll;
        private readonly InputField input;
        private readonly Button send;
        private readonly Text status;
        private readonly Font font;
        private readonly Action<string, Action<bool>> sendAction;
        private readonly ChatMessageListView messagesView;
        private string roomId = "";
        private bool open, pending, canSend;
        private int context;

        public bool IsOpen => open;

        public bool IsEditing => root.activeInHierarchy && input.isFocused;


        public ContentChatPanel(Transform parent, Font font, Action<string, Action<bool>> sendAction)
        {
            this.font = font;
            this.sendAction = sendAction;
            root = Box("Content Room Chat", parent, new Color32(15, 23, 42, 245)).gameObject;
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-16, -68);
            rect.sizeDelta = new Vector2(360, 300);
            var title = Label("Content Chat Title", root.transform, 18);
            title.text = "방 채팅";
            Place(title.rectTransform, new Vector2(12, -40), new Vector2(-12, -10), true);
            var viewport = Box("Content Chat Scroll", root.transform, new Color32(30, 41, 59, 255)).rectTransform;
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(12, 86);
            viewport.offsetMax = new Vector2(-12, -44);
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24;
            content = new GameObject(
                "Content Chat Messages",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter)
            ).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = Vector2.up;
            content.anchorMax = Vector2.one;
            content.pivot = Vector2.up;
            content.sizeDelta = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 6, 6);
            layout.spacing = 6;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            messagesView = new ChatMessageListView(scroll, CreateMessageRow, Color.white, new Color32(74, 222, 128, 255));
            var field = Box("Content Chat Input", root.transform, new Color32(30, 41, 59, 255));
            Place(field.rectTransform, new Vector2(12, 44), new Vector2(-86, 78));
            input = field.gameObject.AddComponent<InputField>();
            input.targetGraphic = field;
            input.characterLimit = 200;
            input.lineType = InputField.LineType.SingleLine;
            var text = Label("Content Chat Input Text", field.transform, 16);
            Stretch(text.rectTransform, 8, 4);
            input.textComponent = text;
            var placeholder = Label("Content Chat Placeholder", field.transform, 15);
            Stretch(placeholder.rectTransform, 8, 4);
            placeholder.text = "메시지 입력";
            placeholder.color = new Color32(148, 163, 184, 255);
            input.placeholder = placeholder;
            var sendImage = Box("Content Chat Send", root.transform, new Color32(37, 99, 235, 255));
            var sendRect = sendImage.rectTransform;
            sendRect.anchorMin = sendRect.anchorMax = new Vector2(1, 0);
            sendRect.offsetMin = new Vector2(-78, 44);
            sendRect.offsetMax = new Vector2(-12, 78);
            send = sendImage.gameObject.AddComponent<Button>();
            send.targetGraphic = sendImage;
            var sendLabel = Label("Content Chat Send Label", sendRect, 16);
            sendLabel.text = "전송";
            sendLabel.alignment = TextAnchor.MiddleCenter;
            Stretch(sendLabel.rectTransform, 2, 2);
            send.onClick.AddListener(Send);
            status = Label("Content Chat Status", root.transform, 12);
            Place(status.rectTransform, new Vector2(12, 8), new Vector2(-12, 40));
            status.color = new Color32(148, 163, 184, 255);
            root.SetActive(false);
        }


        public void Toggle()
        {
            if (open)
            {
                Close();
            }
            else
            {
                open = true;
            }
        }


        public void Close()
        {
            open = false;
            UguiViewUtility.ClearSelection(root.transform);
            root.SetActive(false);
        }


        public void Refresh(
            string currentRoom,
            string localUserId,
            IReadOnlyList<LobbyChatMessage> messages,
            string message,
            bool visible,
            bool allowSend
        )
        {
            if (roomId != currentRoom)
            {
                roomId = currentRoom;
                context++;
                pending = false;
                input.SetTextWithoutNotify("");
                messagesView.Clear();
                Close();
            }

            if (!visible)
            {
                Close();
            }

            root.SetActive(visible && open);
            canSend = allowSend && !pending;
            input.interactable = !pending && visible;
            send.interactable = canSend && !string.IsNullOrWhiteSpace(input.text);
            status.text = pending ? "전송 중…" : message;
            if (!root.activeSelf)
            {
                return;
            }

            var parentSize = ((RectTransform)root.transform.parent).rect.size;
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(
                Mathf.Min(360, parentSize.x - 32),
                Mathf.Min(300, parentSize.y - 150)
            );
            messagesView.Refresh(messages, localUserId, roomId);
        }


        public void Dispose()
        {
            messagesView.Dispose();
        }


        private Text CreateMessageRow()
        {
            var row = Label("Content Chat Message", content, 15);
            row.horizontalOverflow = HorizontalWrapMode.Wrap;
            row.verticalOverflow = VerticalWrapMode.Overflow;
            return row;
        }


        private void Send()
        {
            if (!canSend || string.IsNullOrWhiteSpace(input.text))
            {
                return;
            }

            string draft = input.text;
            int requestContext = context;
            pending = true;
            sendAction(draft, success =>
            {
                if (context != requestContext || root == null)
                {
                    return;
                }

                pending = false;
                if (success && input.text == draft)
                {
                    input.SetTextWithoutNotify("");
                }
            });
        }


        private Text Label(string name, Transform parent, int size)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(parent, false);
            text.font = font;
            text.fontSize = size;
            text.color = Color.white;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.alignment = TextAnchor.MiddleLeft;
            return text;
        }


        private static Image Box(string name, Transform parent, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(parent, false);
            image.color = color;
            return image;
        }


        private static void Place(RectTransform rect, Vector2 minimum, Vector2 maximum, bool top = false)
        {
            rect.anchorMin = new Vector2(0, top ? 1 : 0);
            rect.anchorMax = new Vector2(1, top ? 1 : 0);
            rect.offsetMin = minimum;
            rect.offsetMax = maximum;
        }


        private static void Stretch(RectTransform rect, float x, float y)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(x, y);
            rect.offsetMax = new Vector2(-x, -y);
        }
    }
}
