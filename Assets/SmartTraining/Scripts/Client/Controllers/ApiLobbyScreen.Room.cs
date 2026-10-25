using System;
using System.Collections;
using UnityEngine;

namespace SmartTraining
{
    public sealed partial class ApiLobbyScreen
    {
        private IEnumerator ChangeRoomPhase(bool running)
        {
            var player = network?.LocalPlayer;
            if (player == null || !player.IsOwner || player.PhasePending)
            {
                yield break;
            }

            int reply = player.PhaseReply;
            player.RequestPhaseRpc(running);
            double until = Time.realtimeSinceStartupAsDouble + 15;
            while (network?.IsConnected == true && player != null && player.PhaseReply == reply && Time.realtimeSinceStartupAsDouble < until)
            {
                yield return null;
            }

            status = player != null && player.PhaseReply != reply ? player.PhaseMessage : "방 상태 변경 결과를 확인하지 못했습니다. 연결 상태를 확인해주세요.";
        }

        private IEnumerator UpdateRoomSettings(string name, string capacityText)
        {
            if (network?.IsConnected != true || api?.IsLoggedIn != true)
            {
                yield break;
            }

            if (!int.TryParse(capacityText, out var capacity))
            {
                Failure("최대 인원은 숫자로 입력해주세요.");
                yield break;
            }

            RoomHubResult result = null;
            yield return api.UpdateRoomSettings(networkLobbyId, networkRoomId, name.Trim(), capacity, value => result = value, Failure);
            if (result == null)
            {
                yield break;
            }

            double until = Time.realtimeSinceStartupAsDouble + 5;
            while (network?.IsConnected == true && network.LocalPlayer != null && (network.LocalPlayer.RoomName != result.roomName || network.LocalPlayer.RoomCapacity != result.maxUsers) && Time.realtimeSinceStartupAsDouble < until)
            {
                yield return null;
            }

            status = network?.IsConnected == true && network.LocalPlayer != null && network.LocalPlayer.RoomName == result.roomName && network.LocalPlayer.RoomCapacity == result.maxUsers ? "방 이름과 최대 인원을 변경했습니다." : "방 설정을 저장했습니다. 화면 동기화를 기다리고 있습니다.";
        }

        private IEnumerator SetReady(bool value)
        {
            if (network?.IsConnected != true || !network.SetReady(value))
            {
                status = "플레이어 접속을 확인한 후 준비해주세요.";
                yield break;
            }

            double until = Time.realtimeSinceStartupAsDouble + 5;
            while (network?.IsConnected == true && network.LocalPlayer != null && network.LocalPlayer.IsReady != value && Time.realtimeSinceStartupAsDouble < until)
            {
                yield return null;
            }

            status = network?.IsConnected == true && network.LocalPlayer != null && network.LocalPlayer.IsReady == value ? value ? "준비했습니다." : "준비를 취소했습니다." : "준비 상태 변경을 확인하지 못했습니다. 연결 상태를 확인해주세요.";
        }

        private IEnumerator TransferRoomMaster(string targetId, string targetName)
        {
            if (network?.IsConnected != true || api?.IsLoggedIn != true)
            {
                yield break;
            }

            bool transferred = false;
            yield return api.TransferRoomMaster(
                networkLobbyId,
                networkRoomId,
                targetId,
                () =>
            {
                transferred = true;
                status = targetName + "에게 방장을 위임했습니다. 표시를 갱신하는 중입니다.";
            },
                Failure
            );
            if (!transferred)
            {
                yield break;
            }

            double until = Time.realtimeSinceStartupAsDouble + 5;
            while (network?.IsConnected == true && network.LocalPlayer != null && network.LocalPlayer.RoomMasterUserId != targetId && Time.realtimeSinceStartupAsDouble < until)
            {
                yield return null;
            }

            status = targetName + "에게 방장을 위임했습니다.";
        }

        private IEnumerator SetRoomEntryLock(bool locked)
        {
            if (network?.IsConnected != true || api?.IsLoggedIn != true)
            {
                yield break;
            }

            bool changed = false;
            yield return api.SetRoomEntryLock(networkLobbyId, networkRoomId, locked, () => changed = true, Failure);
            if (!changed)
            {
                yield break;
            }

            double until = Time.realtimeSinceStartupAsDouble + 5;
            while (network?.IsConnected == true && network.LocalPlayer != null && network.LocalPlayer.RoomEntryLocked != locked && Time.realtimeSinceStartupAsDouble < until)
            {
                yield return null;
            }

            status = locked ? "새 참가자의 입장을 잠갔습니다. 기존 참가자의 연결은 유지됩니다." : "새 참가자의 입장을 열었습니다.";
        }

        private IEnumerator KickRoomUser(string targetId, string targetName)
        {
            if (network?.IsConnected != true || api?.IsLoggedIn != true)
            {
                yield break;
            }

            bool sent = false;
            yield return api.KickRoomUser(networkLobbyId, networkRoomId, targetId, () => sent = true, Failure);
            if (!sent)
            {
                yield break;
            }

            double until = Time.realtimeSinceStartupAsDouble + 5;
            while (network?.IsConnected == true && Array.Exists(network.Players, player => player.UserId == targetId) && Time.realtimeSinceStartupAsDouble < until)
            {
                yield return null;
            }

            status = network?.IsConnected == true && !Array.Exists(network.Players, player => player.UserId == targetId) ? targetName + "님을 강퇴했습니다." : "강퇴 요청을 전달했습니다. 접속 종료 확인을 기다리는 중입니다.";
        }

        private IEnumerator CloseRoom()
        {
            bool closed = false;
            yield return api.CloseRoom(networkLobbyId, networkRoomId, () => closed = true, Failure);
            if (!closed)
            {
                yield break;
            }

            ForgetReconnect();
            yield return LeaveRoom("방장이 방을 닫았습니다. 로비로 돌아왔습니다.");
        }

        private IEnumerator UpdateRoomPassword(string password)
        {
            string message = null;
            yield return api.UpdateRoomPassword(networkLobbyId, networkRoomId, password, value => message = value, Failure);
            if (message == null)
            {
                yield break;
            }

            editRoomPassword = "";
            yield return LoadRooms();
            status = message;
        }
    }
}
