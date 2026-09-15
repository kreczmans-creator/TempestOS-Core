# TempestOS v0.21.0 — Release Notes

**Status: in preparation on `release/v0.21.0`, cut from the `v0.20.0`
candidate (`88311649`) on 2026-09-15 at the Product Owner's instruction
to close every technical weakness named in the lead's assessment of that
afternoon.** Nothing in this document is certification. `v0.20.0` stays
under the Product Owner's manual test as the candidate, receiving the
`WP 20.10A`–`20.10G` fixes; every one of those is merged here too.

## Summary

**v0.21.0 is the recovery tranche** — the docking rewrite to `ADR-0153`,
Undo across commands, the editor split, documents from every template,
the Engineering Assets surfaces, typed calculation results with retained
inputs, the commercial edges, the viewer's remaining formats and markup,
the installer with upgrade, backup and restore, lazy rehydration, a
real-shell run in CI, and the mutation threshold met. See
`Execution Plan.md` for the packages and their waves.

## What shipped, by Work Package

| Work Package | Delivered | Merged |
|---|---|---|
| `WP 21.2B` The Engineering Assets surfaces: the bracket verification artefact filled in from the Desktop, calculation traces rendered, the merged capability's own area (`TD-165`, `TD-160`) | Ships on the Product Owner's "close all of those" instruction against the gap list that named this surface's absence; their own decision to park it until the release candidate, and the RC-time call, stand if either is later withdrawn. Engineering → Modules → **Engineering Assets** (`EngineeringAssetsView.cs`): five tabs — Calculation packs, Templates, Verification artefacts (each a filtered list with Open, its own detail showing `AssetApplicability` and its own governance/validation, every finding named by its own rule code), Engineering evidence (every item any of the three cite, flattened, naming which record cites it), and Bracket verification. The last is `TD-165`: a form over `GovernedBracketCheckRequest` (a material picker; applied load, section area, member length, mass limit, each with its own unit picker) — **Check** runs the identical `GovernedBracketCheckService` the unchanged Engineering Calculations surface already uses, and **Record verification artefact** is the first Desktop caller of `BracketEngineeringRecordService.RecordCalculationAsync`/`.RecordVerificationAsync`, writing into an existing calculation pack and verification artefact picked from the two libraries' own live records, so the artefact then lists under Verification artefacts at its own new standing. `TD-160`'s other named gap closes alongside it: a calculation pack's own **Trace** tab renders `EngineeringTraceRegister.CalculationTrace` (inputs traced to the governed references they pin, the template used, the outputs), read-only, exported as text through the file picker. Proven end to end through the real `MainWindow` by `tests/Tempest.Desktop.Tests/EngineeringAssetsJourneyTests.cs`; the shared `AutomationNameCoverageTests` and `LayoutWalkTests` structural walks extended to the new tree entry. | *(pending — `wp/21.2B`, not yet merged to this branch)* |

## Figures

*(re-derived at `WP 21.9.0`)*

## Warnings

- *(filled at each merge)*

## Related

- `docs/releases/v0.21.0/Execution Plan.md`
- `docs/releases/v0.20.0/Release Notes.md`
- `docs/adr/ADR-0153-tear-out-and-dock-everywhere-across-monitors.md`
