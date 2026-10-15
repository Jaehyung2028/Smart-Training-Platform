using System;
using System.Linq;

namespace SmartTraining
{
    public sealed partial class ApiLobbyScreen
    {
        private RoomUguiView roomView;
        internal string UiActiveRoomId => networkRoomId;
        internal void UiRefreshRoomParticipants() { if (UiRoomCanAct) RunOperation(LoadLobbyParticipants(UiCurrentLobby), "참가자 목록을 새로고침하고 있습니다."); }
        internal bool UseUguiRoom => roomView != null && isActiveAndEnabled && network?.IsConnected == true &&
            string.IsNullOrEmpty(roomJoinProgress) && string.IsNullOrEmpty(lobbyJoinProgress);
        internal RoomPlayer UiLocalPlayer => network?.LocalPlayer;
        internal RoomPlayer[] UiRoomPlayers => network?.Players ?? Array.Empty<RoomPlayer>();
        internal bool UiRoomCanAct => UseUguiRoom && !UiProcessing && !HasPendingRoomAction && UiLocalPlayer != null;
        internal bool UiRoomCanManage => UiRoomCanAct && UiLocalPlayer.IsRoomMaster;
        internal bool UiRoomCanEdit => UiRoomCanManage && !UiLocalPlayer.RoomIsRunning && !UiLocalPlayer.PhasePending;
        internal bool UiRoomCanReady => UiRoomCanAct && !UiLocalPlayer.RoomIsRunning && !UiLocalPlayer.PhasePending;
        internal bool UiRoomCanPhase => UiRoomCanManage && !UiLocalPlayer.PhasePending &&
            (UiLocalPlayer.RoomIsRunning || network.GetReadiness().AllReady);
        internal string UiRoomHeading => UiLocalPlayer?.RoomName ?? api?.User?.currentRoomName ?? "방 접속 중";
        internal string UiRoomConnection => "방 서버: " + (network?.ServerEndpoint ?? "");
        internal string UiRoomSummary()
        {
            var local = UiLocalPlayer;
            if (local == null) return "플레이어 생성과 접속 확정을 기다리는 중…";
            var master = Array.Find(UiRoomPlayers, p => p.UserId == local.RoomMasterUserId);
            var ready = network.GetReadiness();
            return (local.PhasePending ? "상태 변경 중…" : local.RoomIsRunning ? "진행 중" : "대기 중") +
                " · 접속 " + UiRoomPlayers.Length + "/" + local.RoomCapacity + " · 준비 " + ready.ReadyCount + "/" + ready.ParticipantCount +
                "\n방장: " + (master?.UserName ?? "확인 중…") + " · 내 역할: " + (local.IsRoomMaster ? "방장" : "참가자") +
                " · 새 입장: " + (local.RoomEntryLocked ? "잠김" : "허용");
        }
        internal void UiToggleReady() { if (UiRoomCanReady) RunOperation(SetReady(!UiLocalPlayer.IsReady), "준비 상태를 변경하고 있습니다."); }
        internal void UiTogglePhase() { if (UiRoomCanPhase) RunOperation(ChangeRoomPhase(!UiLocalPlayer.RoomIsRunning), "방 진행 상태를 변경하고 있습니다."); }
        internal void UiLeaveRoom() { if (UiRoomCanAct) { ForgetReconnect(); RunOperation(LeaveRoom("방에서 나왔습니다."), "방에서 나가고 있습니다."); } }
        internal void UiRoomLogout() { if (UiRoomCanAct) RunOperation(Logout(), "로그아웃하고 있습니다."); }
        internal void UiRequestTransfer(string id) { if (UiRoomCanManage) RequestRoomAction(RoomAction.TransferMaster, id); }
        internal void UiRequestKick(string id) { if (UiRoomCanManage) RequestRoomAction(RoomAction.Kick, id); }
        internal void UiRequestClose() { if (UiRoomCanManage) RequestRoomAction(RoomAction.Close); }
        internal bool UiRoomConfirmation => ValidateRoomAction();
        internal string UiRoomConfirmationText => pendingRoomAction switch
        {
            RoomAction.TransferMaster => actionTargetName + "님에게 방장을 넘길까요?\n내 방장 권한은 대상에게 넘어갑니다.",
            RoomAction.Kick => actionTargetName + "님을 강퇴할까요?\n대상은 로비로 이동하며 다시 입장할 수 있습니다.",
            RoomAction.Close => "이 방을 닫을까요?\n모든 참가자가 로비로 이동하고 방이 삭제됩니다.",
            _ => ""
        };
        internal void UiConfirmRoomAction() => ConfirmRoomAction();
        internal void UiCancelRoomAction() => CancelRoomAction();
        internal string UiEditRoomName { get => editRoomName; set => editRoomName = value; }
        internal string UiEditRoomCapacity { get => editRoomCapacity; set => editRoomCapacity = value; }
        internal string UiEditRoomPassword { get => editRoomPassword; set => editRoomPassword = value; }
        internal void UiPrepareRoomSettings()
        {
            if (!UiRoomCanManage) return;
            editRoomName = UiLocalPlayer.RoomName; editRoomCapacity = UiLocalPlayer.RoomCapacity.ToString(); editRoomPassword = "";
        }
        internal void UiSaveRoomSettings() { if (UiRoomCanEdit) RunOperation(UpdateRoomSettings(editRoomName, editRoomCapacity), "방 설정을 저장하고 있습니다."); }
        internal void UiSaveRoomPassword() { if (UiRoomCanManage && editRoomPassword.Length > 0) RunOperation(UpdateRoomPassword(editRoomPassword), "방 비밀번호를 저장하고 있습니다."); }
        internal void UiClearRoomPassword() { if (UiRoomCanManage) RunOperation(UpdateRoomPassword(""), "방 비밀번호를 해제하고 있습니다."); }
        internal void UiToggleRoomLock() { if (UiRoomCanManage) RunOperation(SetRoomEntryLock(!UiLocalPlayer.RoomEntryLocked), "입장 허용 상태를 변경하고 있습니다."); }
        internal string UiRoomPasswordStatus => "방 비밀번호: " + (Array.Find(rooms, x => x.id == networkRoomId)?.hasPassword == true ? "설정됨" : "없음");
        internal int UiRoomChatChannel => chatChannel;
        internal void UiSelectRoomChat(int channel)
        {
            if (channel != 0 && channel != 1 || chatChannel == channel) return;
            chatChannel = channel; chatInput = ""; chatStatus = "";
        }
        internal bool UiCanSendRoomScreenChat => UiRoomCanAct && UiCanChat && (chatChannel == 0 || RoomChatIsBound());
        internal string UiRoomChatStatus => UiProcessing ? UiStatus : UiChatConnected && (chatChannel == 0 || RoomChatIsBound()) ? chatStatus : "채팅 연결 중…";
        internal LobbyChatMessage[] UiRoomScreenMessages => (chatChannel == 1 ? roomChatMessages : chatMessages).ToArray();
        internal void UiSendRoomScreenChat() { if (UiCanSendRoomScreenChat) RunOperation(chatChannel == 1 ? SendRoomChat() : SendLobbyChat(), "채팅을 전송하고 있습니다."); }
        internal string UiRoomNotices => string.Join("\n", roomNotices.Messages.Select(x => "[" + x.ObservedAt.ToString("HH:mm:ss") + "] " + x.Text));
        internal void UiClearRoomNotices() { if (UiRoomCanAct) roomNotices.ClearMessages(); }
    }
}
