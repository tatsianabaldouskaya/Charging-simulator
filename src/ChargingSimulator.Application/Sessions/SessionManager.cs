using System.Collections.Concurrent;
using System.Security.Cryptography;
using ChargingSimulator.Application.Abstractions;
using ChargingSimulator.Application.History;
using ChargingSimulator.Domain.Models;
using ChargingSimulator.Domain.Transitions;

namespace ChargingSimulator.Application.Sessions;

public sealed record CreateSessionRequest(string OwnerRef, string? ChargerId = null, TimeSpan? LeaseDuration = null);
public sealed record SessionInfo(string SessionId, string LeaseToken, string ChargerId, DateTimeOffset ExpiresAt, string Status);
public sealed record CommandOutcome(string CommandId, string State, string ProtocolOutcome, string? TransactionId = null, string? Reason = null);
public sealed record HistoryEntry(long Sequence, DateTimeOffset Timestamp, string Kind, string Message);

public sealed class SessionManager(TimeProvider timeProvider, IIdentifierGenerator identifiers, int historyLimit = 500, TimeSpan? defaultLease = null)
{
    private readonly ConcurrentDictionary<string, ChargerAggregate> _chargers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Session> _sessions = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();
    private readonly int _historyLimit = historyLimit > 0 ? historyLimit : throw new ArgumentOutOfRangeException(nameof(historyLimit));
    private readonly TimeSpan _defaultLease = defaultLease ?? TimeSpan.FromMinutes(5);
    public void AddCharger(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("A charger ID is required.", nameof(id));
        }

        _ = _chargers.TryAdd(id, new ChargerAggregate(id));
    }
    public IReadOnlyCollection<ChargerAggregate> Chargers => [.. _chargers.Values];

    public SessionInfo Acquire(CreateSessionRequest request)
    {
        lock (_lock)
        {
            ExpireSessions();
            ChargerAggregate? charger = request.ChargerId is { Length: > 0 }
                ? _chargers.GetValueOrDefault(request.ChargerId)
                : _chargers.Values.OrderBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault(c => c.IsReusable);
            if (charger is null || !charger.IsReusable)
            {
                throw new SessionException(409, "charger-unavailable", "The requested charger is unavailable.");
            }

            DateTimeOffset now = timeProvider.GetUtcNow();
            if (!ChargerStateMachine.CanTransition(charger.State, ManagedChargerState.Reserved))
            {
                throw new SessionException(409, "charger-unavailable", "The requested charger is unavailable.");
            }

            charger.State = ManagedChargerState.Reserved;
            charger.State = ManagedChargerState.Connecting;
            charger.State = ManagedChargerState.Ready; // Local runtime is ready until a transport profile is configured.
            TimeSpan lease = request.LeaseDuration ?? _defaultLease;
            if (lease <= TimeSpan.Zero)
            {
                throw new SessionException(422, "invalid-lease-duration", "Lease duration must be positive.");
            }

            Session session = new Session(identifiers.NewId(), identifiers.NewId(), charger, request.OwnerRef, now + lease, _historyLimit);
            session.Add("session", "Acquired charger.", now); _sessions[session.Id] = session;
            return session.Info;
        }
    }
    public SessionInfo Get(string sessionId, string lease)
    {
        return Authorize(sessionId, lease).Info;
    }

    public SessionInfo Renew(string sessionId, string lease, TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            throw new SessionException(422, "invalid-lease-duration", "Lease duration must be positive.");
        }

        Session session = Authorize(sessionId, lease); session.ExpiresAt = timeProvider.GetUtcNow() + duration; session.Add("session", "Lease renewed.", timeProvider.GetUtcNow()); return session.Info;
    }
    public CommandOutcome Command(string sessionId, string lease, ConnectorName connectorName, string commandId, string action, string? idTag, decimal? meter)
    {
        Session session = Authorize(sessionId, lease); ConnectorAggregate connector = session.Charger.Connector(connectorName); DateTimeOffset now = timeProvider.GetUtcNow();
        FailureReason failure; bool accepted;
        switch (action)
        {
            case "Prepare": accepted = connector.TryTransition(ConnectorStatus.Preparing, out failure); break;
            case "Authorize": accepted = connector.Status == ConnectorStatus.Preparing; failure = accepted ? FailureReason.None : FailureReason.InvalidTransition; break;
            case "StartTransaction": accepted = connector.Start(identifiers.NewId(), idTag ?? "test", meter ?? 0, now, out failure); break;
            case "ReportMeterValues": accepted = connector.AddMeter(meter ?? 0, now, out failure); break;
            case "SuspendEv": accepted = connector.TryTransition(ConnectorStatus.SuspendedEV, out failure); break;
            case "SuspendEvse": accepted = connector.TryTransition(ConnectorStatus.SuspendedEVSE, out failure); break;
            case "Resume": accepted = connector.TryTransition(ConnectorStatus.Charging, out failure); break;
            case "StopTransaction": accepted = connector.Stop(meter ?? connector.Transaction?.MeterStart ?? 0, now, out failure); if (accepted) { connector.Complete(); } break;
            default: throw new SessionException(422, "unsupported-action", $"Action '{action}' is not supported.");
        }
        session.Add("command", $"{connectorName}:{action}:{(accepted ? "success" : failure)}", now);
        return new(commandId, connector.Status.ToString(), accepted ? "Success" : "Rejected", connector.Transaction?.Id, accepted ? null : failure.ToString());
    }
    public IReadOnlyList<HistoryEntry> History(string sessionId, string lease, long after)
    {
        return Authorize(sessionId, lease).History.After(after);
    }

    public string? ActiveTransactionId(string sessionId, string lease, ConnectorName connector)
    {
        return Authorize(sessionId, lease).Charger.Connector(connector).Transaction?.Id;
    }

    public ChargerAggregate Charger(string sessionId, string lease)
    {
        return Authorize(sessionId, lease).Charger;
    }

    public void SetTransactionId(string sessionId, string lease, ConnectorName connector, string transactionId)
    {
        Authorize(sessionId, lease).Charger.Connector(connector).SetTransactionId(transactionId);
    }

    public void Release(string sessionId, string lease) { Session session = Authorize(sessionId, lease); Recover(session, "released"); }
    public void Recover(string chargerId)
    {
        lock (_lock)
        {
            ChargerAggregate charger = _chargers.GetValueOrDefault(chargerId) ?? throw new SessionException(404, "charger-not-found", "Charger does not exist.");
            foreach (Session? session in _sessions.Values.Where(s => s.Charger == charger))
            {
                Recover(session, "operator-recovery");
            }

            if (charger.State == ManagedChargerState.RecoveryFailed) { charger.Left.Complete(); charger.Right.Complete(); charger.State = ManagedChargerState.Available; charger.SetRecovery(new RecoveryRecord(identifiers.NewId(), "operator-recovery", timeProvider.GetUtcNow(), timeProvider.GetUtcNow(), true)); }
        }
    }
    private Session Authorize(string id, string lease)
    {
        ExpireSessions();
        return !_sessions.TryGetValue(id, out Session? session) || !CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(session.Lease), System.Text.Encoding.UTF8.GetBytes(lease))
            ? throw new SessionException(403, "invalid-lease", "A valid session lease is required.")
            : session;
    }
    private void ExpireSessions()
    {
        foreach (Session? session in _sessions.Values.Where(s => s.ExpiresAt <= timeProvider.GetUtcNow()))
        {
            Recover(session, "lease-expired");
        }
    }
    private void Recover(Session session, string reason)
    {
        lock (_lock)
        {
            if (!_sessions.TryRemove(session.Id, out _))
            {
                return;
            }

            session.Charger.State = ManagedChargerState.Recovering; session.Charger.Left.Complete(); session.Charger.Right.Complete();
            bool reusable = session.Charger.Left.Transaction is null && session.Charger.Right.Transaction is null && !session.Charger.Left.LifecycleIndeterminate && !session.Charger.Right.LifecycleIndeterminate;
            session.Charger.State = reusable ? ManagedChargerState.Available : ManagedChargerState.RecoveryFailed;
            session.Charger.SetRecovery(new RecoveryRecord(identifiers.NewId(), reason, timeProvider.GetUtcNow(), timeProvider.GetUtcNow(), session.Charger.State == ManagedChargerState.Available, session.Charger.State == ManagedChargerState.Available ? null : FailureReason.RecoveryFailed));
            session.Add("recovery", reason, timeProvider.GetUtcNow());
        }
    }
    private sealed class Session(string id, string lease, ChargerAggregate charger, string owner, DateTimeOffset expiresAt, int historyLimit)
    {
        private long _sequence;
        public string Id { get; } = id; public string Lease { get; } = lease; public ChargerAggregate Charger { get; } = charger; public string Owner { get; } = owner; public DateTimeOffset ExpiresAt { get; set; } = expiresAt;
        public SessionHistory History { get; } = new(historyLimit);
        public SessionInfo Info => new(Id, Lease, Charger.Id, ExpiresAt, Charger.State.ToString());
        public void Add(string kind, string message, DateTimeOffset now)
        {
            History.Add(new(++_sequence, now, kind, message));
        }
    }
}
public sealed class SessionException(int status, string code, string detail) : Exception(detail) { public int Status { get; } = status; public string Code { get; } = code; }
