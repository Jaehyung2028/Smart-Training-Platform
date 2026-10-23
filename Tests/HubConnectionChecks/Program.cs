using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using SmartTraining;

const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
object Get(RoomHubConnection hub, string name) => typeof(RoomHubConnection).GetField(name, flags)!.GetValue(hub)!;
void Set(RoomHubConnection hub, string name, object value) => typeof(RoomHubConnection).GetField(name, flags)!.SetValue(hub, value);
void Check(bool ok, string name)
{
    if (!ok) throw new Exception(name);
    Console.WriteLine("PASS: " + name);
}

using (var hub = new RoomHubConnection())
{
    var gate = (SemaphoreSlim)Get(hub, "sending");
    await gate.WaitAsync();
    var watch = Stopwatch.StartNew();
    var invoke = hub.Invoke("Subscribe", "audit-room");
    Check(await Task.WhenAny(invoke, Task.Delay(11500)) == invoke, "blocked send queue is covered by request timeout");
    try { await invoke; throw new Exception("Timeout expected"); }
    catch (TimeoutException) { }
    Check(watch.Elapsed.TotalSeconds >= 9 && watch.Elapsed.TotalSeconds < 11.5 && hub.Failure != null,
        "timed-out request closes connection instead of sending late");
    Check(((ConcurrentDictionary<string, TaskCompletionSource<RoomHubResult>>)Get(hub, "pending")).IsEmpty,
        "timeout removes pending invocation");
    gate.Release();
}

using (var hub = new RoomHubConnection())
{
    ((TaskCompletionSource<bool>)Get(hub, "handshake")).TrySetResult(true);
    Set(hub, "nextPing", float.MaxValue);
    Set(hub, "lastReceivedTimestamp", Stopwatch.GetTimestamp() - 31L * Stopwatch.Frequency);
    hub.Tick();
    Check(hub.Failure != null, "silent connection is rejected after receive deadline");
}

using (var hub = new RoomHubConnection())
{
    ((TaskCompletionSource<bool>)Get(hub, "handshake")).TrySetResult(true);
    Set(hub, "nextPing", float.MaxValue);
    Set(hub, "lastReceivedTimestamp", Stopwatch.GetTimestamp() - 31L * Stopwatch.Frequency);
    ((ConcurrentQueue<string>)Get(hub, "incoming")).Enqueue("{\"type\":6}");
    hub.Tick();
    Check(hub.Failure == null, "server keepalive resets receive deadline without list changes");
}

using (var hub = new RoomHubConnection())
{
    var gate = (SemaphoreSlim)Get(hub, "sending");
    await gate.WaitAsync();
    var invoke = hub.Invoke("Subscribe", "cancel-room");
    hub.Dispose();
    Check(await Task.WhenAny(invoke, Task.Delay(1000)) == invoke, "connection disposal immediately cancels blocked send");
    try { await invoke; } catch (OperationCanceledException) { }
    Check(((ConcurrentDictionary<string, TaskCompletionSource<RoomHubResult>>)Get(hub, "pending")).IsEmpty,
        "disposal releases pending request bookkeeping");
    gate.Release();
}
