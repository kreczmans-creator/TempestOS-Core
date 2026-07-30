# WP 7.1A — Engineering Data Model — Technical Debt Assessment

## Purpose

Discloses every new debt item or trade-off this Work Package's own
implementation introduces, and confirms which existing debt items (if
any) it touches — mirroring `docs/releases/v0.6.0/WP6.0 Technical Debt
Assessment.md`'s own format.

## Existing Debt: What Actually Happened

**`TD-12`-adjacent, but not worsened.** `TD-12` (`IPersistenceStore` has
no native query/filter capability) remains open, unchanged. This Work
Package's own `GetRevisionHistoryAsync` and `GetReferencesAsync`
implementations were deliberately designed to **avoid** inheriting that
limitation for their own access pattern (sequential, known revision
keys; a per-source-document reference collection) — see `ADR-0053`.
This is a genuine improvement in this Work Package's own narrow scope,
not a resolution of `TD-12` itself, which remains a real limitation for
any future consumer needing a genuinely arbitrary query across
`IPersistenceStore`.

No other existing Technical Debt Register item (`TD-01` through
`TD-16`) is touched by this Work Package.

## New Debt Disclosed by This Work Package

### TD-17 — `Content` Remains String-Only, No Structured/Typed Payload

**What.** `IDocumentRevision.Content` is a plain `string`, exactly as
approved (`WP7.0C Engineering Foundation Contracts.md`'s own disclosed
Extension Point). No consumer has yet needed structured content.

**Why this is debt, not merely a limitation.** Every future consumer
(Materials, a future Requirements Engine) will need to define and
enforce its own content schema outside this framework's own contract —
a real, if currently uncostly, coordination burden once multiple
consumers exist with different schemas.

**Revisit trigger.** A real, demonstrated need for the framework itself
to validate or enforce structure on `Content` — not before.

### TD-18 — No Native Concurrent-Reference-Write Isolation Test at Scale

**What.** `LinkAsync`'s own concurrency behaviour under many simultaneous
calls against the *same* source document was not tested at the same
depth as `ReviseAsync`'s own 20-concurrent-call test — only sequential
`LinkAsync` calls are tested.

**Why this is debt.** `LinkAsync` writes to a per-source-document
reference collection using a randomly-generated key, so no atomicity
concern exists analogous to `ReviseAsync`'s own revision-number
sequencing — but this claim has not been proven under real concurrent
load the way `ReviseAsync`'s has.

**Revisit trigger.** A real, demonstrated need for high-concurrency
reference writes against the same source document — not assumed to be
a current requirement.

## A Genuine, Disclosed Engineering-Review Finding (Not Debt)

**The revision-history and reference-lookup read paths are more
efficient than `WP7.0C`'s own contract required them to be** — recorded
in `ADR-0053` as a positive design decision, not debt. Included here
only to confirm it was considered and correctly classified as an
improvement, not a limitation.

## Summary Table

| # | Item | Status | Revisit Trigger |
|---|---|---|---|
| TD-17 | `Content` is string-only, no structured payload | New, Open | A real, demonstrated need for framework-enforced content structure |
| TD-18 | `LinkAsync` concurrency untested at scale | New, Open | A real, demonstrated need for high-concurrency reference writes |

**Total: 2 new debt items disclosed, 0 existing items worsened, 1
existing item (`TD-12`) confirmed not inherited for this framework's
own access pattern.**

## Related Documents

`docs/governance/Quality/Technical Debt Register.md` (to be updated
with `TD-17`/`TD-18` at the next governance review); `ADR-0053`;
`WP7.1A Implementation Report.md`.
