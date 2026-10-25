using System;

namespace SmartTraining
{
    public sealed partial class ApiLobbyScreen
    {
        private enum RoomAction
        {
            None,
            TransferMaster,
            Kick,
            Close
        }

        private RoomAction pendingRoomAction;

        private RoomNetworkClient actionNetwork;

        private TrainingApiClient actionApi;

        private string actionLobbyId = "", actionRoomId = "", actionTargetId = "", actionTargetName = "";

        private ulong actionOwnerId, actionTargetOwnerId;

        private bool HasPendingRoomAction => pendingRoomAction != RoomAction.None;

        private void ClearRoomAction()
        {
            pendingRoomAction = RoomAction.None;
            actionNetwork = null;
            actionApi = null;
            actionLobbyId = actionRoomId = actionTargetId = actionTargetName = "";
            actionOwnerId = actionTargetOwnerId = 0;
        }

        private void RequestRoomAction(RoomAction action, string targetId = "")
        {
            if (busy || WaitingForExitCleanup || HasPendingRoomAction || action == RoomAction.None)
            {
                return;
            }

            var local = network?.LocalPlayer;
            if (api?.IsLoggedIn != true ||
                network?.IsConnected != true ||
                local == null ||
                !local.IsRoomMaster ||
                local.UserId != api.User.id ||
                string.IsNullOrEmpty(networkRoomId) ||
                string.IsNullOrEmpty(networkLobbyId))
            {
                return;
            }

            RoomPlayer target = null;
            if (action != RoomAction.Close)
            {
                target = Array.Find(network.Players, p => p.UserId == targetId && p.UserId != local.UserId);
                if (target == null)
                {
                    return;
                }
            }

            pendingRoomAction = action;
            actionNetwork = network;
            actionApi = api;
            actionLobbyId = networkLobbyId;
            actionRoomId = networkRoomId;
            actionOwnerId = local.OwnerClientId;
            actionTargetId = target?.UserId ?? "";
            actionTargetName = target?.UserName ?? "";
            actionTargetOwnerId = target?.OwnerClientId ?? 0;
        }

        private bool ValidateRoomAction()
        {
            if (!HasPendingRoomAction)
            {
                return false;
            }

            var local = network?.LocalPlayer;
            bool valid = network == actionNetwork && api == actionApi && api?.IsLoggedIn == true && network?.IsConnected == true && local != null && local.IsRoomMaster && local.UserId == api.User.id && local.OwnerClientId == actionOwnerId && networkLobbyId == actionLobbyId && networkRoomId == actionRoomId;
            if (valid && pendingRoomAction != RoomAction.Close)
            {
                valid = Array.Exists(network.Players, p => p.UserId == actionTargetId && p.OwnerClientId == actionTargetOwnerId);
            }

            if (valid)
            {
                return true;
            }

            ClearRoomAction();
            status = "방 또는 참가자 상태가 바뀌어 확인 요청을 취소했습니다. 현재 목록에서 다시 선택해주세요.";
            return false;
        }

        private void CancelRoomAction()
        {
            ClearRoomAction();
            status = "방 관리 요청을 취소했습니다.";
        }

        private void ConfirmRoomAction()
        {
            if (busy || WaitingForExitCleanup || !ValidateRoomAction())
            {
                return;
            }

            var action = pendingRoomAction;
            string targetId = actionTargetId, targetName = actionTargetName;
            ClearRoomAction();
            switch (action)
            {
                case RoomAction.TransferMaster:
                    RunOperation(TransferRoomMaster(targetId, targetName), "방장을 위임하고 있습니다.");
                    break;
                case RoomAction.Kick:
                    RunOperation(KickRoomUser(targetId, targetName), "참가자를 퇴장시키고 있습니다.");
                    break;
                case RoomAction.Close:
                    RunOperation(CloseRoom(), "방을 닫고 있습니다.");
                    break;
            }
        }
    }
}
