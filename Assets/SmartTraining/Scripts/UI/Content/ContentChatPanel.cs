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
        private readonly Vector2 preferredSize;
        private readonly RectTransform content;
        private readonly ScrollRect scroll;
        private readonly InputField input;
        private readonly Button send;
        private readonly Text status;
        private readonly Action<string, Action<bool>> sendAction;
        private readonly ChatMessageListView messagesView;
        private string roomId = "";
        private bool open, pending, canSend;
        private int context;

        public bool IsOpen => open;

        public bool IsEditing => root.activeInHierarchy && input.isFocused;


        public ContentChatPanel(Transform parent, Font font, Action<string, Action<bool>> sendAction)
        {
            var view = UiPrefab.Bind("ContentChatPanel", parent);
            root = view.Get<GameObject>("root");
            preferredSize = ((RectTransform)root.transform).sizeDelta;
            content = view.Get<RectTransform>("content");
            scroll = view.Get<ScrollRect>("scroll");
            input = view.Get<InputField>("input");
            send = view.Get<Button>("send");
            status = view.Get<Text>("status");
            this.sendAction = sendAction;
            messagesView = new ChatMessageListView(scroll, CreateMessageRow, Color.white, new Color32(74, 222, 128, 255));
            send.onClick.AddListener(Send);
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
                Mathf.Min(preferredSize.x, parentSize.x - 32),
                Mathf.Min(preferredSize.y, parentSize.y - 150)
            );
            messagesView.Refresh(messages, localUserId, roomId);
        }


        public void Dispose()
        {
            messagesView.Dispose();
        }


        private Text CreateMessageRow()
        {
            return UiPrefab.Instantiate("ContentChatMessage", content).GetComponent<Text>();
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
    }
}
