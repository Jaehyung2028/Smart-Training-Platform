using System;
using System.Linq;

namespace SmartTraining
{
    public sealed partial class ApiLobbyScreen
    {
        private readonly RoomNoticeTracker roomNotices = new RoomNoticeTracker();
        private RoomNetworkClient noticeNetwork;
        private TrainingApiClient noticeApi;
        private string noticeRoomId = "", noticeUserId = "";

        private void ClearRoomNotices()
        {
            roomNotices.Reset();
            noticeNetwork = null;
            noticeApi = null;
            noticeRoomId = noticeUserId = "";
        }
        private void UpdateRoomNotices()
        {
            var local = network?.IsConnected == true ? network.LocalPlayer : null;
            if (api?.IsLoggedIn != true || local == null || local.UserId != api.User.id ||
                string.IsNullOrEmpty(networkRoomId) || api.User.currentRoomId != networkRoomId)
            {
                ClearRoomNotices();
                return;
            }
            if (noticeNetwork != network || noticeApi != api || noticeRoomId != networkRoomId || noticeUserId != local.UserId)
            {
                ClearRoomNotices();
                noticeNetwork = network;
                noticeApi = api;
                noticeRoomId = networkRoomId;
                noticeUserId = local.UserId;
            }
            var players = network.Players;
            if (players.Length == 0 || players.Any(p => !p.IsSpawned || string.IsNullOrEmpty(p.UserId) || string.IsNullOrEmpty(p.UserName)))
                return;

            roomNotices.Observe(players.Select(p => new RoomNoticeParticipant(p.UserId, p.UserName, p.OwnerClientId)).ToArray(), local.RoomMasterUserId);
        }
    }
}
