# Feature Specification: Manage Charger Simulation Sessions

**Feature Branch**: `main`

**Created**: 2026-09-20

**Status**: Draft

**Input**: User description: "Create a charging simulator that keeps connections available for automated and manual use, independently controls the left and right ports of each charger, supports parallel chargers, and reliably cleans up incomplete sessions."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Drive an Isolated Charger Session from a Test (Priority: P1)

An automated-test author obtains a managed simulator session for a named charger, waits until it is ready, and drives either connector through the states required by the test. The connection remains available until the test explicitly releases the session or its lease ends, so the test can perform a complete charging flow without relying on timing guesses.

**Why this priority**: Reliable, deterministic automated testing is the primary purpose of the simulator.

**Independent Test**: A test can acquire a charger, wait for readiness, start a transaction on the left connector, submit meter readings, stop the transaction, inspect the resulting state and exchanged messages, and release the charger.

**Acceptance Scenarios**:

1. **Given** an available charger identity, **When** a test acquires its session and waits for readiness, **Then** it receives a usable session only after its connection, registration when required, and initial status have completed successfully.
2. **Given** a ready charger session, **When** a test starts, reports meter values for, and stops a transaction on one connector, **Then** each operation completes with an observable result and the connector returns to its available state.
3. **Given** an active managed session, **When** the test remains in progress, **Then** the charger connection stays open unless the remote system disconnects it or the test explicitly ends it.
4. **Given** a session acquisition that cannot reach readiness within its configured limit, **When** the limit expires, **Then** the caller receives a diagnosable failure and no orphaned session remains reserved.
5. **Given** a healthy ready charger, **When** it reconnects without a change to its registration details, **Then** it resumes the session without treating reconnection as a new registration event.

---

### User Story 2 - Independently Simulate Both Charger Connectors (Priority: P1)

An automated-test author can run distinct charging flows on the left and right connectors of the same simulated charger at the same time. Activity, meter values, transaction identifiers, and failures for one connector do not change the other's valid state.

**Why this priority**: The physical charger has two ports, and independent operation is essential to realistic coverage.

**Independent Test**: A test starts a transaction on the left connector while the right connector remains available, then starts and completes a different transaction on the right connector and verifies both outcomes separately.

**Acceptance Scenarios**:

1. **Given** a ready charger with both connectors available, **When** a transaction starts on the left connector, **Then** the right connector remains independently available for another transaction.
2. **Given** transactions active on both connectors, **When** meter values are submitted for one connector, **Then** the other connector's transaction and readings remain unchanged.
3. **Given** one connector has a failed or stopped transaction, **When** the other connector has a valid active transaction, **Then** the active transaction can continue and stop normally.
4. **Given** a charger with left and right connectors, **When** the central system receives their status or transaction messages, **Then** it can distinguish the left connector as identifier 1 and the right connector as identifier 2.

---

### User Story 3 - Run Parallel Test Sessions Without Cross-Talk (Priority: P1)

An automated test suite reserves several distinct chargers at once, including on parallel workers. Each session has a unique owner and identity, and its connection, commands, state, logs, and cleanup are isolated from every other session.

**Why this priority**: Parallel execution reduces feedback time and must not make results flaky or cause tests to act on each other's chargers.

**Independent Test**: Two concurrent workers acquire different chargers, execute different connector flows, and verify that each sees only its own state and protocol messages.

**Acceptance Scenarios**:

1. **Given** a pool containing at least two available chargers, **When** two test workers acquire different chargers concurrently, **Then** both receive separate ready sessions and can execute independently.
2. **Given** a charger already leased by one test, **When** another test requests that same charger, **Then** it receives a clear unavailable result and cannot alter the first test's session.
3. **Given** parallel active sessions, **When** one session disconnects or fails, **Then** the unaffected sessions continue to be controllable.

---

### User Story 4 - Recover Safely from an Incomplete Session (Priority: P1)

When a test crashes, times out, disconnects, or otherwise fails to finish a charging session, the session manager detects that condition, performs the defined recovery flow, and returns the charger to a reusable state without waiting for the charger's long natural timeout.

**Why this priority**: Stranded charger state can block a test for up to an hour and makes test suites unreliable.

**Independent Test**: A test starts a transaction, abandons its session without stopping it, triggers lease expiry or explicit recovery, and verifies that the charger can be acquired and used again within the configured recovery limit.

**Acceptance Scenarios**:

1. **Given** a leased charger with an active transaction, **When** the lease expires or the owner releases it unexpectedly, **Then** the manager attempts orderly transaction completion and connection cleanup before making the charger available again.
2. **Given** orderly recovery cannot complete, **When** the configured recovery limit expires, **Then** the manager forcefully isolates the failed session, records the reason, and makes only a known-good reset charger available for new work.
3. **Given** a recovered charger, **When** a new test acquires it, **Then** no transaction, meter value, or ownership state from the previous session is visible to the new test.
4. **Given** recovery requires a reset, **When** either connector has an active transaction, **Then** the manager first attempts the applicable transaction-stop flow and records whether that attempt completed before reset or isolation.

---

### User Story 5 - Manually Operate and Observe a Charger (Priority: P2)

An operator can select an available charger, establish or end its session, choose either connector, drive the supported charging states, submit meter values, and view current status and recent exchanged messages. Manual operation follows the same ownership and cleanup rules as automation.

**Why this priority**: It makes exploratory troubleshooting possible without creating a second, divergent simulator behaviour.

**Independent Test**: An operator uses the manual control surface to reserve a charger, complete a left-connector charging flow, observe its state, and release it for reuse.

**Acceptance Scenarios**:

1. **Given** an available charger, **When** an operator reserves it, **Then** its current connection and each connector's state are visible to the operator.
2. **Given** a manually reserved charger, **When** the operator sends a valid action to one connector, **Then** the action's success or failure and the resulting state are displayed.
3. **Given** a charger reserved by automated testing, **When** an operator attempts to control it, **Then** the operator is prevented from changing it and can see that it is unavailable.

### Edge Cases

- A connection drops during boot, an active transaction, or recovery; the affected session becomes diagnosable and follows recovery without corrupting the other connector or other chargers.
- A caller attempts an invalid state transition, such as sending meter values before a transaction starts or stopping an already stopped transaction; the action is rejected without changing state.
- The central system rejects, times out, or returns an error for a command; the caller receives the correlated outcome and the connector moves only to the documented recoverable state.
- A duplicate, malformed, or unmatched protocol message is received; it is recorded and handled without applying it twice or blocking unrelated sessions.
- A central-system request arrives while the charger is awaiting the result of its own request; the incoming request is handled and correlated without breaking the outstanding request.
- A central system rejects the charger identity, does not negotiate the required OCPP version, or leaves a half-open connection; the charger follows bounded retry and recovery behaviour without being reported ready.
- A process shutdown occurs with active sessions; new acquisitions stop, active work is cancelled, timers and connections are closed, and unfinished sessions are marked for recovery on the next start.
- Both connectors are active when the charger needs recovery; each is stopped or isolated safely, and the charger is not released until its overall state is known good.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST manage one or more independently addressable simulated chargers and expose each charger's availability, ownership, connection state, and recovery status.
- **FR-002**: The system MUST provide a session-management interface through which an automated test or manual operator can acquire, wait for, inspect, control, release, and recover a charger session.
- **FR-003**: The system MUST prevent simultaneous ownership of the same charger by different callers and report ownership conflicts without changing the charger's state.
- **FR-004**: The system MUST keep an acquired charger's connection active while its session is healthy and owned, subject to explicit release, cancellation, remote disconnect, or lease expiry.
- **FR-005**: The system MUST model two independently controllable connectors, named left and right, for every charger.
- **FR-006**: The system MUST allow valid charging flows to run independently on both connectors of a charger, including distinct authorization, transaction, and meter-value information.
- **FR-007**: The system MUST reject an invalid connector action with a clear reason and preserve the last valid connector state.
- **FR-008**: The system MUST allow callers to drive supported simulator actions and retrieve their correlated success, rejection, timeout, or error result.
- **FR-009**: The system MUST support concurrent sessions for distinct chargers without cross-talk in commands, state, messages, transaction data, logs, or cleanup.
- **FR-010**: The system MUST detect incomplete sessions caused by release, lease expiry, cancellation, disconnection, or process shutdown and initiate recovery automatically.
- **FR-011**: Before reassigning a recovered charger, the system MUST verify it has no active ownership, outstanding transaction, scheduled meter activity, or live connection from the previous session.
- **FR-012**: The system MUST place a charger in an unavailable recovery-failed state when it cannot be returned to a known-good state, rather than allowing a new caller to use uncertain state.
- **FR-013**: The system MUST provide an operator-facing control and observation experience for manual use that applies the same session ownership, state validation, and recovery rules as automated use.
- **FR-014**: The system MUST retain a per-session operational history sufficient to determine the session owner, significant state changes, protocol action outcomes, recovery attempts, and failure reason.
- **FR-015**: The system MUST allow authorized operational recovery of a charger in recovery-failed state and show whether that recovery succeeded before the charger is made available.
- **FR-016**: The system MUST establish each charger connection as an OCPP 1.6 JSON charge-point connection, using the charger's unique identity in the connection address and negotiating the `ocpp1.6` protocol version.
- **FR-017**: The system MUST exchange OCPP requests, successful results, and protocol errors using correctly correlated message identifiers, valid UTF-8 JSON, and protocol-compliant error outcomes for malformed, unsupported, or invalid requests.
- **FR-018**: The system MUST not send a new outgoing OCPP request from a charger until its preceding outgoing request has completed or timed out, while still accepting and responding to an incoming OCPP request at any time.
- **FR-019**: The system MUST maintain a live ready connection with the central system through the configured heartbeat and connection-health behaviour, and MUST use bounded backoff when reconnecting after a failed or rejected connection.
- **FR-020**: The system MUST send `BootNotification` on initial registration and when its registration details change; it MUST not use an unchanged reconnect alone as a reason to send another boot notification.
- **FR-021**: The system MUST report each connector's externally visible OCPP status using the applicable OCPP states: `Available`, `Preparing`, `Charging`, `SuspendedEV`, `SuspendedEVSE`, `Finishing`, `Reserved`, `Unavailable`, or `Faulted`.
- **FR-022**: The system MUST keep managed-session states such as ownership, lease expiry, recovery, and recovery failure distinct from externally reported OCPP charger and connector status.
- **FR-023**: The system MUST support the OCPP core charging flow of `BootNotification`, `Heartbeat`, `StatusNotification`, `Authorize`, `StartTransaction`, `MeterValues`, and `StopTransaction` with the message data and outcomes required for each flow.
- **FR-024**: The system MUST associate every transaction and meter value with its connector and active transaction whenever the OCPP action supports those fields, and MUST preserve the correct meter-start, meter-stop, timestamp, authorization, and transaction outcome for each connector.
- **FR-025**: The system MUST use the supplied current OCPP 1.6 JSON schemas for message validation, including the published errata on message identifier uniqueness, `BootNotification` interval values, optional stop reason, and accepted Celsius spellings.
- **FR-026**: The system MUST expose session-management controls separately from simulated central-system commands so a test can directly drive the simulator without falsely representing that control as an inbound OCPP request.

### Protocol, State, and Automation Requirements *(mandatory for simulator features)*

- **OCPP Actions**: The first delivery scope is charger-to-system `BootNotification`, `Heartbeat`, `StatusNotification`, `Authorize`, `StartTransaction`, `MeterValues`, and `StopTransaction`. A later delivery within this feature may add system-to-charger `RemoteStartTransaction`, `RemoteStopTransaction`, `ChangeAvailability`, `Reset`, and `TriggerMessage`; when unsupported, these MUST receive the protocol-defined unsupported outcome. Smart charging, reservations, local authorization lists, diagnostics, firmware management, and all other actions are outside this feature.
- **State Transitions**: Managed-session states are available, reserved, connecting, ready, recovering, recovery-failed, and stopped. The left and right connectors are independently identified as 1 and 2 and use the OCPP statuses in FR-021; connector 0 represents the charger as a whole where applicable. A normal transaction flow is available to preparing, authorization, start, charging, optional suspension, stop, finishing, and available. Only documented actions may advance a state; invalid transitions are rejected with no state change.
- **Configuration**: Operators MUST be able to supply the central-system endpoint, charger identities, number of available chargers, connection and recovery limits, session-lease limits, heartbeat and retry settings, meter-reporting schedule, and manual-control availability. Credentials, endpoint values, and environment-specific transport settings are externally supplied and not stored in source control.
- **Background Lifecycle**: The service MUST report readiness only after it can accept and manage sessions, complete required initial protocol activity, and begin connection-health monitoring. On cancellation or shutdown it MUST stop new acquisitions, cancel outstanding work, end scheduled meter reporting, close connections, and report completion or remaining recovery work within a bounded period.
- **Failure and Recovery**: Disconnects, timeouts, remote errors, malformed messages, duplicates, ownership loss, unrecognized charger identities, protocol-version disagreement, and half-open connections MUST be visible to the caller and recorded. Recovery MUST attempt a valid transaction stop and cleanup sequence first, then isolate the charger if that sequence fails; an isolated charger MUST not be reassigned until known-good recovery succeeds.
- **Automation Outcome**: An automated test MUST be able to reserve a specific or available charger, wait for a ready or failed outcome, command and inspect either connector deterministically, obtain exchanged-message and state history for that session, and release the charger with a completion outcome.

### Key Entities *(include if feature involves data)*

- **Charger**: A unique simulated physical charger with connection, availability, ownership, recovery, and two connector states.
- **Connector**: The left or right charging port of a charger, with independent state, current transaction, and meter-value history.
- **Managed Session**: A time-bounded exclusive reservation of one charger by an automated test or operator, including lifecycle status, owner reference, and operational history.
- **Transaction**: A charging lifecycle associated with one connector, including authorization outcome, transaction identifier, start/stop status, and meter values.
- **Recovery Record**: The evidence and outcome of cleanup after an incomplete or failed session, including why recovery began and whether the charger became reusable.
- **Protocol Exchange**: A correlated OCPP request and its result or error, including charger identity, message identifier, action, direction, and outcome.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In an environment with available chargers, 95% of session acquisitions report ready or a clear failure within 15 seconds of the request.
- **SC-002**: Two concurrent connectors on the same charger can complete distinct charging flows with no state or meter-value cross-talk in 100 consecutive automated runs.
- **SC-003**: At least 10 distinct charger sessions can be controlled concurrently, with each caller able to retrieve only its own session's state and history.
- **SC-004**: After an abandoned active session, the charger reaches either reusable or clearly recovery-failed status within 60 seconds, avoiding the one-hour natural blocking period.
- **SC-005**: In end-to-end test runs covering successful, rejected, timed-out, disconnected, and abandoned flows, 100% of session endings produce an observable completion, failure, or recovery outcome.
- **SC-006**: A trained operator can reserve a charger, complete a basic single-connector charging flow, inspect the result, and release the charger in under 5 minutes without direct protocol-message editing.
- **SC-007**: Across 100 consecutive core-flow runs, every exchanged OCPP request has exactly one correlated result or error, and no charger issues more than one unresolved outgoing request at a time.
- **SC-008**: Across 100 consecutive reconnect and recovery runs, no unchanged reconnect produces an extra registration notification, and each failed connection reaches either ready or a recorded failure within the configured acquisition limit.

## Assumptions

- A single simulator deployment manages a configurable pool of isolated charger identities; it is not split into separate automation and manual simulator applications.
- The manual operator experience is a thin control and observation layer over the same session-management rules used by automated tests; detailed visual design is deferred to planning.
- The baseline protocol scope is OCPP 1.6 JSON over WebSocket, consistent with the project constitution and existing integration target.
- The supplied OCPP 1.6-J specification, April 2025 errata sheet, and included JSON schemas are the baseline protocol reference for this feature.
- Left and right are stable human-facing names for OCPP connector identifiers 1 and 2 respectively; connector 0 represents the charge point where OCPP requires a charger-level status.
- Direct test controls are a simulator-management capability, not simulated central-system messages. Remote central-system commands are intentionally phased so they do not delay the core automated-test flow.
- A test framework obtains and releases managed sessions itself; test runners provide a stable caller/session owner reference.
- The default lease and recovery values are configurable. The 60-second recovery result target is an outcome requirement, not a fixed implementation timeout.
- Persistence beyond the operational history needed for the running process, user authentication and authorization policy, billing, and physical hardware emulation are out of scope for this feature.
