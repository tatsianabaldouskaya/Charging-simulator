using ChargingSimulator.Application.Manual;
using ChargingSimulator.Application.Sessions;
using ChargingSimulator.Domain.Models;
using ChargingSimulator.Host.Manual;

namespace ChargingSimulator.Host.Endpoints;

public static class ManualControlEndpoints
{
    public static void MapManualControl(this WebApplication app)
    {
        _ = app.MapPost("/v1/manual/profile/validate", (ManualConnectionProfile profile) =>
        {
            profile.Validate();
            return Results.Ok(profile.SafeProjection());
        });

        _ = app.MapPost("/v1/manual/connect", async (ManualConnectionProfile profile, SessionManager sessions, ManualOcppConnectionRegistry connections, CancellationToken cancellationToken) =>
        {
            profile.Validate();
            string chargerId = string.IsNullOrWhiteSpace(profile.ChargerId) ? profile.OcppId : profile.ChargerId;
            sessions.AddCharger(chargerId);
            SessionInfo session = sessions.Acquire(new CreateSessionRequest("manual-ui", chargerId));
            try { await connections.ConnectAsync(session.SessionId, profile, cancellationToken); }
            catch { sessions.Release(session.SessionId, session.LeaseToken); throw; }
            return Results.Created($"/v1/sessions/{session.SessionId}", new { session, profile = profile.SafeProjection() });
        });

        _ = app.MapGet("/v1/manual/sessions/{sessionId}", (string sessionId, HttpRequest request, SessionManager sessions) =>
        {
            string leaseToken = request.Headers["X-Session-Lease"].FirstOrDefault() ?? throw new SessionException(403, "missing-lease", "A valid session lease is required.");
            ChargerAggregate charger = sessions.Charger(sessionId, leaseToken);
            return Results.Ok(new
            {
                charger.Id,
                state = charger.State.ToString(),
                left = new { connectorId = 1, status = charger.Left.Status.ToString(), charger.Left.Transaction, charger.Left.MeterValues },
                right = new { connectorId = 2, status = charger.Right.Status.ToString(), charger.Right.Transaction, charger.Right.MeterValues }
            });
        });

        _ = app.MapPost("/v1/manual/sessions/{sessionId}/connectors/{connector}/actions", async (string sessionId, string connector, ManualConnectorActionRequest request, ManualOcppConnectionRegistry connections, SessionManager sessions, CancellationToken cancellationToken) =>
        {
            ConnectorName connectorName = connector.Equals("left", StringComparison.OrdinalIgnoreCase) ? ConnectorName.Left : connector.Equals("right", StringComparison.OrdinalIgnoreCase) ? ConnectorName.Right : throw new SessionException(422, "invalid-connector", "Connector must be left or right.");
            string leaseToken = request.LeaseToken ?? throw new SessionException(403, "missing-lease", "A valid session lease is required.");
            string transactionId = sessions.ActiveTransactionId(sessionId, leaseToken, connectorName) ?? string.Empty;
            Ocpp.Protocol.OcppFrame response = await connections.SendActionAsync(sessionId, request.Action, connectorName, request.IdTag, request.MeterValue, transactionId, cancellationToken);
            if (request.Action is "Authorize" or "StartTransaction")
            {
                EnsureAuthorizationAccepted(response);
            }

            CommandOutcome outcome = sessions.Command(sessionId, leaseToken, connectorName, request.CommandId ?? Guid.NewGuid().ToString("N"), request.Action, request.IdTag, request.MeterValue);
            if (request.Action == "StartTransaction" && response is Ocpp.Protocol.OcppCallResult result && result.Payload.TryGetProperty("transactionId", out System.Text.Json.JsonElement transactionIdElement))
            {
                string remoteTransactionId = transactionIdElement.GetRawText().Trim('"');
                sessions.SetTransactionId(sessionId, leaseToken, connectorName, remoteTransactionId);
                outcome = outcome with { TransactionId = remoteTransactionId };
            }

            return Results.Ok(outcome);
        });

        _ = app.MapPost("/v1/manual/disconnect", async (ManualDisconnectRequest request, SessionManager sessions, ManualOcppConnectionRegistry connections) =>
        {
            await connections.DisconnectAsync(request.SessionId);
            sessions.Release(request.SessionId, request.LeaseToken);
            return Results.Accepted();
        });
    }

    private static void EnsureAuthorizationAccepted(Ocpp.Protocol.OcppFrame response)
    {
        if (response is not Ocpp.Protocol.OcppCallResult result || !result.Payload.TryGetProperty("idTagInfo", out System.Text.Json.JsonElement idTagInfo) || !idTagInfo.TryGetProperty("status", out System.Text.Json.JsonElement statusElement))
        {
            return;
        }

        string? status = statusElement.GetString();
        if (!string.Equals(status, "Accepted", StringComparison.OrdinalIgnoreCase))
        {
            throw new SessionException(422, "authorization-rejected", $"The central system rejected the ID tag with status '{status ?? "Unknown"}'. Use an ID tag that is accepted by the central system.");
        }
    }
}

public sealed record ManualDisconnectRequest(string SessionId, string LeaseToken);
public sealed record ManualConnectorActionRequest(string Action, string? CommandId = null, string? IdTag = null, decimal? MeterValue = null, string? LeaseToken = null);
