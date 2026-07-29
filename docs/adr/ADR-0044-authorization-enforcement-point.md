# ADR-0044: `IPermissionEvaluator` Is the Single Authorization Enforcement Point; `CurrentPrincipalAccessor` Is Ambient, Not Request-Scoped

## Status

Accepted — `WP 6.1` (Permissions & Identity), 2026-07-29.

## Context

`Technical Debt Register.md` carries three open items — `TD-09` (no
isolation boundary between a loaded plugin and a first-party module),
`TD-10` (`NavigationService.Unregister` performs no ownership check),
and `TD-11` (Command/Navigation registration-order squatting) — each
explicitly disclosed with the same revisit trigger: "the first Work
Package with a genuine reason to build an authorization concept." `WP
6.1` is that Work Package, and `docs/releases/v0.6.0/Required ADRs.md`
named this ADR as the intended vehicle for closing all three.

A second, unrelated question was left explicitly open by the
architecture package: `Platform Service Contracts.md`'s own Thread
Safety Expectations for `ICurrentPrincipalAccessor` stated it "likely
requires an `AsyncLocal<T>`-backed implementation, not a single mutable
field... a specific design point `WP 6.1`'s own architecture phase must
resolve explicitly." Implementing this surfaced a genuine correctness
problem with the tentative `AsyncLocal<T>` approach, described below.

## Decision, Part 1: The Enforcement Point

**`IPermissionEvaluator.HasPermission`/`RequirePermission` is
implemented exactly as `Public Interface Catalogue.md` specified — the
single, uniform authorization check every future consumer is expected to
call.** `RequirePermission` throws `PermissionDeniedException` for a
denied check; `HasPermission` is the non-throwing form for a caller that
needs to branch (for example, hiding a menu item) rather than fail.

**This Work Package does not itself retrofit an enforcement call into
`NavigationService.Unregister`, Command/Navigation registration, or
plugin loading.** The brief for this Work Package was "Implement: `WP
6.1` — Permissions & Identity" — building the Identity & Permissions
framework itself, not modifying three already-shipped `v0.5.0`/`v0.4.0`
platform services to call into it. Doing so without a separate,
explicit brief for each of those services would itself be exactly the
"do not redesign the architecture" and "do not change approved public
interfaces" boundary this Work Package's own instructions were careful
to draw — `NavigationService`, the Command Framework's registration
path, and plugin loading are each themselves approved, shipped
architecture from a prior release, and inserting a permission check into
any of them is a change to *their* behaviour, not this Work Package's own
scope.

**Consequence, stated plainly: `TD-09`, `TD-10`, and `TD-11` remain
open after this Work Package.** What changes is that all three are now
*resolvable* — the single enforcement point (`ADR-0044`, this document)
exists for the first time, and a future, explicitly-scoped Work Package
can close each by adding one `RequirePermission` call at the relevant
point (`NavigationService.Unregister`, the Command/Navigation
registration path, and the plugin loading boundary) without inventing a
new authorization mechanism first. This is recorded honestly in this
Work Package's own Technical Debt Assessment, not claimed as resolved
here.

## Decision, Part 2: `CurrentPrincipalAccessor` Is Ambient, Not `AsyncLocal<T>`-Scoped

**`CurrentPrincipalAccessor` is backed by a single, `lock`-protected
mutable field, not `AsyncLocal<T>`.** This is a deliberate departure from
`Platform Service Contracts.md`'s own tentative language, made during
this Work Package's own implementation — which that same document named
as the point this specific question would be resolved, not a frozen
prior decision this overrides.

**Why the tentative `AsyncLocal<T>` approach does not fit this
release's actual need.** `AsyncLocal<T>` flows a value forward to child
async operations within the same logical call chain — it does not make
a value visible to a wholly separate, later caller that merely happens
to run after the chain that set it. This is exactly the right behaviour
for a genuinely concurrent, per-request scenario (a future REST API
request, `WP 6.3`) and exactly the wrong behaviour for this release's
own actual, simpler need: `IdentitySampleModule` establishes a principal
once, during Module Initialisation, and every later caller — a command
dispatched from an entirely separate call chain, a test, a future Shell
— is expected to see it. Verified directly:
`CurrentPrincipalAccessorTests.SetCurrent_FromOneAsyncCallChain_IsVisibleToAnUnrelatedLaterCallChain`
proves the ambient, `lock`-protected field behaves as required;
implementing the same test against a naive `AsyncLocal<T>`-backed
prototype during this Work Package's own development failed exactly as
this reasoning predicts, confirming the concern was genuine, not
theoretical.

**This is not a change to any approved public interface.**
`ICurrentPrincipalAccessor.Current` remains exactly `IPrincipal? Current
{ get; }` as drafted — only the concrete class's internal storage
mechanism differs from the architecture package's own tentative
suggestion, which that document itself flagged as unresolved.

**Revisit trigger for `WP 6.3` (REST API):** once concurrent, per-request
principals become a real, demonstrated need — multiple simultaneous
HTTP requests, each potentially authenticated as a different principal
— `CurrentPrincipalAccessor` should be revisited, either becoming
`AsyncLocal<T>`-backed at that point or (more likely, given the finding
above) the REST API introducing its own request-scoped accessor layered
on top of this one, rather than this release inventing per-request
isolation before any concurrent-request scenario exists to test it
against.

## Consequences

**Positive:**

- `TD-09`/`TD-10`/`TD-11` are, for the first time, genuinely resolvable
  by a small, targeted follow-on change rather than requiring their own
  authorization concept to be invented from scratch.
- `CurrentPrincipalAccessor`'s ambient behaviour matches this release's
  actual, local-only, single-process deployment model exactly — a
  principal established once is visible everywhere, with no surprising
  "why did my later call not see the principal I just established"
  failure mode.
- The `AsyncLocal<T>` finding is disclosed explicitly, with a concrete
  regression test proving it, rather than silently adopting the
  architecture package's own tentative suggestion and discovering the
  problem later during `WP 6.3`.

**Negative:**

- `TD-09`/`TD-10`/`TD-11` remaining open, even after the Work Package
  explicitly positioned to close them lands, is a real, disclosed
  outcome some readers may find surprising — mitigated by stating it
  plainly here and in the Technical Debt Assessment, rather than
  overclaiming resolution.
- The ambient `CurrentPrincipalAccessor` will need real reconsideration
  once `WP 6.3` introduces genuine request concurrency — deferred, not
  avoided; a real design cost this Work Package pushes to a future,
  better-informed Work Package rather than paying speculatively now.

## Alternatives Considered

**Retrofitting `RequirePermission` calls into `NavigationService`,
Command/Navigation registration, and plugin loading as part of this
Work Package.** Rejected — none of those three was named in this Work
Package's own brief, each is approved, shipped architecture from a prior
release, and changing any of their behaviour without an explicit,
scoped instruction to do so would itself violate the "do not redesign
the architecture" boundary this Work Package operates under.

**Implementing `CurrentPrincipalAccessor` with `AsyncLocal<T>` as
`Platform Service Contracts.md` tentatively suggested.** Rejected after
direct verification (see the failing prototype test described above) —
it does not satisfy this release's own actual requirement (a single
ambient principal visible platform-wide) and would have made the
sample module's own end-to-end demonstration impossible without an
artificial workaround.

**A hybrid accessor** (an `AsyncLocal<T>` scoped value falling back to
an ambient default). Rejected for this release as unnecessary complexity
solving a concurrency problem — per-request isolation — that does not
exist yet; revisit only when `WP 6.3` introduces a genuine concurrent
caller.

## Related Documents

`docs/releases/v0.6.0/Required ADRs.md` (this decision's own anticipated
form); `Platform Service Contracts.md` (the tentative `AsyncLocal<T>`
language this ADR departs from, with reasoning); `ADR-0043` (Identity
Model Scope, decided alongside this one);
`docs/governance/Quality/Technical Debt Register.md` (`TD-09`, `TD-10`,
`TD-11`); `docs/security/Platform Security Review v0.5.0.md` (Findings
SEC-01, NAV-1); `docs/architecture/Command Framework Architecture.md`
(Finding CMD-1); this Work Package's own Technical Debt Assessment and
Lessons Learned.
