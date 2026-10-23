using System;
using System.Collections;
using UnityEngine;

namespace SmartTraining
{
    public sealed partial class ApiLobbyScreen
    {
        private enum ServerConnectionState
        {
            LoginRequired, Connecting, Connected, RetryWaiting, SessionEnded
        }
        private ServerConnectionState serverConnectionState;
        private int serverReconnectAttempts;
        private bool connectionSessionObserved;
        private TrainingApiClient connectionOwnerApi;

        private void ResetServerConnectionStatus()
        {
            serverConnectionState = ServerConnectionState.LoginRequired;
            serverReconnectAttempts = 0;
            connectionSessionObserved = false;
            connectionOwnerApi = null;
            nextLiveAttempt = 0;
        }
        private void ObserveConnectionSession()
        {
            if (connectionOwnerApi != api)
            {
                ResetServerConnectionStatus();
                connectionOwnerApi = api;
            }
            if (api?.IsLoggedIn == true)
                connectionSessionObserved = true;
        }
        private void EndServerSession()
        {
            ClearView();
            serverConnectionState = ServerConnectionState.SessionEnded;
            status = "서버 인증 세션이 종료되었습니다. 다시 로그인해주세요.";
            connectionOwnerApi = api;
        }
        private void ScheduleLiveRetry(string _)
        {
            if (api?.IsLoggedIn != true && connectionSessionObserved)
            {
                EndServerSession();
                return;
            }
            DisposeLobbyUpdates();
            nextLiveAttempt = Time.realtimeSinceStartup + 3;
            serverConnectionState = ServerConnectionState.RetryWaiting;
            serverReconnectAttempts++;
        }
        private string ServerConnectionStatusText()
        {
            switch (serverConnectionState)
            {
                case ServerConnectionState.Connecting:
                    return "서버 알림 연결: 연결 중…";
                case ServerConnectionState.Connected:
                    return "서버 알림 연결: 연결됨 · 자동 갱신 중";
                case ServerConnectionState.RetryWaiting:
                    return "서버 알림 연결: 끊김 · " + Mathf.Max(0, Mathf.CeilToInt(nextLiveAttempt - Time.realtimeSinceStartup)) + "초 후 재시도 (" + serverReconnectAttempts + "회)";
                case ServerConnectionState.SessionEnded:
                    return "서버 인증 세션 종료 — 다시 로그인해주세요.";
                default:
                    return "서버 알림 연결: 로그인 후 연결됩니다.";
            }
        }
        private IEnumerator RetryLiveConnection()
        {
            if (api?.IsLoggedIn != true)
                yield break;
            DisposeLobbyUpdates();
            nextLiveAttempt = 0;
            yield return ConnectLobbyRooms(api.User.currentLobbyId ?? "");
        }
    }
}
