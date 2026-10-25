using System;
using System.Globalization;
using System.Linq;

namespace SmartTraining
{
    public enum RoomProtectionFilter
    {
        All,
        Public,
        Protected
    }

    public enum RoomSortOrder
    {
        Newest,
        Vacancies
    }

    public static class RoomBrowser
    {
        // 프로세스가 아직 없는 방도 입장 요청을 받고 서버를 실행할 수 있습니다.
        public static bool CanJoin(RoomSummary room) => room != null && room.availableSlots > 0 && !room.isEntryLocked && !room.isRunning && room.state >= 0 && room.state <= 3;

        private static DateTimeOffset Created(RoomSummary room) => DateTimeOffset.TryParse(room.createdAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date) ? date : DateTimeOffset.MinValue;

        public static RoomSummary[] Apply(
            RoomSummary[] source,
            string query,
            bool availableOnly,
            RoomProtectionFilter protection,
            RoomSortOrder sort
        )
        {
            string search = query?.Trim() ?? "";
            var matches = (source ?? Array.Empty<RoomSummary>()).Where(room => room != null && (search.Length == 0 || (room.name ?? "").IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) && (!availableOnly || CanJoin(room)) && (protection == RoomProtectionFilter.All || room.hasPassword == (protection == RoomProtectionFilter.Protected)));
            var ordered = sort == RoomSortOrder.Vacancies ? matches.OrderByDescending(room => room.availableSlots).ThenByDescending(Created) : matches.OrderByDescending(Created);
            return ordered.ThenBy(room => room.id, StringComparer.Ordinal).ToArray();
        }
    }
}
