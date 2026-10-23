using System;
using System.Linq;
using UnityEngine;

namespace SmartTraining.Editor
{
    public static class RoomBrowserChecks
    {
        public static void Run()
        {
            void Check(bool ok, string name)
            {
                if (!ok)
                    throw new Exception(name);
                Debug.Log("UNITY PASS: " + name);
            }
            var source = new[]
            {
                new RoomSummary { id = "cold", name = "Alpha 훈련", availableSlots = 3, state = 0, createdAt = "2026-10-01T00:00:00Z" },
                new RoomSummary { id = "private", name = "alpha 비밀", availableSlots = 2, state = 3, hasPassword = true, createdAt = "2026-10-03T00:00:00Z" },
                new RoomSummary { id = "locked", name = "Alpha 잠김", availableSlots = 5, state = 3, isEntryLocked = true, createdAt = "2026-10-04T00:00:00Z" },
                new RoomSummary { id = "running", name = "Alpha 진행", availableSlots = 5, state = 3, isRunning = true, createdAt = "2026-10-05T00:00:00Z" },
                new RoomSummary { id = "reserved", name = "Beta 예약", availableSlots = 0, connectedUsers = 1, reservedUsers = 1, maxUsers = 2, state = 3 },
                new RoomSummary { id = "closing", name = "Alpha 종료", availableSlots = 8, state = 4 }
            };
            RoomSummary[] Apply(string query, bool available, RoomProtectionFilter protection, RoomSortOrder sort = RoomSortOrder.Newest) =>
                RoomBrowser.Apply(source, query, available, protection, sort);
            Check(Apply(" ALPHA ", false, RoomProtectionFilter.All).Length == 5 && Apply("훈련", false, RoomProtectionFilter.All).Single().id == "cold",
                "room browser matches Korean text and ignores case and surrounding search whitespace");
            Check(Apply("", true, RoomProtectionFilter.All).Select(r => r.id).SequenceEqual(new[] { "private", "cold" }),
                "availability excludes reserved full, locked, running and closing rooms while allowing cold server launch");
            Check(Apply("alpha", true, RoomProtectionFilter.Protected).Single().id == "private" && Apply("alpha", true, RoomProtectionFilter.Public).Single().id == "cold",
                "room browser combines name, availability and protection filters");
            Check(Apply("", true, RoomProtectionFilter.All, RoomSortOrder.Vacancies).Select(r => r.id).SequenceEqual(new[] { "cold", "private" }),
                "vacancy sort uses remaining slots including reservations");
            Check(Apply("absent", true, RoomProtectionFilter.All).Length == 0 && source[0].id == "cold",
                "empty search result preserves original room snapshot ordering");
        }
    }
}
