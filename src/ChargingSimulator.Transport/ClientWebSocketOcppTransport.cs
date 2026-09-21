using System.Net.WebSockets;
using System.Text;

namespace ChargingSimulator.Transport;

public sealed class ClientWebSocketOcppTransport : IAsyncDisposable
{
    private readonly ClientWebSocket _socket = new();
    public async Task ConnectAsync(Uri endpoint, string ocppId, CancellationToken cancellationToken)
    {
        _socket.Options.AddSubProtocol("ocpp1.6");
        UriBuilder addressBuilder = new(endpoint)
        {
            Path = $"{endpoint.AbsolutePath.TrimEnd('/')}/{Uri.EscapeDataString(ocppId)}"
        };
        Uri address = addressBuilder.Uri;
        await _socket.ConnectAsync(address, cancellationToken);
        if (!string.Equals(_socket.SubProtocol, "ocpp1.6", StringComparison.OrdinalIgnoreCase))
        {
            throw new WebSocketException("Central system did not negotiate ocpp1.6.");
        }
    }
    public Task SendAsync(string frame, CancellationToken cancellationToken)
    {
        return _socket.SendAsync(Encoding.UTF8.GetBytes(frame), WebSocketMessageType.Text, true, cancellationToken);
    }

    public async Task<string?> ReceiveAsync(CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[16 * 1024]; WebSocketReceiveResult result = await _socket.ReceiveAsync(buffer, cancellationToken);
        return result.MessageType == WebSocketMessageType.Close ? null : Encoding.UTF8.GetString(buffer, 0, result.Count);
    }
    public async ValueTask DisposeAsync() { if (_socket.State == WebSocketState.Open) { await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "shutdown", CancellationToken.None); } _socket.Dispose(); }
}
