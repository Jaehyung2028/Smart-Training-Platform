using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartTraining;

int count = 0;
void Check(bool valid, string name)
{
    if (!valid) throw new InvalidOperationException(name);
    count++;
    Console.WriteLine("PASS: " + name);
}

async Task Flush(RoomHubConnection hub, Func<bool> ready)
{
    var deadline = DateTime.UtcNow.AddSeconds(3);
    while (!ready() && DateTime.UtcNow < deadline)
    {
        hub.Tick();
        await Task.Delay(10);
    }
}

var builder = WebApplication.CreateBuilder();
builder.Logging.ClearProviders();
builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
builder.Services.AddSignalR().AddJsonProtocol(options => options.PayloadSerializerOptions = TrainingJson.Options);
await using var app = builder.Build();
app.MapHub<ProbeHub>("/probe");
await app.StartAsync();
string url = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>().Addresses.Single();
try
{
    using (var hub = new RoomHubConnection())
    {
        int delivered = 0;
        string text = null;
        string roomId = null;
        hub.LobbyChat += message => { delivered++; text = message; };
        hub.RoomChat += (message, room) => { delivered++; text = message; roomId = room; };
        await hub.Connect(url, "/probe", "probe-token");
        Check(hub.Failure == null, "Microsoft client connects without a manual handshake loop");
        Check((await hub.Invoke("Authorization")).message == "Bearer probe-token", "access token provider sends bearer header");
        var echoes = await Task.WhenAll(Enumerable.Range(0, 12).Select(i => hub.Invoke("Echo", "request-" + i)));
        Check(echoes.Select(result => result.message).SequenceEqual(Enumerable.Range(0, 12).Select(i => "request-" + i)),
            "library matches concurrent invocations to their responses");
        Check((await hub.Invoke("VoidReply")).success, "void registration response preserves common success result");
        string unicode = "한글 메시지 \"인용\" \\ 경로 😀" + new string('가', 5000);
        await hub.Invoke("SendLobby", unicode);
        Check(delivered == 0, "network callbacks wait for Unity main-thread dispatch");
        await Flush(hub, () => delivered == 1);
        Check(delivered == 1 && text == unicode, "unicode and large chat payload survive library framing");
        await hub.Invoke("SendRoom", "room-message", "room-42");
        await Flush(hub, () => delivered == 2);
        Check(delivered == 2 && text == "room-message" && roomId == "room-42", "two-argument room event dispatch preserves order");
        bool rejected = false;
        try { await hub.Invoke("Reject"); }
        catch (HubException) { rejected = true; }
        Check(rejected && hub.Failure == null, "hub rejection does not destroy an otherwise healthy connection");
        Check((await hub.Invoke("Echo", "after-rejection")).message == "after-rejection", "connection remains usable after rejected request");
        await hub.Invoke("SendLobby", "stale");
        hub.Dispose();
        hub.Tick();
        Check(delivered == 2, "queued notifications are not delivered after disposal");
    }

    using (var hub = new RoomHubConnection())
    {
        await hub.Connect(url, "/probe");
        var pending = hub.Invoke("Slow");
        await Task.Delay(100);
        hub.Dispose();
        bool cancelled = false;
        try { await pending.WaitAsync(TimeSpan.FromSeconds(2)); }
        catch (OperationCanceledException) { cancelled = true; }
        Check(cancelled && hub.Failure != null, "disposal cancels an outstanding invocation promptly");
    }

    using (var hub = new RoomHubConnection())
    {
        await hub.Connect(url, "/probe");
        var watch = Stopwatch.StartNew();
        bool timedOut = false;
        try { await hub.Invoke("Slow"); }
        catch (TimeoutException) { timedOut = true; }
        Check(timedOut && watch.Elapsed.TotalSeconds >= 9 && watch.Elapsed.TotalSeconds < 13 && hub.Failure != null,
            "request timeout closes uncertain connection instead of retrying mutations");
    }

    using (var hub = new RoomHubConnection())
    {
        await hub.Connect(url, "/probe");
        try { await hub.Invoke("AbortConnection"); } catch (Exception) { }
        var until = DateTime.UtcNow.AddSeconds(3);
        while (hub.Failure == null && DateTime.UtcNow < until)
        {
            hub.Tick();
            await Task.Delay(20);
        }
        Check(hub.Failure != null, "server disconnect reaches existing Unity reconnect flow");
    }

    Check(TrainingJson.Deserialize<RoomSummary[]>("[]").Length == 0, "JSON library reads top-level arrays directly");
    var content = new ContentSummary { id = 1, rootContent = "root", contentType = "category", children = new[] {
        new ContentSummary { id = 2, contentType = "scenario", assetId = "content/scenario/tools", children = Array.Empty<ContentSummary>() }
    }};
    var restored = TrainingJson.Deserialize<ContentSummary[]>(TrainingJson.Serialize(new[] { content }));
    Check(restored[0].children[0].contentType == "scenario" && restored[0].children[0].assetId == "content/scenario/tools",
        "public-field content tree and resource address survive JSON roundtrip");
    for (int depth = 0; depth < 16; depth++)
        content = new ContentSummary { id = depth + 3, children = new[] { content } };
    Check(TrainingJson.Deserialize<ContentSummary>(TrainingJson.Serialize(content)).children.Length == 1,
        "content trees no longer depend on Unity JsonUtility depth limit");
    bool badJson = false;
    try { TrainingJson.Deserialize<RoomSummary[]>("{bad-json}"); } catch (FormatException) { badJson = true; }
    Check(badJson, "malformed JSON retains existing API format-error contract");
    Console.WriteLine($"{count} checks passed");
}
finally
{
    await app.StopAsync();
}

public sealed class ProbeHub : Hub
{
    public RoomHubResult Echo(string value) => new() { success = true, message = value };
    public RoomHubResult Authorization() => new() { success = true, message = Context.GetHttpContext().Request.Headers.Authorization.ToString() };
    public Task VoidReply() => Task.CompletedTask;
    public Task SendLobby(string text) => Clients.Caller.SendAsync("LobbyChat", text);
    public Task SendRoom(string text, string room) => Clients.Caller.SendAsync("RoomChat", text, room);
    public Task Reject() => throw new HubException("expected rejection");
    public async Task<RoomHubResult> Slow()
    {
        await Task.Delay(TimeSpan.FromSeconds(30), Context.ConnectionAborted);
        return new RoomHubResult { success = true };
    }
    public void AbortConnection() => Context.Abort();
}
