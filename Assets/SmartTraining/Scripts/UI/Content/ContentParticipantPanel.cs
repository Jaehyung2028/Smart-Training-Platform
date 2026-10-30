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
        private readonly Vector2 preferredSize;
        private readonly Text title;
        private readonly RectTransform content;
        private readonly Dictionary<string, Text> rows = new();
        private bool open;
        private readonly List<ContentParticipantState> displayed = new();
        private readonly List<ContentParticipantState> ordered = new();
        private readonly HashSet<string> participantIds = new();
        private static readonly Comparison<ContentParticipantState> compare = (first, second) => first.IsMaster != second.IsMaster ? (first.IsMaster ? -1 : 1) : StringComparer.OrdinalIgnoreCase.Compare(first.Name, second.Name);

        public bool IsOpen => open;


        public ContentParticipantPanel(Transform parent, Font font)
        {
            var view = UiPrefab.Bind("ContentParticipantPanel", parent);
            root = view.Get<GameObject>("root");
            preferredSize = ((RectTransform)root.transform).sizeDelta;
            title = view.Get<Text>("title");
            content = view.Get<RectTransform>("content");
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
                Mathf.Min(preferredSize.x, parentSize.x - 32),
                Mathf.Min(preferredSize.y, parentSize.y - 150)
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
                    row = UiPrefab.Instantiate("ContentParticipantRow", content).GetComponent<Text>();
                    rows.Add(participant.Id, row);
                }

                row.transform.SetSiblingIndex(index++);
                row.text = participant.Name + (participant.IsLocal ? " (나)" : "") + (participant.IsMaster ? " [방장]" : "") + "\n" + participant.ConnectionLabel;
                row.color = participant.IsLocal ? new Color32(74, 222, 128, 255) : Color.white;
            }
        }
    }
}
