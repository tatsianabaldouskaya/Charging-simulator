<!--
Sync Impact Report
- Version change: 1.0.0 -> 2.0.0
- Modified principles: manual operation, focused boundaries, and source-file organization
- Added sections: Core Principles, Protocol and Runtime Constraints, Development Workflow
- Removed sections: none
- Templates requiring updates:
  - ✅ updated: .specify/templates/plan-template.md
  - ✅ updated: .specify/templates/spec-template.md
  - ✅ updated: .specify/templates/tasks-template.md
- Follow-up TODOs:
  - TODO(RATIFICATION_DATE): Record the date on which the project owner formally adopts this constitution.
-->
# Charging Simulator Constitution

## Core Principles

### I. Protocol Fidelity Is Non-Negotiable
The simulator MUST exchange charger-to-central-system messages using native OCPP 1.6
over WebSocket. Implementations MUST preserve OCPP message framing, action names,
message identifiers, request/response correlation, and error semantics. Protocol
behaviour MUST be verified against documented OCPP 1.6 flows or contract fixtures;
application-specific shortcuts may exist only behind an explicit test abstraction.
Rationale: the simulator is useful only when it behaves like a real charger.

### II. Explicit, Controllable Session State
All charging-session transitions—connection, boot/registration, authorization, transaction
start, meter-value reporting, transaction stop, and disconnect—MUST be represented by
an explicit state model. Public controls MUST reject invalid transitions, be safe under
concurrent calls, and expose observable completion or failure. A caller MUST be able to
start, inspect, drive, and stop a session deterministically. Rationale: automated tests
must not depend on timing guesses or hidden device state.

### III. Manual Operation and Connection Ownership
The simulator MUST provide an interactive workflow for an operator to connect, inspect,
operate, and disconnect a charger. It MUST own the OCPP WebSocket; browsers MUST never
connect to the central system directly. Shutdown and cancellation MUST close WebSocket activity cleanly.

### IV. Focused Boundaries and Explicit State
Production code MUST separate protocol models, message generation/parsing, state
orchestration, WebSocket transport, configuration, and external ChargeLab integration
behind focused interfaces. Tests MUST cover state transitions, OCPP serialization and
correlation, error paths, and end-to-end WebSocket exchanges. Clocks, delays, IDs, and
transport dependencies MUST be injectable or otherwise controllable in tests. Rationale:
reliable simulations require repeatable tests without network or wall-clock flakiness.

### V. Clear .NET Design, Formatting, and Operational Diagnostics
The codebase MUST target .NET 10 and follow idiomatic .NET conventions, nullable-aware
APIs, dependency injection, cancellation tokens, and asynchronous I/O. Components MUST
have a single clear responsibility. Every C# file MUST use a file-scoped namespace and
declare no more than one class; duplication and speculative abstractions are
prohibited. Configuration MUST use typed options, validate required connection settings
at startup, and never commit secrets. Structured logs MUST identify simulator instance,
OCPP message ID, action, transaction ID when present, state transition, and failures.
Rationale: clean boundaries and diagnostics make asynchronous protocol failures tractable.

## Protocol and Runtime Constraints

- The simulator MUST connect to ChargeLab using supplied, environment-specific settings;
  endpoint URLs, credentials, charge-point identity, and TLS settings MUST be externalized
  configuration.
- The supported baseline is OCPP 1.6 JSON over WebSocket. Any additional protocol version,
  message type, vendor extension, persistence mechanism, or API MUST be documented in the
  relevant feature specification before implementation.
- Every received OCPP request MUST result in a correlated response or a protocol-compliant
  error. Every outbound request MUST have bounded completion through cancellation or a
  configurable timeout.
- Meter values MUST be tied to the active transaction when OCPP requires it and generated
  through a controllable schedule or explicit caller command; no uncontrolled background
  loop may survive session shutdown.
- Connection loss, remote errors, malformed messages, duplicate messages, and an attempted
  invalid state transition MUST produce diagnosable results without corrupting session state.

## Development Workflow

- Feature specifications MUST state the affected OCPP actions, expected state transitions,
  configuration inputs, failure/recovery paths, and observable automation outcome.
- Implementation plans MUST pass the Constitution Check before design and after design,
  documenting protocol fidelity, state model, testability, background lifecycle, and
  configuration/logging decisions.
- Tasks MUST include automated unit tests for pure protocol/state logic and integration or
  contract tests for every changed WebSocket/OCPP exchange. Tests MUST be implemented before
  the corresponding production code and run with `dotnet test` before completion.
- Reviews MUST reject changes that bypass interfaces, conceal asynchronous failures, weaken
  protocol conformance, add unvalidated configuration, or leave long-running resources alive.
- Public simulator controls, supported OCPP actions, required configuration, and operational
  examples MUST be documented alongside implementation changes.

## Governance

This constitution supersedes conflicting local practices for this repository. Amendments
MUST describe their impact on protocol fidelity, automation, testing, configuration, and
documentation; update this file's Sync Impact Report; and receive project-owner approval.

Versioning follows semantic versioning: MAJOR for incompatible removal or redefinition of a
principle, MINOR for a new principle or material mandatory guidance, and PATCH for
clarifications that preserve policy. Every plan and review MUST include a compliance check;
unjustified violations block implementation. The constitution is reviewed when a protocol
version, external integration, runtime model, or public control surface changes.

**Version**: 2.0.0 | **Ratified**: TODO(RATIFICATION_DATE): Awaiting project-owner adoption | **Last Amended**: 2026-09-21
