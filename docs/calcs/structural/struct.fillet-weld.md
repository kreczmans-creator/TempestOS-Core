# S5 — Fillet weld, simplified method (`struct.fillet-weld@1.0.0`)

**In plain terms.** A fillet weld fails by shearing through its throat, the shortest line from the root to the face. The simplified method adds every force on the weld into one resultant, spreads it along the weld length, and compares the stress on the throat area with one design shear strength. The weld is adequate when the throat carries the resultant; the sheet also gives the smallest throat that would do.

**Scope.** A fillet weld under forces parallel to, transverse to and normal to its axis, by the simplified method. Ported from the legacy `calc.fillet-weld-throat-stress` (ADR-0167). Clause numbers only (BS EN 1993-1-8 4.5.3.3, 4.5.2(2), 4.5.1(2)).

**Method.** `F = √(F_∥² + F_⊥² + F_n²)`; `f_vw,d = f_u/(√3·β_w·γ_M2)`; `τ_w = F/(a·L)`; `F_w,Rd = f_vw,d·a·L`; `u = F/F_w,Rd`; `a_req = max(F/(f_vw,d·L), 3 mm)`; leg `= √2·a`.

**Inputs.** `F_par`, `F_tr`, `F_n`, `L`, `a`, `f_u` (fill from a released Materials record), `beta_w`, `gamma_M2`. **Outputs.** `F`, `f_vw_d`, `tau_w`, `F_w_Rd`, `u`, `a_calc`, `a_req`, `s_leg`. **Checks.** u ≤ 1.

**Limitations.** Simplified method only (no directional method). Throats below 3 mm, effective lengths below max(30 mm, 6a) and an all-zero load block. Uniform share along the length; the long-joint reduction β_Lw (4.11) is not applied, and a weld longer than 150·a warns.

**Test cases.** The legacy module's three hand calculations (`ka-legacy-s355-shear`, `ka-legacy-s235-combined`, `ka-legacy-overloaded`).

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** S355 lap weld, 150 kN in longitudinal shear ([sheet](../../worked-examples/structural/struct.fillet-weld/s355-lap-shear/sheet.txt)).

```text
F = sqrt((150 kN)^2 + (0 kN)^2 + (0 kN)^2) = 150 kN
f_vw_d = 490 MPa / (sqrt(3) × 0.9 × 1.25) = 251.47 MPa
tau_w = 150 kN / (5 mm × 200 mm) = 150 MPa
F_w_Rd = 251.47 MPa × 5 mm × 200 mm = 251.47 kN
u = 150 kN / 251.47 kN = 0.5965
a_calc = 150 kN / (251.47 MPa × 200 mm) = 2.9825 mm
a_req = max(2.9825 mm, 3 mm) = 3 mm
s_leg = sqrt(2) × 5 mm = 7.0711 mm
Governing: PASS  Weld resistance: u = 0.5965 vs u ≤ 1  utilisation 0.596  « BS EN 1993-1-8 4.5.3.3
```
<!-- hand-arithmetic:end -->
