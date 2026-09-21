namespace ChargingSimulator.Application.Abstractions;

public interface IIdentifierGenerator
{
    string NewId();
}

public interface IDelay
{
    Task Delay(TimeSpan delay, CancellationToken cancellationToken);
}
