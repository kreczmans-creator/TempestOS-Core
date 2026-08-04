# ADR-0063: Workspace Views Read Directly; Every Mutation Dispatches Through the Command Framework

## Status

Accepted — `v0.8.0` "Engineering Workspace", `WP 8.0A` (Engineering
Workspace Architecture), 2026-07-30. Resolves how a Workspace View is
permitted to interact with the services it presents.

## Context

Every Engineering Core/Systems Engineering Foundation service
(`IRequirementsService`, `IMaterialCatalog`, `ICalculationEngine`,
`IVerificationService`) exposes both read methods (`FindAsync`,
`ListAsync`, `GetRelationshipsAsync`, `GetEvidenceAsync`) and mutating
methods (`CreateAsync`, `ReviseAsync`, `SetStatusAsync`, `LinkAsync`).
The Workspace's own View layer (`WP8.0A UI Architecture.md` §3) needs an
explicit rule for which of these a View may call directly, since calling
every method identically would give presentation code the same
unmediated write access to engineering data any other caller has —
foreclosing, before it is ever needed, any future cross-cutting concern
(audit trail attribution, undo/redo, optimistic-concurrency retry) that
benefits from every mutation passing through one funnel.

## Decision

**A Workspace View reads directly from the service it presents. Every
mutating action dispatches through the existing Command Framework
(`ICommandDispatcher`), never a direct call to a mutating service
method.** Concretely: `FindAsync`, `ListAsync`,
`GetRelationshipsAsync`, and `GetEvidenceAsync` (and every sibling
framework's own equivalent reads) may be called directly from a View.
`CreateAsync`, `ReviseAsync`, `SetStatusAsync`, `LinkAsync`,
`AddToCollectionAsync`, and every sibling framework's own equivalent
mutator are wrapped in a Command, dispatched via `ICommandDispatcher`,
exactly as every `v0.6.0`/`v0.7.0` sample module already demonstrates
the pattern for its own mutating actions.

The asymmetry is deliberate, not an arbitrary rule applied uniformly for
its own sake: a read has no effect to mediate — calling it twice, or
directly versus indirectly, changes nothing about the system's own
state. A write does, and gating every write through one funnel is what
makes a future cross-cutting concern (see Context) addable later without
having to retrofit every View that ever performed a write directly.

## Consequences

**Positive:**

- Every mutating Workspace action is, from day one, testable and
  reusable exactly as `ICommand`'s own contract already anticipates for
  a menu, a keyboard shortcut, or a future automation/AI caller
  (`FCR-0024`) — a View is simply one more caller among these, not a
  privileged one with its own bypass.
- A future cross-cutting concern (audit attribution once the Command
  Framework is wired to `IAuditRecorder`; undo/redo; optimistic-
  concurrency retry against `TD-25`) can be added at the Command
  dispatch point once, rather than needing to be retrofitted into every
  View that ever wrote data directly.
- No View needs to know how to construct an audit-appropriate,
  attributable action — a Command already carries whatever data its own
  handler needs, mirroring how every existing sample module's own
  Command already does.

**Negative:**

- Every mutating Workspace interaction requires a Command type and
  (eventually) a handler to exist before it can be wired up — a real
  implementation-phase cost this ADR accepts, since the alternative
  (a View calling a mutator directly "for now, to save time") has
  already proven costly to retrofit in this project's own history (the
  Command Framework itself, `WP 4.0`/`WP 5.1A`/`WP 5.1B`, was
  deliberately designed and shipped specifically so that mutating
  actions would not need ad hoc, per-caller invocation logic).

## Alternatives Considered

**Views call every service method directly, reads and writes alike** —
considered and rejected. This forecloses, before it is ever needed, any
future cross-cutting concern a Command-mediated write path would enable
for free, and treats presentation code as a privileged caller with no
principled reason for the privilege.

**Gate reads through Commands too, for uniformity** — considered and
rejected. A read has no effect to mediate; requiring a Command
round-trip for every read would add ceremony (constructing a Command
instance, dispatching it, awaiting a result) with no corresponding
benefit, the same "do not build ahead of a demonstrated need" discipline
`VISION.md`'s own Product Principle 3 already applies elsewhere.

## Related Documents

`WP8.0A Workspace Architecture Document.md`; `WP8.0A UI Architecture.md`
§3; `docs/architecture/Command Framework Architecture.md`; `ADR-0036`–
`ADR-0038`; `TD-25` (Requirements concurrency, a plausible future
beneficiary of a Command-mediated write path).
