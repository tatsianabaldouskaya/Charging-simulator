# Implementation Plan: Manage Charger Simulation Sessions

**Branch**: `main` | **Date**: 2026-09-20 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/001-charger-session-management/spec.md`

## Summary

Build a .NET 10, OCPP 1.6-J charger-simulator host that leases a configurable pool to automation and supports operator-initiated manual connections. The automation session API owns leases and recovery; each charger has one OCPP WebSocket runtime and two isolated connector state machines. In manual mode, the UI accepts a transient ChargeLab profile including manually created `ChargerId` and `OcppId`; Connect opens the simulator-owned WebSocket using `OcppId`—no ChargeLab management API is called. The UI drives native OCPP flows and displays correlated frames. Operational history is in-process and scoped to the active automation session or manual connection.

## Technical Context

**Language/Version**: C# / .NET 10

**Primary Dependencies**: ASP.NET Core minimal APIs/hosting; `System.Net.WebSockets.ClientWebSocket`; `Microsoft.Extensions.Options`, logging, and TimeProvider testing; supplied OCPP 1.6-J JSON schemas as versioned contract fixtures. The schema-validator implementation remains isolated until it is qualified against supplied schemas/errata.

**Storage**: Bounded in-memory operational history for the running process; typed automation configuration from environment/user secrets/deployment configuration; transient, non-persisted manual connection profile. Persistent storage is out of scope.

**Testing**: `dotnet test`; xUnit unit tests for protocol/state logic; contract tests against schemas and fixtures; integration tests using an in-process scripted OCPP WebSocket central system, HTTP host, and controllable clock/IDs.

**Target Platform**: Long-running .NET service exposing a local/network HTTP control surface, deployed where it can reach ChargeLab over secure WebSocket.

**Project Type**: Service with automation session API and a thin manual operator web UI.

**Performance Goals**: 95% acquire-ready or clear-failure results within 15 seconds; 10 distinct chargers controlled concurrently; no connector cross-talk in 100 consecutive two-connector runs.

**Constraints**: OCPP 1.6 JSON over WebSocket with `ocpp1.6` subprotocol; at most one unresolved charger-originated OCPP request per charger; manual Connect opens a simulator-owned WebSocket and does not call ChargeLab management APIs; readiness/recovery are bounded; recovery reaches reusable or recovery-failed within 60 seconds; credentials are never persisted, logged, or returned.

**Scale/Scope**: One deployment with a configurable pool of chargers (at least 10); exactly two connectors per charger; core OCPP actions plus inbound `RemoteStartTransaction`; operational history lasts only for the current process/session.

## Constitution Check

*GATE: Passed before Phase 0 research; re-checked after Phase 1 design.*

- [x] OCPP 1.6 JSON-over-WebSocket actions, framing, correlation, and error semantics are specified in [contracts/ocpp-1.6-runtime.md](./contracts/ocpp-1.6-runtime.md).
- [x] Explicit managed-session and connector states, transition validation, and recovery behaviour are designed in [data-model.md](./data-model.md).
- [x] Background lifecycle, cancellation, timeout, and resource cleanup use a hosted runtime, scoped cancellation, and injectable time, all covered by deterministic integration tests.
- [x] Transport, time, IDs, configuration, protocol models, and orchestration have focused interfaces and ownership boundaries.
- [x] Typed configuration is startup-validated; secrets are externalized; structured logs carry charger, session, OCPP message/action, transaction, transition, and failure data.
- [x] Unit tests cover transitions/correlation/validation and integration-contract tests cover every scoped WebSocket action and recovery path.

No constitutional violations or exceptions are required.

## Project Structure

### Documentation (this feature)

```text
specs/001-charger-session-management/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── session-control-api.md
│   └── ocpp-1.6-runtime.md
└── tasks.md                 # Created later by /speckit-tasks
```

### Source Code (repository root)

```text
src/
├── ChargingSimulator.Host/             # automation API, manual UI/Connect profile, DI
├── ChargingSimulator.Application/      # session manager, commands, recovery orchestration
├── ChargingSimulator.Domain/           # state models and pure transition/validation rules
├── ChargingSimulator.Ocpp/             # OCPP envelopes, schema validation, correlation/router
└── ChargingSimulator.Transport/        # WebSocket client and ChargeLab connection factory

tests/
├── ChargingSimulator.Domain.Tests/     # transition and recovery unit tests
├── ChargingSimulator.Ocpp.Tests/       # frame/schema/correlation contract tests
└── ChargingSimulator.IntegrationTests/ # scripted WebSocket plus HTTP/session flows
```

**Structure Decision**: Use a small service-oriented solution. `Domain` is deterministic and transport-free; `Application` coordinates automation leases and manual connection lifecycles; `Ocpp` owns standards-specific framing/routing; `Transport` owns WebSocket I/O; `Host` exposes an automation API and a manual UI. The manual UI submits only connection intent and OCPP-flow intent to the simulator—not calls to ChargeLab management APIs—creating clear test seams for transport, clock, IDs, and configuration.

## Complexity Tracking

No constitution violations require justification.
