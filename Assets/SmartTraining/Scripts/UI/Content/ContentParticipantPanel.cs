using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace SmartTraining
{
    // HUD에는 현재 참가자의 표시 데이터만 전달합니다. 목록 UI는 네트워크 객체를 직접 조회하지 않습니다.
    internal readonly struct ContentParticipantState : IEquatable<ContentParticipantState>
    {
        public readonly string Id, Name, ConnectionLabel;
        public readonly bool IsLocal, IsMaster;

        public ContentParticipantState(string id, string name, bool isLocal, bool isMaster, string connectionLabel)
        {
            Id = id;
            Name = name;
            IsLocal = isLocal;
            IsMaster = isMaster;
            ConnectionLabel = connectionLabel;
        }


        public bool Equals(ContentParticipantState other)
        {
            return Id == other.Id &&
                Name == other.Name &&
                ConnectionLabel == other.ConnectionLabel &&
                IsLocal == other.IsLocal &&
                IsMaster == other.IsMaster;
        }
    }


    // 콘텐츠 종류에 관계없이 함께 접속한 방 참가자를 표시하는 읽기 전용 목록입니다.
    internal sealed class ContentParticipantPanel
    {
        private readonly GameObject root;
        private readonly Text title;
        private readonly RectTransform content;
        private readonly Font font;
        private readonly Dictionary<string, Text> rows = new();
        private bool open;
        private readonly List<ContentParticipantState> displayed = new();
        private readonly List<ContentParticipantState> ordered = new();
        private readonly HashSet<string> participantIds = new();
        private static readonly Comparison<ContentParticipantState> compare = (first, second) => first.IsMaster != second.IsMaster ? (first.IsMaster ? -1 : 1) : StringComparer.OrdinalIgnoreCase.Compare(first.Name, second.Name);

        public bool IsOpen => open;


        public ContentParticipantPanel(Transform parent, Font font)
        {
            this.font = font;
            root = new GameObject("Content Participants", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-16, -68);
            rect.sizeDelta = new Vector2(360, 300);
            root.GetComponent<Image>().color = new Color32(15, 23, 42, 245);
            title = Label("Content Participants Title", rect, 19);
            title.text = "함께 참여 중 · 0명";
            title.rectTransform.anchorMin = Vector2.up;
            title.rectTransform.anchorMax = Vector2.one;
            title.rectTransform.offsetMin = new Vector2(12, -42);
            title.rectTransform.offsetMax = new Vector2(-12, -10);
            var viewport = new GameObject(
                "Content Participants Scroll",
                typeof(RectTransform),
                typeof(Image),
                typeof(RectMask2D),
                typeof(ScrollRect)
            );
            viewport.transform.SetParent(rect, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(12, 12);
            viewportRect.offsetMax = new Vector2(-12, -48);
            viewport.GetComponent<Image>().color = new Color32(30, 41, 59, 255);
            content = new GameObject(
                "Content Participants Rows",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter)
            ).GetComponent<RectTransform>();
            content.SetParent(viewportRect, false);
            content.anchorMin = Vector2.up;
            content.anchorMax = Vector2.one;
            content.pivot = Vector2.up;
            content.sizeDelta = Vector2.zero;
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 8, 8);
            layout.spacing = 8;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.GetComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.scrollSensitivity = 24;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            root.SetActive(false);
        }


        public void Toggle()
        {
            open = !open;
            root.SetActive(open);
        }


        public void Close()
        {
            open = false;
            root.SetActive(false);
        }


        public void Refresh(ContentParticipantState[] participants, bool visible)
        {
            if (!visible)
            {
                Close();
            }

            root.SetActive(visible && open);
            if (!root.activeSelf)
            {
                return;
            }

            participants ??= Array.Empty<ContentParticipantState>();
            var parentSize = ((RectTransform)root.transform.parent).rect.size;
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(
                Mathf.Min(360, parentSize.x - 32),
                Mathf.Min(300, parentSize.y - 150)
            );
            bool changed = displayed.Count != participants.Length;
            for (int position = 0; !changed && position < participants.Length; position++)
            {
                changed = !displayed[position].Equals(participants[position]);
            }

            if (!changed)
            {
                return;
            }

            title.text = "함께 참여 중 · " + participants.Length + "명";
            displayed.Clear();
            displayed.AddRange(participants);
            ordered.Clear();
            ordered.AddRange(participants);
            ordered.Sort(compare);
            participantIds.Clear();
            foreach (var participant in participants)
            {
                participantIds.Add(participant.Id);
            }

            UguiViewUtility.RemoveMissing(rows, participantIds);
            int index = 0;
            foreach (var participant in ordered)
            {
                if (!rows.TryGetValue(participant.Id, out var row))
                {
                    row = Label("Content Participant Row", content, 16);
                    row.gameObject.AddComponent<LayoutElement>().minHeight = 48;
                    rows.Add(participant.Id, row);
                }

                row.transform.SetSiblingIndex(index++);
                row.text = participant.Name + (participant.IsLocal ? " (나)" : "") + (participant.IsMaster ? " [방장]" : "") + "\n" + participant.ConnectionLabel;
                row.color = participant.IsLocal ? new Color32(74, 222, 128, 255) : Color.white;
            }
        }


        private Text Label(string name, Transform parent, int size)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(parent, false);
            text.font = font;
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = Color.white;
            text.supportRichText = false;
            text.raycastTarget = false;
            return text;
        }
    }
}
