# Session Control API Contract

Direct management controls are REST/JSON for automation, not inbound OCPP commands. The manual UI does not use these endpoints to create a ChargeLab session or invoke a ChargeLab management API. All automation session resources require `X-Session-Lease: <leaseToken>` unless noted; operator recovery uses the deployment's separate operator authorization policy. Failures are RFC 9457 ProblemDetails with stable reason codes.

| Operation | Request / outcome |
|---|---|
| `GET /v1/chargers` | Lists charger ID, availability, managed/connection/recovery state, and both connector snapshots. Does not expose another session's history or token. |
| `POST /v1/sessions` | `{ chargerId?, ownerRef, leaseDuration? }` creates an exclusive lease and begins connection/readiness. Returns `201` with `sessionId`, `leaseToken`, `chargerId`, `expiresAt`, and status; `409` when requested/no chargers are available. |
| `GET /v1/sessions/{sessionId}/readiness` | Returns pending, ready, or failed readiness evidence; supports caller cancellation/timeout. |
| `GET /v1/sessions/{sessionId}` | Returns the owner's charger, session, and connector snapshot. |
| `POST /v1/sessions/{sessionId}/renew` | Optionally renews lease within configured limit. |
| `POST /v1/sessions/{sessionId}/release` | Releases/cancels ownership and starts bounded recovery; returns an operation outcome. |
| `POST /v1/sessions/{sessionId}/connectors/{left|right}/actions` | Submits a direct simulator action; see below. |
| `GET /v1/sessions/{sessionId}/history?afterSequence=` | Returns only the relevant bounded, ordered operational history. |
| `POST /v1/chargers/{chargerId}/recover` | Operator-authorized recovery for `RecoveryFailed`; returns an operation outcome and never makes available before known-good confirmation. |
| `GET /v1/operations/{commandId}` | Returns an accepted long-running command's terminal outcome. |

Connector action body has `commandId`, `action`, and action-specific data. Supported actions are `Prepare`, `Authorize`, `StartTransaction`, `ReportMeterValues`, `SuspendEv`, `SuspendEvse`, `Resume`, and `StopTransaction`. A response is `CommandOutcome { commandId, state, protocolOutcome, ocppMessageId?, transactionId?, reason? }`, where `protocolOutcome` is `Success`, `Rejected`, `TimedOut`, `Error`, or `Indeterminate`. Invalid transitions return `422`; stale lease/state/ownership conflicts return `409`; missing/invalid lease returns `403`.

An eligible incoming `RemoteStartTransaction` is received only on the OCPP contract, correlated in session history, accepted/rejected there, and may be surfaced by session state/history. It is never simulated by this API.

## Manual UI boundary

The manual UI submits a transient connection profile with the following fields: `ChargeLabUrl`, `ChargeLabWss`, `ChargeLabApiKey`, `ChargeLabCompanyId`, `ChargerId`, and `OcppId`. `ChargerId` identifies a charger manually created in ChargeLab; `OcppId` is its charge-point identity and is used in the WebSocket address. Selecting Connect requires both IDs, validates the profile, and asks the simulator to open its own OCPP WebSocket. The UI can then request supported charger-originated OCPP flows and display the simulator's correlated results/state/history. It cannot create or alter the ChargeLab charger, call a ChargeLab management API, own the WebSocket, or read back the API key. Disconnect delegates to the simulator's bounded cleanup/recovery workflow.
