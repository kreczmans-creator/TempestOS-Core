# TempestOS v0.6.0 — Academy Plan

## Purpose

Identifies, for each of the nine `v0.6.0` Work Packages, the existing
Academy material an engineer should read *before* starting
implementation, and the new Academy material that Work Package is
expected to produce once it completes — mirroring the same 13-section
retrospective template obligation every prior Work Package has met
(Engineering Governance §6), named here in advance so it is a known
Definition-of-Done item, not a follow-up pass discovered after the
fact (`Risk Register.md` `R6`'s own named mitigation, applied to Academy
specifically rather than just ADRs).

This document does not itself add anything to `docs/academy/Academy
Index.md` — no new Academy document exists yet for any `v0.6.0` Work
Package, so there is nothing yet to index. Each Work Package's own
completed retrospective is what earns an `Academy Index.md` entry, at
the point it actually exists, exactly as every prior Work Package's
entry was added only once its own retrospective was written.

## How to Use This Document

Before a Work Package's implementation phase begins, its assigned
engineer(s) should read the "Required Reading" list below in full — it
is deliberately curated per-service, not a repeat of the entire
Academy. On completion, the Work Package's own retrospective (the
existing 13-section template under `docs/academy/03 Work Packages/`)
is the "Required Output," and should cross-reference every document
named in "Required Reading" that it actually drew on, exactly as every
prior retrospective's own "Academy references" section already does.

## Cross-Cutting Required Reading (Every `v0.6.0` Work Package)

Every engineer starting any of the nine Work Packages below should
first read, if not already familiar:

- `docs/academy/06 Engineering Standards/Engineering Governance.md` —
  the constitution every Work Package below still operates under.
- `docs/architecture/Platform Service Map.md` and this release's own
  `Platform Services Overview.md` — the existing and proposed service
  landscape a new service must fit into.
- `docs/releases/v0.6.0/Release Architecture.md`, `Platform Service
  Dependency Diagram.md`, `Platform Service Contracts.md`, and `Service
  Registration Matrix.md` — the specific, ratified design for *this*
  release, produced by the architecture and contract review phases this
  document follows.
- `docs/adr/ADR-0023...` (Platform Layering) — the four-layer rule every
  new service must satisfy, checked explicitly in `Governance
  Confirmation.md`.

## Per-Work-Package Required Reading and Expected Output

### `WP 6.0` — Reporting Framework

**Required reading.** `docs/academy/03 Work Packages/
WP5.1A-command-framework-architecture.md` and
`WP5.1B-command-framework-implementation.md` (the imperative-
registration, `RD-0040`-avoiding pattern Reporting's own
`IReportingService.RegisterDefinition` directly mirrors); `Command
Framework Architecture.md` (the orthogonality-drawing precedent
Reporting's own Export/Import distinction follows).

**Expected output.** A new retrospective,
`WP6.0-reporting-framework-implementation.md`, under `docs/academy/03
Work Packages/`, cross-referencing `ADR-0040` once formally authored.

### `WP 6.1` — Permissions & Identity

**Required reading.** `docs/academy/03 Work Packages/
WP5.0A-navigation-framework-architecture.md` and
`WP5.0B-navigation-framework-implementation.md` (the closest existing
precedent for a Work Package likely needing its own architecture/
implementation split); `docs/security/Threat Model.md` (assumptions 4
and 5, directly governing `ADR-0043`'s local-only scope decision);
`docs/security/Platform Security Review v0.5.0.md` (Findings SEC-01,
NAV-1) and `docs/architecture/Command Framework Architecture.md`
Finding CMD-1 — the three `TD-09`/`TD-10`/`TD-11` findings this Work
Package is positioned to resolve, each read in its own original context
before attempting a fix.

**Expected output.** Given this Work Package's own likely
architecture/implementation split (`Risk Register.md` `R1`), two
retrospectives are anticipated:
`WP6.1A-permissions-and-identity-architecture.md` and
`WP6.1B-permissions-and-identity-implementation.md` — mirroring the
`WP 5.0A`/`WP 5.0B` naming convention exactly. Each should explicitly
confirm whether `TD-09`, `TD-10`, and `TD-11` were actually closed, with
the specific regression test proving each (see `Testing Strategy.md`).

### `WP 6.2` — Notification Framework

**Required reading.** `docs/academy/03 Work Packages/
WP4.4-event-bus-architecture.md`,
`WP4.4D-event-bus-implementation.md`, and the existing "Building an
Event-Driven Module" Academy concept guide — Notifications is built
directly on top of the Event Bus and should not re-derive its dispatch/
failure-isolation model from scratch.

**Expected output.** A new retrospective,
`WP6.2-notification-framework-implementation.md`, cross-referencing
`ADR-0046` once formally authored, and an extension (not a
replacement) of "Building an Event-Driven Module" covering the
notification-specific addition.

### `WP 6.3` — REST API

**Required reading.** `docs/academy/03 Work Packages/
WP4.5-background-services-architecture.md` and
`WP4.5-background-services-implementation.md` (the `IHostedService`
model the REST API's own hosting scaffold reuses directly);
`WP5.1A-command-framework-architecture.md` (the dispatch mechanism
every REST route delegates to); this release's own `WP 6.1` retrospective
(a hard prerequisite — `WorkPackages.md`'s own stated block) before
authorization can be wired in at all.

**Expected output.** Given the ASP.NET Core/Kestrel adoption decision
(`ADR-0049`) is itself substantial enough to warrant separate treatment
from the route-dispatch design, an architecture/implementation split is
anticipated here as well:
`WP6.3A-rest-api-architecture.md` and
`WP6.3B-rest-api-implementation.md`. The architecture retrospective
should include a dedicated "Why ASP.NET Core/Kestrel" section, since
this is this platform's first departure from `ADR-0005`'s pure-custom-
container precedent and deserves the same explicit justification
`ADR-0005` itself originally gave for the opposite choice.

### `WP 6.4` — Settings Framework (establishes Persistence)

**Required reading.** `docs/academy/05 Case Studies/` Case Study 05
("Why Isn't Configuration Mutable?") — the exact question Settings
exists to answer differently, for the runtime-mutable case; `docs/
academy/03 Work Packages/WP5.2-diagnostics-improvements.md` (a
precedent for a Work Package that had to reason carefully about a new
service's own storage/timing constraints, even though Diagnostics
itself has no persistence).

**Expected output.** A new retrospective,
`WP6.4-settings-framework-implementation.md`, cross-referencing
`ADR-0041` and `ADR-0042` once formally authored, with an explicit
section on the `IPersistenceStore` design decision (since this Work
Package establishes it on `WP 6.5`'s behalf as well as its own).

### `WP 6.5` — Audit Framework

**Required reading.** `WP 6.4`'s own completed retrospective (a hard
prerequisite, since Audit depends on the Persistence abstraction it
establishes); `WP 6.1`'s own completed retrospective (Audit's other hard
dependency, for attribution); `docs/academy/06 Engineering Standards/
02-testing-strategy.md`'s own guidance on regression-test naming, since
this Work Package's own tests must prove a genuine behavioural
distinction from Logging/Diagnostics, not merely new functionality.

**Expected output.** A new retrospective,
`WP6.5-audit-framework-implementation.md`, cross-referencing
`ADR-0045` once formally authored, with an explicit worked comparison
against Logging and Diagnostics proving the orthogonality claim in
practice, not just in design.

### `WP 6.6` — Licensing Framework

**Required reading.** `docs/architecture/Platform Service Map.md`'s own
Platform Version entry (`WP 4.2A`) — the "deliberately a leaf" precedent
Licensing directly mirrors; `docs/academy/03 Work Packages/
WP5.2-diagnostics-improvements.md` (the `Func<T>`-accessor
Composition-Root-timing pattern this Work Package deliberately avoids
recreating, per `ADR-0050` — worth reading precisely to understand what
Licensing is *not* doing and why).

**Expected output.** A new retrospective,
`WP6.6-licensing-framework-implementation.md`, cross-referencing
`ADR-0050` once formally authored, with an explicit statement of
whether license-file integrity/signature verification was included or
deliberately deferred (a named open question in `Platform Service
Contracts.md`).

### `WP 6.7` — Export / Import

**Required reading.** `docs/architecture/Command Framework
Architecture.md` and `Event Bus Architecture.md`'s own orthogonality
sections (the precedent for drawing a boundary between two
surface-similar concepts, applied here to Export/Import vs. Persistence
and Export/Import vs. Reporting).

**Expected output.** A new retrospective,
`WP6.7-export-import-implementation.md`, cross-referencing `ADR-0051`
once formally authored, with a worked example of exporting and
re-importing at least one real `WP 6.4`-established Settings value
end-to-end.

### `WP 6.8` — Platform Services Integration Review

**Required reading.** `docs/academy/03 Work Packages/
WP4.2D-platform-services-architecture-review.md`,
`WP5.0S-platform-security-baseline-audit.md`, and
`WP5.4-v0.5.0-release-candidate-and-engineering-sign-off.md` — the
three direct precedents for a closing, re-verifying review Work Package,
each of which found and corrected real governance drift by re-deriving
counts from the file system rather than trusting prior claims.

**Expected output.** A new retrospective,
`WP6.8-platform-services-integration-review.md`, plus the completed
`docs/releases/v0.6.0/Retrospective.md` (already scaffolded, awaiting
this Work Package's own closing content per its own "Status" note).
This Work Package's own retrospective should explicitly confirm every
"Expected output" listed above for `WP 6.0`–`WP 6.7` actually exists,
mirroring the file-system-level verification discipline
`WP 5.4`/`WP 4.2D`/`WP 5.0S` each already established.

## Summary Table

| Work Package | Key Required Reading | Expected New Academy Document(s) |
|---|---|---|
| `WP 6.0` | Command Framework architecture/implementation | `WP6.0-reporting-framework-implementation.md` |
| `WP 6.1` | Navigation architecture/implementation; Threat Model; Security Review findings | `WP6.1A-...-architecture.md`, `WP6.1B-...-implementation.md` |
| `WP 6.2` | Event Bus architecture/implementation; Building an Event-Driven Module | `WP6.2-notification-framework-implementation.md` |
| `WP 6.3` | Background Services architecture/implementation; Command Framework architecture; `WP 6.1` retrospective | `WP6.3A-...-architecture.md`, `WP6.3B-...-implementation.md` |
| `WP 6.4` | Case Study 05; Diagnostics Improvements | `WP6.4-settings-framework-implementation.md` |
| `WP 6.5` | `WP 6.4` and `WP 6.1` retrospectives; testing-strategy standard | `WP6.5-audit-framework-implementation.md` |
| `WP 6.6` | Platform Version entry; Diagnostics Improvements | `WP6.6-licensing-framework-implementation.md` |
| `WP 6.7` | Command Framework and Event Bus orthogonality sections | `WP6.7-export-import-implementation.md` |
| `WP 6.8` | `WP 4.2D`, `WP 5.0S`, `WP 5.4` retrospectives | `WP6.8-platform-services-integration-review.md`, completed `Retrospective.md` |

## Related Documents

`docs/academy/Academy Index.md`; `docs/academy/06 Engineering Standards/
Engineering Governance.md` §6; `Release Architecture.md`; `Required
ADRs.md`; `Testing Strategy.md`; `docs/releases/v0.6.0/WorkPackages.md`;
`docs/releases/v0.6.0/Retrospective.md`.
