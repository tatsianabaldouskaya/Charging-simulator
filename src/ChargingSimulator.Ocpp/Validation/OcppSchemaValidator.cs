using ChargingSimulator.Ocpp.Protocol;

namespace ChargingSimulator.Ocpp.Validation;

/// <summary>Small, dependency-free structural guard for OCPP-J calls. Full JSON-schema validation can be supplied behind the same interface.</summary>
public sealed record OcppValidationResult(bool IsValid, string? ErrorCode = null, string? Detail = null)
{
    public static OcppValidationResult Valid { get; } = new(true);
}

public static class OcppSchemaValidator
{
    public static OcppValidationResult Validate(OcppCall call)
    {
        return string.IsNullOrWhiteSpace(call.Id) || string.IsNullOrWhiteSpace(call.Action)
            ? new(false, "FormationViolation", "A CALL requires a unique ID and action.")
            : call.Payload.ValueKind is System.Text.Json.JsonValueKind.Object
            ? OcppValidationResult.Valid
            : new(false, "FormationViolation", "A CALL payload must be an object.");
    }
}
