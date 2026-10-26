# S5 — Fillet weld, simplified method (`struct.fillet-weld@1.1.0`)

**In plain terms.** A fillet weld fails by shearing through its throat, the shortest line from the root to the face. The simplified method adds every force on the weld into one resultant, spreads it along the weld length, and compares the stress on the throat area with one design shear strength. The weld is adequate when the throat carries the resultant; the sheet also gives the smallest throat that would do.

**Scope.** A fillet weld under forces parallel to, transverse to and normal to its axis, by the simplified method. Ported from the legacy `calc.fillet-weld-throat-stress` (ADR-0167). Clause numbers only (BS EN 1993-1-8 4.5.3.3, 4.5.2(2), 4.5.1(2)).

**Method.** `F = √(F_∥² + F_⊥² + F_n²)`; `f_vw,d = f_u/(√3·β_w·γ_M2)`; `τ_w = F/(a·L)`; `F_w,Rd = f_vw,d·a·L`; `u = F/F_w,Rd`; `a_req = max(F/(f_vw,d·L), 3 mm)`; leg `= √2·a`.

**Inputs.** `F_par`, `F_tr`, `F_n`, `L`, `a`, `f_u` (fill from a released Materials record), `beta_w`, `gamma_M2`. **Outputs.** `F`, `f_vw_d`, `tau_w`, `F_w_Rd`, `u`, `a_calc`, `a_req`, `s_leg`. **Checks.** u ≤ 1.

**Limitations.** Simplified method only (no directional method). Throats below 3 mm, effective lengths below max(30 mm, 6a) and an all-zero load block. Uniform share along the length; the long-joint reduction β_Lw (4.11(3)) is applied to a lap joint longer than 150·a (1.1.0).

**Changes in 1.1.0 (C-2 fix, F2).** The long-joint reduction of BS EN 1993-1-8 4.11(3) is applied (it was a warning that said it was not, and the sheet passed what the code reduces). New input `joint` (default Lap joint; Other = a weld whose stress follows the base metal's, as a flange-to-web weld, for which 4.11(2) gives no reduction): a lap joint longer than 150·a has β_Lw = 1.2 − 0.2·L/(150·a), not above 1, `F_w,Rd = β_Lw·f_vw,d·a·L`, `u = F/F_w,Rd`, and the throat needed `a_calc = F/(β_Lw·f_vw,d·L)` with the β_Lw of the throat provided; new output `beta_Lw` (1 when not reduced). python3, a = 5 mm, f_u 490 MPa, β_w 0.9, γ_M2 1.25 (f_vw,d 251.468 MPa): L = 800 mm gives β_Lw 0.986667 and F_w,Rd 992.461 kN (1005.872 kN unreduced); L = 1500 mm β_Lw 0.8, F_w,Rd 1508.809 kN; L = 3000 mm β_Lw 0.4, F_w,Rd 1508.809 kN (the reduced resistance stops growing with the length). A lap longer than 900·a, where the formula reaches zero, blocks (`long-joint-range`). A saved instance without `joint` reads as a lap joint; the three known-answer cases are shorter than 150·a and are unchanged.

**Test cases.** The legacy module's three hand calculations (`ka-legacy-s355-shear`, `ka-legacy-s235-combined`, `ka-legacy-overloaded`).

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** S355 lap weld, 150 kN in longitudinal shear ([sheet](../../worked-examples/structural/struct.fillet-weld/s355-lap-shear/sheet.txt)).

```text
beta_Lw = lap not longer than 150·a = 1
F = sqrt((150 kN)^2 + (0 kN)^2 + (0 kN)^2) = 150 kN
f_vw_d = 490 MPa / (sqrt(3) × 0.9 × 1.25) = 251.47 MPa
tau_w = 150 kN / (5 mm × 200 mm) = 150 MPa
F_w_Rd = 1 × 251.47 MPa × 5 mm × 200 mm = 251.47 kN
u = 150 kN / 251.47 kN = 0.5965
a_calc = 150 kN / (1 × 251.47 MPa × 200 mm) = 2.9825 mm
a_req = max(2.9825 mm, 3 mm) = 3 mm
s_leg = sqrt(2) × 5 mm = 7.0711 mm
Governing: PASS  Weld resistance: u = 0.60 vs u ≤ 1  utilisation 0.60  « BS EN 1993-1-8 4.5.3.3
```
<!-- hand-arithmetic:end -->
