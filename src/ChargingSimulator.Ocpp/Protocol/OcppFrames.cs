using System.Text.Json;

namespace ChargingSimulator.Ocpp.Protocol;

public static class OcppActions
{
    public const string BootNotification = nameof(BootNotification);
    public const string Heartbeat = nameof(Heartbeat);
    public const string StatusNotification = nameof(StatusNotification);
    public const string Authorize = nameof(Authorize);
    public const string StartTransaction = nameof(StartTransaction);
    public const string MeterValues = nameof(MeterValues);
    public const string StopTransaction = nameof(StopTransaction);
    public const string RemoteStartTransaction = nameof(RemoteStartTransaction);
}
public abstract record OcppFrame(string UniqueId);
public sealed record OcppCall(string Id, string Action, JsonElement Payload) : OcppFrame(Id);
public sealed record OcppCallResult(string Id, JsonElement Payload) : OcppFrame(Id);
public sealed record OcppCallError(string Id, string ErrorCode, string Description, JsonElement Details) : OcppFrame(Id);
public sealed record OcppParseResult(OcppFrame? Frame, string? Error) { public bool IsValid => Frame is not null; }
