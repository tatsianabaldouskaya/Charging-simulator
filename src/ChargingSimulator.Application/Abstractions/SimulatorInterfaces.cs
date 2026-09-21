using ChargingSimulator.Ocpp.Protocol;
using ChargingSimulator.Ocpp.Validation;

namespace ChargingSimulator.Application.Abstractions;

public interface IOcppConnection : IAsyncDisposable
{
    Task ConnectAsync(CancellationToken cancellationToken);
    Task SendAsync(string frame, CancellationToken cancellationToken);
    Task<string?> ReceiveAsync(CancellationToken cancellationToken);
    Task CloseAsync(CancellationToken cancellationToken);
}

public interface IOcppConnectionFactory
{
    IOcppConnection Create(Uri endpoint, string ocppId);
}

public interface IOcppSchemaValidator
{
    OcppValidationResult Validate(OcppCall request);
}

public interface IBoundedHistory<T>
{
    void Add(T entry);
    IReadOnlyList<T> After(long sequence);
}
