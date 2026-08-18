# ADR-0110: Plugin Isolation Boundary Is Capability-Scoped Enforcement, Not `AssemblyLoadContext` or Process Separation

## Status

Accepted — `WP 13.0A` (Plugin & Registration Trust Isolation
Architecture), 2026-08-13. Resolves `Security Roadmap.md` item 1, the
central decision that roadmap item names as a prerequisite before
third-party plugins ship.

## Context

`Security Roadmap.md` item 1 named three candidate mechanisms to
evaluate, without deciding among them: a separate `AssemblyLoadContext`
(ALC) per plugin; a manifest-declared, enforced capability/permission
scope; code-signing verification before load; or some combination. The
Product Owner's confirmed commitment to third-party plugins
(`docs/releases/v0.13.0/WorkPackages.md`) is this decision's trigger,
per `Security Principles.md` Principle 7.

Today, `PluginAssemblyLoader.LoadPlugins` loads a plugin's declared
assembly via `Assembly.LoadFrom` into the process's single, default
`AssemblyLoadContext`. `ADR-0026`'s entire design depends on
`AppDomain.CurrentDomain.GetAssemblies()` seeing every loaded assembly,
regardless of which context loaded it — a fact that remains true whether
or not a plugin gets its own ALC, since .NET Core's single-AppDomain,
multi-ALC model still surfaces every loaded assembly through that same
API.

## Decision

**The isolation boundary is capability-scoped, in-process enforcement —
manifest-declared, tier-gated permission grants, checked through
`IPermissionEvaluator` at each sensitive call site, plus a
construction-time reflection conformance check at Plugin Loading — layered
on top of the signing decision `ADR-0112` makes. No separate
`AssemblyLoadContext` per plugin. No process separation.**

**This does not enable per-plugin unload.** A loaded plugin remains
loaded for the life of the process run, exactly as `Plugin Manifest
Architecture.md`'s existing, disclosed Risk already states, and exactly
as `ADR-0015` establishes for the Host itself. Any future decision to
adopt a collectible `AssemblyLoadContext` for unload/hot-reload is
separate from, and not unlocked by, this decision.

**Why capability scoping, not ALC.** `AssemblyLoadContext` is not a
security boundary in modern .NET — Code Access Security and
AppDomain-based sandboxing were removed entirely from .NET Core. An ALC
governs assembly *identity and unload*, not *privilege*: a plugin loaded
into its own ALC still runs with the process's full OS privileges, can
still call any public API of any type loaded anywhere in the process
(the same cross-ALC `AppDomain.CurrentDomain.GetAssemblies()` visibility
`ADR-0026` already depends on applies identically to types, not only
assemblies), and can still hold and use any DI-resolved service reference
it was ever given. Adopting an ALC here would add real, non-trivial
complexity (collectible-context lifetime management, type-identity-
across-context hazards) while closing none of `TD-09`'s actual complaint
— a plugin's unrestricted trust once loaded. See Alternatives Considered.

**Why not process separation.** Process separation, with IPC to the
host, is the only mechanism in modern .NET that provides a genuine
privilege boundary. It is disproportionate to the actual, disclosed
threat this Work Package defends against — a Product Owner commitment to
vetted, signed, commercial third-party plugins, not an open marketplace
accepting anonymous, unvetted, actively adversarial publishers. See
Alternatives Considered for the full reasoning and this decision's own
revisit trigger.

## Consequences

**Positive:**

- Reuses `ADR-0044`'s existing enforcement point (`IPermissionEvaluator`)
  almost entirely unchanged — no new authorization mechanism invented.
- Requires no new runtime dependency, no collectible-context lifetime
  management, no IPC/marshalling layer.
- Proportionate to the actual, named threat (`Threat Model.md` assumption
  7, as scoped by this Work Package's own commissioning) rather than
  built against a hypothetical, more adversarial actor no named trigger
  yet requires defending against.
- ~~Leaves Module Discovery, Registration, and Lifecycle completely
  unchanged — `Plugin Manifest Architecture.md`'s own load-bearing
  "Unchanged" claim survives this decision intact, since the
  construction-time conformance check (`ADR-0111`) runs entirely within
  the existing Plugin Loading phase boundary.~~

  **Corrected, `WP 13.12.2` Release Documentation Closure.** This bullet
  is no longer true at the letter, and has not been since `WP 13.9.6`.
  Module Discovery, Module Registration, Hosted Service Registration, and
  Module/Hosted Service Lifecycle each did change: `ReflectionFrameworkDiscoveryService`
  gained an optional `Func<Type, bool>? isTypeExcluded` predicate
  (`WP 13.9.6`), `ModuleLifecycleManager` and `HostedServiceManager` each
  gained an optional `Func<string, IDisposable?>`/`Func<Type, IDisposable?>`
  `componentScopeProvider` hook (`WP 13.2A`, extended `WP 13.10B`), and
  `TempestHost` gained trust-denial filters at both Registration points
  (`WP 13.9.4`). The change was disclosed at the time — `ADR-0111`'s own
  "Corrected, `WP 13.9.6`" note states it — but this bullet was never
  amended to match, so this ADR carried a false claim for the remainder of
  the release.
  **This decision's own substance is unaffected.** What `ADR-0110`
  actually decided — capability-scoped, in-process enforcement, not a
  separate `AssemblyLoadContext` and not process separation — is honoured
  by the shipped code, verified by direct source read (`grep` for
  `AssemblyLoadContext` across `src/` returns zero hits). The components
  above also remain plugin-**unaware at the type-reference level**, which
  is the property this bullet was protecting: every hook added is a
  generic `Func<>`, and `Tempest.Core.Modules` and
  `Tempest.Core.BackgroundServices` contain no code reference to
  `Tempest.Core.Plugins`. The accurate statement is therefore "leaves
  Module Discovery, Registration, and Lifecycle **plugin-unaware**", not
  "completely unchanged". Struck rather than deleted, preserving the
  audit trail.

**Negative:**

- **Not a hard sandbox, disclosed plainly.** A sufficiently determined
  plugin could, in principle, use reflection to reach past a
  `RequirePermission` call site. This decision closes the ordinary,
  cooperative API surface — the actual, disclosed shape of `TD-09`,
  `TD-10`, `TD-11`, and `CMD-1` — and does not claim to defend against an
  actively adversarial assembly willing to bypass its own declared trust.
  See `Plugin Trust & Isolation Architecture.md`, Risks.
- **No per-plugin unload.** A future Work Package wanting real unload
  must make its own, separate decision to adopt a collectible ALC —
  this decision neither builds nor blocks that, but does mean it is not
  a free side effect of this one.
- A future, genuinely more adversarial third-party plugin scenario (an
  open marketplace, unvetted publishers) would require revisiting this
  decision toward process separation — a substantially larger redesign
  of DI resolution, event dispatch, and module construction across an
  IPC boundary. Deferred, not avoided; disclosed here rather than
  discovered later.

## Alternatives Considered

**A separate `AssemblyLoadContext` per plugin.** Seriously considered —
the Security Roadmap's own first-named option. Rejected as the *trust*
mechanism specifically, for the concrete reason above: an ALC boundary is
not enforced by the CLR for privilege in .NET Core, only for assembly
identity/unload. Its one genuine benefit — enabling unload — is an
explicit Non-Goal of both this decision and the already-implemented
Plugin Manifest Architecture (`Plugin Manifest Architecture.md`,
Non-Goals: "dynamic unloading"). Building it now, for a benefit this
release does not use, would be exactly the "security theatre ahead of a
real need" `Security Principles.md` Principle 7 warns against.
**Revisit trigger:** a real, demonstrated need for in-process plugin
unload or update-without-restart — a separate lifecycle decision, not a
security one, mirroring `ADR-0015`'s own "layer a supervisor above
`TempestHost`" reasoning applied one level down to plugins.

**Separate OS process per plugin, with IPC to the host.** Seriously
considered — the only mechanism available in modern .NET providing a
genuine privilege boundary. Rejected for this Work Package: disproportionate
to the actual, disclosed threat (vetted, signed, commercial plugins, not
an open, unvetted marketplace); would require redesigning DI resolution,
event dispatch, and every module constructor-injection point across an
IPC boundary — an order of magnitude larger than this Work Package's own
brief, and not named as a current trigger by `Security Roadmap.md`.
**Revisit trigger:** TempestOS is asked to run genuinely adversarial,
unvetted third-party code (an open marketplace with no publisher
accountability) rather than signed, accountable commercial plugins.

**Code-signing alone, with no capability scoping.** Seriously
considered — cheapest option, and it solves a real, distinct problem
(authenticity, tamper-detection). Rejected as a *complete* answer: it
establishes *who* published a plugin but says nothing about *what* it
may then do once loaded — a properly signed, fully accountable plugin
would still receive `TD-09`'s exact, unrestricted DI-container trust.

**A manifest-declared capability/permission scope alone, with no
signing.** Seriously considered as the minimal option. Rejected: without
a signature backing it, a capability declaration is an unverifiable,
self-asserted claim — nothing prevents a malicious manifest from
declaring every capability it wants. `ADR-0112`'s signing decision is
what gives a Verified-Signed plugin's capability request real
accountability, and is why Unsigned-Local's ceiling is clamped low
regardless of what it requests.

## Related Documents

`Security Roadmap.md` item 1; `Threat Model.md` Scenario 1; `Security
Principles.md` Principles 2, 7; `Plugin Trust & Isolation Architecture.md`
(this decision's full design context); `ADR-0111` (the capability model
built on top of this decision); `ADR-0112` (the signing decision this
one depends on); `ADR-0044`; `ADR-0026`; `ADR-0015`; `Plugin Manifest
Architecture.md`.
