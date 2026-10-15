#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace SmartTraining
{
    // SessionState는 에디터 메모리입니다. 도메인 재로드를 통과하며 에디터 종료 시 사라집니다.
    [InitializeOnLoad]
    public static class EditorSessionCleanup
    {
        private const string Key = "SmartTraining.PendingLogout.v1";
        [Serializable] private class Entry { public string url, token; public int attempts; public bool refreshing, refreshed; }
        [Serializable] private class QueueData { public List<Entry> entries = new List<Entry>(); }
        private static QueueData queue;
        private static UnityWebRequest request;
        private static double retryAt;
        public static bool HasPendingRequests => queue.entries.Count > 0;

        static EditorSessionCleanup()
        {
            var saved = SessionState.GetString(Key, "");
            queue = string.IsNullOrEmpty(saved) ? new QueueData() : JsonUtility.FromJson<QueueData>(saved);
            if (queue?.entries == null) queue = new QueueData();
            EditorApplication.update += Pump;
            AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
        }
        public static void Enqueue(string baseUrl, string token)
        {
            if (string.IsNullOrEmpty(token) || queue.entries.Exists(item => item.url == baseUrl && item.token == token)) return;
            queue.entries.Add(new Entry { url = baseUrl, token = token }); Save();
        }
        private static void Save()
        {
            if (queue.entries.Count == 0) SessionState.EraseString(Key);
            else SessionState.SetString(Key, JsonUtility.ToJson(queue));
        }
        private static void BeforeReload()
        {
            Save(); request?.Abort(); request?.Dispose(); request = null;
        }
        private static void Pump()
        {
            if (queue.entries.Count == 0 || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode != EditorApplication.isPlaying) return;
            var entry = queue.entries[0];
            if (request == null)
            {
                if (EditorApplication.timeSinceStartup < retryAt) return;
                request = new UnityWebRequest(entry.url + (entry.refreshing ? "/api/Auth/refresh" : "/api/Auth/logout"), "POST");
                request.downloadHandler = new DownloadHandlerBuffer(); request.timeout = 5;
                request.SetRequestHeader("Authorization", "Bearer " + entry.token);
                request.SendWebRequest(); return;
            }
            if (!request.isDone) return;
            var ok = request.result == UnityWebRequest.Result.Success;
            var status = request.responseCode;
            var payload = request.downloadHandler.text;
            request.Dispose(); request = null;
            if (ok)
            {
                try
                {
                    if (entry.refreshing)
                    {
                        var session = JsonUtility.FromJson<LoginResponse>(payload);
                        if (session == null || !session.success || string.IsNullOrEmpty(session.accessToken)) throw new FormatException();
                        entry.token = session.accessToken; entry.refreshing = false; entry.refreshed = true; entry.attempts = 0; Save();
                    }
                    else
                    {
                        if (JsonUtility.FromJson<ApiError>(payload)?.success != true) throw new FormatException();
                        queue.entries.RemoveAt(0); Save(); Debug.Log("Play 종료: 서버 사용자 정리가 완료됐습니다.");
                    }
                    return;
                }
                catch (Exception error) when (error is ArgumentException || error is FormatException) { ok = false; }
            }
            if (status == 401 && !entry.refreshing && !entry.refreshed)
            { entry.refreshing = true; Save(); return; }
            if (status == 401 || ++entry.attempts >= 3)
            {
                queue.entries.RemoveAt(0); Save();
                Debug.LogWarning("Play 종료: 서버 정리를 확인하지 못했습니다. 이미 종료되었거나 만료된 세션 또는 서버 연결을 확인해주세요. HTTP " + status);
            }
            else { retryAt = EditorApplication.timeSinceStartup + 1; Save(); }
        }
    }
}
#endif
