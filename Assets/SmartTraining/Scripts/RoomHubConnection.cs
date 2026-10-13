using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SmartTraining
{
    // Room servers only: a small JSON SignalR client for the existing RoomHub contract.
    public sealed class RoomHubConnection : IDisposable
    {
        [Serializable] private sealed class Message
        {
            public int type; public string invocationId, target, error;
            public string[] arguments; public RoomHubResult result;
        }
        [Serializable] private sealed class Invocation
        {
            public int type = 1; public string invocationId, target; public string[] arguments;
        }
        private readonly ClientWebSocket socket = new ClientWebSocket();
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly SemaphoreSlim sending = new SemaphoreSlim(1, 1);
        private readonly ConcurrentQueue<string> incoming = new ConcurrentQueue<string>();
        private readonly Dictionary<string, TaskCompletionSource<RoomHubResult>> pending = new Dictionary<string, TaskCompletionSource<RoomHubResult>>();
        private readonly TaskCompletionSource<bool> handshake = new TaskCompletionSource<bool>();
        private int nextId; private bool disposed; private float nextPing;
        private string transportFailure;
        public string Failure { get; private set; }
        public event Action<string, string> DisconnectUser;
        public event Action<string, string> KickUser;

        public async Task Connect(string baseUrl)
        {
            var address = new UriBuilder(new Uri(baseUrl.TrimEnd('/') + "/RoomHub"));
            address.Scheme = address.Scheme == "https" ? "wss" : "ws";
            await socket.ConnectAsync(address.Uri, cancellation.Token);
            _ = Receive();
            await Send("{\"protocol\":\"json\",\"version\":1}");
            await WithTimeout(handshake.Task);
        }
        public async Task<RoomHubResult> Invoke(string target, params string[] arguments)
        {
            if (disposed || Failure != null) throw new InvalidOperationException("RoomHub 연결이 종료되었습니다.");
            var id = (++nextId).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var completion = new TaskCompletionSource<RoomHubResult>();
            pending.Add(id, completion);
            try
            {
                await Send(JsonUtility.ToJson(new Invocation { invocationId = id, target = target, arguments = arguments }));
                return await WithTimeout(completion.Task);
            }
            finally { pending.Remove(id); }
        }
        private async Task<T> WithTimeout<T>(Task<T> task)
        {
            if (await Task.WhenAny(task, Task.Delay(10000, cancellation.Token)) != task)
                throw new TimeoutException("RoomHub 응답 시간이 초과되었습니다.");
            return await task;
        }
        private async Task Send(string json)
        {
            var data = Encoding.UTF8.GetBytes(json + '\u001e');
            await sending.WaitAsync(cancellation.Token);
            try { await socket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, cancellation.Token); }
            finally { sending.Release(); }
        }
        private async Task Receive()
        {
            try
            {
                var buffer = new byte[4096];
                var decoder = Encoding.UTF8.GetDecoder();
                var chars = new char[4096];
                var frame = new StringBuilder();
                while (!cancellation.IsCancellationRequested)
                {
                    var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellation.Token);
                    if (result.MessageType != WebSocketMessageType.Text) throw new InvalidOperationException("RoomHub 연결이 종료되었습니다.");
                    int count = decoder.GetChars(buffer, 0, result.Count, chars, 0);
                    for (int i = 0; i < count; i++)
                    {
                        if (chars[i] == '\u001e') { incoming.Enqueue(frame.ToString()); frame.Clear(); }
                        else { frame.Append(chars[i]); if (frame.Length > 65536) throw new InvalidOperationException("RoomHub 메시지가 너무 큽니다."); }
                    }
                }
            }
            catch (Exception) { if (!disposed) transportFailure = "RoomHub 연결이 종료되었습니다."; }
        }
        // Called on Unity's main thread, so all invocations and callbacks stay on that thread.
        public void Tick()
        {
            while (incoming.TryDequeue(out var json))
            {
                try
                {
                    var message = JsonUtility.FromJson<Message>(json);
                    if (!handshake.Task.IsCompleted)
                    {
                        if (!string.IsNullOrEmpty(message.error)) throw new InvalidOperationException("RoomHub 프로토콜을 시작하지 못했습니다.");
                        handshake.TrySetResult(true); continue;
                    }
                    if (message.type == 3 && message.invocationId != null && pending.TryGetValue(message.invocationId, out var completion))
                    {
                        if (!string.IsNullOrEmpty(message.error)) completion.TrySetException(new InvalidOperationException("RoomHub 요청이 거부되었습니다."));
                        else completion.TrySetResult(message.result ?? new RoomHubResult { success = true });
                    }
                    else if (message.type == 1 && message.target == "DisconnectUser" && message.arguments?.Length == 2)
                        DisconnectUser?.Invoke(message.arguments[0], message.arguments[1]);
                    else if (message.type == 1 && message.target == "KickUser" && message.arguments?.Length == 2)
                        KickUser?.Invoke(message.arguments[0], message.arguments[1]);
                    else if (message.type == 7) throw new InvalidOperationException("RoomHub 연결이 종료되었습니다.");
                }
                catch (Exception) { Fail(); }
            }
            if (transportFailure != null) Fail();
            if (!disposed && Failure == null && handshake.Task.IsCompletedSuccessfully && Time.realtimeSinceStartup >= nextPing)
            { nextPing = Time.realtimeSinceStartup + 10; _ = Ping(); }
        }
        private async Task Ping() { try { await Send("{\"type\":6}"); } catch (Exception) { transportFailure = "RoomHub 연결이 종료되었습니다."; } }
        private void Fail()
        {
            if (Failure != null) return;
            Failure = "RoomHub 연결이 종료되었습니다.";
            handshake.TrySetException(new InvalidOperationException(Failure));
            foreach (var completion in new List<TaskCompletionSource<RoomHubResult>>(pending.Values)) completion.TrySetException(new InvalidOperationException(Failure));
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; Fail(); cancellation.Cancel(); socket.Dispose();
        }
    }
}
