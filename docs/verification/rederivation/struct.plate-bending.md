# struct.plate-bending — independent re-derivation (Track P, P1)
Calculator: Plate bending, version 1.2.1. Source files: src/Tempest.Calc/Tempest.Calc.Structural/PlateBendingCalculator.cs and PlateBendingMath.cs. Spec: docs/calcs/structural/struct.plate-bending.md.
Script: tools/verification/struct.plate-bending.py (exit 0 = all KATs match).
Verdict: MATCH WITH FINDINGS

All 29 KAT values match, including the values of the 4 Unconfirmed cases. The script re-runs 133 product values from 24 probe cases. I ran further probes ad hoc. The corner extrapolation and the Ritz convergence claims hold, with one wording point. Findings are Low or Info.

## Method re-derived
Sources: Timoshenko and Woinowsky-Krieger, Theory of Plates and Shells, 2nd ed., Ch. 3, 5, 6. Roark's Formulas for Stress and Strain, 8th ed., Tables 11.2 and 11.4 (cross-check only). Leissa, Vibration of Plates (NASA SP-160). BS EN 1993-1-1 6.2.1(5). Units: mm, N, MPa, N·m/m, Hz.
- D = E·t³/(12(1−ν²)). Surface stress σ = 6M/t². Von Mises with plane stress: √(σx² − σxσy + σy² + 3τ²).
- Circular plate, any central patch or ring: statics gives the shear. I integrated twice by quadrature. The edge condition (w′ = 0, or M_r = 0) fixes the constant by a linear solve. I did not use the closed forms of the product.
- Circular uniform: clamped w = q·a⁴/(64D), edge σ = 0.75·q·a²/t². Simply supported w = (5+ν)q·a⁴/(64(1+ν)D), centre σ = 3(3+ν)q·a²/(8t²).
- Circular frequency: determinant of w = A·J₀(λr/a) + B·I₀(λr/a) with the edge conditions. λ² = 10.2158 clamped. λ² = 4.9351 simply supported at ν = 0.3.
- Rectangle, simply supported: Navier double series over odd m, n. Sums at 512, 1024, 2048 odd terms, tail 1/N² removed. Centre w, M_x, M_y and corner M_xy. Von Mises at centre and corner.
- Rectangle, clamped: Ritz with clamped-clamped beam functions, not the polynomial basis of the product. Orders 14 to 48 per direction, moments extrapolated with a 1/n² tail. ν drops out of the stiffness. Frequency from the generalised eigenproblem.
- Field scan (ad hoc, not in the script): for 9 uniform and patch cases I scanned the whole quarter plate. The maximum von Mises moment is always at the centre or at the corner (simply supported).

## Known-answer cases
All values of a case were compared. The table shows the headline value and the largest relative difference of the case.
| Case | Symbol (values compared) | KAT expected | Independent | Rel. diff | KAT tol | Result |
|---|---|---|---|---|---|---|
| ka-circular-clamped-uniform | w_max (5) | 0.533203125 mm | 0.533203125 mm | 2.9e-16 | 1e-9 | PASS |
| ka-circular-simple-uniform | f1 (4) | 95.9796677 Hz | 95.9796677 Hz | 3.0e-16 | 1e-8 | PASS |
| ka-circular-simple-patch | w_max (3) | 0.733270526 mm | 0.733270526 mm | 4.5e-16 | 1e-9 | PASS |
| ka-circular-clamped-ring | sigma_max (3) | 50.9256028 MPa | 50.9256028 MPa | 7.6e-16 | 1e-6 | PASS |
| ka-rect-simple-square-series | M_x_c (4) | 119.715949 N·m/m | 119.715949 N·m/m | 1.4e-7 | 2e-6 | PASS |
| ka-rect-simple-square-book | w_max (1) | 0.25771484 mm | 0.25786418 mm | 5.8e-4 | 1e-3 | PASS |
| ka-rect-simple-natural | f1 (1) | 133.172787 Hz | 133.172787 Hz | 0 | 1e-9 | PASS |
| ka-rect-simple-patch | M_y_c (3) | 152.721873 N·m/m | 152.721872 N·m/m | 4.0e-9 | 2e-6 | PASS |
| ka-rect-clamped-square | M_edge (3) | 128.334192 N·m/m | 128.333658 N·m/m | 8.1e-6 | 1e-3 | PASS |
| ka-rect-clamped-square-book | w_max (1) | 0.07998047 mm | 0.08031811 mm | 4.2e-3 | 5e-3 | PASS |
| ka-rect-clamped-natural | f1 (1) | 286.851697 Hz | 286.851717 Hz | 7.0e-8 | 1e-6 | PASS |

The two "book" KATs compare a 3-figure coefficient (0.00406 and 0.00126). They pass with a loose tolerance. The converged values are 0.0040624 and 0.0012653.
Cross-checks against Roark (coefficients from memory, not pinned): simply supported square β 0.2873 (book 0.2874), α 0.0444. Clamped square edge β 0.3079 (book 0.3078), α 0.0138. Circular: clamped edge 0.75, simply supported centre 1.2375, deflection 0.1706 and 0.6956 (book 0.171, 0.696).
My converged clamped-square centre moment is 0.02290·q·a². The KAT value is 0.022905. I recall 0.0231 in the book, 0.9 % higher. The KAT is the converged value.

## Extra probes (product vs independent)
Product values are hard-coded in the script (133 values, 24 cases). Rows marked * were run ad hoc with the probe tool and are not in the script. Differences are the worst of each group.
| Probe | Inputs (short) | Symbol | Product | Independent | Rel. diff | Result |
|---|---|---|---|---|---|---|
| C1-C10 | Circular: clamped and simply supported, uniform, patch β 0.02 to 1, ring β 0.1 to 0.99, ν 0 to 0.49 | w, σ_vm, σ_p, f1 | 40 values | same | 4e-10 | PASS |
| C11* | Force basis: clamped patch 100 mm, 5 kN; simply supported uniform 3 kN | w_max | 1.99159 mm; 1.66068 mm | same | 1e-9 | PASS |
| S1 | Simply supported 2:1, 5:1, 10:1 uniform, ν 0.3 | w, σ, M_x, M_y, M_xy | 21 values | same | 3e-6 (M_x at 5:1) | PASS |
| S2 | Square ν = 0 (corner governs); ν = 0.49 (centre governs) | σ_vm, location | 42.87 MPa corner; 29.27 MPa centre | same | 1e-7 | PASS |
| S3 | 3:1 patch 20 % × 20 %; square patch 2 % (cap); patch = plate; 100 % × 10 % | w, M, σ | 28 values | same | 3e-6 | PASS |
| S4* | 27 uniform cases, aspect 1 to 10, ν 0, 0.3, 0.49 | M_xy_k, w, σ | corner extrapolation | same | 1.3e-7 worst | PASS |
| S5* | 18 random cases, uniform and patch, aspect up to 10 | w, M_x, M_y, M_xy | 72 values | same | 3e-6 (one M_x, see -02) | PASS |
| K1 | Clamped 2:1 and 3:1 uniform | w / M_c / M_edge / σ / f1 | 0.160783 mm / 207.165 / ... | same | 3e-5 (M_x_c), 3e-6 (σ) | PASS |
| K2 | Clamped square patch 50 % and 25 %; 3:1 band patch 25 % × 100 % | M_edge / σ | 650.998, 565.436, 345.150 | 650.39, 565.12, 345.12 | 9e-4, 6e-4, 1e-4 | PASS |
| K3* | Clamped 600 × 400 and 1000 × 1000, patch 25 % | σ_vm | 3.3420 MPa, 25.774 MPa | 3.3384, 25.791 | 1.1e-3, 7e-4 | PASS |
| K4 | Clamped 10:1 (5000 × 500) uniform | M_edge | 208.386 N·m/m | 208.333 (strip limit q·b²/12) | 2.5e-4 | PASS, see -03 |
| K5* | Clamped 5000 × 500 | f1 | 178.8168 Hz | 178.8168 Hz | 1e-8 | PASS, see -01 |
| G1* | Large deflection: SS circular w/t 0.4964 and 0.5017 | warning | none, then warns | threshold 0.5 | - | PASS |
| G2* | span/t 9.99, 10; patch 24.9 %, 25 %; aspect 10, 10.01; ring 0.5 %, 99.5 % | block / warn | as in the spec | same | - | PASS |
| G3* | Hard limits: 20 m × 2 m × 0.1 mm at 10⁵ kPa; 5 × 5 × 0.1 mm; t 300 mm | finite results | finite, warns | - | - | PASS |

## Implementation comparison
| Aspect | Spec / textbook | Implementation | Agrees? |
|---|---|---|---|
| Circular closed forms and patch, ring | Kirchhoff, T&WK Ch. 3 | Constants from continuity and edge conditions | Yes |
| Moment search | Grid, load radius, refinement | 2000 points, kink tested, golden section | Yes |
| Navier terms and tolerance | Doubling to N = 1024, 1e-6 | Same, centre and corner tested | Yes |
| Corner M_xy extrapolation | s_N + (s_N − s_N/2)/3 | Same; patch uses it only when it changes less | Yes, tested |
| Governing stress, simply supported | Centre or corner twist | √3·|M_xy| at corner, centre von Mises | Yes, field scan agrees |
| Clamped Ritz | Polynomial basis, K to 30 | Same; stall rule 0.7 | Yes, within 1e-3 of mine |
| Stress grid, clamped | 21 × 21 quarter grid with edges | Same; includes the edge mid-points | Yes |
| Frequency | Rectangle ω₁₁, circular λ², Ritz | Same | Yes |
| Large deflection guard | Warn at w/t > 0.5 | Warning only, at 0.5 | Yes |
| Thin plate guard | Warn at span/t < 10 | Warning only | Yes |
| Patch and ring limits | 1 %, 2 %, 25 %; ring 1 to 99 % | Blocks at those limits | Yes |
| Utilisations | σ/(f_y/γ_M0); w/(span/ratio) | Same | Yes |
| Edge-moment output | M_edge | Circular signed, rectangle absolute | Partly, see -04 |

## Findings
| ID | Severity | Finding | Evidence | Suggested fix (for Track S) |
|---|---|---|---|---|
| P1-struct.plate-bending-01 | Low | The `ritz-frequency` warning fires on a converged frequency. The text says the frequency is approximate. | Clamped 5000 × 500 × 8, 10 kPa: warning with last change 3.06e-11 (limit 1e-11). Product f1 = 178.8168006 Hz. Mine = 178.81680 Hz (orders 40×5 to 48×8 agree to 1e-8). | Use a limit of 1e-9 on the Rayleigh quotient, or a relative limit on ω. |
| P1-struct.plate-bending-02 | Low | The `navier-converge` warning is driven by a corner moment that is negligible. The governing value had converged. | Simply supported 5000 × 500 × 8, patch 100 × 500, 100 kPa: corner M_xy = 0.0017 N·m/m against M_y_c = 565 N·m/m. Warning with last change 0.017. Also M_x_c for ν = 0, 8:1 is 0.5 % off (3e-5 of M_y_c). | Weigh the corner change by the corner share of the governing von Mises moment. |
| P1-struct.plate-bending-03 | Low | The spec says the stalled 5000 × 500 stress at K = 16 is within 3e-5 of K = 30. The true error is larger. It is conservative. | Product M_edge = 208.386. The exact strip limit q·b²/12 = 208.333 (end effects below 1e-6 at 10:1). Error +2.5e-4, eight times the claim. The warning estimate (3 × last change, 1.9e-3) covers it. | Reword the spec claim: "within 3e-5 of K = 30; about 2.5e-4 above the exact value". |
| P1-struct.plate-bending-04 | Low | `M_edge` has different sign conventions. Circular plates give the signed moment. Clamped rectangles give the larger absolute value. | Circular clamped, q 20 kPa, a 250: M_edge = −156.25 N·m/m. Rectangle clamped 2:1: M_edge = +207.16. | Use one convention, or name the sign in the label. |
| P1-struct.plate-bending-05 | Info | A ring load with a radius below the plate thickness gives no warning. A patch gets `patch-thin`. The Kirchhoff stress is conservative there. | Circular clamped, a 200, t 20, ring r 2 mm, 10 kN: no notice. | Optional: add a `ring-thin` warning. |
| P1-struct.plate-bending-06 | Info | Below span/t = 10 the thin-plate result is unconservative. The sheet warns only. My Mindlin estimate (not run in the product): simply supported circular plate, shear adds about 4.5 % to deflection at span/t = 10 and about 18 % at 5. | Probe: span/t = 9.9 and 2 give the warning and a result. | None needed. Optionally quote the size of the error in the warning. |
| P1-struct.plate-bending-07 | Info | The Unconfirmed clamped KATs are supported by two independent bases. The clamped square agrees with the Roark coefficients I recall. | Beam-function Ritz: w 0.080318 mm, M_x_c 57.263, M_edge 128.334. Frequency λ² 35.985. | Track S may mark ka-rect-clamped-square and ka-rect-clamped-natural as confirmed after the owner checks the book. |

## Notes for the owner's manual test
- Circular clamped plate, a 250, t 5, q 20 kPa, steel E 200 GPa, ν 0.3: expect w = 0.5332 mm, σ = 37.5 MPa at the edge, f1 = 198.68 Hz.
- Square simply supported, 500 × 500 × 8, 10 kPa, E 210 GPa: expect w = 0.2579 mm (0.00406·q·a⁴/D). Centre stress 11.2 MPa. The corner twist governs at 13.19 MPa.
- Clamped square, same data: expect w = 0.08032 mm and edge moment 128.33 N·m/m (0.0513·q·a²). Expect the `ritz-approx` warning.
- Clamped 5000 × 500 × 8 at 10 kPa: edge moment should be close to q·b²/12 = 208.3 N·m/m. The sheet gives 208.4.
- Raise the load until w/t passes 0.5 and check the warning. Try a patch of 24 % on a clamped plate and expect a block.
