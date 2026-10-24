# Handover — Addendum C Track P (parallel, read-only verification)

Date: 2026-10-24. Base: `release/v0.26.0` @ e283c07e (Addendum A-4, A-5 and A7a merged).
Branch: `claude/tempestos-v0260-test-readiness-s4jwh1`, rebased on that release head.
The cloud session could push only to its `claude/` branch, so it did not use `v0.26.0/addC-parallel`.
Track S merges this branch with `--no-ff`. It touches only `docs/verification/` and `tools/verification/`.
Track P changed no product code, test project, migration, build-log file or CI workflow.

## What was verified
| Item | Output | Result |
|---|---|---|
| P1 independent re-derivation | `rederivation/<id>.md`, `tools/verification/<id>.py` | 28 calculators, 3 packs, the Desktop sheet. All 109 KATs match independently. Every script exits 0. |
| P1 senior review | "Reviewed" column and "Independent review" section in each report | Every Critical, High and Medium reproduced with the probe or rejected. |
| P1 re-check | "Status @ <sha>" columns | Reports re-checked after DA-17/DA-18, A-4/A-5 and A7a. 7 findings fixed on release. |
| P2 KAT sources | `kat-sources.md` | 35 cases checked: 28 confirmed, 5 still unconfirmed, 2 contradicted. |
| P3 record templates | `record-templates/` | 51 header-only CSV templates with READMEs, in priority order, matched to the record kinds in code. |
| P4 manual tests | `manual-tests/` | One script per calculator and per pack, with negative tests and one step per finding. |
| P5 consistency audit | `consistency-audit.md` | 60 findings, glossary seeds, port rename map. |
| P6 risk triage | `risk-triage.md` (regenerate with `tools/verification/triage.py`) | Open: 4 Critical, 47 High, 67 Medium. Test order for the owner. |
| P7 oracle map | `oracles.md` | Free external cross-checks per calculator, with inputs and tolerance. |

## Critical findings (fix before owner testing)
1. **S1 section properties:** a tube or hollow rectangle in a composite counts its hole as solid (568.6 cm² for a 59.7 cm² tube).
2. **B2 psychrometrics:** cooling with dehumidification swaps sensible and latent load. SHR reads 0.35 instead of about 0.65.
3. **Drive-train pack:** motor RMS torque reaches the shaft as steady torque. Peak and reversing torque are never checked.
4. **Drive-train pack:** no gear ratio or efficiency on the torque link. It is unconservative by about i·η when i > 1.

## High findings by area (47 open)
- Electrical: E4 ambient temperature ignored unless C_a is typed; no C_c for buried cable; E5 resistivity, let-through and curve-end gaps; E1 holding and braking torque, blank load torque passes; E6 duty-weighted currents; motor-circuit device type, protective conductor and duty propagation.
- Structural: S4 oversize holes, single-lap cap, M12 clearance; S3 k-factor default and stale shear-area dimensions; S2 couple on a member and blank load position; S6 CHS t_req.
- Mechanical: M3 linear check at one bolt, ΣA x′y′, collinear moment, assembly stress; M6 I_uxy; M4 Rayleigh sign; M2 18 mm step, N9, p/r–u deviations, hollow-shaft bore, external collapse; M5 Y default and Y₁.
- Building services: B1 NPSH gaps, trim law, speed change not passed to E1; B2 wet leaving state on a dry coil; B6 vessel margins (owner to confirm BS EN 12828); B4 MVHR infiltration dropped (also in the wet-heating pack).
- Desktop: a seeded quantity whose unit is not in the pick list keeps its number and takes the default unit.
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
- The app cannot reopen a saved worksheet and has no pack screen. Manual stale-upstream and pack steps are BLOCK until then.
- A table record replaces the whole table input; this affects the B4 surface-resistance and conductivity records (P3 README).
- The governance check limits Markdown lines to code lines added. This branch is mostly Markdown; Track S may need to merge it with a justification.
- Addendum B calculators (S7, S8, F2 and others) are not yet on release. Track P polls every 2 hours and verifies each as it merges.
