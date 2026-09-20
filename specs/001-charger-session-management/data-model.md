# Data Model: Charger Session Simulator

## Charger

| Field | Rules |
|---|---|
| `chargerId` | Unique configured identity; used in the OCPP connection path. |
| `managedState` | `Available`, `Reserved`, `Connecting`, `Ready`, `Recovering`, `RecoveryFailed`, or `Stopped`; distinct from OCPP status. |
| `connectionState` | Disconnected, connecting, registering, ready, reconnecting, closing, or failed; never declares ready before required initialization. |
| `registrationFingerprint` | In-process fingerprint of registration data; determines whether boot is required. |
| `session` | Zero or one current managed session. |
| `connectors` | Exactly two: left (OCPP connector 1) and right (2). |
| `recovery` | Zero or one current/latest recovery record. |

Charger-level transitions: `Available → Reserved → Connecting → Ready`; `Ready|Connecting|Reserved → Recovering → Available|RecoveryFailed`; shutdown ends at `Stopped`. `RecoveryFailed` is only exited by successful authorized recovery; `Stopped` admits no acquisition.

## Manual Connection Profile

The UI submits a transient `ManualConnectionProfile` containing `ChargeLabUrl`, `ChargeLabWss`, `ChargeLabApiKey`, `ChargeLabCompanyId`, `ChargerId`, `OcppId`, and validated transport/runtime values. `ChargerId` identifies the charger manually created in ChargerLab and is retained only as operational correlation metadata; `OcppId` is the unique charge-point identity used in the OCPP WebSocket address. Both are required and non-empty. `ChargeLabApiKey` is a secret: it is redacted from logs/history, never returned by read endpoints, never persisted, and cleared when Connect fails or Disconnect/recovery completes. The profile is used only by the simulator-owned OCPP transport; it is not an instruction to call a ChargerLab management API or create a charger.

## Managed Session / SessionLease

| Field | Rules |
|---|---|
| `sessionId` | Server-generated immutable identifier. |
| `leaseToken` | Opaque secret capability, never logged or returned in history. Required with session commands. |
| `ownerRef` | Caller-supplied stable, non-secret test/operator reference. |
| `chargerId` | The exclusively reserved charger. |
| `acquiredAt`, `expiresAt` | Clock-derived UTC instants; expiry revokes ownership before recovery. |
| `status` | Acquiring, ready, failed, releasing, recovering, released, or expired. |
| `historySequence` | Monotonically increasing sequence for filtered history reads. |

Only the current matching token may command, inspect private state, renew, or release. A conflict returns unavailable/ownership conflict and causes no charger mutation.

## Connector

| Field | Rules |
|---|---|
| `connectorId` | 1 left or 2 right; immutable. |
| `ocppStatus` | `Available`, `Preparing`, `Charging`, `SuspendedEV`, `SuspendedEVSE`, `Finishing`, `Reserved`, `Unavailable`, or `Faulted`. |
| `transaction` | Zero or one active/indeterminate transaction, always connector-scoped. |
| `meterSchedule` | Cancellable scheduled work; must be absent before charger becomes available. |
| `lastTransition` | Timestamp, command/protocol correlation, previous/new state, and outcome. |

Normal flow: `Available → Preparing → Charging ↔ SuspendedEV|SuspendedEVSE → Finishing → Available`. Authorization and start are valid only after preparation; meter values require active charging/suspension with an active transaction; stop requires an active transaction. Invalid actions retain the prior valid state and return a stable reason. One connector's transition never mutates the other.

## Transaction and MeterValue

| Entity | Required fields and rules |
|---|---|
| `Transaction` | connector ID, authorization/idTag result, local start correlation, remote transaction ID when assigned, meter start, started/stopped instants, stop reason, meter stop, status (active/stopped/indeterminate/failed). Never automatically resend an indeterminate lifecycle request. |
| `MeterValue` | connector ID, active transaction ID when supported, timestamp, sampled values, source command/schedule correlation. Reject if no active transaction or invalid payload. |

## Protocol Exchange

Each received or sent frame records charger/session scope, direction, OCPP message ID, frame type, action, timestamp, redacted payload metadata, correlated exchange ID, and outcome (`Success`, `Rejected`, `TimedOut`, `Error`, `Indeterminate`, `Malformed`, `Unmatched`). A charger has at most one unresolved outgoing CALL; incoming CALLs are independently retained/routed.

## Recovery Record

`recoveryId`, trigger (release, expiry, cancellation, disconnect, shutdown, command uncertainty, operator), started/deadline/completed instants, per-connector stop attempts/outcomes, connection cleanup outcome, reset/isolation evidence, final outcome (`Reusable` or `RecoveryFailed`), and failure reason. A charger is reusable only after no owner, active transaction, scheduled meter job, outstanding protocol work, or old live connection remains.
