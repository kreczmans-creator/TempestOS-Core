# TempestOS v0.6.0 — Work Packages

## How to Read This Document

`v0.6.0` ("Platform Services") is the first release since the Runtime
Foundation (`v0.3.0`) and the Platform Foundation (`v0.4.0`) to add
genuinely new *domain-facing* capability rather than infrastructure the
platform needed to become usable (`v0.5.0`, "Developer Experience").
Every Work Package below builds *on* the stable foundation those three
releases established — Runtime Host, Dependency Injection, Module
Framework, Plugin Framework, the Event Bus, Background Services,
Navigation, the Command Framework, Diagnostics, Logging, Configuration,
and Versioning — not a redesign of any of it (`docs/releases/
FOUNDATION.md`).

No implementation has begun on any Work Package below. This document
records the agreed scope so design work has a fixed target, following
the same discipline every prior release's own `WorkPackages.md` was
held to: architecture precedes implementation for anything non-trivial
(`FOUNDATION.md` §1), Work Packages that turn out to need their own
architecture phase are split into `A`/`B` pairs once that becomes clear
(the `WP 5.0A`/`WP 5.0B`, `WP 5.0C`/`WP 5.0D`, and `WP 5.1A`/`WP 5.1B`
precedent) rather than assumed in advance, and every Work Package's own
Definition of Done includes an ADR where Engineering Governance §5's
criteria are met, a Rejected Designs entry where §10's are, Academy
documentation, and full governance register maintenance.

Three of the nine Work Packages below — Permissions & Identity, the REST
API, and Licensing — are named triggers in `docs/security/Security
Roadmap.md` (items 6, 7, and 8 respectively) for security design work
that must happen *before*, not after, the corresponding capability ships.
Each Work Package's own entry below cross-references the relevant
Threat Model assumption and Security Roadmap item explicitly, so that
design work does not rediscover a question this project has already
flagged.

## Status

**Not started.** `feature/v0.6.0-platform-services` was cut from `main`
at the `v0.5.0` tag. This document and its own companions
(`ReleaseNotes.md`, `Retrospective.md`) were prepared ahead of any code,
per this release's own opening instruction.

---

## WP 6.0 — Reporting Framework

**Status note.** Not started.

### Objective

Give the platform a way to produce structured, formatted output — a
report — from data the platform (or a module) already holds, without
requiring every module that needs one to invent its own rendering and
export mechanism.

### Scope

- A report definition model (what data a report needs, how it is laid
  out) and a rendering pipeline producing at least one concrete output
  format.
- Whether reporting is its own DI-public Platform Service (mirroring the
  Event Bus/Navigation/Command Framework/Diagnostics precedent) or a
  Module SDK-level convenience is this Work Package's own first
  question to answer, not assumed here.
- Relationship to `WP 6.7` (Export/Import): a report is one kind of
  exportable artifact; whether Reporting depends on, or is depended on
  by, Export/Import needs an explicit answer, mirroring `ADR-0022`'s own
  "decide orthogonality explicitly, in writing" precedent for Navigation
  and the Command Framework.

### Dependencies

The Module Framework, Dependency Injection, and (if reports are invoked
uniformly across UI/automation/AI callers, mirroring the Command
Framework's own justification) the Command Framework.

### Deliverables

A report definition contract; at least one concrete renderer; at least
one real, working report produced against real platform data (mirroring
the Sample Module's own "living reference" precedent, `WP 4.3`).

### Acceptance Criteria

A report can be defined, rendered, and produced from real platform data
without hand-rolled, one-off rendering code duplicated per report.

### Estimated Complexity

**M** — provisional; to be confirmed once this Work Package's own
architecture phase (if one proves necessary) settles the DI-public
question above.

### Risks

Scope creep into a full templating/layout engine, which this release
does not need — a single, well-chosen concrete output format is
sufficient to prove the model; more formats are additive later.

---

## WP 6.1 — Permissions & Identity

**Status note.** Not started.

### Objective

Introduce the platform's first concept of "who is doing this" and "are
they allowed to." This is the Work Package `docs/security/Security
Roadmap.md` item 6 names as a trigger requirement: authentication and
authorisation design must happen as its own dedicated architecture Work
Package, with an explicit threat-model addendum, *before* this capability
exists in any form — not folded into an unrelated feature Work Package,
and not designed after the fact.

### Scope

- Whether TempestOS's identity model is local-only, or anticipates a
  future networked/multi-user model (`Threat Model.md` assumption 4) —
  an explicit decision, not an implicit one inherited from whichever
  capability happens to need identity first.
- A permission/role model sufficient for `WP 6.3` (REST API) to gate
  endpoints and for `WP 6.5` (Audit) to attribute an action to an actor.
- Explicitly informed by `TD-09`/`TD-10`/`TD-11` (plugin isolation and
  Navigation/Command registration-order ownership) — a real identity
  model may be the natural place to finally resolve those three related,
  long-open debt items, rather than solving ownership twice.

### Dependencies

Should precede or land alongside `WP 6.3` (REST API), since a
network-facing surface without an authentication/authorisation model
would be a Threat Model violation on its own terms. Benefits from, but
does not strictly require, `WP 6.5` (Audit) — an audit trail is more
useful once actions can be attributed to a real identity.

### Deliverables

An architecture document; new ADR(s) covering the identity model, the
permission model, and the local-vs-networked decision; a Threat Model
addendum reflecting whichever of assumptions 4/5 this Work Package
actually resolves.

### Acceptance Criteria

A concrete decision exists, in writing, for what "identity" and
"permission" mean in this platform, before any endpoint or command is
gated by it.

### Estimated Complexity

**L** — the least architecturally grounded objective in this release,
mirroring Navigation's own `WP 5.0A` position in `v0.5.0`.

### Risks

The highest-risk Work Package in this release by a clear margin, for the
identical reason Navigation was in `v0.5.0`: no existing architectural
grounding to build from. Should very likely run as its own
architecture-then-implementation pair (`WP 6.1A`/`WP 6.1B`), decided
once real investigation begins, not assumed here.

---

## WP 6.2 — Notification Framework

**Status note.** Not started.

### Objective

Give the platform a uniform way to tell a user, a module, or an external
system that something happened — distinct from the Event Bus (an
internal, module-to-module publish/subscribe mechanism with no delivery
or presentation guarantee) and distinct from Logging (an append-only
diagnostic record, not a user-facing message).

### Scope

- The relationship to the existing Event Bus: a notification is likely
  *produced from* an event, not a replacement for one — this Work
  Package's own first question, mirroring the Command-Framework-vs-
  Event-Bus distinction `ADR-0037`/`RD-0039` already drew once for a
  structurally similar question.
- Delivery targets: at minimum, the Shell (a user-visible notification
  region); optionally, once `WP 6.3` exists, an external channel.
- Explicitly out of scope unless a real need emerges: email/SMS/push
  delivery infrastructure — this release's own scope is the platform
  mechanism, not every possible delivery channel.

### Dependencies

The Event Bus; the Shell (for in-app presentation). Benefits from, but
does not require, `WP 6.3` (REST API) for external delivery.

### Deliverables

A notification contract and dispatch mechanism; at least one real,
working delivery target (the Shell).

### Acceptance Criteria

A module can raise a notification and have it observably reach a user,
through a mechanism that is not simply "another event," with the
Event-Bus-vs-Notification distinction stated explicitly in writing.

### Estimated Complexity

**M.**

### Risks

Being designed as "an event with extra steps" rather than answering
what a notification genuinely needs that an event does not (delivery
guarantee, presentation, possibly persistence/read-state) — the same
category of mistake `Command Framework.md`'s own Academy guide already
warns against for a structurally similar pair.

---

## WP 6.3 — REST API

**Status note.** Not started.

### Objective

Expose TempestOS's own platform services (the Command Framework,
Diagnostics, Navigation, and whatever `WP 6.0`/`WP 6.1`/`WP 6.2` add) to
a caller outside the running process — the platform's first
network-facing surface. This is the Work Package `docs/security/Security
Roadmap.md` item 7 names directly: "when [a network-facing surface] is
first proposed, it should be threat-modelled on its own terms —
authentication, transport security, input validation at the network
boundary, and rate-limiting/DoS considerations all become relevant the
moment a socket is opened that was not open before."

### Scope

- **Must not begin before `WP 6.1` (Permissions & Identity) has an
  answer** — an unauthenticated network surface over a platform that can
  invoke arbitrary commands is not an acceptable interim state, even
  temporarily.
- Which platform capability(ies) this release actually exposes over the
  API — likely the Command Framework (dispatch by Id, mirroring its own
  existing UI-agnostic design intent) and Diagnostics (read-only status)
  as the first two, real, concrete surfaces; the REST layer itself
  should add no new capability, only a new caller for capability that
  already exists.
- Transport security, request/response contract stability, and
  rate-limiting/DoS posture — each a mandatory section of this Work
  Package's own eventual Security Review, not optional.

### Dependencies

**`WP 6.1`** (required, blocking — see above). The Command Framework and
Diagnostics (both already implemented, `v0.5.0`).

### Deliverables

An architecture document addressing every point `Security Roadmap.md`
item 7 names; new ADR(s); a Threat Model addendum for assumption 9 (the
platform will eventually expose APIs); a working API surface over at
least the Command Framework.

### Acceptance Criteria

A caller outside the running process can invoke at least one command and
query Diagnostics, authenticated per `WP 6.1`'s own model, with a
documented, reviewed security posture — not merely "it responds to
requests."

### Estimated Complexity

**L.**

### Risks

Shipping before `WP 6.1` is genuinely ready, under schedule pressure —
explicitly named here as unacceptable regardless of pressure, mirroring
this project's own standing rule against building security-relevant
capability ahead of the design work it depends on.

---

## WP 6.4 — Settings Framework

**Status note.** Not started.

### Objective

Give the platform a place for *user-changeable* configuration, distinct
from `IConfigurationProvider` (read-only, immutable, loaded once at
startup — `ADR-0009`, Case Study 05). Settings are read-write, at
runtime, by design; Configuration is deliberately not.

### Scope

- A settings store and a DI-public (or Module-SDK-level) surface for a
  module to read and react to a changed setting.
- Persistence: where a changed setting is actually stored between runs —
  this Work Package's own first real, concrete persistence decision on
  this platform since the bootstrap-era, currently-dead
  `JsonProjectRepository` code.
- Explicitly distinct from `WP 6.1`'s own permission model — *what* can
  be changed and *by whom* is Permissions & Identity's concern; *how a
  changed value is stored and observed* is this Work Package's.

### Dependencies

Dependency Injection; benefits from the Event Bus (notifying a module
that a setting it cares about changed).

### Deliverables

A settings contract; a concrete, real persistence mechanism; at least
one real module observing a real, user-changed setting.

### Acceptance Criteria

A user-facing setting can be changed at runtime, persisted, and observed
by a module without a Host restart.

### Estimated Complexity

**M.**

### Risks

Confusing this with `IConfigurationProvider` and attempting to make
Configuration itself mutable, contradicting `ADR-0009`'s own settled
reasoning (Case Study 05) rather than introducing a genuinely new,
parallel concept for genuinely different needs.

---

## WP 6.5 — Audit Framework

**Status note.** Not started.

### Objective

Record *who did what, when* across the platform — distinct from Logging
(diagnostic, developer-facing, not necessarily durable or queryable as a
historical record) and distinct from Diagnostics (a live snapshot of
current state, not a history of past actions).

### Scope

- An audit record contract and a durable store for it.
- Attribution: an audit record naming an actor is only as meaningful as
  the identity model behind it — this Work Package benefits enormously
  from `WP 6.1` landing first, though it can begin with a
  process-level/anonymous actor if `WP 6.1` is not yet ready, provided
  that limitation is disclosed, not hidden.
- Relationship to Diagnostics: whether `IDiagnosticsProvider` should gain
  an audit-history projection, or Audit remains an entirely separate
  concern with its own query surface, is this Work Package's own
  question to answer, mirroring the "is this the same concept with a
  different name, or a genuinely different one" discipline the Command
  Framework/Event Bus and Notification/Event Bus distinctions above
  already applied.

### Dependencies

Benefits from, but does not strictly require, `WP 6.1` (Permissions &
Identity) for real actor attribution.

### Deliverables

An audit record contract; a durable store; at least one real, audited
action.

### Acceptance Criteria

An action taken through the platform (for example, a Command Framework
dispatch) can be found later in a durable, queryable audit record.

### Estimated Complexity

**M.**

### Risks

Building a bespoke persistence mechanism here that duplicates whatever
`WP 6.4` (Settings) or a future data-storage decision already
establishes — this Work Package should reuse, not reinvent, wherever a
prior Work Package in this same release already answered "how does
TempestOS persist something."

---

## WP 6.6 — Licensing Framework

**Status note.** Not started.

### Objective

Give the platform a concept of a license — what capability is enabled,
for whom, until when. This is the Work Package `docs/security/Security
Roadmap.md` item 8 names directly: "no licensing concept exists in the
codebase... deferred until a concrete design exists to review."

### Scope

- A license model (what it grants, how it is validated) and where a
  license is checked — likely at Host startup, or lazily per gated
  capability; an explicit decision, not assumed.
- Explicitly **not** in scope unless a real, demonstrated need arises:
  a licensing *server* or remote validation service — `Security
  Roadmap.md`'s own "Explicit Non-Recommendations" principle (build
  security/compliance machinery only once a real need exists) applies
  here directly.

### Dependencies

Benefits from `WP 6.1` (Permissions & Identity) if licensing gates
capability per-user rather than per-installation.

### Deliverables

A license model and a validation mechanism; at least one real,
license-gated capability (even a trivial one) proving the mechanism
actually gates something.

### Acceptance Criteria

A capability can be enabled or disabled based on a real, validated
license, proven directly, not merely asserted.

### Estimated Complexity

**M.**

### Risks

Over-building — a distributed licensing/entitlement server this release
does not need. Keep the validation mechanism local and simple until a
real, demonstrated need for anything more exists.

---

## WP 6.7 — Export / Import

**Status note.** Not started.

### Objective

Let data the platform manages leave and re-enter it in a durable,
portable form — distinct from `WP 6.0` (Reporting), which produces
human-readable output, not necessarily round-trippable data.

### Scope

- A format decision (or a small, deliberately limited set of formats)
  and a contract for what a module makes exportable/importable.
- The relationship to `WP 6.0` (Reporting): explicitly decided, not
  assumed — a report is presentation-oriented and may be lossy by
  design; an export is round-trip-oriented and must not be.

### Dependencies

The Module Framework. Benefits from `WP 6.4` (Settings) if
export/import scope includes settings themselves.

### Deliverables

An export/import contract; at least one real, working round trip
(export a real artifact, import it back, confirm equivalence).

### Acceptance Criteria

Data exported from the platform can be re-imported into it and observed
to be equivalent to the original — proven directly, by test, not merely
asserted.

### Estimated Complexity

**M.**

### Risks

Designing an export format that cannot actually round-trip cleanly,
discovered only after `WP 6.0`'s own reporting format has already been
built around different assumptions — resolve the Reporting/Export
relationship (see Scope, above) before either format is finalised.

---

## WP 6.8 — Platform Services Integration Review

**Status note.** Not started.

### Objective

A dedicated, formal milestone review — not a feature Work Package —
mirroring `WP 4.2D` (Platform Services Architecture Review) and
`WP 5.0S` (Platform Security Baseline Audit): confirm the eight Work
Packages above compose correctly as one coherent platform, not eight
independently-correct but mutually-inconsistent additions.

### Scope

- Re-verify every ADR this release produced is implemented or
  intentionally deferred, mirroring `WP 5.4`'s own v0.5.0 precedent.
- A full repository review: every governance register internally
  consistent, re-derived from the file system directly rather than
  trusting each register's own prior arithmetic — the specific,
  standing-practice recommendation `WP 5.4`'s own retrospective produced,
  now exercised for the first time as a deliberate practice rather than
  an incidental finding.
- A security review against the v0.5.0 Security Baseline, checking
  specifically whether `WP 6.1`/`WP 6.3`/`WP 6.6` (the three Security
  Roadmap trigger items this release fires) were each resolved with a
  genuine architecture decision, not quietly deferred again.
- Explicit orthogonality/dependency checks between all eight Work
  Packages above — Reporting vs. Export/Import; the Event Bus vs.
  Notifications; Configuration vs. Settings; Diagnostics vs. Audit —
  each pair this document's own entries above named as needing an
  explicit decision, confirmed to have actually received one.

### Dependencies

**`WP 6.0` through `WP 6.7`** (all, complete).

### Deliverables

An architecture/security review document mirroring `WP 4.2D`/
`WP 5.0S`'s own depth; a v0.6.0 Release Candidate assessment mirroring
`WP 5.4`'s own precedent, feeding directly into `docs/releases/v0.6.0/
Retrospective.md` and `ReleaseNotes.md`.

### Acceptance Criteria

No Critical or High severity security finding; every governance register
internally consistent, re-verified by direct file-system count, not
carried forward; every cross-Work-Package orthogonality question this
document named has an explicit, written answer.

### Estimated Complexity

**L.**

### Risks

Treated as a rubber-stamp rather than a genuine review — explicitly
guarded against by requiring direct re-derivation of every governance
count, not a restatement of what the eight prior Work Packages already
claimed about themselves.
