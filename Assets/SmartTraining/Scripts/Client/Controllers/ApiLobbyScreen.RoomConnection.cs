using System;
using System.Collections;
using UnityEngine;

namespace SmartTraining
{
    public sealed partial class ApiLobbyScreen
    {
        private string roomJoinProgress = "";
        private bool roomJoinCanCancel, roomJoinCancelRequested;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private bool testPauseRoomJoinAfterConnection;
#endif

        private void RequestCancelRoomJoin()
        {
            if (!roomJoinCanCancel || roomJoinCancelRequested)
                return;
            roomJoinCancelRequested = true;
            roomJoinProgress = "입장 취소 중 — 접속 정보를 정리하고 있습니다…";
            RefreshUguiState();
        }

        private void DisposeRoomConnection()
        {
            network?.Dispose();
            network = null;
            networkRoomId = "";
            networkLobbyId = "";
        }

        private IEnumerator JoinRoom(string roomId, string password = null)
        {
            string lobbyId = selectedLobbyId;
            roomJoinCancelRequested = false;
            roomJoinCanCancel = true;
            roomJoinProgress = "1/4 · 접속 예약과 방 서버 준비 중…";
            try
            {
                yield return JoinRoomCore(roomId, password);
                roomJoinCanCancel = false;
                if (roomJoinCancelRequested)
                {
                    ForgetReconnect();
                    if (api?.IsLoggedIn == true)
                    {
                        // Even a response lost during cancellation may already have reserved or confirmed this room.
                        networkRoomId = roomId;
                        networkLobbyId = lobbyId;
                        yield return LeaveRoom("입장을 취소했습니다. 로비에서 다시 참가할 수 있습니다.");
                    }
                }
                else if (network?.IsConnected == true && network.LocalPlayer != null)
                {
                    roomJoinProgress = "";
                    yield return LoadState();
                    if (api?.IsLoggedIn == true)
                        status = api.User.currentRoomId == networkRoomId ? "방에 접속했습니다." : "네트워크 연결됨. 서버의 접속 확인을 기다리고 있습니다.";
                }
            }
            finally
            {
                roomJoinProgress = "";
                roomJoinCanCancel = false;
                roomJoinCancelRequested = false;
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                testPauseRoomJoinAfterConnection = false;
#endif
            }
        }

        private IEnumerator JoinRoomCore(string roomId, string password)
        {
            status = "방 서버 준비와 접속 예약을 기다리는 중…";
            JoinRoomResponse reservation = null;
            string reservationError = null;
            yield return api.JoinRoom(selectedLobbyId, roomId, value => reservation = value, message => { reservationError = message; if (!roomJoinCancelRequested) Failure(message); }, password ?? joinRoomPassword, () => roomJoinCancelRequested);
            if (roomJoinCancelRequested)
                yield break;
            if (reservation == null)
            {
                // 서버가 예약했지만 응답을 받지 못한 경우도 내 정보로 확인합니다.
                if (api.IsLoggedIn)
                    yield return api.GetMe(_ => { }, _ => { });
                if (api.IsLoggedIn && (api.User.pendingRoomId == roomId || api.User.currentRoomId == roomId))
                {
                    networkRoomId = roomId;
                    networkLobbyId = selectedLobbyId;
                    yield return LeaveRoom(reservationError);
                }
                else if (reservationError != null)
                    Failure(reservationError);
                yield break;
            }
            joinRoomPassword = "";
            networkRoomId = reservation.room.id;
            networkLobbyId = reservation.room.lobbyId;
            network = new RoomNetworkClient();
            string error = null;
            roomJoinProgress = "2/4 · 방 서버에 연결 중…";
            yield return network.Connect(api.User.id, reservation, () => { }, message => error = message, api.BaseUrl, () => roomJoinCancelRequested);
            if (roomJoinCancelRequested)
                yield break;
            if (error != null)
            {
                yield return LeaveRoom(error);
                yield break;
            }
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            while (testPauseRoomJoinAfterConnection && !roomJoinCancelRequested) yield return null;
            if (roomJoinCancelRequested) yield break;
#endif
            roomJoinProgress = "3/4 · 서버의 입장 승인 확인 중…";
            double confirmedBy = Time.realtimeSinceStartupAsDouble + 10;
            while (!roomJoinCancelRequested && api.IsLoggedIn && network.IsConnected && Time.realtimeSinceStartupAsDouble < confirmedBy)
            {
                yield return api.GetMe(_ => { }, message => error = message);
                if (error != null || api.User.currentRoomId == networkRoomId)
                    break;
                yield return new WaitForSecondsRealtime(0.2f);
            }
            if (roomJoinCancelRequested)
                yield break;
            if (error != null || !api.IsLoggedIn || !network.IsConnected || api.User.currentRoomId != networkRoomId)
            {
                yield return LeaveRoom(error ?? "서버의 접속 확인을 받지 못했습니다. 다시 참가해주세요.");
                yield break;
            }
            double playerBy = Time.realtimeSinceStartupAsDouble + 10;
            roomJoinProgress = "4/4 · 내 참가자 정보 준비 중…";
            while (!roomJoinCancelRequested && network.IsConnected && network.LocalPlayer == null && Time.realtimeSinceStartupAsDouble < playerBy)
                yield return null;
            if (roomJoinCancelRequested)
                yield break;
            if (!network.IsConnected || network.LocalPlayer == null)
            {
                yield return LeaveRoom("플레이어를 생성하지 못했습니다. 다시 참가해주세요.");
                yield break;
            }

            ForgetReconnect();
        }

        private IEnumerator LeaveRoom(string message)
        {
            if (!string.IsNullOrEmpty(roomJoinProgress))
                roomJoinProgress = roomJoinCancelRequested
                ? "입장 취소 중 — 접속 정보를 정리하고 있습니다…" : "입장 실패 — 접속 정보를 정리하는 중…";
            ClearRoomNotices();
            ClearRoomAction();
            ClearRoomPasswords();
            editRoomName = "";
            editRoomCapacity = "";
            string roomId = !string.IsNullOrEmpty(networkRoomId) ? networkRoomId :
                !string.IsNullOrEmpty(api?.User?.pendingRoomId) ? api.User.pendingRoomId : api?.User?.currentRoomId ?? "";
            string lobbyId = !string.IsNullOrEmpty(networkLobbyId) ? networkLobbyId : api?.User?.currentLobbyId ?? "";
            DisposeRoomConnection();
            string error = null;

            if (!string.IsNullOrEmpty(roomId))
                yield return api.LeaveRoom(lobbyId, roomId, () => { }, text => error = text);
            bool checkedState = false;
            if (api?.IsLoggedIn == true)
                yield return api.GetMe(_ => checkedState = true, text => error = text);
            if (checkedState && api.User.currentRoomId != roomId && api.User.pendingRoomId != roomId)
                error = null;
            if (api?.IsLoggedIn == true)
                yield return LoadState();
            if (serverConnectionState == ServerConnectionState.SessionEnded)
                yield break;
            status = error == null ? message : message + " 정리 확인: " + error;
        }

        private IEnumerator Logout()
        {
            ClearRoomPasswords();
            ClearRoomNotices();
            ClearRoomAction();
            ForgetReconnect();
            DisposeRoomConnection();
            yield return api.Logout(() => { ClearView(); status = "로그아웃했습니다."; }, Failure);
        }

        private void ForgetReconnect()
        {
            reconnectRoomId = "";
            reconnectLobbyId = "";
            reconnectPassword = "";
        }

        private IEnumerator HandleConnectionLost()
        {
            bool kicked = network?.DisconnectReason == "방장에 의해 강퇴되었습니다.";
            bool closed = network?.DisconnectReason == "방장이 방을 닫았습니다.";
            string roomId = networkRoomId, lobbyId = networkLobbyId;
            ForgetReconnect();
            yield return LeaveRoom(closed ? "방장이 방을 닫았습니다. 로비로 돌아왔습니다." : kicked ? "방장에 의해 강퇴되었습니다. 로비로 돌아왔습니다."
                : "방 서버 연결이 끊겼습니다. 로비에서 다시 접속할 수 있습니다.");
            if (!kicked && !closed && api?.IsLoggedIn == true && api.User.currentLobbyId == lobbyId && !string.IsNullOrEmpty(roomId))
            {
                reconnectRoomId = roomId;
                reconnectLobbyId = lobbyId;
            }
        }

        private IEnumerator ReconnectRoom()
        {
            string roomId = reconnectRoomId, lobbyId = reconnectLobbyId;
            string password = reconnectPassword;
            reconnectPassword = "";
            if (api?.IsLoggedIn != true || string.IsNullOrEmpty(roomId) || network?.IsConnected == true)
                yield break;
            // 이전 REST 정리가 실패했으면 새 예약 전에 다시 확인합니다.
            yield return LeaveRoom("이전 접속을 확인했습니다.");
            if (!api.IsLoggedIn)
                yield break;
            bool checkedState = false;
            yield return api.GetMe(_ => checkedState = true, Failure);
            if (!checkedState)
                yield break;
            if (api.User.currentLobbyId != lobbyId)
            {
                ForgetReconnect();
                Failure("이전 방의 로비에 있지 않습니다.");
                yield break;
            }
            if (!string.IsNullOrEmpty(api.User.currentRoomId) || !string.IsNullOrEmpty(api.User.pendingRoomId))
            {
                Failure("이전 접속 정리가 끝나지 않았습니다. 잠시 후 다시 접속해주세요.");
                yield break;
            }
            RoomSummary room = null;
            yield return api.GetRoom(lobbyId, roomId, value => room = value, Failure);
            if (room == null)
                yield break;
            if (room.isRunning)
            {
                Failure("방이 진행 중입니다. 대기 상태로 돌아오면 다시 접속해주세요.");
                yield break;
            }
            if (room.isEntryLocked)
            {
                Failure("방 입장이 잠겨 있습니다. 방장이 열면 다시 접속해주세요.");
                yield break;
            }
            if (room.availableSlots <= 0)
            {
                Failure("방에 빈 자리가 없습니다. 잠시 후 다시 접속해주세요.");
                yield break;
            }
            selectedLobbyId = lobbyId;
            yield return JoinRoom(roomId, password);
        }
    }
}

