# TempestOS Security Roadmap

## Purpose

The prioritised, sequenced list of future security work identified by
`Platform Security Review v0.5.0.md`, each item tied to the specific
`Threat Model.md` assumption that first makes it necessary. Nothing here
is scheduled against a date — TempestOS's engineering discipline
deliberately avoids building security machinery ahead of a real,
demonstrated need (`Security Principles.md`, Principle 7). Instead, each
item names its own **trigger**: the point at which it must be designed
*before*, not after, the corresponding capability ships.

## How to Use This Roadmap

When a future Work Package proposes implementing one of the "eventually"
capabilities in `Threat Model.md` (authentication, licensing, plugins for
third parties, cloud sync, an API, multi-user support), check this
roadmap first. If an item below names that capability as its trigger,
the corresponding design work is a prerequisite for that Work Package,
not an afterthought to be added once the capability already exists.

## Roadmap Items

### 1. Plugin isolation boundary — trigger: third-party plugins (assumption 7)

**Finding:** `SEC-01` / `Technical Debt Register.md` TD-09. A loaded
plugin currently has identical trust and DI-container access to a
first-party module.

**Before third-party plugins ship**, a dedicated Architecture Work
Package should evaluate and decide among: a separate
`AssemblyLoadContext` per plugin; a manifest-declared, enforced
capability/permission scope (e.g., "this plugin may publish events of
type X, may not resolve type Y"); code-signing verification before load;
or some combination. This decision should produce its own ADR with a
genuinely considered rejected alternative, not a quiet code change.

**Resolved at the architecture level — `WP 13.0A`.** The trigger fired
(the Product Owner's confirmed third-party plugin commitment) and the
dedicated Architecture Work Package this item names ran directly:
`ADR-0110` decides the combination — capability-scoped enforcement plus
code-signing establishes the trust tier the capability scope is gated
by — evaluating and rejecting `AssemblyLoadContext` alone (not a
security boundary in modern .NET), process separation alone
(disproportionate to the disclosed threat), and each half of the
combination alone (`RD-0053`–`RD-0056`). See `Plugin Trust & Isolation
Architecture.md`. **Not yet implemented** — `WP 13.0B` is the
still-open, separately-scoped implementation task this item's own
"before third-party plugins ship" condition still gates.

### 2. Navigation ownership — trigger: paired with item 1, or any multi-author navigation scenario

**Finding:** `NAV-1` / `Technical Debt Register.md` TD-10.
`NavigationService.Unregister` has no ownership check.

Once item 1's isolation model exists, extend the same capability/identity
concept to `NavigationService.Unregister` (and any future shared registry
with the same shape) so a component can only remove what it registered.

**Resolved at the architecture level — `WP 13.0A`**, alongside item 1 as
this item's own text anticipated. `ADR-0111` extends item 1's own
capability/identity concept (a new `ICurrentComponentAccessor`) to
`NavigationService.Unregister`: the registering component's identity is
captured out-of-band at `Register`, and `Unregister` rejects a mismatch
via a reserved, First-Party-only override permission. See `Plugin Trust
& Isolation Architecture.md`. **Not yet implemented** — `WP 13.0B`.

### 3. Secrets-redaction logging convention — trigger: any credential, token, or connection string entering the platform (assumptions 5, 8)

**Finding:** `SEC-02`. No mechanism exists to mark a logged value as
sensitive.

Before authentication (assumption 5) or cloud synchronisation
(assumption 8) introduces the platform's first real secret, design a
redaction convention — a marker attribute, a wrapper type, or an
`ILogSink`-level filter — and require its use from that point forward.
Retrofitting this after secrets already exist in the codebase risks
missing a call site that logs one in plaintext before the convention is
adopted everywhere.

### 4. Project-data security design — trigger: reviving or replacing the bootstrap-era project-data subsystem (assumptions 1, 2, 3)

**Finding:** `FS-1`. `JsonProjectRepository`/`ProjectModel` already model
classification, export-control, and customer data with no encryption,
access control, or audit trail — currently dead code.

Whichever Work Package next builds real, persistent project-data storage
must design encryption at rest, access control (tied to item 5's
multi-user decision if concurrent), and audit logging for
classified/export-controlled fields as part of that design, not as a
follow-up. `FS-2` (the hard-coded `C:\Tempest` workspace root) should be
resolved as part of the same effort, since both live in the same
currently-dormant subsystem.

### 5. Multi-user / tenant isolation architecture decision — trigger: assumption 4 (multi-user support)

**Finding:** `FR-1`. The DI container has no Scoped lifetime; every
platform service is a single, process-wide singleton.

Before multi-user support is implemented, deliberately decide — with its
own ADR — whether isolation is achieved via separate OS processes per
user (no DI change required) or via a genuine Scoped lifetime and
per-tenant isolation model (a DI redesign). Do not let this decision be
made implicitly by whichever Work Package happens to touch it first.

### 6. Authentication and authorisation design — trigger: assumption 5

No readiness work exists yet, because no authentication concept exists
anywhere in the codebase to review. When this capability is first
proposed, it should receive its own architecture Work Package (design
phase, then implementation phase, per this project's own established
pattern) with an explicit threat model addendum, not be folded into an
unrelated feature Work Package.

### 7. API and networking exposure — trigger: assumption 9 (and, indirectly, assumption 8)

No network-facing surface exists yet. When one is first proposed
(a local API, a remote API, or a cloud-sync protocol), it should be
threat-modelled on its own terms — authentication, transport security,
input validation at the network boundary, and rate-limiting/DoS
considerations all become relevant the moment a socket is opened that
was not open before.

### 8. Licensing — trigger: assumption 6

No licensing concept exists in the codebase. Deferred until a concrete
design exists to review; noted here only so it is not forgotten when
that design work begins.

### 9. Offline synchronisation and mobile devices — trigger: assumption 8/9's eventual extension to non-desktop clients

No readiness work exists yet, and none is recommended until a concrete
design exists — these capabilities are furthest from anything in the
current codebase, and speculative design against them now would violate
`Security Principles.md` Principle 7.

### 10. Command and Navigation Id ownership/priority model — trigger: paired with item 1 (real third-party plugin support)

**Finding:** `CMD-1` / `Technical Debt Register.md` TD-11, surfaced by
`WP 5.1A`'s own Security Review (`docs/architecture/Command Framework
Architecture.md`). "First registration wins" — the duplicate-rejection
rule both `NavigationService.Register` (since `WP 5.0B`) and the newly-
designed Command Framework (`WP 5.1A`) use — rejects a *later* duplicate
but does not establish that the *first* registrant was the well-known
Id's intended owner. Because `ModuleLifecycleManager` initialises
modules in ascending-Id order, a plugin-loaded module whose own Id sorts
earlier than a first-party module's can legitimately claim a well-known
command or navigation Id before its real owner ever registers —
entirely within each registry's own stated rules.

Before third-party plugins are supported in practice (the same trigger
as item 1, since this finding requires a plugin already loaded with full
process trust to matter), design and implement an ownership/priority/
reservation model for command and navigation Ids — for example,
reserving a namespace prefix for first-party Ids, or giving a
first-party registration explicit priority over a plugin-sourced one
regardless of initialisation order. This should be designed alongside
item 1's own isolation-boundary work, not as a separate, later effort,
since both share the same root precondition and the same future
Architecture Work Package is the natural place to resolve them together.

**Resolved at the architecture level — `WP 13.0A`**, designed alongside
items 1 and 2 exactly as this item recommended. `ADR-0111` chose
trust-tier priority comparison over an Id-namespace-prefix reservation
(`RD-0059` — no such convention exists in the codebase today; retrofitting
one would touch every existing registration call site for a purely
cosmetic change): first registration wins only among registrants of the
same trust tier; a higher tier always evicts and replaces a lower one,
regardless of order, logged loudly, never silently. See `Plugin Trust &
Isolation Architecture.md`. **Not yet implemented** — `WP 13.0B`.

## Explicit Non-Recommendations

This roadmap deliberately does **not** recommend, at this time:

- Building a plugin sandbox, capability model, or code-signing scheme
  today, with no third-party plugin author to defend against yet.
- Building a secrets-redaction convention today, with no secret in the
  codebase to redact.
- Building encryption-at-rest or access control for project data that is
  not currently stored anywhere reachable.
- Building a Scoped DI lifetime for a multi-user capability that does not
  exist.

Each of these becomes the right thing to build only once its trigger,
above, is a real, scheduled piece of work — not before.

## Related Documents

`Threat Model.md`; `Security Principles.md`; `Platform Security Review
v0.5.0.md`; `Technical Debt Register.md` (TD-09, TD-10).
