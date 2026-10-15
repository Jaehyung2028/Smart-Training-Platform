using System;
using System.Collections;
using UnityEngine;
namespace SmartTraining
{
    public sealed class LobbyDedicatedServer : MonoBehaviour
    {
        private RoomHubConnection hub;
        private bool stopping;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-lobby") >= 0)
                new GameObject("ContentLobbyServer").AddComponent<LobbyDedicatedServer>();
        }
        private IEnumerator Start()
        {
            Application.runInBackground = true; Application.targetFrameRate = 30;
            var args = Environment.GetCommandLineArgs();
            string Value(string name) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : ""; }
            string id = Value("-Id"), api = Value("-apiUrl"), secret = Environment.GetEnvironmentVariable("SMART_TRAINING_SERVER_TOKEN");
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(secret) ||
                !Uri.TryCreate(api, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
            { Stop("로비 서버 설정이 없습니다."); yield break; }
            hub = new RoomHubConnection(); var connect = hub.Connect(api, "/LobbyServerHub");
            while (!connect.IsCompleted && !stopping) yield return null;
            if (!connect.IsCompletedSuccessfully) { Stop("로비 관리 서버 연결 실패"); yield break; }
            var register = hub.Invoke("Register", id, secret); secret = null;
            while (!register.IsCompleted && !stopping) yield return null;
            if (!register.IsCompletedSuccessfully || !register.Result.success) { Stop("로비 서버 등록 실패"); yield break; }
            Debug.Log("LOBBY SERVER READY: " + id);
        }
        private void Update() { hub?.Tick(); if (!stopping && hub?.Failure != null) Stop(hub.Failure); }
        private void Stop(string reason) { if (stopping) return; stopping = true; Debug.LogError(reason); hub?.Dispose(); Application.Quit(1); }
        private void OnDestroy() { stopping = true; hub?.Dispose(); }
    }
}
