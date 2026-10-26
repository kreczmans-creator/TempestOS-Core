# Calc pack — Drive train (`pack.drive-train`)

**Chain.** Motor sizing (`elec.motor-sizing`) → Shaft, Bearing A life, Coupling flange bolts; Shaft (`mech.shaft`) → Bearing A life, Bearing A seat fit.

**Purpose.** Motor sizing gives the torque and speed the shaft carries; the shaft gives the bearing load and the seat diameter; the bearing life uses the shaft reaction and the motor speed; the seat fit (a rolling-bearing bore against the shaft seat) uses the shaft diameter; the coupling flange bolts, the shaft's yield check and its key carry the peak torque the load demands at the motor shaft. The shaft is the motor-side (gearbox input) shaft: it carries the motor-shaft torque, split into a steady part and an alternating part for its fatigue check. The gear mesh forces on the shaft are entered for the E1 torque and must be re-derived if it changes.

A 4-pole motor (about 23 kW at 1475 rpm) turns a conveyor drum at 118 rpm through a 12.5:1 gearbox against a steady 1.8 kN·m. The shaft is the motor-side shaft, the gearbox input shaft: its steady torque is the motor's load torque at the motor shaft (151.6 N·m, no ratio or efficiency applied; the output-side torques are separate ports of the motor step for a shaft after the gearbox), its alternating torque is the motor step's torque swing (0 in continuous duty, as here; (T_pk − T_dec)/2 on a point-to-point cycle) and its speed is the motor's; the bearing's axial load is the motor step's peak ball-screw thrust (0 for this rotary drive); the shaft's reaction and speed give bearing A its load and speed; the shaft's seat diameter is the nominal size of the bearing-bore fit (the bore has its own tolerance, entered from the bearing record, against a k6 seat); and the peak torque the load demands at the motor shaft (T_pk, run-up included) is the torque the shaft's yield check sees, while the four-bolt coupling flange carries the motor step's design torque (T_design), whose basis the owner chooses on the motor step (T_basis): the load peak T_pk (the default, used here), the motor's typed peak torque T_peak, a service factor times the rated torque, or a protective torque limit. The gear mesh forces on the shaft are entered for the motor's torque (a pinion of 60 mm pitch radius), not linked.

**Issue.** Project “Conveyor 7 head drive”, pack “Conveyor head drive calc pack”, checked once as a bundle and issued under issue reference **`CNV-DRV-001`** to Client Co in one transaction. After issue every step is read-only and carries that reference. Checked against SPEC-CNV-07 and DRG-CNV-410 to 412 rev B.

## Steps

| Step | Calculator | Revision | Checks |
|---|---|---|---|
| Motor sizing | `elec.motor-sizing` | A | 4 of 4 PASS |
| Shaft | `mech.shaft` | A | 4 of 4 PASS |
| Bearing A life | `mech.bearing-life` | A | 2 of 2 PASS |
| Bearing A seat fit | `mech.iso286-fit` | A | 1 of 1 PASS |
| Coupling flange bolts | `mech.bolt-group` | A | 4 of 4 PASS |

## Links

Every link is pinned to the upstream revision named in the issued sheet (`link: <step> rev <n> · <output>`); changing an input of a step makes every step that takes from it, directly or through other steps, stale, and the pack cannot be checked or issued until each is updated in order.

| From | Output | To | Input | Value carried |
|---|---|---|---|---|
| Motor sizing | `T_load` | Shaft | `T` | 0.151579 kN*m |
| Motor sizing | `T_alt` | Shaft | `T_a` | 0 kN*m |
| Motor sizing | `n_pk` | Shaft | `n` | 1475 rpm |
| Shaft | `R_A` | Bearing A life | `F_r` | 1.34604 kN |
| Motor sizing | `n_pk` | Bearing A life | `n` | 1475 rpm |
| Motor sizing | `F_ax_pk` | Bearing A life | `F_a` | 0 kN |
| Shaft | `d_A` | Bearing A seat fit | `D` | 35 mm |
| Motor sizing | `T_pk` | Shaft | `T_pk` | 0.159248 kN*m |
| Motor sizing | `T_design` | Coupling flange bolts | `T_z` | 0.159248 kN*m |

## Inputs and provenance

Every input of every step carries a provenance on the issued sheet: the figures chosen for this example cite an (invented) specification or drawing, an input left at the sheet's default is recorded as an assumption, and a linked input names the calculation and output it comes from. Project, drawing, specification and issue references are illustrative; they are not real documents.

## Files

- `pack-issue-sheet.txt` — the pack's issue sheet (index, members, revisions) as stored on the Evidence record.
- `e1-elec-motor-sizing.sheet.txt` — Motor sizing, the issued sheet.
- `m4-mech-shaft.sheet.txt` — Shaft, the issued sheet.
- `m5-mech-bearing-life.sheet.txt` — Bearing A life, the issued sheet.
- `m2-mech-iso286-fit.sheet.txt` — Bearing A seat fit, the issued sheet.
- `m3-mech-bolt-group.sheet.txt` — Coupling flange bolts, the issued sheet.
- `inputs.json` — a record of every step's inputs as issued, links included: one JSON object keyed by step, each value that step's list of inputs. It is not a file that one worksheet imports whole (a worksheet takes the inputs of one calculation: copy one step's list out of it to load that step). Links and attached-file references in it name the original calculations and their Evidence, so they do not resolve in another project.

Generated by `ChainPackWorkedExampleTests` in `tests/Tempest.Calc.Tests.Library/Chains`. The PDFs are rendered separately.
