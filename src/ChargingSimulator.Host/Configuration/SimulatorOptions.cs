namespace ChargingSimulator.Host.Configuration;

public sealed class SimulatorOptions
{
    public const string SectionName = "Simulator";
    public int HistoryLimit { get; init; } = 500;
    public int LeaseMinutes { get; init; } = 5;
    public int ReadinessSeconds { get; init; } = 15;
    public int RecoverySeconds { get; init; } = 60;
    public int HeartbeatSeconds { get; init; } = 60;
    public string? OperatorRecoveryKey { get; init; }

    public void Validate()
    {
        if (HistoryLimit is < 1 or > 10_000)
        {
            throw new Microsoft.Extensions.Options.OptionsValidationException(nameof(SimulatorOptions), typeof(SimulatorOptions), ["HistoryLimit must be between 1 and 10000."]);
        }

        if (LeaseMinutes is < 1 or > 120)
        {
            throw new Microsoft.Extensions.Options.OptionsValidationException(nameof(SimulatorOptions), typeof(SimulatorOptions), ["LeaseMinutes must be between 1 and 120."]);
        }
    }
}
