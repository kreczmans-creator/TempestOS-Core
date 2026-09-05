# TempestOS v0.16.0 — Release Notes

**Status: engineering complete, awaiting Product Approval.** Not merged
to `main`, not tagged, not published. `D-021`–`D-026` remain **Proposed**.
Certification is Product Approval's own act (Engineering Governance §9);
nothing in this document constitutes it.

---

## Summary

**v0.16.0 is v1.0 Readiness Hygiene. It adds no product capability.**

Its subject is the gap between what this repository *said* about itself
and what was *true* of it. The `v1.0.0 Release Candidate Audit` found
three unreconciled definitions of what v1.0 even is, six governance
registers that had drifted from source, a release gate that existed in a
document but not in CI, an Academy that had been one release behind since
`v0.10.0`, durable state with no schema version, and a desktop
application with no accessibility baseline. This release closes the items
that audit rated mandatory, except the v1.0 gate itself.

The most useful thing about it is not the thirteen planned Work Packages.
It is that a nine-perspective review board was then run against the
finished tree, and **found two release-blocking defects the programme had
shipped** — one of which was a genuine data-loss path in code whose
Technical Debt row already read *Resolved*. Five further Work Packages
were commissioned from the board's findings. A second, fully independent
review then examined the remediation itself.

## What shipped

**Durable state now carries a schema version** (`ADR-0120`).
`EngineeringObjectState` gains an explicit `SchemaVersion`; an absent one
normalises to `1` in code rather than by serialiser default; migrations
form an ordered per-Kind chain applied **on read only**; a record that
cannot be bridged is logged and skipped rather than aborting startup; and
enums now serialise as strings, which closes `TD-87`'s concretely named
risk — a `LifecycleState` reordering silently reinterpreting a persisted
status — with no migration required at all. A committed golden corpus of
real pre-v0.16.0 records must keep loading after every future bump.

**The release gate is enforced in CI.** `CI Gate` now depends on the
Governance Health Check, so a branch carrying register drift cannot go
green. `release.yml` — the workflow that actually publishes a tag — runs
the same check, and `scripts/new-release.ps1` now refuses to tag a commit
whose CI did not conclude `success`.

**The governance health check went from 8 checks to 16**, deriving the
Interface, Exception and Namespace registers directly from `src/` instead
of trusting their prose. Its two new checks found real drift on their
first run, including a namespace the register's own documented `grep`
could never have seen because the file carries a UTF-8 byte-order mark.

**Every governance register was re-derived from source**, and
`PROJECT_STATUS.md` was cut from 565,445 bytes to about 11,000, with the
remainder archived byte-for-byte rather than deleted.

**The Academy is current for the first time since `v0.10.0`.**
Fifty-two retrospectives were written: forty-one historical, plus this
release's own eleven — the latter commissioned by the review board, which
noticed that nothing was scoped to write them and that release Definition
of Done item 4 would have been re-broken the moment `WP 16.2B` made it
true.

**Test determinism.** `TD-34`, `TD-83`, `TD-100` and `TD-119` closed,
including a process-wide `Console.Out` race between parallel test classes.

**An accessibility baseline** for the shipped Desktop application: six
genuinely modal dialogs with focus trapped and restored, automation names,
live regions, keyboard operability for the Digital Thread graph, and
measured WCAG contrast fixes.

**Linux launches.** Avalonia 11.2.3 → 11.3.20 and a `Tmds.DBus.Protocol`
repin closed `TD-116`. See *Platform support* below for exactly what that
evidence does and does not establish.

**Durability and loopback hygiene.** The REST listener is now **off by
default** (`Runtime:RestApi:Enabled`, evaluated before any ASP.NET Core
object is constructed), and its OpenAPI document — previously served to
any unauthenticated caller complete with the route → permission map — now
runs through the same identity, permission and audit path as every command
route. `AsyncKeyedLock` no longer leaks a semaphore per key. The DI
container refuses a duplicate registration instead of silently replacing
a platform service (`ADR-0122`).

## Defects this release found in its own work

Recorded here rather than in a retrospective nobody reads.

**A data-loss race shipped inside `WP 16.4B` and was caught by the review
board.** The attachment reconciliation sweep could permanently delete a
file while a user was attaching it, leaving metadata that still claimed
the file existed. `TD-97` was already marked *Resolved* against that code.
The release plan had specified a write-intent marker; the implementing
stream substituted a simpler comparison, and that substitution opened the
race. It is now closed by the marker the plan originally asked for, with
the marker sampled *between* the content scan and the state scan — the
ordering is what makes it airtight rather than merely narrow. The first
remediation attempt was rejected for getting that ordering wrong.

**The accessibility baseline shipped an invisible focus ring.** The
`:focus-visible` ring was painted in a brush that resolved to the same
colour as the Primary button's own fill — 1.00:1 in both themes, on the
shell's single call-to-action. The test written to prove the ring worked
compared it only against its own token, never against the background it
was drawn on, so it could not have failed.

**A closed test race was reopened eleven minutes after it was closed.**
A new test class redirected `Console.Out` outside the collection that
serialises exactly that, on a branch cut before the convention was
reinforced.

**Two decisions shipped without the ADR this project's own governance
requires** — now `ADR-0121` and `ADR-0122`.

## Platform support

Stated precisely, because the difference matters.

- **Windows** — CI-verified on `windows-2022`: build and full test suite,
  both configurations, every push.
- **Linux** — the desktop application launches. Verified under `xvfb-run`,
  and a `linux-launch-smoke` CI job asserts the application reaches
  `Host -> Running.` rather than merely surviving a timeout. That job is
  **advisory**: it is not a required check and has no long track record.
- **macOS** — **expected to work, untested. There is no macOS CI.**
  Nothing in this release verifies it.

## Upgrading — read this before you do

**Upgrading from v0.15.0 is safe.** Records written by earlier builds
load correctly; the golden corpus proves it against real fixtures.

**Downgrading back to v0.15.0 is not.** This is a one-way door, verified
rather than inferred. `ADR-0120` makes enums serialise as strings;
v0.15.0 deserialises with default options that accept only numeric enum
values, and its read path logs a warning and returns `null` on failure.
So any object v0.16.0 re-saves becomes unreadable to v0.15.0, and **the
object silently disappears from that workspace rather than raising an
error**. Take a copy of your data directory before upgrading if you may
need to go back.

## Known issues and disclosed debt

- `TD-45` — GitHub branch protection on `main` is still not configured.
  The gate exists in the workflow; nothing on GitHub enforces it. This is
  a repository-administrator action.
- `TD-123` — the Dependency Injection, Platform Services and Validation
  registers have no machine derivation, which is how register drift
  recurred inside this very release.
- `TD-124` — the health check cannot detect a single stale Technical Debt
  row inside an internally consistent summary.
- `TD-128` — Digital Thread graph relationship edges remain
  keyboard-unreachable.
- `TD-129` — the REST handler's 404-versus-401 split lets an
  unauthenticated caller enumerate routes one probe at a time. Deliberately
  not changed: altering documented status codes is a Product decision, and
  exposure is nil while the listener is off by default.
- `TD-130` — the three reconciliation services take no identity or
  permission dependency, and `SweepAsync` deletes data. Nothing wires them
  today; whichever Work Package first does owns adding the check.
- `PersistenceStore`'s atomic writes are crash-safe but not power-loss
  durable — there is no `fsync` before or after the rename.
- `TD-65` remains **Partially resolved**: this release delivered a
  baseline, not a conformance claim.

## Governance

`D-021` through `D-026` — the six decisions defining v1.0 scope, the
Companion disposition, plugins, REST, the platform matrix and the
`v0.15.1` folder — are **Proposed and unratified**. The Decision Register
contains `D-001`–`D-020`. PR #6 is open with its Product Approval section
blank. Every Work Package in this release cites them as Proposed; none
claims otherwise.

`WP 16.0A`'s own closing line stated that `WP 16.0B` would not begin until
the six were approved. The programme proceeded on later, explicit Product
Owner direction to execute it. That sequencing is disclosed here rather
than smoothed over: **if any of the six is answered differently from the
draft, specific landed artefacts must be revisited** — `Product
Roadmap.md`'s Phase 5.5 section, `TD-82`'s disposition, the deleted
`v0.15.1` folder, and this release's REST default.
