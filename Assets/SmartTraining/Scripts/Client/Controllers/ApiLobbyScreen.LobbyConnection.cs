using System;
using System.Collections;

namespace SmartTraining
{
    public sealed partial class ApiLobbyScreen
    {
        private string lobbyJoinProgress = "", lobbyJoinName = "";
        private bool lobbyJoinCanCancel, lobbyJoinCancelRequested;
        private string lobbyJoinRetryId = "", lobbyJoinError = "";

        private void ClearLobbyJoinFailure() { lobbyJoinRetryId = lobbyJoinError = ""; }

        private void HandleLobbyJoinFailure(string id, string message)
        {
            Failure(message);
            if (api?.IsLoggedIn == true) { lobbyJoinRetryId = id; lobbyJoinError = message; }
        }

        private IEnumerator RetryLobbyJoin()
        {
            string id = lobbyJoinRetryId;
            if (string.IsNullOrEmpty(id) || api?.IsLoggedIn != true) yield break;
            bool checkedState = false;
            yield return api.GetMe(_ => checkedState = true, message => HandleLobbyJoinFailure(id, message));
            if (!checkedState) yield break;
            if (api.User.currentLobbyId == id)
            { ClearLobbyJoinFailure(); yield return LoadState(); status = "이미 로비에 입장했습니다."; yield break; }
            if (!string.IsNullOrEmpty(api.User.currentLobbyId) ||
                (!string.IsNullOrEmpty(api.User.pendingLobbyId) && api.User.pendingLobbyId != id))
            { HandleLobbyJoinFailure(id, "다른 로비의 입장 정보를 먼저 정리해주세요."); yield break; }
            if (api.User.pendingLobbyId == id)
            {
                yield return CancelLobbyEntry(id);
                if (api?.IsLoggedIn != true || api.User.pendingLobbyId == id || api.User.currentLobbyId == id) yield break;
            }
            yield return JoinLobby(id);
        }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private bool testPauseLobbyJoinAfterApproval;
#endif

        private void ClearLobbyJoinProgress()
        {
            lobbyJoinProgress = lobbyJoinName = "";
            lobbyJoinCanCancel = lobbyJoinCancelRequested = false;
        }

        private void RequestCancelLobbyJoin()
        {
            if (!lobbyJoinCanCancel || lobbyJoinCancelRequested) return;
            lobbyJoinCancelRequested = true;
            lobbyJoinProgress = "로비 입장 취소 중 — 입장 정보를 정리하고 있습니다…";
            RefreshUguiState();
        }

        private IEnumerator JoinLobby(string id)
        {
            if (api?.IsLoggedIn != true) { Failure("로그인이 필요합니다."); yield break; }
            ClearLobbyJoinFailure();
            var selected = Array.Find(lobbies, item => item.id == id);
            lobbyJoinName = selected?.name ?? id;

            lobbyJoinCancelRequested = false; lobbyJoinCanCancel = true;
            lobbyJoinProgress = selected?.serverState == "running"
                ? "1/2 · 실행 중인 로비에 입장을 요청하고 있습니다…"
                : "1/2 · 로비 서버 준비와 입장 승인을 기다리고 있습니다…";
            ForgetReconnect();
            try
            {
                bool joined = false;
                yield return api.JoinLobby(id, () => joined = true,
                    message => { if (!lobbyJoinCancelRequested) HandleLobbyJoinFailure(id, message); }, () => lobbyJoinCancelRequested);
                if (joined && !lobbyJoinCancelRequested)
                {
                    lobbyJoinProgress = "2/2 · 입장 승인 완료 — 로비 정보를 불러오고 있습니다…";
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    while (testPauseLobbyJoinAfterApproval && !lobbyJoinCancelRequested) yield return null;
#endif
                    if (!lobbyJoinCancelRequested) yield return LoadState();
                    if (!lobbyJoinCancelRequested && api?.User?.currentLobbyId == id)
                        status = "로비에 입장했습니다. 방을 만들거나 참가할 수 있습니다.";
                }
                lobbyJoinCanCancel = false;
                if (lobbyJoinCancelRequested) yield return CancelLobbyEntry(id);
            }
            finally
            {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                testPauseLobbyJoinAfterApproval = false;
#endif
                ClearLobbyJoinProgress();
            }
        }

        private IEnumerator CancelLobbyEntry(string id)
        {
            if (api?.IsLoggedIn != true) { Failure("로그인 상태를 확인할 수 없습니다. 다시 로그인해주세요."); yield break; }
            // The server may already have committed membership before the response was aborted.
            string cleanupError = null;
            yield return api.LeaveLobby(id, () => { }, message => cleanupError = message);
            bool confirmed = false;
            yield return api.GetMe(user => confirmed = user.currentLobbyId != id && user.pendingLobbyId != id,
                message => cleanupError = message);
            if (!confirmed)
            {
                Failure("로비 입장 취소를 확인하지 못했습니다. 내 정보를 새로고침하고 남은 입장 정보를 정리해주세요. " + cleanupError);
                yield break;
            }
            yield return LoadState();
            status = "로비 입장을 취소했습니다. 로비 목록에서 다시 참가할 수 있습니다.";
        }
    }
}
