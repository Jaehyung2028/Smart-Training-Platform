# Local UDP receive-buffer fix

This embedded package is Unity Transport 2.7.2. Original package licensing and metadata are retained.

The only runtime change is in `Runtime/UDPNetworkInterface.cs`, `ReceiveJob.Execute()`:
failed Baselib receive completions release their buffer before continuing.

On Windows, abruptly killing a connected client can produce ICMP errors. The unpatched failed-completion path leaves buffers acquired, eventually exhausting the pool. The server remains bound but stops receiving, preventing new connections and eventually disconnecting survivors.

Unity confirmed this bug and the buffer-release fix on September 9, 2026:
https://discussions.unity.com/t/unity-transport-6-6-0-udp-host-stops-receiving-after-abrupt-client-disconnect-on-windows/1736023

The project regression uses three separate Unity player processes, kills the current master's process, verifies automatic transfer and admits a previously rejected client while preserving the survivor connection.

This is an embedded project dependency, not a modification to `Library/PackageCache`. It survives cache regeneration and can be checked into Git with the project. No firewall changes are required. After adopting an upstream version containing this fix, remove this embedded directory and verify the same regression before using the registry package again. Preserve the protocol version across client and dedicated-server builds.
