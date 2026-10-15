namespace SmartTraining
{
    public sealed partial class ApiLobbyScreen
    {
        private string roomSearch = "";
        private bool availableRoomsOnly;
        private RoomProtectionFilter roomProtectionFilter;
        private RoomSortOrder roomSort;
        private RoomSummary[] GetDisplayedRooms() => RoomBrowser.Apply(rooms, roomSearch, availableRoomsOnly, roomProtectionFilter, roomSort);
        private void ResetRoomBrowser()
        { roomSearch = ""; availableRoomsOnly = false; roomProtectionFilter = RoomProtectionFilter.All; roomSort = RoomSortOrder.Newest; }
    }
}
