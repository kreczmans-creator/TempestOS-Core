# 4 kW conveyor motor circuit in a machine panel, type C MCB (`elec.conductor-sizing`)

**Source.** Analytic (current, device and conductor selection; illustrative test values, not from any standard), I_b = 4000/(√3·400·0.82·0.85) = 8.28336 A; I_n = 10 A; I_t,req = 10/(0.9·0.8) = 13.8889 A; 1.5 mm² (illustrative 15 A); I_z = 15·0.9·0.8 = 10.8 A — Confirmed.

A 4 kW conveyor motor (design current 8.28 A) behind a 10 A type C MCB, three circuits sharing trunking at 40 °C. With the illustrative factors 0.9 (ambient) and 0.8 (grouping) the required tabulated capacity is 13.9 A, met by 1.5 mm² at an illustrative 15 A, leaving I_z = 10.8 A against I_n = 10 A (92.6 %): the coordination checks pass narrowly, so a longer run or another circuit in the trunking would push the size up. The capacities, ratings and factors here are illustrative test values entered as if from the owner's licensed table; replace them with the owner's own records before relying on the sheet.

| Input | Value | Source |
|---|---|---|
| `C_a` | 0.9 | reference: record: owner's licensed table (not shipped); 0.9 is an illustrative value, not from any standard |
| `C_g` | 0.8 | reference: record: owner's licensed table (not shipped); 0.8 is an illustrative value, not from any standard |
| `C_i` | 1 | reference: record: owner's licensed table (not shipped); 1 is an illustrative value, not from any standard |
| `L` | 35 m | drawing: E-LAY-021 rev B |
| `N_g` | 3 | drawing: E-LAY-021 rev B |
| `P` | 4 kW | spec: Motor schedule MS-04: conveyor drive 4 kW input |
| `U` | 400 V | spec: Site supply: 400 V three-phase |
| `ampacity` | 5 row(s) | reference: record: owner's licensed table (not shipped); capacities are illustrative test values, not from any standard |
| `context` | BS EN 60204-1 machine wiring | spec: Machine design basis MDB-2: BS EN 60204-1 wiring |
| `cos_phi` | 0.82 | spec: Motor datasheet MD-04: 0.82 at full load |
| `derive_Ca` | no | assumption: The entered C_a is used; the derived approximation is not applied. |
| `device` | MCB type C | drawing: E-SCH-021 rev A |
| `eta` | 0.85 | spec: Motor datasheet MD-04: 85 % at full load |
| `insulation` | PVC 70 °C | spec: Cable schedule CS-21: PVC 70 °C single cores |
| `method` | B2 | drawing: E-LAY-021 rev B |
| `ratings` | 9 row(s) | reference: record: owner's licensed table (not shipped); ratings are illustrative test values, not from any standard |
| `supply` | three-phase | drawing: E-SCH-021 rev A |
| `system` | metric | spec: Cable schedule CS-21: metric cross-sections |
| `theta_a` | 40 degC | spec: Machine design basis MDB-2: 40 °C inside the panel and trunking |
| `theta_ref` | 30 degC | assumption: Reference ambient of the licensed table; only used when C_a is derived. |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `I_b` | 8.28336 A | 8.28336 A | PASS |
| `I_n` | 10 A | 10 A | PASS |
| `I_t_req` | 13.8889 A | 13.8889 A | PASS |
| `CSA` | 1.5 mm^2 | 1.5 mm^2 | PASS |
| `I_z` | 10.8 A | 10.8 A | PASS |
| `u_In` | 0.925926  | 0.925926  | PASS |

Governing check: Conventional operating current within 1.45·I_z (I₂ ≤ 1.45·I_z) — PASS (utilisation 0.926). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
