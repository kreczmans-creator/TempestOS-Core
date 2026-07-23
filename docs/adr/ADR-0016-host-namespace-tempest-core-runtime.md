# ADR-0016: The Host Lives in Tempest.Core.Runtime, Distinct From Tempest.Core.Hosting

## Status

Accepted — resolves WP 2.7's Open Question 3, 2026-07-22. Architecture only;
no code changes accompany this decision. No implementation exists yet; this
ADR fixes the namespace and type names a future implementation must use.

## Context

*Runtime Host Architecture.md* (WP 2.7) flagged a real risk without resolving
it: `Tempest.Core.Hosting` already exists — the platform's original,
pre-module-pipeline `HostingService`, which creates a handful of workspace
directories on disk and has nothing to do with orchestrating the module
pipeline. The new Runtime Host is a categorically larger concept, and placing
it in, or naming it too similarly to, the existing `Hosting` namespace would
be a genuine, avoidable source of confusion.

## Decision

The Runtime Host lives in a new namespace, `Tempest.Core.Runtime`, with these
names: `TempestHost` (the running instance), `TempestHostBuilder` (assembles
configuration sources and any other pre-registration inputs, then produces a
`TempestHost`), `ITempestHost`, `ITempestHostBuilder` (the corresponding
abstractions — named, not yet defined; no interface members are specified by
this ADR, consistent with WP 2.7's "no interfaces intended for implementation"
constraint).

`Tempest.Core.Hosting` is retained, not merged into or replaced by the new
namespace. Its scope is explicitly reframed: **environment and deployment
adapters** — how a `TempestHost` is embedded into a specific deployment
target (a console application, a Windows Service, a Linux daemon, a
container, an embedded process) — never the platform's own orchestration,
which `Tempest.Core.Runtime` now owns entirely.

The governing rule, stated exactly as proposed: **Runtime = platform. Hosting
= environment.**

## Consequences

**Positive:**

- Resolves the naming/placement risk *Runtime Host Architecture.md* flagged,
  before any implementation exists to get it wrong.
- Establishes a clean seam for future deployment-target support: a console
  entry point, a Windows Service wrapper, a Linux daemon wrapper, a container
  entry point are all, under this split, adapters that construct and drive a
  `TempestHost` through whatever a specific environment's own start/stop
  contract requires (Service Control Manager callbacks, `SIGTERM` handling,
  container orchestrator health/readiness probes) — none of which
  `Tempest.Core.Runtime` needs to know anything about.
- Matches a widely-recognised architectural pattern (separating an
  application's runtime from its hosting environment) without adopting any
  external package — the distinction is TempestOS's own, expressed through
  namespace boundaries already available in .NET.

**Negative:**

- Two "Host"-adjacent namespaces now exist for a new contributor to learn —
  mitigated by this ADR's explicit rule and by cross-referencing it from both
  namespaces' own future documentation, but a real, ongoing cost of
  onboarding, not eliminated by naming alone.

## Future Considerations

The existing `HostingService` (workspace directory creation) should be
revisited under this split by whichever work package next touches
`Tempest.Core.Hosting` — either as the first genuine `Hosting`-namespace
adapter (a console/local-filesystem deployment concern) or folded into
`TempestHostBuilder`'s own configuration surface, if workspace-path setup
turns out to belong with the platform's own startup rather than with a
specific deployment adapter. Not decided here.
