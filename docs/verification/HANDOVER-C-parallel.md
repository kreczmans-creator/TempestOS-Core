# Handover — Addendum C Track P (parallel, read-only verification)

Date: 2026-10-24. Base: `release/v0.26.0` @ 2f53791c (Addendum A closed; Addendum B-0 to B-3 merged).
Branch: `claude/tempestos-v0260-test-readiness-s4jwh1`, rebased on that release head.
The cloud session could push only to its `claude/` branch, so it did not use `v0.26.0/addC-parallel`.
Track S merges this branch with `--no-ff`. It touches only `docs/verification/` and `tools/verification/`.
Track P changed no product code, test project, migration, build-log file or CI workflow.

## What was verified
| Item | Output | Result |
|---|---|---|
| P1 independent re-derivation | `rederivation/<id>.md`, `tools/verification/<id>.py` | 40 calculators, 6 packs, the Desktop sheet. All 219 KATs match independently. Every script exits 0. |
| P1 senior review | "Reviewed" column and "Independent review" section in each report | Every Critical, High and Medium reproduced with the probe or rejected. |
| P1 re-check | "Status @ <sha>" columns | Reports re-checked after DA-17/DA-18, A-4/A-5, A7a, A7b/A8 and the Addendum A Blue round. 31 findings fixed on release; a senior reviewer confirmed every Critical/High fix. |
| P2 KAT sources | `kat-sources.md` | 51 cases checked to B-3: 38 confirmed, 9 still unconfirmed, 4 contradicted. |
| P3 record templates | `record-templates/` | 82 header-only CSV templates (to B-3) with READMEs, in priority order, matched to the record kinds in code. |
| P4 manual tests | `manual-tests/` | One script per calculator (40) and per pack (6), using the saved-calculation and pack panels. |
| P5 consistency audit | `consistency-audit.md` | 60 findings, glossary seeds, port rename map. |
| P6 risk triage | `risk-triage.md` (regenerate with `tools/verification/triage.py`) | Open counts: see the summary table in `risk-triage.md`. Test order for the owner. |
| P7 oracle map | `oracles.md` | Free external cross-checks for all 40 calculators, with inputs and tolerance. |

## Critical findings (fix before owner testing)
1. **S1 section properties:** a tube or hollow rectangle in a composite counts its hole as solid (568.6 cm² for a 59.7 cm² tube).
2. **B2 psychrometrics:** cooling with dehumidification swaps sensible and latent load. SHR reads 0.35 instead of about 0.65.
3. **Drive-train pack:** motor RMS torque reaches the shaft as steady torque. Since the Blue round, yield sees peak torque; fatigue and keys still do not.
4. **F2 accumulator:** emergency-stroke sizing treats the charge as adiabatic from pre-charge. A charge held at full pressure cools first, so the stroke needs about 1.26 times the volume.

## High findings by area (55 open at 2f53791c)
- Electrical: E4 ambient temperature ignored unless C_a is typed, no C_c for buried cable; E5 default resistivity, CPC near-origin check; E1 holding and braking torque, blank load torque; E6 duty-weighted currents (and in the motor-circuit pack); E3 resistivity.
- Structural: S4 oversize holes, single-lap cap, M12 clearance; S3 k-factor default, stale shear-area dimensions; S2 couple on a member, blank load position; S6 CHS t_req.
- Mechanical: M3 linear check at one bolt, assembly stress; M2 18 mm step, N9, p/r–u deviations, hollow-shaft bore, external collapse; drive-train gear ratio and coupling torque.
- Building services: B1 NPSH gaps, trim law, speed change not passed to E1; B2 wet leaving state; B6 vessel margins; B4 MVHR infiltration.
- Addendum B: F2 real-gas cap and adiabatic exponent cap; HPU pack stroke ends below the push pressure and p_1 is not tied to the load; F1 buckling length leaves out the cylinder body; F3 face-seal flange gap ignored; S7 C1 for double curvature and welded-section factors; M7 uncorrected stress in fatigue; B7 verdict depends on layer splitting; B9 frost in cold stores; E8 bend tension 64 % low; E1 rated speed unchecked.
- Desktop: a seeded quantity whose unit is not in the pick list keeps its number and takes the default unit.
- Fixed in the Blue round (confirmed): E5 let-through and curve end, motor-circuit device type, M3 ΣA x′y′ and collinear moment, M6 I_uxy, M5 Y default and Y₁, E6 starting peak, S4 legacy picker. M4 Rayleigh is now Medium.
The ranked list with evidence is in `risk-triage.md`.

## KAT sources
- Contradicted: `ka-shigley-10-3` (Shigley prints L0 = 162.8 mm), `ka-stretch-rule` (Parker 2001 prints 5 % stretch, not 6 %), `ka-shigley-stiffness` (bolt versus hole diameter; upheld on review) and `ka-shigley-7-1` (provisional).
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
- Addendum B-4 (E9, W1, M9, W2) and B-5 (S8) are not yet on release. Track P polls every 2 hours and verifies each as it merges.
- Drive-train pack: no gear ratio on the torque link, so an output-side shaft gets motor torque (was Critical; High since the example shaft is on the motor side).
- Drive-train pack: coupling bolts are checked at the load's run-up torque, not the drive's peak (High, D1).
