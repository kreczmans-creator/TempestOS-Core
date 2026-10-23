# TempestOS v0.27.0 — Release Notes (release candidate)

**Theme.** One calculation surface. The legacy calculation modules are replaced by worksheet templates that show their working, carry every legacy test figure, and draw a reference diagram beside the form (ADR-0167).

## What is new

- **Eleven more worksheets** (27 in all): column buckling, lifting lug, bearing life, fatigue (Palmgren–Miner), shaft stress, thermal expansion, thin-walled vessel, material margin, fillet weld, plane wall heat transfer, heat sink thermal chain. Every hand calculation and test vector of the legacy modules runs as a known-answer case.
- **Reference diagrams** for every worksheet, bound to the inputs: focus a field and its shapes light; point at a shape and its row is marked; click it and the field takes focus.
- **Your old calculations stay readable.** At start-up every stored legacy calculation becomes a read-only archive worksheet, listed under *Archived legacy calculations* in the worksheet catalogue.
- **Compare with previous revision** sets two revisions of a calculation side by side.
- **Fill from a reference record**: material, fastener and bearing properties come from released records, with the record named as the source.

## Corrections that change results

- **Bolted joint, separated joints.** When the working load opens the joint, the bolt now carries the whole working load and the clamp reads zero. Before, a separated joint understated bolt stress and fatigue amplitude; re-check any bolted-joint calculation whose separation check failed.
- **Tolerance stack, very small reject rates.** Predicted DPMO beyond about 6σ is now right to five figures.

## Removed

- Engineering → Modules → Calculations → **Calculators**. Use **Worksheets**. Calculation-register entries made by the old surface remain; their runs open as archives.

## Upgrade

No schema change. On first start the migration copies each legacy calculation record into an archive worksheet with the same Id; the record itself is untouched and the copy is made once.

See `CHANGELOG.md`, `docs/decisions/v0.27.0-DECISIONS.md`, `docs/reviews/` and `docs/handover/v0.27.0-handover.md`.
