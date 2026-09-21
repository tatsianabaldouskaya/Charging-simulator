using System.Collections.Concurrent;
using ChargingSimulator.Ocpp.Protocol;

namespace ChargingSimulator.Ocpp.Runtime;

public sealed class OcppMessageRouter : IDisposable
{
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly ConcurrentQueue<string> _history = new();
    private TaskCompletionSource<OcppFrame>? _pending;
    private string? _pendingId;
    public IReadOnlyCollection<string> History => [.. _history];

    public async Task<OcppFrame> SendCallAsync(OcppCall call, Func<string, CancellationToken, Task> send, CancellationToken cancellationToken)
    {
        await _sendGate.WaitAsync(cancellationToken);
        try
        {
            if (_pending is not null)
            {
                throw new InvalidOperationException("Only one outgoing OCPP CALL may be unresolved.");
            }

            _pendingId = call.Id;
            _pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
            await send(OcppFrameCodec.Serialize(call), cancellationToken);
            return await _pending.Task.WaitAsync(cancellationToken);
        }
        finally { _pending = null; _pendingId = null; _ = _sendGate.Release(); }
    }
    public void Receive(string json)
    {
        OcppParseResult parsed = OcppFrameCodec.Parse(json);
        if (!parsed.IsValid) { _history.Enqueue($"invalid:{parsed.Error}"); return; }
        if (parsed.Frame is OcppCallResult or OcppCallError)
        {
            if (_pending is null || parsed.Frame.UniqueId != _pendingId) { _history.Enqueue($"unmatched:{parsed.Frame.UniqueId}"); return; }
            _ = _pending.TrySetResult(parsed.Frame);
            return;
        }
        OcppCall call = (OcppCall)parsed.Frame!;
        _history.Enqueue($"incoming:{call.Action}:{call.UniqueId}");
    }
    public void Dispose()
    {
        _sendGate.Dispose();
    }
}
