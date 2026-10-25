using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartTraining
{
    public readonly struct RoomNoticeParticipant
    {
        public string UserId { get; }

        public string Name { get; }

        public ulong ConnectionId { get; }

        public RoomNoticeParticipant(string userId, string name, ulong connectionId)
        {
            UserId = userId;
            Name = name;
            ConnectionId = connectionId;
        }
    }

    public readonly struct RoomNotice
    {
        public string Text { get; }

        public DateTimeOffset ObservedAt { get; }

        public RoomNotice(string text)
        {
            Text = text;
            ObservedAt = DateTimeOffset.Now;
        }
    }

    // 동기화된 상태 변화를 클라이언트에서 관찰한 기록입니다. 서버의 영구 이벤트 이력은 아닙니다.
    public sealed class RoomNoticeTracker
    {
        public const int Limit = 20;

        private readonly List<RoomNotice> messages = new List<RoomNotice>();

        private Dictionary<string, RoomNoticeParticipant> previous;

        private string previousMaster;

        public IReadOnlyList<RoomNotice> Messages => messages;

        public long Revision { get; private set; }

        public void ClearMessages() => messages.Clear();

        public void Reset()
        {
            messages.Clear();
            previous = null;
            previousMaster = null;
        }

        private void Add(string text)
        {
            messages.Add(new RoomNotice(text));
            Revision++;
            if (messages.Count > Limit)
            {
                messages.RemoveAt(0);
            }
        }

        public void Observe(RoomNoticeParticipant[] participants, string masterId)
        {
            if (participants == null ||
                participants.Length == 0 ||
                participants.Any(p => string.IsNullOrEmpty(p.UserId) ||
                string.IsNullOrEmpty(p.Name)) ||
                participants.Select(p => p.UserId).Distinct().Count() != participants.Length)
            {
                return;
            }

            var current = participants.ToDictionary(p => p.UserId);
            if (previous != null)
            {
                foreach (var old in previous.Values.OrderBy(p => p.UserId, StringComparer.Ordinal))
                {
                    if (!current.ContainsKey(old.UserId))
                    {
                        Add(old.Name + "님이 퇴장했습니다.");
                    }
                }

                foreach (var participant in current.Values.OrderBy(p => p.UserId, StringComparer.Ordinal))
                {
                    if (!previous.TryGetValue(participant.UserId, out var old))
                    {
                        Add(participant.Name + "님이 입장했습니다.");
                    }
                    else if (old.ConnectionId != participant.ConnectionId)
                    {
                        Add(participant.Name + "님이 다시 접속했습니다.");
                    }
                }
            }

            // 플레이어 생성·제거보다 방장 정보가 늦게 올 수 있으므로 새 방장을 확인할 때까지 기다립니다.
            if (!string.IsNullOrEmpty(masterId) && current.TryGetValue(masterId, out var master))
            {
                if (previousMaster != null && previousMaster != masterId)
                {
                    Add(master.Name + "님이 방장이 되었습니다.");
                }

                previousMaster = masterId;
            }

            previous = current;
        }
    }
}
