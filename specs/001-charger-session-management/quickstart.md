# Quickstart: Validate Charger Session Management

## Prerequisites

- .NET 10 SDK.
- For automation, deployment configuration supplying ChargerLab WebSocket endpoint, credentials/TLS values, at least two unique charger identities, and secrets outside source control. For manual use, create the charger first in ChargerLab, then enter a transient profile with `ChargeLabUrl`, `ChargeLabWss`, `ChargeLabApiKey`, `ChargeLabCompanyId`, `ChargerId`, and `OcppId`.
- Supplied OCPP 1.6-J schemas/errata fixtures available to the test project.

## Validate locally

1. Restore and run tests:

   ```sh
   dotnet test
   ```

   Expected: domain transition tests, OCPP frame/schema/correlation contract tests, and scripted-WebSocket integration tests pass without a live central system.

2. Start the service with development configuration that points to a scripted local central system (or configured ChargerLab):

   ```sh
   dotnet run --project src/ChargingSimulator.Host
   ```

3. Create a session through `POST /v1/sessions`, then poll its readiness endpoint with the returned lease token. Expected: ready only after required OCPP initialization; an unavailable charger returns 409 and does not change owner state. See [session-control-api.md](./contracts/session-control-api.md).

4. Submit `Prepare`, `Authorize`, `StartTransaction`, meter reports, and `StopTransaction` for left. In parallel execute a distinct valid flow for right. Expected: connector IDs 1/2, transaction IDs, meter records, and histories remain isolated as described in [data-model.md](./data-model.md).

5. With an outgoing request pending, have the scripted central system send `RemoteStartTransaction` for an available connector. Expected: the runtime records its OCPP correlation, writes the accepted response, then starts the matching connector; unsupported inbound actions receive a protocol error. See [ocpp-1.6-runtime.md](./contracts/ocpp-1.6-runtime.md).

6. Abandon a session with an active transaction or advance the fake clock past lease expiry. Expected: recovery attempts per-connector stop, cancels meter work, closes the old connection, and within the configured deadline leaves the charger `Available` or `RecoveryFailed`. Reacquisition must never reveal prior transaction/history state.

7. Force a disconnect or invalid/malformed/unmatched frame. Expected: history provides diagnosable evidence; the other connector and other chargers remain usable; no uncertain start/stop is automatically replayed.

8. Create the charger manually in ChargerLab. In the manual UI, enter valid `ChargeLabUrl`, `ChargeLabWss`, `ChargeLabApiKey`, `ChargeLabCompanyId`, its `ChargerId`, and its `OcppId`, then select Connect. Expected: the simulator validates that both IDs are present, uses `OcppId` in the OCPP WebSocket address, keeps `ChargerId` for correlation, never exposes the key, and does not create/change the ChargerLab charger or call a ChargerLab management API. Drive a left-connector OCPP flow and select Disconnect. Expected: correlated OCPP outcomes and state are visible; the simulator performs bounded cleanup before reporting reusable or recovery-failed.
