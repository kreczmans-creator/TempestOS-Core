# WP 2.6 — Logging & Diagnostics Framework

## 1. Introduction

WP 2.6 introduced logging as a first-class platform service and, in doing so,
became the first work package to reach backward into every previous one.
WP 2.1 through WP 2.5 had each, independently, threaded an optional
`LoggingService?` parameter through their public constructors — a reasonable,
low-friction choice at the time, since nothing better existed yet. WP 2.6's
stated objective made that choice untenable: "no runtime component shall know
where logs are written." Satisfying that sentence literally meant migrating
six previously-completed classes from a concrete dependency to an abstraction
— not a redesign of what any of them do, but a genuine, deliberate change to
how they depend on logging.

## 2. Purpose

To give every runtime component — the module pipeline and any future service
alike — one logging abstraction (`ILogger`) to depend on, with filtering,
structured properties, exception support, and a pluggable destination
(`ILogSink`), all configured once from `Runtime:Logging:MinimumLevel`, and to
retire the module pipeline's dependency on the concrete, pre-existing
`LoggingService` in favour of it.

## 3. Background

`LoggingService` (`Tempest.Core.Logging`) predates the module pipeline
entirely — it is part of the platform's original bootstrap code, alongside
`ApplicationConfiguration`/`ConfigurationService`, `BootstrapService`, and
`HostingService`. It writes formatted lines to the console and to a dated file
under a configured log directory. When WP 2.1 needed *some* way to record
discovery progress, `LoggingService` was the only logging mechanism that
existed, so it became the pipeline's logging dependency by default — carried
forward, unchanged, through WP 2.2, WP 2.3, WP 2.4, and WP 2.5, each of which
added their own optional `LoggingService? logger` parameter following the same
convention.

By WP 2.5, this convention was doing real diagnostic work: discovery,
registration, lifecycle transitions, DI resolution, and configuration building
all logged through it. But every one of those log calls depended on a concrete
class that wrote to a specific place, in a specific format, with no filtering,
no structured data, and no way to swap the destination without changing the
class itself.

## 4. The Problem

1. **What does the logging contract actually look like** — one method per
   severity, each with a message, an optional exception, and optional
   structured properties, with "no formatting logic in callers"?
2. **Who creates loggers, and how are they named**, given the brief's own
   examples (`CreateLogger("Runtime")`, `CreateLogger("Discovery")`,
   `CreateLogger("Configuration")`) map directly onto the module pipeline's
   existing components?
3. **Where do log entries actually go**, and how can that destination be
   swapped later (file, database, telemetry, network) without touching
   `ILogger` or anything that logs?
4. **What does an immutable log record need to carry** — timestamp, level,
   category, message, exception, structured properties, thread ID — and how is
   "do not automatically flatten stack traces" honoured while still carrying an
   exception at all?
5. **Where does the minimum log level come from**, and what happens if
   `Runtime:Logging:MinimumLevel` is missing versus present-but-invalid?
6. **How is filtering enforced** so that a filtered-out message never reaches
   a sink, and never even causes an allocation it didn't need to?
7. **How does the existing module pipeline actually adopt this** — do WP 2.1
   through WP 2.5's six classes change, and if so, how much?
8. **How is a fully-wired logging setup registered in the DI container**, given
   that producing a working default logger requires *calling*
   `ILoggerFactory.CreateLogger`, something the container cannot do by
   reflection alone?

## 5. The Design

**`LogLevel`** — `Trace`, `Debug`, `Information`, `Warning`, `Error`,
`Critical`, `None`, ordered by increasing severity, with `None` as a sentinel
meaning "never emit."

**`LogEntry`** — sealed, immutable, every property get-only: `Timestamp`
(UTC), `Level`, `Category`, `Message`, `Exception` (the real exception object,
never pre-flattened to a string), `Properties`
(`IReadOnlyDictionary<string, object?>`, never null, empty if none supplied),
`ThreadId` (the managed thread ID the message was logged from).

**`ILogger`** — six methods (`Trace`, `Debug`, `Information`, `Warning`,
`Error`, `Critical`), each taking `(string message, Exception? exception =
null, IReadOnlyDictionary<string, object?>? properties = null)`. Optional
parameters, not true C# method overloads, give callers the exact calling
ergonomics the brief asked for ("each overload shall support... optional
Exception... optional structured properties") without multiplying the
interface's method count.

**`ILoggerFactory`** — one method, `CreateLogger(string category)`.

**`ILogSink`** — one method, `Write(LogEntry entry)`, called only for entries
that already passed filtering.

**`ConsoleLogSink`** — the one sink WP 2.6 implements. Plain text, no colour,
one line per entry plus an appended exception (via its own `ToString()`, which
is this sink's own formatting choice, not something `LogEntry` imposes).

**`Logger`** (concrete `ILogger`, `internal` constructor, created only by
`LoggerFactory`) — bound for its whole life to one category, one minimum
level, and one sink. Filtering is the first line of every logging method: a
message below the minimum level returns immediately, before a `LogEntry` is
constructed and before the sink is ever touched.

**`LoggerFactory`** (concrete `ILoggerFactory`) — reads
`Runtime:Logging:MinimumLevel` from an injected `IConfigurationProvider` once,
at construction: missing → `LogLevel.Information`; present but not a valid
`LogLevel` name → throws `ConfigurationException`, at startup, before any
logger exists to log the failure itself.

**`LoggingServiceCollectionExtensions.AddLogging`** — the DI bridge. Builds a
`ConsoleLogSink`, a `LoggerFactory`, and a default `ILogger` (category
`"Runtime"`) directly — not via the container's reflection-based
construction — and registers all three via `AddInstance`, reusing ADR-0009's
principle a second time (see that ADR's WP 2.6 update).

**The migration**: `ReflectionFrameworkDiscoveryService`, `RuntimeModuleManager`,
`ModuleLifecycleManager`, `ServiceCollection`, `TempestServiceProvider`, and
`ConfigurationBuilder` all had their `LoggingService? logger` parameter and
backing field retyped to `ILogger?` — nothing else in any of the six classes
changed. `TempestServiceProvider` additionally gained one new log call ("service
provider built"), the one diagnostic event from requirement #8 that had no
existing home anywhere.

## 6. Alternatives Considered

**True method overloads instead of optional parameters** (`Information(string
message)`, `Information(string message, Exception exception)`, and so on, as
separate signatures). Rejected: optional parameters give exactly the same
calling ergonomics with a sixth of the method count, and "no formatting logic
in callers" doesn't require overload resolution, just optional arguments.

**Letting each of the six migrated classes format its own log messages
differently**, or leaving some of them on `LoggingService` if migrating felt
disruptive. Rejected outright — WP 2.6's objective was unconditional, and a
partial migration would have left "no runtime component shall know where logs
are written" false for whichever components were left behind, with no
principled reason to choose which.

**Registering `LoggerFactory`/`ConsoleLogSink`/the default `ILogger` via
ordinary `Singleton<T,TImpl>()` reflection-based registration**, relying on the
container to resolve `LoggerFactory`'s own constructor dependencies. This
works for `LoggerFactory` and `ConsoleLogSink` individually (both have
ordinary, DI-resolvable constructors), but breaks down for the *default
logger*: there is no reflection path from "construct a `Logger`" to "call
`CreateLogger` on an already-built factory" — a factory method invocation is
not something `Add`/`Singleton`/`Transient` can express. Rejected in favour of
building all three manually at the composition root and registering them via
`AddInstance`, keeping the construction story for logging consistent and
simple rather than split across two different registration mechanisms for no
clear benefit.

**Supporting multiple simultaneous sinks (fan-out) in this work package.**
Considered, since `Logger` could trivially hold `IReadOnlyList<ILogSink>`
instead of one `ILogSink`. Rejected for WP 2.6 specifically because
`TempestServiceProvider` has no support for resolving `IEnumerable<TService>`
(multiple registrations of the same interface) — introducing multi-sink
support now would either require extending the DI container (a different,
larger, unrequested change) or hand-assembling a sink list outside the
container entirely, neither of which this work package's scope covers.
`Logger` was still designed to hold a `sink` field typed as the `ILogSink`
*interface* rather than concretely as `ConsoleLogSink`, so a future,
composite `ILogSink` that internally fans out to several real sinks can be
introduced without changing `Logger` at all — see Future Evolution.

## 7. Why This Solution Was Chosen

Every non-obvious decision in this work package resolves to the same test:
does it make `ILogger` the *only* thing a runtime component needs to know
about logging? Optional parameters over true overloads, filtering before
construction, `ILogSink` as an interface `Logger` depends on rather than a
concrete `ConsoleLogSink`, and the wholesale migration of six existing classes
are all different expressions of that one test.

## 8. Architectural Principles

- **Separation of Concerns** — `ILogger` (what a component calls),
  `ILoggerFactory` (how loggers are created), and `ILogSink` (where entries
  end up) are three independent contracts; a component that logs never
  references the second or third at all.
- **Immutability** — `LogEntry` is immutable end-to-end, following exactly
  the same pattern established for `ModuleDescriptor`, `RuntimeModule`, and
  `ConfigurationProvider`.
- **Fail Fast** — an invalid `Runtime:Logging:MinimumLevel` value throws
  `ConfigurationException` at `LoggerFactory` construction (startup), not the
  first time a message happens to need filtering.
- **Deterministic Systems / Performance** — filtering happens before
  allocation and before sink invocation, unconditionally; a filtered message
  costs one comparison and nothing else.
- **Fail-safe infrastructure** — logging is explicitly infrastructure, not
  business logic: nothing in this framework can influence what a caller's own
  code does, and nothing in `Logger`/`ConsoleLogSink` catches or suppresses
  exceptions from a caller's own logic — a logging call can only ever affect
  whether and how a message is recorded, never the outcome of the operation
  being logged.

## 9. Benefits

- Every diagnostic event requirement #8 named that already had a call site
  (module discovery, module registration, lifecycle transitions, configuration
  loading) is now satisfied automatically by the migration alone, with no new
  code in those classes beyond the one added "service provider built" line.
- A future sink (file, database, telemetry, network) can be introduced by
  implementing `ILogSink` alone — no change to `ILogger`, `ILoggerFactory`,
  `Logger`, or any of the six migrated components.
- The `RecordingLogger`/`RecordingLogSink` test doubles introduced for this
  migration make every "with logger" test in the suite more precise than
  before: the old tests only proved construction didn't throw; the new ones
  assert messages were actually recorded.

## 10. Trade-offs

- Two logging mechanisms now coexist: `ILogger` (module pipeline) and the
  original `LoggingService` (bootstrap/hosting, untouched). See the
  Architectural Debt Assessment below.
- `LoggerFactory`/`Logger` support exactly one sink, not a fan-out list —
  correct and sufficient for WP 2.6 (only `ConsoleLogSink` exists), but a real,
  named limitation the moment a second sink is introduced.
- "Startup complete," "Shutdown initiated," and "Shutdown complete" — three of
  requirement #8's eight named diagnostic events — have no code path producing
  them anywhere in the codebase, because no composition root exists yet to be
  "starting up" or "shutting down." The logging framework fully supports
  emitting them (any future composition root can call
  `logger.Information("Startup complete")` directly), but WP 2.6 does not
  fabricate a composition root merely to produce these three log lines — see
  the Architectural Debt Assessment.

## 11. Common Mistakes

The mistake most worth preserving from this work package is not a bug that
shipped, but a scope question that had to be resolved carefully: whether
"integrate with these systems" (the brief's own words, under "CURRENT
PLATFORM") meant migrating WP 2.1–2.5's existing classes, or merely meant the
new framework should be *capable* of being used by them eventually. Reading
the objective narrowly (build the framework, leave existing code alone) would
have left "no runtime component shall know where logs are written" false for
every component that mattered most — the entire module pipeline. The
resolution was to read the brief's stated objective as the actual requirement,
and "do not redesign existing architecture" as bounding *how* the integration
happened (a narrow, mechanical type substitution, not a rewrite of any
algorithm) rather than *whether* it happened at all. A future engineer facing
a similarly-worded brief should apply the same test: does the objective, taken
literally, require touching existing code, and if so, is the touch a
substitution (safe, in scope) or a redesign (not)?

A second, more mechanical trap: several of the "with logger" tests migrated
from `LoggingService` originally used a temporary directory and a `finally`
block to clean it up, purely to satisfy `LoggingService`'s constructor
requiring a real filesystem path. Migrating to `RecordingLogger` eliminated
that ceremony entirely — a future contributor tempted to add filesystem setup
to a *new* logging test should recognise that need as a sign the test is
exercising `ConsoleLogSink`/a real sink, not `ILogger` itself, and reach for
`RecordingLogger` (or `RecordingLogSink`) instead.

## 12. Future Evolution

- **Additional sinks** (file, database, telemetry, network) are the most
  obvious next step, each implementing `ILogSink` independently.
- **Multi-sink fan-out** — if more than one sink is ever needed
  simultaneously, the correct shape is very likely a composite `ILogSink`
  implementation that itself holds and writes to several inner sinks, rather
  than changing `Logger`/`LoggerFactory` to accept a collection — this keeps
  the DI container's lack of `IEnumerable<TService>` support from being a
  blocker at all.
- **A real composition root** — repeatedly flagged since WP 2.4's own Future
  Evolution notes and now sharpened by this work package: "Startup complete,"
  "Shutdown initiated," and "Shutdown complete" cannot be logged by anything
  until a composition root exists to log them. This is very likely the next
  structural gap worth closing, not a logging-specific one.
- **Logging severity for construction/resolution failures** — WP 2.4 and
  WP 2.5's retrospectives both noted that failures were logged at
  `Information` severity for lack of anything better; now that `ILogger`
  supports `Warning`/`Error`/`Critical` properly, a follow-up pass revisiting
  those call sites (duplicate registration, invalid transitions, resolution
  failures) to use a more appropriate severity is worth doing, though it was
  judged outside this work package's own scope (a call-site content change,
  not a logging-framework change).

## 13. Key Takeaways

1. An objective stated in the present tense ("no runtime component shall know
   where logs are written") is not aspirational — if it isn't true of the
   actual codebase after the work package ships, the work package isn't done,
   regardless of how much new, correct framework code exists alongside the
   untouched old dependency.
2. A mechanical, type-only migration across many files is still "in scope" for
   a work package whose explicit objective requires it — the test is whether
   any *logic* changed (it didn't, here), not how many files were touched.
3. ADR-0009's principle (some services must exist before dependency injection
   begins) generalised cleanly to a second, independent case — logging — on
   the very first opportunity, which is exactly the validation an
   architectural principle is supposed to earn before being trusted for a
   third.
4. Not every diagnostic event a brief names will already have a natural home
   in the existing codebase — distinguishing "this event's call site already
   exists and just needs a working ILogger" from "this event has no call site
   because the component that would emit it doesn't exist yet" is itself an
   important scoping judgement, not a detail to gloss over.
