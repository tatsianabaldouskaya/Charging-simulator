using System.Collections.Concurrent;
using ChargingSimulator.Application.Manual;
using ChargingSimulator.Domain.Models;
using ChargingSimulator.Ocpp.Actions;
using ChargingSimulator.Ocpp.Protocol;
using ChargingSimulator.Ocpp.Runtime;
using ChargingSimulator.Transport;

namespace ChargingSimulator.Host.Manual;

public sealed class ManualOcppConnectionRegistry : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, Connection> _connections = new(StringComparer.Ordinal);

    public async Task ConnectAsync(string sessionId, ManualConnectionProfile profile, CancellationToken cancellationToken)
    {
        ClientWebSocketOcppTransport transport = new();
        Connection connection = new(transport);
        if (!_connections.TryAdd(sessionId, connection))
        {
            throw new InvalidOperationException("A connection already exists for this session.");
        }

        try
        {
            await transport.ConnectAsync(new Uri(profile.ChargeLabWss), profile.OcppId, cancellationToken);
            connection.StartReceivePump();
            _ = await connection.SendAsync(CoreChargingActions.BootNotification(MessageId(), profile.ChargeLabCompanyId, "ChargingSimulator"), cancellationToken);
            _ = await connection.SendAsync(CoreChargingActions.StatusNotification(MessageId(), ConnectorName.Left, ConnectorStatus.Available, DateTimeOffset.UtcNow), cancellationToken);
            _ = await connection.SendAsync(CoreChargingActions.StatusNotification(MessageId(), ConnectorName.Right, ConnectorStatus.Available, DateTimeOffset.UtcNow), cancellationToken);
        }
        catch
        {
            _ = _connections.TryRemove(sessionId, out _);
            await connection.DisposeAsync();
            throw;
        }
    }

    public async Task<OcppFrame> SendActionAsync(string sessionId, string action, ConnectorName connector, string? idTag, decimal? meterValue, string? transactionId, CancellationToken cancellationToken)
    {
        if (!_connections.TryGetValue(sessionId, out Connection? connection))
        {
            throw new InvalidOperationException("No live OCPP connection exists for this session.");
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        decimal meter = meterValue ?? 0;
        OcppCall call = action switch
        {
            "Prepare" => CoreChargingActions.StatusNotification(MessageId(), connector, ConnectorStatus.Preparing, now),
            "Authorize" => CoreChargingActions.Authorize(MessageId(), idTag ?? "manual-user"),
            "StartTransaction" => CoreChargingActions.StartTransaction(MessageId(), connector, idTag ?? "manual-user", meter, now),
            "ReportMeterValues" => CoreChargingActions.MeterValues(MessageId(), connector, meter, now),
            "SuspendEv" => CoreChargingActions.StatusNotification(MessageId(), connector, ConnectorStatus.SuspendedEV, now),
            "SuspendEvse" => CoreChargingActions.StatusNotification(MessageId(), connector, ConnectorStatus.SuspendedEVSE, now),
            "Resume" => CoreChargingActions.StatusNotification(MessageId(), connector, ConnectorStatus.Charging, now),
            "StopTransaction" => CoreChargingActions.StopTransaction(MessageId(), int.TryParse(transactionId, out int id) ? id : 0, meter, now),
            _ => throw new InvalidOperationException($"Action '{action}' is not supported.")
        };
        return await connection.SendAsync(call, cancellationToken);
    }

    public async Task DisconnectAsync(string sessionId)
    {
        if (_connections.TryRemove(sessionId, out Connection? connection))
        {
            await connection.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (KeyValuePair<string, Connection> item in _connections)
        {
            if (_connections.TryRemove(item.Key, out Connection? connection))
            {
                await connection.DisposeAsync();
            }
        }
    }

    private static string MessageId()
    {
        return Guid.NewGuid().ToString("N");
    }

    private sealed class Connection(ClientWebSocketOcppTransport transport) : IAsyncDisposable
    {
        private readonly CancellationTokenSource _stopping = new();
        private readonly OcppMessageRouter _router = new();
        private Task? _receivePump;
        public void StartReceivePump()
        {
            _receivePump = Task.Run(ReceiveAsync);
        }

        public async Task<OcppFrame> SendAsync(OcppCall call, CancellationToken cancellationToken)
        {
            OcppFrame response = await _router.SendCallAsync(call, transport.SendAsync, cancellationToken);
            return response is OcppCallError error
                ? throw new InvalidOperationException($"OCPP {error.ErrorCode}: {error.Description}")
                : response;
        }
        private async Task ReceiveAsync()
        {
            try
            {
                while (!_stopping.IsCancellationRequested)
                {
                    string? frame = await transport.ReceiveAsync(_stopping.Token);
                    if (frame is null)
                    {
                        return;
                    }

                    _router.Receive(frame);
                }
            }
            catch (OperationCanceledException) when (_stopping.IsCancellationRequested) { }
        }
        public async ValueTask DisposeAsync()
        {
            await _stopping.CancelAsync();
            if (_receivePump is not null)
            {
                await _receivePump;
            }

            _router.Dispose(); _stopping.Dispose(); await transport.DisposeAsync();
        }
    }
}
