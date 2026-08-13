# ADR-0111: Plugin Trust Capability Model Extends `IPermissionEvaluator` via a Component Principal and Trust-Ordered Registration

## Status

Accepted — `WP 13.0A` (Plugin & Registration Trust Isolation
Architecture), 2026-08-13. Makes `TD-09`, `TD-10`, `TD-11`, and `CMD-1`
genuinely resolvable by a targeted retrofit, exactly as `ADR-0044`
anticipated — this decision does not itself perform that retrofit;
`WP 13.0B` does.

## Context

`ADR-0044` built `IPermissionEvaluator.HasPermission`/`RequirePermission`
as the single, uniform authorization enforcement point, explicitly
declining to retrofit it into `NavigationService.Unregister`, Command/
Navigation registration, or plugin loading — naming that retrofit as
future, explicitly-scoped work. `ADR-0110` decided the isolation
boundary this retrofit enforces through is capability scoping, not
`AssemblyLoadContext` or process separation. This ADR designs the
concrete capability model that boundary is made of.

Two gaps stood between `ADR-0044`'s existing mechanism and a working
retrofit. First, `IPermissionEvaluator` checks an `IPrincipal` — but
every existing `IPrincipal` (`ADR-0043`) represents a **user**, and
`ICurrentPrincipalAccessor` is deliberately ambient and *not*
call-chain-scoped, exactly the wrong shape for "which loaded plugin's own
code is currently executing" (which must revert the instant control
returns to the plugin's caller, and nest correctly across cross-component
calls). Second, "first registration wins" (`NavigationService.Register`,
`CommandHandlerTable.Register`/`CommandRegistry.RegisterDescriptor`) has
no ownership concept at all — `TD-10` and `TD-11`/`CMD-1` are both direct
consequences.

## Decision

### A new, additive identity axis: the component principal

`ICurrentComponentAccessor` (`IPrincipal? Current { get; }`, mirroring
`ICurrentPrincipalAccessor`'s exact shape) is introduced as a second,
independent accessor, backed by an `AsyncLocal<T>`-flowed stack with a
`IDisposable BeginScope(IPrincipal componentPrincipal)` token. It answers
"which loaded component's own code is currently executing," not "which
user is acting" — a genuinely different question `ICurrentPrincipalAccessor`
was never designed to answer and should not be overloaded to answer.

This is not a revision of `CurrentPrincipalAccessor` — `ADR-0044`'s own
ambient, non-scoped design remains exactly right for its own question
(a human user, established once, visible to any later, unrelated
caller). It is the additional, layered accessor `ADR-0044` itself
anticipated: "more likely… the REST API introducing its own
request-scoped accessor layered on top of this one." This decision
exercises that exact precedent for a different, non-REST caller.

The Host pushes a plugin's component principal onto this stack around
every point it re-enters plugin-owned code: a module's own
`InitialiseAsync`/`StartAsync`/`StopAsync`/`DisposeAsync` calls, an event
subscriber invocation, and a command handler invocation — popping on
return via the scope token, so nested cross-component calls resolve
correctly and control returning to first-party code is never mistakenly
attributed to a plugin.

A plugin's component principal is constructed once, at Plugin Loading,
from its manifest `Id` (`IIdentity.Id`) and its final, tier-clamped,
eligibility-checked capability grant set (`IPrincipal.Permissions`) —
reusing `IPrincipal`/`IIdentity` exactly as `ADR-0043` already defines
them, with no new principal type.

### Capability keys reuse `Permission` directly

No new permission type is introduced. A plugin capability **is** a
`Permission`, namespaced `plugin.*`: `plugin.navigation.register`,
`plugin.commands.register`, `plugin.di.register`,
`plugin.events.publish:<FullTypeName>`,
`plugin.services.resolve:<FullTypeName>`. See `Plugin Trust & Isolation
Architecture.md` for the full key table and the manifest field shape
(`RequestedCapabilities` — the sibling Plugin Architecture workstream's
own already-reserved flat `IReadOnlyList<string>` manifest field,
`Plugin Platform Architecture.md`) that declares them.

### Two enforcement mechanisms

**Static, at Plugin Loading (Phase 3.2), entirely Host-owned:** every
requested capability key is checked against the plugin's assigned trust
tier's ceiling (`ADR-0112`); every constructor parameter type on a
discovered `IModule` implementer in the plugin's own assembly — reflected
over independently, before handoff to Module Discovery — is checked
against a fixed always-allowed baseline (`ILogger`, `IConfigurationProvider`,
`IDiagnosticsProvider`) plus the plugin's own granted
`plugin.services.resolve:*` declarations. Either failure isolates the
whole plugin before Module Discovery ever sees it — no change to Module
Discovery, Registration, or Lifecycle. This is the concrete mechanism
that closes the "resolve a given service" half of `TD-09` without a DI
resolution interceptor.

**Dynamic, at each call site:** `NavigationService.Register`/`Unregister`,
the Command Framework's registration path, and `IEventBus.PublishAsync`
each gain one `IPermissionEvaluator.RequirePermission(componentPrincipal,
permission)` call. The check is skipped — not merely satisfied — when the
ambient component principal is `null` or First-Party, so every actor that
exists today observes zero behavioural change.

### Trust-ordered registration, replacing unconditional "first wins"

Applied identically to `NavigationService.Register`/`Unregister` and the
Command Framework's registration path (`CommandHandlerTable.Register`,
`CommandRegistry.RegisterDescriptor`) — "any future shared registry with
the same shape," per `Security Roadmap.md` item 2.

**Ownership (`TD-10`):** each registry captures the registering
component principal alongside the item, out-of-band — no change to
`NavigationItem`'s or `CommandDescriptor`'s own public shape.
`Unregister`/the equivalent removal path compares the caller's current
component principal against the stored owner; a mismatch requires a
reserved override permission (`navigation.unregister.any`/
`commands.unregister.any`), held only by First-Party by construction,
rather than succeeding unconditionally as today.

**Priority (`TD-11`/`CMD-1`):** first-registration-wins is preserved
*among registrants of the same trust tier* (unchanged for every
registrant that exists today — all First-Party); a **higher-trust-tier
registration always wins over a lower one, regardless of registration
order** — evicting and replacing a lower-tier registrant that claimed the
Id first, with a loud, always-logged "Id ownership override" event. This
directly answers the finding's own stated problem without inventing an
Id-namespace-prefix convention the codebase does not otherwise have.

**This is a real, acknowledged behavioural change to already-shipped,
already-Accepted architecture, not merely an additive extension of it.**
`ADR-0032` (Navigation is DI-public with imperative registration) ships
`NavigationService.Register` throwing `DuplicateNavigationItemException`
unconditionally on any duplicate Id, with no ownership or tier concept;
`ADR-0037` (Command registration model) is, if anything, more explicit —
it states directly that "no Unregister/Deregister is defined... a
registration, once accepted, persists for the Host's entire remaining
run," a decision this design's own `Unregister`/removal-path ownership
check and eviction-on-higher-tier behaviour both revise. Neither
`ADR-0032` nor `ADR-0037` is reversed at the level either originally
decided (a first-party module registering, unregistering, and being
rejected on duplicate Ids among its own peers behaves exactly as before,
confirmed above) — but the unconditional, tier-blind nature of both
original decisions is genuinely superseded for the specific case a
plugin now introduces: a second, lower-trust registrant contesting an
Id. This design does not formally mark `ADR-0032`/`ADR-0037` as
Superseded (per `Engineering Governance.md` §5, reserved for a decision
reversed at its own original scope, not one extended to a scope that did
not exist when it was made) — it extends both additively, the same
relationship `ADR-0096`/`ADR-0097` bear to the `WP 8.0B` Workspace
contracts they add Kind-keyed categories to — but the change is real
enough, and departs far enough from either ADR's own stated absolutes
("no Unregister/Deregister is defined," "unconditionally"), to warrant
citing both explicitly here rather than leaving the connection only in
`Security Roadmap.md` item 10's own narrative.

## Consequences

**Positive:**

- `TD-09` (the DI/resolve half), `TD-10`, `TD-11`, and `CMD-1` are all
  directly, concretely resolvable by `WP 13.0B` using exactly this
  design — no further architectural invention required.
- Zero behavioural change for any actor that exists today: every check
  is skipped for `null`/First-Party component principals, and every
  existing registrant is First-Party.
- Reuses `Permission`/`IPrincipal`/`IIdentity`/`IPermissionEvaluator`
  exactly as they exist — no new authorization type family.
- `AsyncLocal<T>` is used exactly where `ADR-0044` itself said it belongs
  (a genuine same-call-chain, nesting-sensitive scenario) — this decision
  does not reopen `ADR-0044`'s own, deliberately different, choice for
  `CurrentPrincipalAccessor`.

**Negative:**

- Introduces a second ambient identity concept alongside
  `ICurrentPrincipalAccessor` — a future contributor must understand
  *which* of the two axes ("who is the user" vs. "which component's code
  is running") a given check answers. Mitigated by this ADR's explicit
  naming and by mirroring `ICurrentPrincipalAccessor`'s own shape exactly,
  so the pattern is already familiar.
- The construction-time conformance check duplicates a narrow slice of
  Module Discovery's own type-scanning logic, run independently and
  earlier. A future Module Discovery change not mirrored here is a real,
  disclosed maintenance hazard — see `Plugin Trust & Isolation
  Architecture.md`, Risks.
- Does not intercept a plugin's own code calling `IServiceProvider.GetService`
  directly, only constructor-injected dependencies — a disclosed,
  deliberate scope limit, not an oversight (see Non-Goals,
  `Plugin Trust & Isolation Architecture.md`).

## Alternatives Considered

**Reusing `ICurrentPrincipalAccessor` directly for plugin/component
identity, rather than introducing a second accessor.** Seriously
considered — it would avoid a second ambient concept. Rejected: it would
conflate two genuinely different questions. `ICurrentPrincipalAccessor`
is deliberately non-call-chain-scoped (`ADR-0044`) so a user, once
established, remains visible to any later, unrelated caller — exactly
wrong for "which component's code is executing right now," which must
revert on return and nest correctly. Stuffing a plugin's identity into
the same ambient slot would also silently let a plugin's registration
inherit whatever user happens to be ambiently current (for example, an
administrator), granting it ownership/priority rights the plugin itself
was never granted — a real correctness defect, not merely an aesthetic
one.

**Backing the new component accessor with the same ambient,
`lock`-protected single-field pattern `ADR-0044` chose for
`CurrentPrincipalAccessor`, instead of `AsyncLocal<T>`.** Seriously
considered, for consistency with the existing precedent. Rejected: `ADR-0044`
itself explains exactly why `AsyncLocal<T>` was wrong *for its own
question* (a value must remain visible to a wholly separate, later,
unrelated caller) — and explains exactly why it would be right for a
different, genuinely call-chain-scoped question. Plugin/component
identity is that different question: enforcement always happens within
the same logical call chain that entered the plugin's code, and must
correctly revert when that chain returns. Using the ambient pattern here
would misattribute registrations made by code the plugin's own call
happened to invoke afterward, in an unrelated later chain, to the wrong
component.

**An Id-namespace-prefix reservation** (for example, reserving a
`tempest.*` prefix for first-party Ids), rather than trust-tier priority
comparison. Considered directly, as the Security Roadmap's own suggested
example. Rejected: no such naming convention exists across the
codebase's existing first-party Ids today: retrofitting one would touch
every existing registration call site for a purely cosmetic naming
change, for a problem trust-tier comparison already solves generally,
without requiring any existing Id to be renamed.

**A dedicated `navigation.register`/`commands.register` permission
required of every registrant, including First-Party**, rather than
exempting First-Party entirely. Considered, for uniformity. Rejected:
First-Party is, by this design's own definition, exactly as trusted as
the platform itself — requiring it to hold an explicit permission for an
operation it has always been able to perform unconditionally would add a
check with no possible denial outcome, pure overhead for no behavioural
guarantee, and risk a real regression if a future change ever failed to
grant it.

## Related Documents

`ADR-0044` (the enforcement point this decision extends); `ADR-0043`
(the `IPrincipal`/`IIdentity` shapes this decision reuses); `ADR-0110`
(the isolation-boundary decision this capability model implements);
`ADR-0112` (the signing decision that assigns the trust tier this
model's ceilings are checked against); `ADR-0032` (Navigation is
DI-public with imperative registration — the unconditional
duplicate-rejection behaviour this design's ownership/priority rule
additively revises for the plugin-contested case); `ADR-0037` (Command
registration model — its own "no Unregister/Deregister is defined"
absolute, similarly revised); `Plugin Trust & Isolation
Architecture.md`; `docs/governance/Quality/Technical Debt Register.md`
`TD-09`, `TD-10`, `TD-11`; `docs/architecture/Command Framework
Architecture.md` Finding `CMD-1`; `Security Roadmap.md` items 1, 2, 10.
