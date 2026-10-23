using System;
using UnityEngine;
namespace SmartTraining
{
    public sealed partial class ApiLobbyScreen
    {
        private LobbyUguiView uguiView;
        private void RefreshUguiState()
        {
            if (uguiView != null)
                uguiView.RefreshState();
            if (interiorView != null)
                interiorView.RefreshState();
            if (roomView != null)
                roomView.RefreshState();
        }
        private void ResetUguiTransientState()
        {
            if (uguiView != null)
                uguiView.ResetTransientState();
            if (interiorView != null)
                interiorView.ResetTransientState();
            if (roomView != null)
                roomView.ResetTransientState();
        }
        private void EnsureUgui()
        {
            var layout = GetComponentInChildren<UguiSceneLayout>(true);
            if (layout != null)
            {
                uguiView = layout.loginView;
                interiorView = layout.interiorView;
                roomView = layout.roomView;
            }
            if (EnsureView(ref uguiView, "SmartTraining/LobbyUI"))
                uguiView.Bind(this);
            if (EnsureView(ref interiorView, "SmartTraining/LobbyInteriorUI"))
                interiorView.Bind(this);
            if (EnsureView(ref roomView, "SmartTraining/RoomUI"))
                roomView.Bind(this);
        }

        private bool EnsureView<T>(ref T view, string resourcePath) where T : Component
        {
            if (view == null)
                view = GetComponentInChildren<T>(true);
            if (view != null)
                return true;
            var prefab = Resources.Load<T>(resourcePath);
            if (prefab == null)
            {
                Debug.LogError("UI 프리팹을 찾을 수 없습니다: Resources/" + resourcePath, this);
                return false;
            }
            view = Instantiate(prefab, transform);
            return true;
        }
        private static bool ShutdownBlocksEntry(LobbySummary lobby) => lobby != null &&
            (lobby.operatingMode == "scheduled" || lobby.operatingMode == "maintenance");

        internal bool UseUgui => uguiView != null && isActiveAndEnabled &&
            network?.IsConnected != true && string.IsNullOrEmpty(roomJoinProgress) &&
            (api?.IsLoggedIn != true || string.IsNullOrEmpty(api.User.currentLobbyId) || !string.IsNullOrEmpty(lobbyJoinProgress));
        internal bool UiLoggedIn => api?.IsLoggedIn == true;
        internal bool UiProcessing => busy || WaitingForExitCleanup;
        internal string UiAddress
        {
            get => serverUrl; set
            {
                if (CanEditServerAddress())
                    serverUrl = value;
            }
        }
        internal string UiNickname
        {
            get => nickname; set
            {
                if (!UiProcessing && !UiLoggedIn)
                    nickname = value;
            }
        }
        internal string UiUser => api?.User?.name ?? "";
        internal string UiConnection => ServerConnectionStatusText();
        internal string UiStatus => WaitingForExitCleanup ? "이전 접속을 정리하는 중…" : busy
            ? "처리 중… " + (roomJoinCancelRequested || lobbyJoinCancelRequested ? "입장 취소 정보를 정리하고 있습니다." : operationMessage) : status;
        internal string UiAddressNotice => (string.IsNullOrEmpty(serverAddressMessage) ? "다른 PC에서는 localhost 대신 서버 PC의 IP 주소를 입력해주세요." : serverAddressMessage) + (string.IsNullOrEmpty(savedServerUrl) ? "" : "\n저장된 주소: " + savedServerUrl);
        internal LobbySummary[] UiLobbies => lobbies;
        internal string UiSelectedLobby => selectedLobbyId;
        internal string UiProgress => lobbyJoinProgress;
        internal string UiProgressTitle => lobbyJoinName;
        internal bool UiCanCancel => lobbyJoinCanCancel && !lobbyJoinCancelRequested;
        internal string UiFailure => lobbyJoinError;
        internal bool UiCanRetry => !UiProcessing && !string.IsNullOrEmpty(lobbyJoinRetryId) &&
            Array.Find(lobbies, x => x.id == lobbyJoinRetryId)?.isMaintenance != true;
        internal bool UiCanEnter => UiLoggedIn && !UiProcessing && string.IsNullOrEmpty(api.User.currentLobbyId) &&
            Array.Find(lobbies, x => x.id == selectedLobbyId) is LobbySummary selected &&
            !selected.isMaintenance && !ShutdownBlocksEntry(selected);
        internal void UiHealth() => RunOperation(CheckHealth(), "서버 연결을 확인하고 있습니다.");
        internal void UiLogin()
        {
            if (!UiLoggedIn)
                RunOperation(Login(), "로그인하고 있습니다.");
        }
        internal void UiLogout()
        {
            if (UiLoggedIn)
                RunOperation(Logout(), "로그아웃하고 있습니다.");
        }
        internal void UiRefresh()
        {
            if (UiLoggedIn)
                RunOperation(LoadState(), "목록을 새로고침하고 있습니다.");
        }
        internal void UiSelect(string id)
        {
            if (UiProcessing || !Array.Exists(lobbies, item => item.id == id))
                return;
            selectedLobbyId = id;
            ClearLobbyData();
        }
        internal void UiEnter()
        {
            if (UiCanEnter)
                RunOperation(JoinLobby(selectedLobbyId), "로비에 입장하고 있습니다.");
        }
        internal void UiCancel() => RequestCancelLobbyJoin();
        internal void UiRetry()
        {
            if (UiCanRetry)
                RunOperation(RetryLobbyJoin(), "로비 입장을 다시 시도하고 있습니다.");
        }
        internal void UiCloseFailure() => ClearLobbyJoinFailure();
        internal void UiSaveAddress() => SaveServerAddress();
        internal void UiResetAddress() => ResetServerAddress();
        internal void UiReconnectNotifications()
        {
            if (UiLoggedIn)
                RunOperation(RetryLiveConnection(), "알림 연결을 다시 열고 있습니다.");
        }
        internal string UiSelectedDetail()
        {
            var selected = Array.Find(lobbies, x => x.id == selectedLobbyId);
            if (selected == null)
                return "목록에서 이용할 콘텐츠 로비를 선택해주세요.";
            string state = selected.operatingMode == "scheduled" ? "종료 예정" : selected.isMaintenance ? "점검 중" : "입장 가능";
            string text = selected.name + "\n" + state + " · 사용자 " + selected.userCount + " · 방 " + selected.roomCount;
            if (selected.isMaintenance)
                text += "\n사유: " + selected.operatingReason;
            if (selected.operatingMode == "scheduled" && DateTimeOffset.TryParse(selected.shutdownAtUtc, out var at))
                text += "\n종료 " + at.ToLocalTime().ToString("MM/dd HH:mm:ss") + " · 남은 시간 " + Math.Max(0, (long)Math.Ceiling((at - DateTimeOffset.UtcNow).TotalSeconds)) + "초";
            return text;
        }
    }
}
