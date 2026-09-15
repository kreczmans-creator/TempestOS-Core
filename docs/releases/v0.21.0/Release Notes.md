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
| `WP 21.7A` | Eleven engineering calculation modules under `Tempest.Core.Calculations.Modules` — beam bending and deflection, bolted joint preload, bolt group under eccentric shear, fillet weld throat stress (EN 1993-1-8), lifting lug and pin, column buckling (Perry-Robertson), shaft combined stress, bearing rating life (ISO 281), thick-walled cylinder (Lamé), thermal expansion stress, fatigue with Miner's rule — each specified under `docs/engineering/calculations/`, registered in the product catalogue, refusing inputs outside its method (`EngineeringCheckOutcome.OutsideMethodLimits`), material properties read from released records through `MaterialPropertyReader`, form descriptors for `WP 21.7B`; the Engineering Calculations catalogue now derives from the product list | — |

## Figures

*(re-derived at `WP 21.9.0`)*

## Warnings

- *(filled at each merge)*

## Related

- `docs/releases/v0.21.0/Execution Plan.md`
- `docs/releases/v0.20.0/Release Notes.md`
- `docs/adr/ADR-0153-tear-out-and-dock-everywhere-across-monitors.md`
