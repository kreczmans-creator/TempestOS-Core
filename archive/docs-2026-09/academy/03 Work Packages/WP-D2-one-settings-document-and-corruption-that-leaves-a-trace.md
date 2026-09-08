# WP-D2 — One Settings Document, and Corruption That Leaves a Trace

## 1. Introduction

`WP-D2` (`171dc68`) consolidated nine hand-copied settings-store
implementations into one `SettingsDocument<TDocument>`, and made a corrupt
read log a warning naming the key instead of failing silently. It closed
`TD-112`. Nine new tests; three mutations designed and killed.

## 2. Purpose

To separate two things nine copies of the same code had conflated:
degrading **safely** and degrading **silently**.

## 3. Background

Nine settings stores across `Tempest.Core`, `Tempest.App` and
`Tempest.Desktop` had each written out the same two blocks by hand: an
idempotent `RegisterDefinition` guarded by a `try`/`catch`, and a load that
deserialised the stored JSON, swallowed `JsonException`, and fell back on
defaults.

**Six of the nine had no logger at all.** A torn write therefore discarded
a user's window geometry, recent objects, favourites, panel layout,
workspace arrangement or saved macros with nothing recorded anywhere that
it had happened.

## 4. The Problem

`TD-60`'s recovery contract — missing and corrupt both return `null`, which
every caller reads as "use the documented defaults", and neither raises —
is correct and was not the defect. The defect is that a contract about
*recovery* had been implemented as a contract about *invisibility*. A user
whose favourites vanished had no way to learn why, and neither did anyone
supporting them.

Nine copies also meant nine places to fix it, and six of them had nowhere
to write to even if someone had tried.

## 5. The Design

`SettingsDocument<TDocument>` holds both blocks once. `TD-60`'s contract is
unchanged and now asserted directly rather than assumed. What changed is
that a corrupt read logs a warning naming the key.

Migrated: `UserSettings`, `WindowUiState`, `RecentObjectsState`,
`FavouriteObjectsState`, `DesktopPanelUiState`, `WorkspaceState`,
`ProjectContext`, `ShellNavigator`, `MacroManager`.

**Logging is wired, not merely made possible.** `ILogger` is registered in
the container (`TempestHost` `AddInstance`), so `MacroManager` receives one
through the existing optional-parameter convention `RequirementsService`
already established. The manually-constructed stores are threaded
explicitly: `DesktopCompositionRoot` exposes the Host's logger, `MainWindow`
passes it to `DesktopSessionState` and on to its five stores, `WorkspaceHost`
passes it to `ProjectContext` and `ShellNavigator`, and `WorkspaceManager`
passes it to `WorkspaceState`.

A caller supplying no logger keeps the previous behaviour exactly — which
is what the existing tests constructing these stores directly rely on.

## 6. Alternatives Considered

**Add a logger to each of the nine in place.** Fixes the silence, keeps the
duplication, and leaves nine future copies to get right.

**Make a corrupt read throw.** Rejected: it inverts `TD-60`'s contract and
turns a recoverable first-run-shaped condition into a startup failure.

**Combine with `WP-D1`.** Explicitly rejected — "the `ActionOutcome`
reporting consolidation is a different layer and a different risk, and
stays deferred." Two consolidations in one commit would have made a
behavioural regression in either hard to attribute.

**Make the logger required.** Rejected because it would have broken every
existing test that constructs these stores directly, for no behavioural
gain.

## 7. Why This Solution Was Chosen

Because it changed exactly one observable thing. The recovery contract, the
return values, the defaults and the no-logger behaviour are all identical;
the single difference is that a corrupt read is now recorded. A
consolidation whose entire user-visible delta is "the failure is no longer
invisible" is one that can be reviewed on its merits.

## 8. Architectural Principles

`ADR-0053`'s one-persistence-authority principle extends naturally: one
substrate, and now one document abstraction over it, rather than nine
independent readings of the same contract.

The optional-logger convention (`RequirementsService`'s) was reused rather
than replaced — a new mandatory dependency would have been a wider change
than the defect justified.

## 9. Benefits

Corrupt persisted state is now traceable to a named key. One implementation
of the load-and-degrade block instead of nine. `TD-60`'s contract is
asserted rather than assumed, so a future change that quietly starts
throwing fails a test.

## 10. Trade-offs

Nine construction sites had to be threaded with a logger, which touched
`DesktopCompositionRoot`, `MainWindow`, `WorkspaceHost` and
`WorkspaceManager` — more files than a purely local fix. The alternative
was a consolidation whose logging was theoretical.

## 11. Common Mistakes

**Trusting the audit's count.** The audit said eight sites; the precise
count is **nine** — `WorkspaceState` was missed in its own listing. Counted
from the repository and corrected rather than carried forward.

**Consolidating a contract you have not asserted.** `TD-60`'s behaviour was
written down and widely relied on but not directly tested. The
consolidation added those assertions first.

**Registering a logger and calling it wired.** Six of the nine stores are
constructed by hand, not resolved, so container registration alone would
have left them exactly as silent as before.

## 12. Future Evolution

`TD-112`'s row was not updated when this shipped in Gate 0; `WP-G`'s own
Stage 1 audit found the omission and corrected it, recording the row as
completed. That is worth noting as a pattern: work completed in an early
gate can outrun the register that tracks it.

## 13. Key Takeaways

- "Degrade gracefully" and "degrade invisibly" are different requirements,
  and copying code is how they get conflated.
- Re-derive the audit's counts from the repository. This one was off by one,
  in the audit's favour.
- Wiring a dependency and registering it are different claims; check which
  one you have made for hand-constructed objects.
- Three mutations, three killed: restoring the silent swallow, rethrowing on
  corruption, and logging on a healthy load.

## Related Documents

- `docs/governance/Quality/Technical Debt Register.md` — `TD-112`, `TD-60`
- `ADR-0053` — one persistence authority
- `WP-G` retrospective — where `TD-112`'s stale row was found and corrected
- Commit `171dc68`
