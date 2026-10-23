using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace SmartTraining
{
    // Unity 방 서버의 관리 통신과 클라이언트의 로비 알림에 사용하는 SignalR JSON 연결입니다.
    public sealed class RoomHubConnection : IDisposable
    {
        [Serializable]
        private sealed class Message
        {
            public int type; public string invocationId, target, error;
            public string[] arguments; public RoomHubResult result;
        }
        [Serializable]
        private sealed class Invocation
        {
            public int type = 1; public string invocationId, target; public string[] arguments;
        }
        private readonly ClientWebSocket socket = new ClientWebSocket();
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly SemaphoreSlim sending = new SemaphoreSlim(1, 1);
        private readonly ConcurrentQueue<string> incoming = new ConcurrentQueue<string>();
        private readonly ConcurrentDictionary<string, TaskCompletionSource<RoomHubResult>> pending = new ConcurrentDictionary<string, TaskCompletionSource<RoomHubResult>>();
        private readonly TaskCompletionSource<bool> handshake = new TaskCompletionSource<bool>();
        private int nextId; private bool disposed; private float nextPing;
        private const int RequestTimeoutMilliseconds = 10000;
        private const double ReceiveTimeoutSeconds = 30;
        private long lastReceivedTimestamp = Stopwatch.GetTimestamp();
        private volatile string transportFailure;
        public string Failure
        {
            get; private set;
        }
        public event Action<string, string> DisconnectUser;
        public event Action<string, string> KickUser;
        public event Action<string> RoomsChanged;
        public event Action<string> CloseRoom;
        public event Action<string> SubscriptionEnded;
        public event Action<string> LobbyChat;
        // 방 그룹의 메시지 JSON과 공통 방 ID입니다.
        public event Action<string, string> RoomChat;

        // 연결 시작·핸드셰이크 전송·응답 대기를 하나의 제한 시간으로 처리합니다.
        public async Task Connect(string baseUrl, string hubPath = "/RoomHub", string accessToken = null)
        {
            if (disposed || Failure != null)
            {
                throw new InvalidOperationException("RoomHub 연결이 종료되었습니다.");
            }

            using (var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token))
            {
                operation.CancelAfter(RequestTimeoutMilliseconds);
                try
                {
                    var address = new UriBuilder(new Uri(baseUrl.TrimEnd('/') + hubPath));
                    address.Scheme = address.Scheme == "https" ? "wss" : "ws";
                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        socket.Options.SetRequestHeader("Authorization", "Bearer " + accessToken);
                    }

                    await socket.ConnectAsync(address.Uri, operation.Token);
                    _ = Receive();
                    await Send("{\"protocol\":\"json\",\"version\":1}", operation.Token);
                    await WaitForCompletion(handshake.Task, operation.Token);
                }
                catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
                {
                    Fail("RoomHub 연결 시간이 초과되었습니다.");
                    throw new TimeoutException(Failure);
                }
                catch
                {
                    Fail();
                    throw;
                }
            }
        }

        // 전송 차례 대기부터 응답까지 전체 요청에 10초를 적용합니다.
        public async Task<RoomHubResult> Invoke(string target, params string[] arguments)
        {
            if (disposed || Failure != null)
            {
                throw new InvalidOperationException("RoomHub 연결이 종료되었습니다.");
            }

            var id = (++nextId).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var completion = new TaskCompletionSource<RoomHubResult>();
            pending.TryAdd(id, completion);
            using (var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token))
            {
                operation.CancelAfter(RequestTimeoutMilliseconds);
                try
                {
                    await Send(JsonUtility.ToJson(new Invocation { invocationId = id, target = target, arguments = arguments }), operation.Token);
                    return await WaitForCompletion(completion.Task, operation.Token);
                }
                catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
                {
                    // 이미 전송된 요청은 서버에서 실행됐을 수도 있습니다. 연결을 재사용하지 않고 실제 상태를 다시 조회합니다.
                    Fail("RoomHub 요청 시간이 초과되었습니다.");
                    throw new TimeoutException(Failure);
                }
                finally
                {
                    pending.TryRemove(id, out _);
                    if (completion.Task.IsFaulted)
                    {
                        _ = completion.Task.Exception;
                    }
                }
            }
        }

        // Unity에서 지원하는 Task API로 응답 대기 취소를 연결합니다. 등록은 완료 시 해제합니다.
        private static async Task<T> WaitForCompletion<T>(Task<T> task, CancellationToken token)
        {
            var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (token.Register(() => cancelled.TrySetResult(true)))
            {
                if (await Task.WhenAny(task, cancelled.Task) != task)
                {
                    token.ThrowIfCancellationRequested();
                }

                return await task;
            }
        }

        private async Task Send(string json, CancellationToken token)
        {
            var data = Encoding.UTF8.GetBytes(json + '\u001e');
            await sending.WaitAsync(token);
            try
            {
                await socket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, token);
            }
            catch (Exception error) when (error is WebSocketException || error is InvalidOperationException)
            {
                Fail();
                throw;
            }
            finally
            {
                sending.Release();
            }
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
                    if (result.MessageType != WebSocketMessageType.Text)
                        throw new InvalidOperationException("RoomHub 연결이 종료되었습니다.");
                    int count = decoder.GetChars(buffer, 0, result.Count, chars, 0);
                    for (int i = 0; i < count; i++)
                    {
                        if (chars[i] == '\u001e')
                        {
                            incoming.Enqueue(frame.ToString());
                            frame.Clear();
                        }
                        else
                        {
                            frame.Append(chars[i]);
                            if (frame.Length > 65536)
                                throw new InvalidOperationException("RoomHub 메시지가 너무 큽니다.");
                        }
                    }
                }
            }
            catch (Exception) { if (!disposed) transportFailure = "RoomHub 연결이 종료되었습니다."; }
        }
        // Unity 메인 스레드에서 수신 메시지와 화면 콜백을 처리합니다.
        public void Tick()
        {
            if (disposed || Failure != null)
            {
                return;
            }

            while (incoming.TryDequeue(out var json))
            {
                try
                {
                    var message = JsonUtility.FromJson<Message>(json);
                    // 목록 변경이 없어도 SignalR의 연결 유지 메시지가 수신 시각을 갱신합니다.
                    lastReceivedTimestamp = Stopwatch.GetTimestamp();
                    if (!handshake.Task.IsCompleted)
                    {
                        if (!string.IsNullOrEmpty(message.error))
                            throw new InvalidOperationException("RoomHub 프로토콜을 시작하지 못했습니다.");
                        handshake.TrySetResult(true);
                        continue;
                    }
                    if (message.type == 3 && message.invocationId != null && pending.TryGetValue(message.invocationId, out var completion))
                    {
                        if (!string.IsNullOrEmpty(message.error))
                            completion.TrySetException(new InvalidOperationException("RoomHub 요청이 거부되었습니다."));
                        else
                            completion.TrySetResult(message.result ?? new RoomHubResult { success = true });
                    }
                    else if (message.type == 1 && message.target == "DisconnectUser" && message.arguments?.Length == 2)
                        DisconnectUser?.Invoke(message.arguments[0], message.arguments[1]);
                    else if (message.type == 1 && message.target == "KickUser" && message.arguments?.Length == 2)
                        KickUser?.Invoke(message.arguments[0], message.arguments[1]);
                    else if (message.type == 1 && message.target == "CloseRoom" && message.arguments?.Length == 1)
                        CloseRoom?.Invoke(message.arguments[0]);
                    else if (message.type == 1 && message.target == "RoomsChanged" && message.arguments?.Length == 1)
                        RoomsChanged?.Invoke(message.arguments[0]);
                    else if (message.type == 1 && message.target == "SubscriptionEnded" && message.arguments?.Length == 1)
                        SubscriptionEnded?.Invoke(message.arguments[0]);
                    else if (message.type == 1 && message.target == "LobbyChat" && message.arguments?.Length == 1)
                        LobbyChat?.Invoke(message.arguments[0]);
                    else if (message.type == 1 && message.target == "RoomChat" && message.arguments?.Length == 2)
                        RoomChat?.Invoke(message.arguments[0], message.arguments[1]);
                    else if (message.type == 7)
                        throw new InvalidOperationException("RoomHub 연결이 종료되었습니다.");
                }
                catch (Exception) { Fail(); return; }
            }
            if (transportFailure != null)
                Fail();
            if (handshake.Task.IsCompletedSuccessfully &&
                (Stopwatch.GetTimestamp() - lastReceivedTimestamp) / (double)Stopwatch.Frequency >= ReceiveTimeoutSeconds)
            {
                Fail("서버 응답이 없어 알림 연결을 다시 확인합니다.");
            }
            if (!disposed && Failure == null && handshake.Task.IsCompletedSuccessfully && Time.realtimeSinceStartup >= nextPing)
            {
                nextPing = Time.realtimeSinceStartup + 10;
                _ = Ping();
            }
        }
        private async Task Ping()
        {
            using (var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token))
            {
                operation.CancelAfter(RequestTimeoutMilliseconds);
                try
                {
                    await Send("{\"type\":6}", operation.Token);
                }
                catch (Exception)
                {
                    Fail();
                }
            }
        }

        private void Fail(string message = "RoomHub 연결이 종료되었습니다.")
        {
            if (Failure != null)
            {
                return;
            }

            Failure = message;
            // 보내는 중인 작업도 취소해 늦게 전송되거나 전송 차례를 계속 기다리지 않도록 합니다.
            socket.Abort();
            cancellation.Cancel();
            handshake.TrySetException(new InvalidOperationException(Failure));
            _ = handshake.Task.Exception;
            foreach (var completion in pending.Values)
            {
                completion.TrySetException(new InvalidOperationException(Failure));
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            Fail();
            socket.Dispose();
        }
    }
}
