using ChargingSimulator.Application.Abstractions;

namespace ChargingSimulator.Application.Identifiers;

public sealed class GuidIdentifierGenerator : IIdentifierGenerator
{
    public string NewId()
    {
        return Guid.NewGuid().ToString("N");
    }
}
