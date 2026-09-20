---
description: "Implementation tasks for charger session management"
---

# Tasks: Manage Charger Simulation Sessions

**Input**: Design documents from `/specs/001-charger-session-management/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/`, and `quickstart.md`

**Tests**: Required by the project constitution and task template. Write each listed test before its corresponding production implementation.

**Organization**: Tasks are grouped by user story so every increment has an independently executable verification path.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the .NET 10 solution and the project boundaries in the implementation plan.

- [ ] T001 Create `ChargingSimulator.sln` and add the Host, Application, Domain, Ocpp, Transport, and test projects at the repository root in `ChargingSimulator.sln`
- [ ] T002 Create .NET 10 nullable-enabled class-library and test project files in `src/ChargingSimulator.Domain/ChargingSimulator.Domain.csproj`, `src/ChargingSimulator.Application/ChargingSimulator.Application.csproj`, `src/ChargingSimulator.Ocpp/ChargingSimulator.Ocpp.csproj`, `src/ChargingSimulator.Transport/ChargingSimulator.Transport.csproj`, `src/ChargingSimulator.Host/ChargingSimulator.Host.csproj`, `tests/ChargingSimulator.Domain.Tests/ChargingSimulator.Domain.Tests.csproj`, `tests/ChargingSimulator.Ocpp.Tests/ChargingSimulator.Ocpp.Tests.csproj`, and `tests/ChargingSimulator.IntegrationTests/ChargingSimulator.IntegrationTests.csproj`
- [ ] T003 [P] Configure package versions, project references, xUnit test SDK dependencies, analyzers, warnings-as-errors, and `net10.0` properties in `Directory.Build.props` and `Directory.Packages.props`
- [ ] T004 [P] Add development-safe configuration defaults and documented environment-variable names in `src/ChargingSimulator.Host/appsettings.json` and `src/ChargingSimulator.Host/appsettings.Development.json`
- [ ] T005 [P] Add supplied OCPP schema and errata fixtures as versioned test content under `tests/ChargingSimulator.Ocpp.Tests/Fixtures/Ocpp16/`
- [ ] T006 [P] Add a solution-level test command and format verification configuration in `.editorconfig` and `README.md`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Establish deterministic state, protocol, transport, configuration, and hosted-runtime boundaries required by every story.

**⚠️ CRITICAL**: Complete this phase before implementing user stories.

- [ ] T007 [P] Define managed charger/session states, connector identities/statuses, transaction/meter/recovery/history records, and stable failure reasons in `src/ChargingSimulator.Domain/Models/ChargerModels.cs`
- [ ] T008 [P] Define pure connector transition validation and resulting transition evidence in `src/ChargingSimulator.Domain/Transitions/ConnectorStateMachine.cs`
- [ ] T009 [P] Define charger lifecycle transition validation, reusable-state invariant checks, and recovery admission rules in `src/ChargingSimulator.Domain/Transitions/ChargerStateMachine.cs`
- [ ] T010 [P] Define injectable clock, delay/scheduler, identifier generator, and bounded-history abstractions in `src/ChargingSimulator.Application/Abstractions/RuntimeAbstractions.cs`
- [ ] T011 [P] Define typed OCPP-J CALL/CALLRESULT/CALLERROR envelopes, parse results, protocol errors, and action constants in `src/ChargingSimulator.Ocpp/Protocol/OcppFrames.cs`
- [ ] T012 [P] Define OCPP schema-validator, per-charger runtime, transport connection/factory, and session-store interfaces in `src/ChargingSimulator.Application/Abstractions/SimulatorInterfaces.cs`
- [ ] T013 [P] Define validated pool, connection, retry, heartbeat, lease, recovery, meter, and manual-profile options (including secret redaction) in `src/ChargingSimulator.Host/Configuration/SimulatorOptions.cs`
- [ ] T014 [P] Implement JSON OCPP-J envelope parsing/serialization and invalid-frame classification in `src/ChargingSimulator.Ocpp/Protocol/OcppFrameCodec.cs`
- [ ] T015 [P] Implement supplied-schema fixture loading and an isolated schema-validation adapter with errata handling in `src/ChargingSimulator.Ocpp/Validation/OcppSchemaValidator.cs`
- [ ] T016 [P] Implement a `ClientWebSocket` OCPP 1.6 transport that negotiates `ocpp1.6`, builds the identity address, and supports cancellable send/receive/close in `src/ChargingSimulator.Transport/ClientWebSocketOcppTransport.cs`
- [ ] T017 [P] Implement structured logging scopes and redaction for charger, session, OCPP message/action, transaction, transition, and failure fields in `src/ChargingSimulator.Host/Observability/SimulatorLogging.cs`
- [ ] T018 Implement the single-receive-pump, send mutex, one-unresolved-outgoing-CALL gate, exact-ID dispatcher, and unmatched-frame history recording in `src/ChargingSimulator.Ocpp/Runtime/OcppMessageRouter.cs`
- [ ] T019 Implement the per-charger actor mailbox, cancellation ownership, and safe shutdown boundary in `src/ChargingSimulator.Application/Runtime/ChargerActor.cs`
- [ ] T020 Implement host dependency injection, options startup validation, ProblemDetails middleware, health endpoint, and graceful hosted-service shutdown in `src/ChargingSimulator.Host/Program.cs`
- [ ] T021 [P] Add domain transition and reusable-state invariant tests in `tests/ChargingSimulator.Domain.Tests/Transitions/ChargerAndConnectorStateMachineTests.cs`
- [ ] T022 [P] Add frame codec, errata/schema fixture, malformed/duplicate/unmatched, exact-correlation, and one-outstanding-CALL contract tests in `tests/ChargingSimulator.Ocpp.Tests/Protocol/OcppProtocolContractTests.cs`
- [ ] T023 [P] Create controllable time/ID/scheduler and scripted central-system WebSocket test doubles in `tests/ChargingSimulator.IntegrationTests/Support/DeterministicRuntime.cs` and `tests/ChargingSimulator.IntegrationTests/Support/ScriptedOcppCentralSystem.cs`

**Checkpoint**: A configured host starts safely; protocol/state boundaries and deterministic test fixtures are available for all stories.

---

## Phase 3: User Story 1 - Drive an Isolated Charger Session from a Test (Priority: P1) 🎯 MVP

**Goal**: Acquire a named or available charger under an exclusive lease, wait for OCPP readiness, execute the core single-connector flow, inspect results/history, and release it.

**Independent Test**: Acquire a charger through the HTTP API, observe readiness only after scripted boot/status succeeds, execute `Prepare` through `StopTransaction` on left, inspect correlated history, and release the lease.

### Tests for User Story 1

- [ ] T024 [P] [US1] Add REST contract tests for charger listing, session create/readiness/snapshot/renew/release, lease header validation, and RFC 9457 responses in `tests/ChargingSimulator.IntegrationTests/SessionApi/SessionControlContractTests.cs`
- [ ] T025 [P] [US1] Add scripted-WebSocket integration tests for boot, initial status, authorization, start, meter values, stop, unchanged reconnect, heartbeat, and readiness timeout in `tests/ChargingSimulator.IntegrationTests/Sessions/ManagedSessionCoreFlowTests.cs`
- [ ] T026 [P] [US1] Add OCPP payload/action contract tests for `BootNotification`, `Heartbeat`, `StatusNotification`, `Authorize`, `StartTransaction`, `MeterValues`, and `StopTransaction` in `tests/ChargingSimulator.Ocpp.Tests/Protocol/CoreChargingActionContractTests.cs`

### Implementation for User Story 1

- [ ] T027 [P] [US1] Implement bounded per-session operational history with sequence filtering and token/secret redaction in `src/ChargingSimulator.Application/History/SessionHistory.cs`
- [ ] T028 [P] [US1] Implement core outgoing OCPP payload factories and response mappers in `src/ChargingSimulator.Ocpp/Actions/CoreChargingActions.cs`
- [ ] T029 [US1] Implement boot fingerprinting, initial status reporting, accepted heartbeat scheduling, bounded readiness, and no-automatic-replay handling in `src/ChargingSimulator.Application/Runtime/ChargerConnectionSupervisor.cs`
- [ ] T030 [US1] Implement exclusive acquire, opaque lease validation/renewal, readiness tracking, private snapshot/history access, and release initiation in `src/ChargingSimulator.Application/Sessions/SessionManager.cs`
- [ ] T031 [US1] Implement direct left/right command orchestration for Prepare, Authorize, StartTransaction, ReportMeterValues, SuspendEv, SuspendEvse, Resume, and StopTransaction in `src/ChargingSimulator.Application/Sessions/ConnectorCommandService.cs`
- [ ] T032 [US1] Implement session API request/response models and all `/v1/chargers`, `/v1/sessions`, session readiness, snapshot, renew, release, connector-action, history, and operation routes in `src/ChargingSimulator.Host/Endpoints/SessionControlEndpoints.cs`
- [ ] T033 [US1] Add session/command/recovery structured logging and redacted history projection to `src/ChargingSimulator.Application/Sessions/SessionManager.cs`

**Checkpoint**: One test can fully control and observe a single connector in a ready, leased OCPP session, then release it.

---

## Phase 4: User Story 2 - Independently Simulate Both Charger Connectors (Priority: P1)

**Goal**: Drive left and right connector flows concurrently without shared transaction, meter, state, or failure data.

**Independent Test**: Start a left transaction, run a distinct right transaction concurrently, submit meter values to one, then complete both and verify connector IDs 1/2 and histories remain independent.

### Tests for User Story 2

- [ ] T034 [P] [US2] Add concurrent two-connector transition, transaction, meter-value, and invalid-transition unit tests in `tests/ChargingSimulator.Domain.Tests/Transitions/IndependentConnectorFlowTests.cs`
- [ ] T035 [P] [US2] Add API and scripted-WebSocket integration tests for simultaneous left/right transactions and no cross-talk across 100 deterministic runs in `tests/ChargingSimulator.IntegrationTests/Sessions/DualConnectorIsolationTests.cs`

### Implementation for User Story 2

- [ ] T036 [US2] Extend connector aggregates to hold connector-scoped transaction, meter schedule, transition correlation, and indeterminate lifecycle state in `src/ChargingSimulator.Domain/Models/ConnectorAggregate.cs`
- [ ] T037 [US2] Implement connector-scoped meter validation, controlled scheduled reporting, cancellation, and meter-start/stop preservation in `src/ChargingSimulator.Application/Sessions/MeterValueService.cs`
- [ ] T038 [US2] Update command orchestration to isolate left/right state changes, protocol payload connector IDs, and outcomes in `src/ChargingSimulator.Application/Sessions/ConnectorCommandService.cs`
- [ ] T039 [US2] Extend API snapshots and session history projections with independent connector transaction/meter data in `src/ChargingSimulator.Host/Endpoints/SessionControlEndpoints.cs`

**Checkpoint**: Both connectors can complete concurrent distinct flows while retaining fully separate state and history.

---

## Phase 5: User Story 3 - Run Parallel Test Sessions Without Cross-Talk (Priority: P1)

**Goal**: Allow separate callers to lease and operate different pool chargers concurrently while preventing duplicate ownership.

**Independent Test**: Two parallel API clients lease two configured chargers, execute different flows, verify private history isolation, then prove a competing lease request for an already-owned charger receives `409` without mutation.

### Tests for User Story 3

- [ ] T040 [P] [US3] Add concurrent pool acquire, named-charger conflict, lease-token isolation, and ten-charger parallelism tests in `tests/ChargingSimulator.IntegrationTests/Sessions/ParallelSessionIsolationTests.cs`
- [ ] T041 [P] [US3] Add per-charger actor mailbox and cross-charger non-blocking unit tests in `tests/ChargingSimulator.Domain.Tests/Runtime/ChargerActorIsolationTests.cs`

### Implementation for User Story 3

- [ ] T042 [US3] Implement configured charger-pool creation, atomic named/available reservation selection, and per-charger actor registry in `src/ChargingSimulator.Application/Sessions/ChargerPool.cs`
- [ ] T043 [US3] Update session management to enforce ownership conflict invariants and keep state, commands, logs, histories, and cleanup scoped to one charger/session in `src/ChargingSimulator.Application/Sessions/SessionManager.cs`
- [ ] T044 [US3] Add pool availability and ownership-conflict endpoint behavior without exposing other sessions' private histories in `src/ChargingSimulator.Host/Endpoints/SessionControlEndpoints.cs`

**Checkpoint**: Parallel workers operate distinct chargers independently, and duplicate ownership is rejected deterministically.

---

## Phase 6: User Story 4 - Recover Safely from an Incomplete Session (Priority: P1)

**Goal**: Detect incomplete work and return each charger to verified reusable state or explicit recovery failure within bounded recovery.

**Independent Test**: Abandon a leased active transaction, advance the fake clock beyond lease expiry, confirm per-connector stop/cleanup attempts, and verify the charger is either safely reacquirable with clean state or `RecoveryFailed`.

### Tests for User Story 4

- [ ] T045 [P] [US4] Add recovery state-machine tests for release, expiry, cancellation, disconnect, shutdown, indeterminate lifecycle calls, and failed reset/isolation in `tests/ChargingSimulator.Domain.Tests/Transitions/RecoveryStateMachineTests.cs`
- [ ] T046 [P] [US4] Add scripted-WebSocket integration tests for active-session abandonment, dual-connector recovery, timeout to recovery-failed, clean reacquisition, and no lifecycle replay in `tests/ChargingSimulator.IntegrationTests/Sessions/RecoveryWorkflowTests.cs`
- [ ] T047 [P] [US4] Add operator recovery endpoint authorization and known-good confirmation contract tests in `tests/ChargingSimulator.IntegrationTests/SessionApi/RecoveryEndpointContractTests.cs`

### Implementation for User Story 4

- [ ] T048 [US4] Implement lease-expiry monitoring, ownership revocation, release/cancellation/disconnect/shutdown triggers, and recovery record creation in `src/ChargingSimulator.Application/Recovery/RecoveryCoordinator.cs`
- [ ] T049 [US4] Implement bounded per-connector stop attempts, meter-job cancellation, socket closure, outstanding-work invalidation, and reusable-state verification in `src/ChargingSimulator.Application/Recovery/ChargerRecoveryWorkflow.cs`
- [ ] T050 [US4] Implement recovery-failed isolation, authorized explicit recovery, and evidence/outcome projection in `src/ChargingSimulator.Application/Recovery/RecoveryCoordinator.cs`
- [ ] T051 [US4] Add `/v1/chargers/{chargerId}/recover` authorization, operation tracking, and recovery status responses in `src/ChargingSimulator.Host/Endpoints/RecoveryEndpoints.cs`
- [ ] T052 [US4] Update graceful host shutdown to reject acquisition, cancel active runtime work, close connections, and record remaining recovery work within the configured deadline in `src/ChargingSimulator.Host/HostedServices/SimulatorLifecycleService.cs`

**Checkpoint**: Incomplete sessions never strand an apparently reusable charger; recovery is observable and bounded.

---

## Phase 7: User Story 5 - Manually Connect, Operate, and Observe a Charger (Priority: P2)

**Goal**: Let an operator use a transient validated ChargerLab profile to make the simulator own a manual OCPP connection, drive flows, view results, and disconnect safely.

**Independent Test**: Submit valid manual settings with pre-created `ChargerId` and `OcppId`, connect using the latter in the WebSocket address, run a left flow, inspect redacted correlated history, disconnect, and verify no ChargerLab management API request occurred.

### Tests for User Story 5

- [ ] T053 [P] [US5] Add manual profile validation, required ID, API-key redaction/clearing, and no-management-API unit tests in `tests/ChargingSimulator.Domain.Tests/Manual/ManualConnectionProfileTests.cs`
- [ ] T054 [P] [US5] Add scripted-WebSocket integration tests for OcppId address use, `ocpp1.6` negotiation, manual flow, disconnect cleanup, and rejected settings in `tests/ChargingSimulator.IntegrationTests/Manual/ManualConnectionWorkflowTests.cs`
- [ ] T055 [P] [US5] Add manual UI HTTP contract tests for profile submission, connect/disconnect, state/history display models, and secret omission in `tests/ChargingSimulator.IntegrationTests/Manual/ManualUiContractTests.cs`

### Implementation for User Story 5

- [ ] T056 [US5] Implement transient manual profile validation, in-memory secret lifetime/redaction, ChargerId correlation metadata, and OcppId transport identity mapping in `src/ChargingSimulator.Application/Manual/ManualConnectionService.cs`
- [ ] T057 [US5] Implement a manual connection transport-profile adapter that applies the configured authentication convention without logging the API key or invoking management APIs in `src/ChargingSimulator.Transport/ManualProfileOcppConnectionFactory.cs`
- [ ] T058 [US5] Implement manual connect, supported connector actions, correlated state/history projection, and bounded disconnect delegates in `src/ChargingSimulator.Host/Endpoints/ManualControlEndpoints.cs`
- [ ] T059 [US5] Implement the thin operator dashboard profile form, connector action controls, correlated exchange history, and reusable/recovery-failed display in `src/ChargingSimulator.Host/wwwroot/index.html` and `src/ChargingSimulator.Host/wwwroot/app.js`
- [ ] T060 [US5] Add manual UI styling that makes connection/recovery state and left/right connector state distinguishable in `src/ChargingSimulator.Host/wwwroot/site.css`

**Checkpoint**: An operator can make and safely close a simulator-owned OCPP connection using a transient profile, without API-secret exposure or ChargerLab management calls.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Qualify the integrated service against performance, diagnostics, security, and quickstart expectations.

- [ ] T061 [P] Add a 15-second readiness, ten-charger concurrency, 100-run correlation, and recovery-limit performance suite in `tests/ChargingSimulator.IntegrationTests/Scenarios/SuccessCriteriaTests.cs`
- [ ] T062 [P] Add tests for rejected protocol versions, half-open connections, malformed/unsupported incoming CALL responses, and incoming `RemoteStartTransaction` correlation while an outgoing CALL is pending in `tests/ChargingSimulator.IntegrationTests/Ocpp/InboundCommandAndFailureTests.cs`
- [ ] T063 Implement inbound `RemoteStartTransaction` eligibility validation, Accepted/Rejected correlated response-before-start behavior, and `NotSupported` responses for deferred actions in `src/ChargingSimulator.Ocpp/Runtime/IncomingCentralSystemCommandHandler.cs`
- [ ] T064 Implement final configuration validation, history bounds, secret-safe logging audit, and production health/readiness reporting in `src/ChargingSimulator.Host/Configuration/SimulatorOptions.cs` and `src/ChargingSimulator.Host/Program.cs`
- [ ] T065 [P] Document environment configuration, automation API examples, manual UI workflow, security boundaries, and operational recovery in `README.md` and `specs/001-charger-session-management/quickstart.md`
- [ ] T066 Run `dotnet test` and resolve all failures across `ChargingSimulator.sln`
- [ ] T067 Run `dotnet format --verify-no-changes` and resolve formatting/analyzer failures across `ChargingSimulator.sln`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)** has no prerequisites.
- **Foundational (Phase 2)** depends on Setup and blocks all user stories.
- **US1 (Phase 3)** depends on Foundation and is the MVP.
- **US2 (Phase 4)** depends on US1's command/session baseline.
- **US3 (Phase 5)** depends on Foundation and the US1 session API; it can proceed alongside US2 after US1.
- **US4 (Phase 6)** depends on US1 lifecycle and US2 connector activity support.
- **US5 (Phase 7)** depends on Foundation, the core OCPP runtime, and recovery; it does not require automation pool concurrency.
- **Polish (Phase 8)** depends on the intended completed stories.

### User Story Completion Graph

```text
Setup → Foundation → US1 (MVP) → US2 → US4 → US5
                         └──────→ US3 ────────────┘
                                      \            ↓
                                       └──────→ Polish
```

### Parallel Opportunities

- T003–T006 and T007–T017 can be split by project/file boundary; T018–T020 integrate their results.
- In US1, T024–T026 and T027–T028 can run in parallel before the coordinating tasks.
- In US2, T034–T035 run in parallel; in US3, T040–T041 run in parallel; in US4, T045–T047 run in parallel; in US5, T053–T055 run in parallel.
- After US1, US2 and US3 can be delivered by separate developers; US5 can start once the shared OCPP runtime and recovery contract are stable.

## Parallel Example: User Story 4

```text
Task: "Add recovery state-machine tests in tests/ChargingSimulator.Domain.Tests/Transitions/RecoveryStateMachineTests.cs"
Task: "Add scripted recovery integration tests in tests/ChargingSimulator.IntegrationTests/Sessions/RecoveryWorkflowTests.cs"
Task: "Add recovery endpoint contract tests in tests/ChargingSimulator.IntegrationTests/SessionApi/RecoveryEndpointContractTests.cs"
```

## Implementation Strategy

### MVP First (US1)

1. Complete Setup and Foundation.
2. Complete US1 through T033.
3. Run the US1 contract and scripted-WebSocket tests; validate a complete leased left-connector flow.
4. Demo or deploy the automation MVP only after the readiness, protocol, and lease behaviors pass.

### Incremental Delivery

1. Add US2 to make both connectors independent.
2. Add US3 to qualify parallel pool sessions.
3. Add US4 before relying on the service in long-running test suites.
4. Add US5 as the operator surface over the same runtime.
5. Complete the final protocol/failure qualification and quickstart validation.

## Format Validation

All 67 tasks use the required checklist form: checkbox, sequential `T###` ID, a `[P]` marker only where parallel work is viable, a `[US#]` label for every story task, and at least one exact file path.
