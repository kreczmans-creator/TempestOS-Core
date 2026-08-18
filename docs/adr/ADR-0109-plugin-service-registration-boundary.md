# ADR-0109: A Plugin Registers Services Exactly Like Any Module — No New DI Container Capability, No `IServiceCollection` Access

## Status

Accepted — `v0.13.0`, `WP 13.0A` (Plugin Platform Architecture), 2026-08-13.
Architecture only; no code changes accompany this decision — implementation
is `WP 13.0B`'s own, separately-scoped task.

## Context

The brief for this work package asked directly: can a plugin register
services into the platform's DI container, and if so, through what
mechanism — a restricted, plugin-scoped registration API, or full
`IServiceCollection` access?

Three already-decided facts bound the answer tightly, confirmed by direct
re-reading of already-Accepted architecture, not assumed:

- **`Host Lifecycle.md`'s phase ordering is frozen, approved architecture
  this work package does not reopen.** Plugin Loading (Phase 3.2) happens
  *before* Platform Services Registered (Phase 6) and *before*
  Dependency Injection Built (Phase 7) — a plugin's assembly is already
  loaded into the process well before the `ServiceCollection` that will
  become `TempestServiceProvider` is even populated, let alone built.
- **`RD-0043`, decided during `WP 5.2`, confirmed by direct inspection of
  `TempestServiceProvider`'s own construction: `IServiceCollection.AddInstance`/
  `Singleton` have no effect once the container has already been built.**
  Registration must happen during Phase 6 or not at all — there is no
  later point in `Host Lifecycle.md` where a new DI registration can take
  effect, for a plugin or for anything else.
- **No module — first-party or plugin-sourced — has ever had a way to
  register a service into the DI container.** Phase 6 is entirely
  Host-driven code (`TempestHost` itself calling `AddInstance`/`Singleton`/
  `AddDiscoveredModules`); a module's own code does not run at all until
  Module Initialisation (Phase 8), two phases later. `RD-0040` already
  confirmed `TempestServiceProvider` supports neither open-generic nor
  keyed registration, and that no module-level mechanism exists to add a
  registration after the container is frozen — decided once, for the
  Command Framework, and equally true here without needing to be
  re-decided.

The question this ADR actually answers, once these constraints are made
explicit, is narrower than it first appears: not "should plugins get a
weaker or stronger DI surface than modules," but "should *any* module,
plugin-sourced or not, gain a DI registration surface it does not have
today, as part of this work package" — and, separately, "what should a
plugin use instead, today, to make a capability available to other
modules."

## Decision

**No new DI container capability is introduced, and no plugin — nor any
module, plugin-sourced or first-party — gains any form of
`IServiceCollection` access.** A plugin's own `IModule` continues to
receive constructor-injected services exactly as any discovered module
does (`ADR-0027`), and continues to make its own capabilities available to
other modules through exactly the same **existing, imperative, DI-public
platform-service registration surfaces** every module already has access
to today, called during its own Module Initialisation lifecycle step —
not a new plugin-specific mechanism:

| A plugin wants to… | Uses (unchanged, already DI-public) |
|---|---|
| Announce an event other modules may react to | `IEventBus.Subscribe`/`PublishAsync` (ADR-0028) |
| Contribute a navigable item | `INavigationProvider.Register` (ADR-0032) |
| Contribute an invocable command | `ICommandRegistry.Register` (ADR-0037) |
| Expose its own read-only status for observation | The same pattern `IDiagnosticsProvider` already establishes for the platform's own state (`ADR-0039`) — a plugin's module publishes a value another module can read via one of the surfaces above, never a new container-level registration |

**This is not a plugin-specific restriction — it is the same, unmodified
boundary every module has always operated inside.** Extending it
selectively to plugins alone would be backwards (a new capability, not a
new restriction); extending it to all modules, plugin-sourced and
first-party alike, is a genuine DI container redesign (open-generic or
keyed registration, or a post-construction registration mechanism) —
exactly the category of change `RD-0040`/`RD-0043` already found this
platform's container does not support and did not build a workaround for,
each time reusing an existing, already-DI-public imperative surface
instead of inventing a container-level one. This ADR reaches the identical
conclusion a third time, for plugins specifically, rather than reopening
the container question on the strength of "but this time it's a plugin."

## Consequences

**Positive:**

- Zero change to `TempestServiceProvider`, `ServiceCollection`, or
  `Host Lifecycle.md`'s phase table — this decision is fully realised by
  documentation and convention, not by any new code surface.
- A plugin author's mental model is exactly a module author's mental
  model — "how do I make my module's capability available to others" has
  one answer, regardless of whether the module arrived via Discovery or
  via a plugin, closing a question that could otherwise have grown into a
  second, parallel registration convention.
- Directly forecloses a plausible-sounding future ask ("just give plugins
  `IServiceCollection` access") with a reasoned, citable answer, rather
  than leaving it to be reinvented and re-argued the first time someone
  proposes it.

**Negative:**

- A plugin cannot contribute a genuinely new, container-resolvable service
  type that a first-party module could constructor-inject by interface —
  it can only participate in the existing publish/subscribe- and
  registry-shaped surfaces named above. This is an accepted limitation,
  not a gap unique to plugins: no first-party module can do this either,
  today.
- If a future, unrelated work package does extend `TempestServiceProvider`
  with open-generic or keyed registration (per `RD-0040`'s own revisit
  trigger — "a real, demonstrated need... arising from a requirement
  broader than [any single capability] alone"), this ADR's own boundary
  should be revisited alongside it, since a genuinely new DI capability
  would change the shape of what "no new capability" means here. Not
  expected to be triggered by plugins alone.

## Alternatives Considered

**A restricted, plugin-scoped `IServiceCollection`-like registration API**,
distinct from the full container but still container-shaped (a plugin
calls `RegisterService<TInterface, TImplementation>()` on some
plugin-scoped object, later merged into the real container). Rejected:
this would still require *some* point in `Host Lifecycle.md` where a
plugin's own code executes before Phase 6/7 — but plugin code does not run
at all until Module Initialisation (Phase 8), one phase *after* the
container is already built. Building such an API would either require
running arbitrary plugin code far earlier than any module's code runs
today (a new, first-class trust and ordering question squarely inside the
sibling Trust & Isolation Architecture's own scope, not a DI convenience
to introduce as a side effect) or silently do nothing once called at
Phase 8, which would be strictly worse than not offering the API at all.

**Full, unrestricted `IServiceCollection` access**, handed to a plugin at
whatever point its code first runs. Rejected outright — beyond the timing
problem above, this would let a plugin register services under types the
Host or other modules also depend on, with no conflict detection of any
kind (`TempestServiceProvider` has none), a materially larger trust
surface than this work package's own scope extends to, and squarely a
question for the sibling Trust & Isolation Architecture, not this
document, to ever authorise.

**Extending `TempestServiceProvider` with open-generic/keyed registration
now, so a plugin-contributed service could be resolved like any other.**
Rejected for the same reason `RD-0040` already rejected it for the Command
Framework: a genuine container redesign, out of this work package's own
scope, for a need no real plugin has yet demonstrated. `src/Plugins/`
remains empty (`Plugin Register.md`) at the time of this decision.

## Related Documents

`Plugin Platform Architecture.md` (this decision's own full context);
`ADR-0005` (custom, deliberately minimal DI container); `ADR-0027`
(constructor injection for discovered modules, unaffected — a plugin's
module is injected into exactly as before); `ADR-0017` (Discovery/
Registration/Lifecycle remain Host-owned — the same reasoning this ADR
applies one layer over, to DI registration authority specifically);
`docs/architecture/Rejected Designs.md` (RD-0040, RD-0043, both directly
reused, not re-argued).
