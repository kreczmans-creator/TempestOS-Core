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
| `WP 21.3A` Typed calculation results with retained inputs — `TD-22`, `TD-29`, `TD-30` | `TD-22`: `CalculationIntermediateResult` carries its own declared `ValueTypeName`; a typed read-back (`As<TValue>()`) returns the declared type or throws `CalculationReadbackException` (naming the key and both types) — never an `InvalidCastException` — whether the value is still the in-process CLR object or a `JsonElement` read back after persistence. `CalculationContext` takes a configured count/total-size bound (defaults 200 intermediates / 1 MiB, disclosed as guesses); exceeding either throws `CalculationBoundExceededException` naming the definition, refusing the record rather than recording without limit. `TD-29`: `CalculationRecord<TResult>` retains the input it ran with (`Input`/`InputTypeName`, the same informal nullable-trailing-field precedent `ResultTypeName` already set — no export-schema migration needed, confirmed against `WP 20.3A`'s own, separate export/import scope). `ICalculationEngine` gains `ReRunAsync` (identical retained input, or a supplied changed input) and `CompareAsync`, producing a typed `CalculationComparison` — which input and result fields changed, old and new, with units; a record with no retained input is reported in the diff rather than thrown for. Surfaced as two new commands, `calculations.rerun` (`Mutates = true`) and `calculations.compare-with-previous` (read-only), invocable through the Command Palette and the Ribbon's own data-driven Calculations tab — no new view. `TD-30`: `ICalculationResult`/`IVerificationResult`/`IApprovalGate` stay declared (still referenced structurally by `EvidenceComposer`/`IEvidence`/`ISimulation`) but are retired — each interface's own remark now says plainly why no implementation exists or is planned: the first two would require turning an immutable evidentiary snapshot into a live, addressable `IEngineeringObject` with its own revision/relationship service dependencies (`FCR-0051`, "a real Domain design question, not a mechanical add"); the third is a workflow-gate concept the product's own "no workflow engine" guard and `ADR-0087`/`ADR-0090` already rule out. The six built-in calculation definitions and their unit-invariance property proofs are unchanged. |

## Figures

*(re-derived at `WP 21.9.0`)*

## Warnings

- **The `TD-22` intermediate-result bound's defaults are guesses**
  (`WP 21.3A`): 200 intermediates / 1 MiB total per execution, sized off
  the six built-in definitions (at most five intermediates each) with two
  orders of magnitude of headroom, not measured against any real
  step-heavy calculation the product does not yet have. `CalculationContext`'s
  own constructor lets a caller override either; the lead should confirm
  the defaults before a calculation with a genuinely large intermediate
  set ships against them.

## Related

- `docs/releases/v0.21.0/Execution Plan.md`
- `docs/releases/v0.20.0/Release Notes.md`
- `docs/adr/ADR-0153-tear-out-and-dock-everywhere-across-monitors.md`
