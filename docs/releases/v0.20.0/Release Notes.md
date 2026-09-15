# TempestOS v0.20.0 — Release Notes

**Status: in preparation on `release/v0.20.0`, cut from the `v0.19.1`
candidate (`4f5ee83a`) on 2026-09-15 at the Product Owner's instruction
to close the P1–P3 technical debt before the release rather than after
it.** Nothing in this document is certification. `v0.19.1` stays as
pushed and gated as the fallback candidate.

## Summary

**v0.20.0 is the debt tranche** — every item the technical-debt
rationalisation of 2026-09-14 rated P1 to P3 that a gate can prove,
plus the two Product Owner definitions of 2026-09-15 (payment terms per
client; a calculation is a task from creation) and the draft ADR for
tear-out and dock everywhere. No new surface beyond what those closures
require.

## What shipped, by Work Package

| Work Package | Delivered | Merged |
|---|---|---|
| `WP 20.2C` Macros over real commands; `Unregister` (`ADR-0099`) | `ICommandRegistry.Unregister(id)` removes a descriptor outright (`Items` was already read fresh everywhere, so no second change mechanism was needed); `MacroManager.DeleteAsync` calls it, so a deleted macro's own descriptor is genuinely gone rather than left as a permanent, graceful-failing ghost. `MacroStep` carries a command Id plus the parameter values recorded for it at Add Step time (the same `CommandParameterPrompt` seam a live invocation already uses); `MacroManagerDialog.IsMacroEligible` widens to admit a parameterised binding (a confirmation-gated one still excluded — no recording answers a person's "yes"); `IMacroManager.CreateAsync` refuses a step whose own binding is declared `Unavailable` (the object-picker set), naming the reason, before the macro is ever created; `RunMacroCommandHandler` replays each step's recorded values, asking an optional fallback only for a value that was not recorded. `ADR-0099`'s own two disclosed gaps close (addendum). | 2026-09-15 (pending merge) |

## Figures

*(re-derived at `WP 20.9.0`)*

## Warnings

- *(filled at each merge)*

## Related

- `docs/releases/v0.20.0/Execution Plan.md`
- `docs/releases/v0.19.1/Release Notes.md`
- `docs/releases/v0.19.1/Product Owner Decisions 2026-09-15.md`
- `docs/releases/v0.19.1/Technical Debt Rationalisation — Part 1.md` and `Part 2.md`
