using System;
using System.Collections;

namespace SmartTraining
{
    public sealed partial class ApiLobbyScreen
    {
        private LobbyParticipant[] lobbyParticipants = Array.Empty<LobbyParticipant>();
        private string participantsLobbyId = "";
        private TrainingApiClient participantsApi;
        private bool participantsLoaded;

        private void ClearParticipants()
        {
            lobbyParticipants = Array.Empty<LobbyParticipant>();
            participantsLobbyId = "";
            participantsApi = null;
            participantsLoaded = false;
        }
        private void SyncParticipantsContext()
        {
            var lobby = api?.IsLoggedIn == true ? api.User.currentLobbyId ?? "" : "";
            if (lobby == participantsLobbyId && api == participantsApi)
                return;
            ClearParticipants();
            participantsLobbyId = lobby;
            participantsApi = api;
        }
        private IEnumerator LoadLobbyParticipants(string lobby)
        {
            var source = api;
            if (string.IsNullOrEmpty(lobby) || source?.IsLoggedIn != true || source.User.currentLobbyId != lobby)
                yield break;
            yield return source.GetLobbyParticipants(lobby, items =>
            {
                if (api != source || api.User.currentLobbyId != lobby)
                    return;
                SyncParticipantsContext();
                lobbyParticipants = items;
                participantsLoaded = true;
            }, message =>
            {
                if (api != source || api.User?.currentLobbyId != lobby)
                    return;
                ClearParticipants();
                ScheduleLiveRetry(message);
                if (api?.IsLoggedIn == true)
                    status = message;
            });
        }
        private static string ParticipantStateLabel(LobbyParticipant participant)
        {
            if (participant.connectionState == "reserved")
                return "방 접속 예약 — " + participant.roomName;
            if (participant.connectionState == "connected")
                return "방 접속 중 — " + participant.roomName +
                (participant.isRoomMaster ? " [방장]" : "") + (participant.roomIsRunning ? " [진행 중]" : " [대기 중]");
            return "로비 대기";
        }
    }
}
