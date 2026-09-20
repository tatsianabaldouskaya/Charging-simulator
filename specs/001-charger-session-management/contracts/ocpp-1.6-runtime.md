# OCPP 1.6-J Runtime Contract

Each charger opens a WebSocket to the configured central-system endpoint using its unique identity in the connection address and requires the `ocpp1.6` subprotocol. It exchanges UTF-8 JSON OCPP-J frames:

For manual use, a Connect action supplies a transient profile (`ChargeLabUrl`, `ChargeLabWss`, `ChargeLabApiKey`, `ChargeLabCompanyId`, `ChargerId`, and `OcppId`) to the simulator. `ChargerId` must already exist in ChargerLab and is carried as diagnostic correlation metadata; `OcppId` is used in the charge-point WebSocket address. The simulator validates the profile and opens this same charge-point WebSocket; the browser never connects directly or calls a ChargerLab management API. The concrete mapping of API key/company ID into the WebSocket authentication convention is configuration/transport-adapter responsibility and must be integration-tested without logging the key.

| Frame | Shape | Handling |
|---|---|---|
| CALL | `[2, uniqueId, action, payload]` | Validate envelope/action schema; route supported incoming `RemoteStartTransaction`; reply with CALLRESULT or correlated CALLERROR. Deferred commands reply `NotSupported`. |
| CALLRESULT | `[3, uniqueId, payload]` | Validate and complete the exact pending charger-originated CALL only. |
| CALLERROR | `[4, uniqueId, errorCode, description, details]` | Validate and complete the exact pending CALL as error. |

Scoped outgoing actions: `BootNotification`, `Heartbeat`, `StatusNotification`, `Authorize`, `StartTransaction`, `MeterValues`, and `StopTransaction`. Outgoing payloads are schema-validated before sending. One receive pump owns each socket; a send lock prevents frame interleaving and a one-CALL dispatcher permits no second unresolved charger-originated request. Incoming CALLs can be handled while a CALL is pending.

Malformed, duplicate, unmatched, and unsolicited frames are recorded. They never change connector/session state. Where a safely correlated incoming CALL is unsupported or invalid, send the applicable protocol error (`NotSupported`, `FormationViolation`, `PropertyConstraintViolation`, or `OccurrenceConstraintViolation`); never invent an ID for uncorrelatable malformed input.

Readiness requires successful connection, required boot/registration where applicable, initial status, and health monitoring. Registration fingerprinting prevents an unchanged reconnect from sending a second boot. Heartbeats use the accepted BootNotification interval or validated configuration. Close/error/timeout makes a pending lifecycle effect indeterminate; the runtime records it and recovery, rather than automatic replay, establishes known-good state.
