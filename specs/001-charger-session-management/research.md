# Phase 0 Research: Charger Session Simulator

## Service boundaries

### Decision: One .NET 10 hosted ASP.NET Core service with a worker, minimal HTTP API, and thin manual dashboard

**Rationale**: A single host owns pool lifecycle while giving automation and operators the same control facade. Domain transitions, orchestration, protocol processing, and WebSocket I/O remain independently testable.

**Alternatives considered**: A console-only worker lacks the shared operator surface; separate UI and simulator services add deployment and ownership complexity.

### Decision: Manual Connect creates a simulator-owned OCPP connection from a transient UI profile

**Rationale**: The manual UI collects `ChargeLabUrl`, `ChargeLabWss`, `ChargeLabApiKey`, `ChargeLabCompanyId`, and the identifiers of a manually pre-created ChargerLab charger: `ChargerId` for operational correlation and `OcppId` for the WebSocket connection address. It validates them and asks the simulator runtime to connect. The runtime—not the browser—uses the profile for the configured WebSocket authentication convention, retains secrets only in memory for the connection lifetime, and sends all manual charging actions as native charger-originated OCPP calls. No ChargerLab management API is invoked.

**Alternatives considered**: Calling a ChargerLab API from the UI would make manual use dependent on a separate management workflow; allowing the browser to own the socket would bypass simulator state, correlation, and cleanup.

### Decision: Retain bounded, redacted operational history in memory per session

**Rationale**: This fulfils diagnostics requirements without introducing persistence outside feature scope. History records OCPP metadata/outcomes, transitions, and recovery evidence but never credentials or lease tokens.

**Alternatives considered**: Database persistence is out of scope; a global mutable history leaks session information.

## Leasing and recovery

### Decision: Use a single charger actor and exclusive opaque `SessionLease` capability

**Rationale**: One actor owns its session, two connector aggregates, transport, timers, outstanding request, and history. Its mailbox serializes charger-level mutation while different chargers run in parallel. Acquire atomically issues session ID plus secret lease token; all inspection/control/release commands compare the current token, blocking stale callers after expiry or recovery.

**Alternatives considered**: Endpoint locks do not cover background callbacks; owner-name-only authorization is stale/guessable; a global lock prevents pool parallelism.

### Decision: Use explicit bounded recovery

**Rationale**: Release, expiry, cancellation, disconnect, shutdown, and indeterminate lifecycle outcomes enter `Recovering`. The actor revokes ownership, cancels meter jobs, attempts `StopTransaction` for each active connector, closes the connection, and verifies no old owner/transaction/schedule/socket before `Available`. Deadline failure produces isolated `RecoveryFailed` until authorized recovery establishes known-good state.

**Alternatives considered**: Waiting for natural timeout violates SC-004; releasing after a socket close risks state leakage; replaying uncertain start/stop violates FR-030.

## OCPP protocol and connection behaviour

### Decision: One receive loop and typed OCPP-J envelopes per charger

**Rationale**: Decode `CALL [2,id,action,payload]`, `CALLRESULT [3,id,payload]`, and `CALLERROR [4,id,code,description,details]` in one receiver. Exact message-ID correlation completes the sole pending outbound request; a received CALL dispatches to its handler. Malformed, duplicate, unmatched, and unsolicited frames are retained without state mutation.

**Alternatives considered**: Competing readers can consume the wrong response; order/action matching is non-compliant; dropping invalid frames loses required evidence.

### Decision: Serialize sends and allow exactly one unresolved charger-originated CALL

**Rationale**: A write mutex avoids WebSocket frame interleaving; a separate one-CALL gate enforces FR-018 while still allowing an immediate response to an incoming central-system CALL. Outgoing payload/schema validation happens before ID allocation/send. Schema-invalid correlated outcomes are protocol failures, not success.

**Alternatives considered**: Multiple concurrent calls violate FR-018; automatic retry of lifecycle calls can duplicate unknown remote effects.

### Decision: Handle RemoteStartTransaction only through the incoming OCPP router

**Rationale**: Under target connector state validation, record its correlation/connector/idTag, write `Accepted` before initiating the normal matching transaction flow, and reject ineligible requests without changing state. Deferred incoming commands return `NotSupported` CALLERROR.

**Alternatives considered**: Treating it as a direct simulator command violates FR-026 and loses correlation; starting before an accepted response breaks FR-029.

### Decision: Connection supervisor uses fingerprinted boot, heartbeat, and bounded backoff

**Rationale**: Connect with the charger identity path and negotiate `ocpp1.6`. Send BootNotification only initially or after registration-data change; unchanged reconnects reinitialize status/health without another boot. Accepted boot enables heartbeat scheduling; rejected/pending/connect/health failure yields a bounded diagnosable retry/recovery path. Lifecycle calls whose final effect is uncertain are never replayed.

**Alternatives considered**: Booting every reconnect violates FR-020; socket-open readiness is too early; unbounded immediate reconnect hides failure and can overload the target.

## Deterministic state and testing

### Decision: Separate managed-session state from independent connector aggregates

**Rationale**: Charger state owns availability/lease/recovery; each immutable connector identity (left=1/right=2) owns its OCPP-visible status, transaction, meter schedule, and history. Connector mutation is validated against a documented transition table.

**Alternatives considered**: Shared transaction fields cannot represent simultaneous flows; representing lease state as OCPP status conflates concerns.

### Decision: Inject TimeProvider, scheduler/delay, ID generator, transport factory, and schema validator

**Rationale**: Tests advance lease/retry/heartbeat/meter/recovery time immediately, select IDs, script frames/disconnects, and qualify schema behavior against supplied fixtures and errata without a live ChargerLab endpoint.

**Alternatives considered**: Wall-clock APIs/real sockets create slow, flaky tests; bespoke global time/ID utilities hide dependencies.
