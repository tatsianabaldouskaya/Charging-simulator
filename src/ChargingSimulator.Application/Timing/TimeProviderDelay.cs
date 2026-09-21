using ChargingSimulator.Application.Abstractions;

namespace ChargingSimulator.Application.Timing;

public sealed class TimeProviderDelay(TimeProvider timeProvider) : IDelay
{
    public Task Delay(TimeSpan delay, CancellationToken cancellationToken)
    {
        return Task.Delay(delay, timeProvider, cancellationToken);
    }
}
