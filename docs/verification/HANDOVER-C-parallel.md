# Handover — Addendum C Track P (parallel, read-only verification)

Date: 2026-10-24. Base: `release/v0.26.0` @ 0772fcf8 (Addendum A closed: §A9 gates met, Blue round merged).
Branch: `claude/tempestos-v0260-test-readiness-s4jwh1`, rebased on that release head.
The cloud session could push only to its `claude/` branch, so it did not use `v0.26.0/addC-parallel`.
Track S merges this branch with `--no-ff`. It touches only `docs/verification/` and `tools/verification/`.
Track P changed no product code, test project, migration, build-log file or CI workflow.

## What was verified
| Item | Output | Result |
|---|---|---|
| P1 independent re-derivation | `rederivation/<id>.md`, `tools/verification/<id>.py` | 28 calculators, 5 packs, the Desktop sheet. All 115 KATs match independently. Every script exits 0. |
| P1 senior review | "Reviewed" column and "Independent review" section in each report | Every Critical, High and Medium reproduced with the probe or rejected. |
| P1 re-check | "Status @ <sha>" columns | Reports re-checked after DA-17/DA-18, A-4/A-5, A7a, A7b/A8 and the Addendum A Blue round. 31 findings fixed on release; a senior reviewer confirmed every Critical/High fix. |
| P2 KAT sources | `kat-sources.md` | 35 cases checked: 28 confirmed, 5 still unconfirmed, 2 contradicted. |
| P3 record templates | `record-templates/` | 51 header-only CSV templates with READMEs, in priority order, matched to the record kinds in code. |
| P4 manual tests | `manual-tests/` | One script per calculator and per pack (648+ steps), using the A8 saved-calculation and pack panels. |
| P5 consistency audit | `consistency-audit.md` | 60 findings, glossary seeds, port rename map. |
| P6 risk triage | `risk-triage.md` (regenerate with `tools/verification/triage.py`) | Open counts: see the summary table in `risk-triage.md`. Test order for the owner. |
| P7 oracle map | `oracles.md` | Free external cross-checks per calculator, with inputs and tolerance. |

## Critical findings (fix before owner testing)
1. **S1 section properties:** a tube or hollow rectangle in a composite counts its hole as solid (568.6 cm² for a 59.7 cm² tube).
2. **B2 psychrometrics:** cooling with dehumidification swaps sensible and latent load. SHR reads 0.35 instead of about 0.65.
3. **Drive-train pack:** motor RMS torque reaches the shaft as steady torque. Since the Blue round, yield sees peak torque; fatigue and keys still do not.

## High findings by area (41 open at 0772fcf8)
- Electrical: E4 ambient temperature ignored unless C_a is typed, no C_c for buried cable; E5 default resistivity, CPC near-origin check; E1 holding and braking torque, blank load torque; E6 duty-weighted currents (and in the motor-circuit pack); E3 resistivity.
- Structural: S4 oversize holes, single-lap cap, M12 clearance; S3 k-factor default, stale shear-area dimensions; S2 couple on a member, blank load position; S6 CHS t_req.
- Mechanical: M3 linear check at one bolt, assembly stress; M2 18 mm step, N9, p/r–u deviations, hollow-shaft bore, external collapse; drive-train gear ratio and coupling torque.
- Building services: B1 NPSH gaps, trim law, speed change not passed to E1; B2 wet leaving state; B6 vessel margins; B4 MVHR infiltration.
- Desktop: a seeded quantity whose unit is not in the pick list keeps its number and takes the default unit.
- Fixed in the Blue round (confirmed): E5 let-through and curve end, motor-circuit device type, M3 ΣA x′y′ and collinear moment, M6 I_uxy, M5 Y default and Y₁, E6 starting peak, S4 legacy picker. M4 Rayleigh is now Medium.
The ranked list with evidence is in `risk-triage.md`.

## KAT sources
- Contradicted: `ka-shigley-stiffness` (bolt versus hole diameter; upheld on review) and `ka-shigley-7-1` (provisional).
- `ka-chs-moment` (S6) pins defect P1-struct.base-plate-01 and must change with the fix.
- Still unconfirmed, owner's copy needed: Incropera 11.1, Shigley screw stack, SCI P358 fin plate, Designers' Guide to EN 1990, Phoenix Contact QUINT4 derating.
- The E4, B4 and S6 KATs are analytic or synthetic, so no published source applies.

## Tools for Track S and the owner
- `tools/verification/probe/`: runs any calculator from the command line (`kat`, `run`, `steps`, `template`). Build notes are in its README.
- `tools/verification/<id>.py`: independent recomputation of every KAT plus extra probes. Exit 0 means all KATs match.
- `tools/verification/kat_inventory.py`, `findings.py`, `triage.py`: inventory, finding list, triage regeneration.
- None of these is referenced by the solution or CI.

## Open items for Track S
- Fix or escalate every Critical and High in `risk-triage.md` (C-1). Add the probe evidence as regression tests.
- Several findings rest on recalled standard values and are marked "owner to confirm". Record them in DECISIONS.md if they are spec disagreements.
- A8 added saved-calculation and pack panels. Two manual steps stay BLOCK: motor-circuit pack create (cable tables) and wet-heating table-cell links by hand.
- A table record replaces the whole table input; this affects the B4 surface-resistance and conductivity records (P3 README).
- The governance check limits Markdown lines to code lines added. This branch is mostly Markdown; Track S may need to merge it with a justification.
- Addendum B calculators (S7, S8, F2 and others) are not yet on release. Track P polls every 2 hours and verifies each as it merges.
- Drive-train pack: no gear ratio on the torque link, so an output-side shaft gets motor torque (was Critical; High since the example shaft is on the motor side).
- Drive-train pack: coupling bolts are checked at the load's run-up torque, not the drive's peak (High, D1).
