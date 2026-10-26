# M12 class 8.8 flange bolt, 80 N·m, 10 kN working load (`mech.bolted-joint`)

**Source.** Analytic (VDI 2230 Part 1 steps R0–R9; Shigley eqs 8-17, 8-20), F_M = 40.69 kN; Φ = 0.0720; F_KR = 25.4 − 9.3 − 3.0 ≈ 13 kN — Confirmed.

A steel flange clamped 40 mm thick by an M12 class 8.8 bolt torqued to 80 N·m with μ = 0.12: the bolt takes 7 % of the 10 kN working load, keeps about 13 kN of clamp after scatter and embedding, and sits well inside yield and the 50 MPa endurance amplitude.

| Input | Value | Source |
|---|---|---|
| `E_P` | 205 GPa | spec: steel |
| `E_S` | 205 GPa | spec: steel |
| `F_A` | 10 kN | spec: LOAD-CASE-3 |
| `F_KR_req` | 5 kN | spec: SPEC-FLG §6 sealing |
| `K` | 0.2 | assumption: Not used by the friction method. |
| `M_A` | 80 N*m | spec: Assembly procedure AP-7 |
| `P` | 1.75 mm | spec: ISO 261 coarse pitch |
| `R_p02` | 640 MPa | spec: ISO 898-1 class 8.8 |
| `S_strip` | 1.25 | assumption: Stripping safety factor. |
| `alpha_A` | 1.6 | assumption: Torque wrench, VDI 2230 Table A8 order. |
| `d` | 12 mm | drawing: DRG-FLG-02 rev A |
| `d_h` | 13 mm | drawing: DRG-FLG-02 rev A |
| `d_w` | 18 mm | spec: ISO 4762 head bearing |
| `f_Z` | 11 µm | assumption: Three interfaces, machined. |
| `l_k` | 40 mm | drawing: DRG-FLG-02 rev A |
| `m_eff` | 12 mm | drawing: DRG-FLG-02 rev A |
| `method` | friction | spec: Assembly procedure AP-7 |
| `mu_G` | 0.12 | assumption: Lightly oiled zinc-plated thread. |
| `mu_K` | 0.12 | assumption: Lightly oiled head bearing. |
| `n` | 0.5 | assumption: Load introduced at mid-thickness. |
| `sigma_A` | 50 MPa | assumption: Rolled-before-heat-treatment M12, VDI 2230 order of magnitude. |
| `tau_B` | 400 MPa | assumption: Nut material shear strength. |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `F_M` | 40.69 kN | 40.6899 kN | PASS |
| `Phi` | 0.07205  | 0.0720469  | PASS |

Governing check: Equivalent stress at assembly within the permitted ν of the proof strength — PASS (utilisation 0.97). Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
