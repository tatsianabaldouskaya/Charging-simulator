namespace ChargingSimulator.Domain.Models;

public enum ManagedChargerState { Available, Reserved, Connecting, Ready, Recovering, RecoveryFailed, Stopped }
public enum ConnectorStatus { Available, Preparing, Charging, SuspendedEV, SuspendedEVSE, Finishing, Reserved, Unavailable, Faulted }
public enum FailureReason { None, InvalidTransition, LeaseExpired, LeaseConflict, NotReady, ProtocolError, TimedOut, Indeterminate, RecoveryFailed }
public enum ConnectorName { Left = 1, Right = 2 }

public sealed record MeterReading(DateTimeOffset Timestamp, decimal Value, string Unit = "Wh");
public sealed record Transaction(string Id, string IdTag, decimal MeterStart, DateTimeOffset StartedAt, decimal? MeterStop = null, DateTimeOffset? StoppedAt = null);
public sealed record RecoveryRecord(string Id, string Trigger, DateTimeOffset StartedAt, DateTimeOffset? CompletedAt, bool Reusable, FailureReason? FailureReason = null);

public sealed class ConnectorAggregate(ConnectorName name)
{
    public ConnectorName Name { get; } = name;
    public ConnectorStatus Status { get; private set; } = ConnectorStatus.Available;
    public Transaction? Transaction { get; private set; }
    public IReadOnlyList<MeterReading> MeterValues => _meterValues;
    public bool LifecycleIndeterminate { get; private set; }
    private readonly List<MeterReading> _meterValues = [];

    public bool TryTransition(ConnectorStatus target, out FailureReason failure)
    {
        if (!Transitions.ConnectorStateMachine.CanTransition(Status, target)) { failure = FailureReason.InvalidTransition; return false; }
        Status = target; failure = FailureReason.None; return true;
    }
    public bool Start(string transactionId, string idTag, decimal meterStart, DateTimeOffset now, out FailureReason failure)
    {
        if (Transaction is not null || Status != ConnectorStatus.Preparing) { failure = FailureReason.InvalidTransition; return false; }
        Transaction = new Transaction(transactionId, idTag, meterStart, now); Status = ConnectorStatus.Charging; failure = FailureReason.None; return true;
    }
    public bool AddMeter(decimal value, DateTimeOffset now, out FailureReason failure)
    {
        if (Transaction is null || value < Transaction.MeterStart) { failure = FailureReason.InvalidTransition; return false; }
        _meterValues.Add(new MeterReading(now, value)); failure = FailureReason.None; return true;
    }
    public bool Stop(decimal meterStop, DateTimeOffset now, out FailureReason failure)
    {
        if (Transaction is null || meterStop < Transaction.MeterStart) { failure = FailureReason.InvalidTransition; return false; }
        Transaction = Transaction with { MeterStop = meterStop, StoppedAt = now }; Status = ConnectorStatus.Finishing; failure = FailureReason.None; return true;
    }
    public void Complete() { Transaction = null; _meterValues.Clear(); LifecycleIndeterminate = false; Status = ConnectorStatus.Available; }
    public void SetTransactionId(string transactionId)
    {
        if (Transaction is not null && !string.IsNullOrWhiteSpace(transactionId))
        {
            Transaction = Transaction with { Id = transactionId };
        }
    }
    public void MarkIndeterminate()
    {
        LifecycleIndeterminate = true;
    }
}

public sealed class ChargerAggregate(string id)
{
    public string Id { get; } = id; public ManagedChargerState State { get; set; } = ManagedChargerState.Available;
    public ConnectorAggregate Left { get; } = new(ConnectorName.Left); public ConnectorAggregate Right { get; } = new(ConnectorName.Right); public RecoveryRecord? Recovery { get; private set; }
    public ConnectorAggregate Connector(ConnectorName name)
    {
        return name == ConnectorName.Left ? Left : Right;
    }

    public bool IsReusable => State == ManagedChargerState.Available && Left.Transaction is null && Right.Transaction is null && !Left.LifecycleIndeterminate && !Right.LifecycleIndeterminate;
    public void SetRecovery(RecoveryRecord recovery)
    {
        Recovery = recovery;
    }
}
