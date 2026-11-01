using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace SmartTraining
{
    // SignalR 호출의 시간 제한과 Unity 메인 스레드의 이벤트 전달을 관리합니다.
    public sealed class RoomHubConnection : IDisposable
    {
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);
        private readonly CancellationTokenSource lifetime = new();
        private readonly ConcurrentQueue<Action> callbacks = new();
        private HubConnection connection;
        private volatile bool disposed;
        private string failure;
        private int shutdownStarted;

        public string Failure => failure;

        public event Action<string, string> DisconnectUser;

        public event Action<string, string> KickUser;

        public event Action<string> RoomsChanged;

        public event Action<string> CloseRoom;

        public event Action<string> SubscriptionEnded;

        public event Action<string> LobbyChat;

        public event Action<string, string> RoomChat;

        public async Task Connect(string baseUrl, string hubPath = "/RoomHub", string accessToken = null)
        {
            ThrowIfClosed();
            if (connection != null)
            {
                throw new InvalidOperationException("이미 연결을 시작한 RoomHub입니다.");
            }

            connection = new HubConnectionBuilder().WithUrl(
                baseUrl.TrimEnd('/') + hubPath,
                options =>
                {
                    // WebSocket 연결과 토큰 헤더는 SignalR 클라이언트가 처리합니다.
                    options.Transports = HttpTransportType.WebSockets;
                    options.SkipNegotiation = true;
                    options.AccessTokenProvider = () => Task.FromResult(accessToken);
                }
            )
                .AddJsonProtocol(options => options.PayloadSerializerOptions = TrainingJson.Options)
                .Build();
            connection.HandshakeTimeout = RequestTimeout;
            connection.ServerTimeout = TimeSpan.FromSeconds(30);
            connection.KeepAliveInterval = TimeSpan.FromSeconds(10);
            RegisterEvents();
            using (var operation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
            {
                operation.CancelAfter(RequestTimeout);
                try
                {
                    await connection.StartAsync(operation.Token);
                }
                catch (OperationCanceledException) when (!lifetime.IsCancellationRequested)
                {
                    Fail("RoomHub 연결 시간이 초과되었습니다.");
                    throw new TimeoutException(Failure);
                }
                catch
                {
                    Fail("RoomHub 연결이 종료되었습니다.");
                    throw;
                }
            }
        }

        // 라이브러리의 On<T>()로 서버 이벤트 이름과 수신 형식을 등록합니다.
        private void RegisterEvents()
        {
            connection.On<string, string>("DisconnectUser", (user, token) => Enqueue(() => DisconnectUser?.Invoke(user, token)));
            connection.On<string, string>("KickUser", (user, token) => Enqueue(() => KickUser?.Invoke(user, token)));
            connection.On<string>("CloseRoom", room => Enqueue(() => CloseRoom?.Invoke(room)));
            connection.On<string>("RoomsChanged", lobby => Enqueue(() => RoomsChanged?.Invoke(lobby)));
            connection.On<string>("SubscriptionEnded", reason => Enqueue(() => SubscriptionEnded?.Invoke(reason)));
            connection.On<string>("LobbyChat", json => Enqueue(() => LobbyChat?.Invoke(json)));
            connection.On<string, string>("RoomChat", (json, room) => Enqueue(() => RoomChat?.Invoke(json, room)));
            connection.Closed += error =>
            {
                Enqueue(() => Fail("RoomHub 연결이 종료되었습니다."));
                return Task.CompletedTask;
            };
        }

        public async Task<RoomHubResult> Invoke(string target, params string[] arguments)
        {
            ThrowIfClosed();
            if (connection == null)
            {
                throw new InvalidOperationException("먼저 RoomHub에 연결해주세요.");
            }

            using (var operation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
            {
                operation.CancelAfter(RequestTimeout);
                try
                {
                    // 본문 없는 Hub 응답도 기존 호출부가 사용하는 성공 결과로 맞춥니다.
                    return await connection.InvokeCoreAsync<RoomHubResult>(target, arguments.Cast<object>().ToArray(), operation.Token) ?? new RoomHubResult
                    {
                        success = true
                    };
                }
                catch (OperationCanceledException) when (!lifetime.IsCancellationRequested)
                {
                    // 응답을 받기 전에 시간이 초과되면 연결을 닫고 서버 상태를 다시 조회합니다.
                    Fail("RoomHub 요청 시간이 초과되었습니다.");
                    throw new TimeoutException(Failure);
                }
            }
        }

        private void Enqueue(Action callback)
        {
            if (!disposed && Failure == null)
            {
                callbacks.Enqueue(callback);
            }
        }

        // Unity에서 호출하는 Tick은 화면 콜백만 실행합니다. 연결 유지와 프로토콜 처리는 하지 않습니다.
        public void Tick()
        {
            if (disposed || Failure != null)
            {
                return;
            }

            while (callbacks.TryDequeue(out var callback))
            {
                if (disposed || Failure != null)
                {
                    break;
                }

                try
                {
                    callback();
                }
                catch
                {
                    Fail("RoomHub 이벤트를 처리하지 못했습니다.");
                    return;
                }
            }
        }

        private void ThrowIfClosed()
        {
            if (disposed || Failure != null)
            {
                throw new InvalidOperationException("RoomHub 연결이 종료되었습니다.");
            }
        }

        private void Fail(string message)
        {
            if (Interlocked.CompareExchange(ref failure, message, null) != null)
            {
                return;
            }

            lifetime.Cancel();
            if (Interlocked.Exchange(ref shutdownStarted, 1) == 0)
            {
                _ = ShutdownAsync();
            }
        }

        private async Task ShutdownAsync()
        {
            try
            {
                if (connection != null)
                {
                    // 종료 시 Unity 프레임을 막지 않습니다. 이 후처리에서는 Unity API를 사용하지 않습니다.
                    await connection.DisposeAsync().ConfigureAwait(false);
                }
            }
            catch
            {
                // 종료 중인 연결은 재사용하지 않습니다. 종료 예외가 미관찰 Task로 남지 않도록 합니다.
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            Fail("RoomHub 연결이 종료되었습니다.");
            while (callbacks.TryDequeue(out _))
            {
            }
        }
    }
}
