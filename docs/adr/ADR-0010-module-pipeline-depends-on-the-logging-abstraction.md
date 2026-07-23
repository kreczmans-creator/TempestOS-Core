# ADR-0010: The Module Pipeline Depends on the Logging Abstraction, Not a Concrete Logger

## Status

Accepted — WP 2.6 (Logging & Diagnostics Framework), 2026-07-22.

## Context

Since WP 2.1, every runtime component in the module pipeline
(`ReflectionFrameworkDiscoveryService`, `RuntimeModuleManager`,
`ModuleLifecycleManager`, `ServiceCollection`, `TempestServiceProvider`,
`ConfigurationBuilder`) has taken an optional constructor parameter typed
`LoggingService?` — a concrete class from the platform's original,
pre-module-pipeline bootstrap code, doing double duty as the pipeline's only
logging mechanism.

WP 2.6's objective was explicit and unconditional: "Logging shall become a core
platform service. All runtime components shall depend only upon the logging
abstraction. No runtime component shall know where logs are written." Left as
`LoggingService?`, every one of those six components would continue to depend
on a concrete class — not an abstraction — and would continue to know,
implicitly, that logs are written to a console and a file at a specific path,
because that is literally what `LoggingService`'s constructor does.

Requirement #8 (Diagnostics) reinforced the same point from a different angle:
"module discovery," "module registration," and "lifecycle transitions" are
diagnostic events the brief requires the runtime to log — and every one of
those events already had a corresponding `_logger?.Information(...)` call
inside the relevant WP 2.1–2.3 class. For those existing calls to flow through
the new logging framework's filtering, sinks, and configuration integration,
they had to be calls against the new abstraction, not the old concrete class.

## Decision

All six components were migrated from `LoggingService?` to `ILogger?`. In every
case the change was the same, narrow substitution: the constructor parameter's
type, the backing field's type, and the accompanying XML documentation. No
other line of logic in any of the six classes changed — every existing
`_logger?.Information(...)` call site remained syntactically valid unchanged,
since `ILogger.Information(string, Exception?, IReadOnlyDictionary<string,
object?>?)`'s first parameter accepts exactly what those call sites were
already passing.

The pre-existing `LoggingService` class itself was **not** touched, deleted, or
redesigned. It remains exactly as it was, still used by the platform's
original bootstrap code (`BootstrapService`, `HostingService`, `Program.cs`),
which sits outside the module pipeline this work package's "CURRENT PLATFORM"
section named (Discovery, Registration, Lifecycle, Dependency Injection,
Configuration). Migrating that older code was judged out of scope — see the
WP 2.6 retrospective's Architectural Debt Assessment for the resulting
coexistence of two logging mechanisms and why it was left as debt rather than
resolved here.

## Consequences

**Positive:**

- WP 2.6's central objective is now literally true for the module pipeline:
  every one of its six components depends on `ILogger`, an interface, and none
  of them can observe or reason about console output, file paths, or any other
  detail of where a message actually ends up.
- Every existing diagnostic log call (discovery progress, registration,
  lifecycle transitions, DI resolution, configuration building) now flows
  through minimum-level filtering and the registered sink automatically,
  without any of those classes needing new code — the migration alone was
  sufficient to satisfy requirement #8 for every event already covered by an
  existing call.
- The change was mechanical and low-risk specifically because logging was
  already threaded through as an *optional* parameter in every one of these
  six classes (WP 2.1 through WP 2.5 each established that convention
  independently) — there was no logic to disentangle, only a type to swap.

**Negative:**

- Two logging mechanisms now coexist in the codebase: `ILogger` (module
  pipeline) and `LoggingService` (bootstrap/hosting). A new contributor reading
  `Tempest.Core.Logging` for the first time will find both and needs to know
  which one is which — mitigated by this ADR and the WP 2.6 retrospective, not
  eliminated.
- Every test across WP 2.1–2.5 that previously constructed a real
  `LoggingService` pointed at a temporary directory (to prove "logging doesn't
  throw") had to be updated to use the new abstraction instead — a mechanical,
  but real, one-time cost paid by this work package. A `RecordingLogger` test
  double was introduced specifically to make this migration simpler and the
  resulting tests more precise (asserting messages were actually recorded,
  not just that construction didn't throw) — see the WP 2.6 retrospective.

## Future Considerations

If the platform's original bootstrap code (`BootstrapService`, `HostingService`,
`Program.cs`) is ever revisited or brought into the module pipeline's own
architecture, it should migrate to `ILogger` at that point, retiring
`LoggingService` entirely — not before, and not as an incidental side effect of
unrelated work. Until then, `LoggingService`'s continued existence should be
read as intentional, scoped debt, not an oversight.
