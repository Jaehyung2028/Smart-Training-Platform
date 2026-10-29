using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace SmartTraining
{
    // 로비·대기실·콘텐츠의 채팅 행을 재사용합니다. 풀은 화면별로 소유하고 화면 종료 시 비웁니다.
    internal sealed class ChatMessageListView : IDisposable
    {
        private readonly ScrollRect scroll;
        private readonly Color defaultColor, ownColor;
        private readonly Dictionary<string, Text> rows = new();
        private readonly HashSet<string> messageIds = new();
        private readonly List<string> removed = new();
        private readonly ObjectPool<Text> pool;
        private string context, displayedUserId;

        public ChatMessageListView(ScrollRect scroll, Func<Text> createRow, Color defaultColor, Color? ownColor = null)
        {
            this.scroll = scroll;
            this.defaultColor = defaultColor;
            this.ownColor = ownColor ?? ChatMessagePresentation.OwnMessageColor;
            // 수신 이력 상한 100개에 맞춰 대기 행도 최대 100개만 유지합니다.
            pool = new ObjectPool<Text>(createRow, row =>
            {
                // 새 메시지는 끝에 추가됩니다. 재사용 시에만 순서를 옮겨 매 프레임 계층 조회를 피합니다.
                row.transform.SetAsLastSibling();
                row.gameObject.SetActive(true);
            }, row =>
            {
                if (row != null)
                {
                    row.text = "";
                    row.gameObject.SetActive(false);
                }
            }, row =>
            {
                if (row != null)
                {
                    UnityEngine.Object.Destroy(row.gameObject);
                }
            }, true, 8, 100);
        }


        public void Clear()
        {
            foreach (var row in rows.Values)
            {
                pool.Release(row);
            }

            rows.Clear();
            messageIds.Clear();
            removed.Clear();
            context = null;
            displayedUserId = null;
        }


        public void Refresh(IReadOnlyList<LobbyChatMessage> messages, string localUserId, string currentContext)
        {
            if (context != currentContext)
            {
                Clear();
                context = currentContext;
            }

            messages ??= Array.Empty<LobbyChatMessage>();
            bool follow = rows.Count == 0 || scroll.verticalNormalizedPosition < .05f;
            messageIds.Clear();
            for (int index = 0; index < messages.Count; index++)
            {
                messageIds.Add(messages[index].id);
            }

            removed.Clear();
            foreach (var id in rows.Keys)
            {
                if (!messageIds.Contains(id))
                {
                    removed.Add(id);
                }
            }

            foreach (var id in removed)
            {
                pool.Release(rows[id]);
                rows.Remove(id);
            }

            bool changed = removed.Count > 0;
            bool userChanged = displayedUserId != localUserId;
            for (int index = 0; index < messages.Count; index++)
            {
                var message = messages[index];
                bool added = !rows.TryGetValue(message.id, out var row);
                if (added)
                {
                    row = pool.Get();
                    rows.Add(message.id, row);
                    changed = true;
                }

                if (added || userChanged)
                {
                    ChatMessagePresentation.SetChatMessage(row, message, localUserId, defaultColor, ownColor);
                }

            }

            displayedUserId = localUserId;
            if (changed && follow)
            {
                // 다른 Canvas까지 강제 갱신하지 않고 이 목록의 배치만 계산합니다.
                LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
                scroll.verticalNormalizedPosition = 0;
            }
        }


        public void Dispose()
        {
            Clear();
            pool.Clear();
        }
    }
}
