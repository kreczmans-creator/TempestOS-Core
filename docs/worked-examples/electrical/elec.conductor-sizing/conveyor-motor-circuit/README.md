# 4 kW conveyor motor circuit in a machine panel, type C MCB (`elec.conductor-sizing`)

**Source.** Analytic (current, device and conductor selection; synthetic test values invented for this example, not taken from any table), I_b = 4000/(√3·400·0.82·0.85) = 8.28336 A; I_n = 10 A; I_t,req = 10/(0.91·0.77) = 14.27144 A; 2.5 mm² (synthetic 17.7 A); I_z = 17.7·0.91·0.77 = 12.40239 A — Confirmed.

A 4 kW (rated output) conveyor motor, design current 8.28 A, behind a 10 A type C MCB, three circuits sharing trunking at 40 °C. With the synthetic factors 0.91 (ambient) and 0.77 (grouping) the required tabulated capacity is 14.3 A, met by 2.5 mm² at a synthetic 17.7 A, leaving I_z = 12.40 A against I_n = 10 A (81 %): all three coordination checks pass. The capacities and factors here are synthetic values invented for this example and entered as if from the owner's licensed table; the ratings are the typical preferred series. Replace them with the owner's own records before relying on the sheet.

| Input | Value | Source |
|---|---|---|
| `C_a` | 0.91 | reference: record: owner's licensed table (not shipped); 0.91 is a synthetic value invented for this example |
| `C_c` | 1 | reference: record: owner's licensed table (not shipped); 1 is the neutral value, as the cable runs in trunking, not buried or in ducts |
| `C_g` | 0.77 | reference: record: owner's licensed table (not shipped); 0.77 is a synthetic value invented for this example |
| `C_i` | 1 | reference: record: owner's licensed table (not shipped); 1 is a neutral value for this example |
| `L` | 35 m | drawing: E-LAY-021 rev B |
| `N_g` | 3 | drawing: E-LAY-021 rev B |
| `P` | 4 kW | spec: Motor schedule MS-04: conveyor drive 4 kW rated output |
| `T_amb` | 40 degC | spec: Machine design basis MDB-2: 40 °C inside the panel and trunking |
| `T_ref` | 30 degC | assumption: Reference ambient of the licensed table; only used when C_a is derived. |
| `U` | 400 V | spec: Site supply: 400 V three-phase |
| `ampacity` | 5 row(s) | reference: record: owner's licensed table (not shipped); capacities are synthetic values invented for this example |
| `context` | BS EN 60204-1 machine wiring | spec: Machine design basis MDB-2: BS EN 60204-1 wiring |
| `cos_phi` | 0.82 | spec: Motor datasheet MD-04: 0.82 at full load |
| `derive_Ca` | no | assumption: The entered C_a is used; the derived approximation is not applied. |
| `device` | MCB type C | drawing: E-SCH-021 rev A |
| `eta` | 0.85 | spec: Motor datasheet MD-04: 85 % at full load |
| `insulation` | PVC 70 °C | spec: Cable schedule CS-21: PVC 70 °C single cores |
| `method` | B2 | drawing: E-LAY-021 rev B |
| `ratings` | 9 row(s) | reference: record: owner's licensed table (not shipped); ratings are the typical preferred series |
| `supply` | three-phase | drawing: E-SCH-021 rev A |
| `system` | metric | spec: Cable schedule CS-21: metric cross-sections |

| Output | Expected | Computed | Matches expected |
|---|---|---|---|
| `I_b` | 8.28336 A | 8.28336 A | yes |
| `I_n` | 10 A | 10 A | yes |
| `I_t_req` | 14.2714 A | 14.2714 A | yes |
| `CSA` | 2.5 mm^2 | 2.5 mm^2 | yes |
| `I_z` | 12.4024 A | 12.4024 A | yes |
| `u_In` | 0.806296  | 0.806296  | yes |

Governing check: Design current against the device rating — PASS (utilisation 0.83). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
