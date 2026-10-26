# The same bolt under a 60 kN working load (separation) (`mech.bolted-joint`)

**Source.** Analytic (VDI 2230 R7; after separation the bolt carries the whole working load), F_M,min − (1 − Φ) F_A − F_Z < 0, so F_KR = 0 and F_S,max = F_A = 60 kN; σ_z = 60 kN / 84.27 mm² = 712.0 MPa — Confirmed.

With a 60 kN working load the relieved member load exceeds the minimum preload: the joint separates, the clamp falls to zero and the bolt carries the whole 60 kN. The sheet fails separation, clamp, fatigue and yield (115 % of proof): before v0.27.0 it reported yield at 89 % because it kept adding only Φ·F_A after separation (Green G-05).

| Input | Value | Source |
|---|---|---|
| `E_P` | 205 GPa | spec: steel |
| `E_S` | 205 GPa | spec: steel |
| `F_A` | 60 kN | spec: LOAD-CASE-3 |
| `F_KR_req` | 5 kN | spec: SPEC-FLG §6 sealing |
| `K` | 0.2 | assumption: Not used by the friction method. |
| `M_A` | 80 N*m | spec: Assembly procedure AP-7 |
| `R_p02` | 640 MPa | spec: ISO 898-1 class 8.8 |
| `S_strip` | 1.25 | assumption: Stripping safety factor. |
| `alpha_A` | 1.6 | assumption: Torque wrench, VDI 2230 Table A8 order. |
| `d` | 12 mm | drawing: DRG-FLG-02 rev A |
| `d_h` | 13 mm | drawing: DRG-FLG-02 rev A |
| `d_w` | 18 mm | spec: ISO 4762 head bearing |
| `f_embed` | 11 µm | assumption: Three interfaces, machined. |
| `l_k` | 40 mm | drawing: DRG-FLG-02 rev A |
| `m_eff` | 12 mm | drawing: DRG-FLG-02 rev A |
| `method` | friction | spec: Assembly procedure AP-7 |
| `mu_G` | 0.12 | assumption: Lightly oiled zinc-plated thread. |
| `mu_K` | 0.12 | assumption: Lightly oiled head bearing. |
| `n_load` | 0.5 | assumption: Load introduced at mid-thickness. |
| `p_thread` | 1.75 mm | spec: ISO 261 coarse pitch |
| `sigma_a_limit` | 50 MPa | assumption: Rolled-before-heat-treatment M12, VDI 2230 order of magnitude. |
| `tau_B` | 400 MPa | assumption: Nut material shear strength. |

| Output | Expected | Computed | Result |
|---|---|---|---|
| `F_M` | 40.69 kN | 40.6899 kN | PASS |
| `F_KR` | 0 kN | 0 kN | PASS |
| `F_S_max` | 60 kN | 60 kN | PASS |

Governing check: Residual clamp force against separation — FAIL. Files: `inputs.json` (loads into the worksheet), `sheet.txt` (rendered text), `sheet.pdf` (rendered by the Desktop).
