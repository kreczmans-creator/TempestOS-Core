# mech.snap-fit — independent re-derivation (Track P, P1)
Calculator: Snap-fits: cantilever, annular, torsion, version 1.2.0. Source files: src/Tempest.Calc/Tempest.Calc.Mechanical/SnapFitCalculator.cs and SnapFitGeometry.cs. Spec: docs/calcs/mechanical/mech.snap-fit.md.
Script: tools/verification/mech.snap-fit.py (exit 0 = all KATs match).
Verdict: MATCH WITH FINDINGS

All 79 KAT values match my values to better than 1e-10. 81 extra probe values match to better than 1e-9. One guard is missing: large tip deflection (Medium). Three Low or Info items follow.

## Method re-derived
Sources: Covestro (Bayer), Snap-Fit Joints for Plastics: A Design Guide, cantilever, annular and torsion sections (relations only, no text copied). Euler-Bernoulli beam theory. Thin-ring hoop mechanics. Torsion of a solid round bar. Units: mm, N, MPa, N·m.
- Constant section: ε = 1.5·h·y/l², so K_y = 2/3 (the guides print 0.67, 0.5 % higher). P = b·h²·Es·ε/(6·l).
- Tapered thickness and tapered width: I integrated M·m/(E·I) numerically (scipy quad), and again by double integration of the curvature. No closed form is used.
  - Thickness taper: strain shape (1−t)/(1−a·t)². The peak is at t* = (2a−1)/a when a > 1/2. K_y = 2·J/φ_max. For h/2 the factor is 1.0904 (guides: 1.09).
  - Width taper: strain is largest at the root. K_y = 2·J. For b/4 the factor is 0.8552 (guides: 0.86).
- Annular, thin free ring: ε_nom = ΔD/D. Inner surface ΔD/(D−h). Checked strain is the larger of K_ann·ε_nom and the inner value. Force P = K_F·2π·Es·ε_nom·h·b. I checked P by strain energy.
- Torsion bar: φ = y/a, γ = φ·r/l_eff, ε = γ/(1+ν), G = Es/(2(1+ν)), T = n·G·(π r⁴/2)·φ/l_eff, P = T/a. Held at both ends: l_eff = l/2 and n = 2. I checked P by strain energy.
- Joining forces: W = P·(μ + tan α)/(1 − μ·tan α). Self-locking when α′ ≥ arctan(1/μ). Jam when μ·tan α ≥ 1.
- Secant modulus: linear interpolation of Es(ε), end values held outside the points.

## Known-answer cases
All values of a case were compared. The table shows the largest relative difference of the case and one headline value.
| Case | Symbol (values compared) | KAT expected | Independent | Rel. diff | KAT tol | Result |
|---|---|---|---|---|---|---|
| ka-constant | P (8) | 8.53333333 N | 8.53333333 N | 2.5e-12 | 1e-9 | PASS |
| ka-taper-thickness | K_y (8) | 1.09035489 | 1.09035489 | 3.9e-11 | 1e-9 | PASS |
| ka-taper-thickness-deep | phi_max (9) | 1.19047619 | 1.19047619 | 1.6e-11 | 1e-9 | PASS |
| ka-taper-width | K_y (7) | 0.855198329 | 0.855198329 | 1.1e-11 | 1e-9 | PASS |
| ka-annular | P (9) | 1507.96447 N | 1507.96447 N | 2.5e-12 | 1e-9 | PASS |
| ka-annular-inner-surface | eps (8) | 1.48148148 % | 1.48148148 % | 2.5e-12 | 1e-9 | PASS |
| ka-annular-force-factor | P (6) | 1884.95559 N | 1884.95559 N | 2.1e-12 | 1e-9 | PASS |
| ka-torsion | T_bar (11) | 0.136590985 N·m | 0.136590985 N·m | 3.4e-12 | 1e-9 | PASS |
| ka-torsion-both-ends | T_bar (9) | 0.546363940 N·m | 0.546363940 N·m | 8.0e-13 | 1e-9 | PASS |
| ka-curve | Es (4) | 2100 MPa | 2100 MPa | 0 | 1e-9 | PASS |

The KAT values agree with physics and with the guide relations I know. The derived factors 1.09 and 0.86 match the guide values as I remember them. That memory is not a pinned source. The owner should confirm both, and the 0.67, against the printed guide.

## Extra probes (product vs independent)
Product values are hard-coded in the script. Every row agrees to 1e-9 or better unless noted.
| Probe | Inputs (short) | Symbol | Product | Independent | Rel. diff | Result |
|---|---|---|---|---|---|---|
| X1 | l 12, h 1.2, b 5, y 0.8, Es 1800, μ 0.4, α 25°, α′ 70° | P / W | 1.8 N / 1.9169 N | same | 1e-11 | PASS |
| X1 | same, α′ above arctan(1/μ) = 68.2° | W′ | self-locking (infinite) | infinite | - | PASS |
| X2 | thickness taper r = 0.49, 0.975, 0.99, 1.0 (seam at a = 0.02) | K_y | 1.10471, 0.67942, 0.67171, 0.66667 | same | 1e-10 | PASS |
| X3 | thickness taper r = 0.05 (hard minimum) | φ_max / t* / K_y | 5.2632 / 0.9474 / 0.70670 | same | 5e-11 | PASS |
| X4 | width taper r = 0.02, 0.5, 0.99 | K_y | 0.98208, 0.77259, 0.66834 | same | 4e-11 | PASS |
| X5 | μ = 1, α = 44.9° (just below jam), α = 45° | W | 4889.2 N, then "jams" | 4889.2 N, then infinite | 6e-11 | PASS |
| X6 | μ = 0.5, α′ = 63.43° and 63.44° | W′ | 98 796 N, then self-locking | same | 4e-11 | PASS |
| X7 | μ = 0, α′ = 89.9° and 90° | W′ | 4889.2 N, then self-locking | same | 6e-11 | PASS |
| X8 | y = 0, y = 20 mm (l 15, h 2) | ε / u | 0 / 0 and 26.67 % / 6.67 | same | 1e-10 | PASS |
| X9 | Es points, strain below, above, unsorted with a zero point | Es | 2600, 1800, 2533.33 MPa | same | 1e-10 | PASS |
| X10 | ring D 40, ΔD 0.5, h 2, b 8, K_ann 1 | ε_inner / P / ΔD_perm | 1.3158 % / 1884.96 N / 1.14 mm | same | 2e-10 | PASS |
| X11 | ring D 5, h 3 (thick) and K_ann 2, K_F 0.5 | ε / P | 5 %, 4523.9 N and 2.5 %, 942.48 N | same | 4e-11 | PASS |
| X12 | torsion l 25, r 1.8, a 9, y 2, ν 0.4, one end | T / P / y_perm | 0.094226 N·m / 10.4696 N / 6.125 mm | same | 2e-10 | PASS |
| X13 | same, both ends, ν 0.45 | T / P | 0.363908 N·m / 40.4342 N | same | 1e-10 | PASS |
| X14 | torsion with Es points, ν = −0.5 and ν = 0.499 | P | 10.8296, 29.3148, 9.7781 N | same | 4e-10 | PASS |
| X15 | l 20, h 1.2, b 6, y 8.2, Es 1800, ε_perm 4 % | ε / P | 3.69 % / 4.782 N | 3.69 % / 4.782 N (small-deflection) | 0 | PASS, see -01 |

Guards behave as the spec says: slender at l/h < 5, thick-ring at h/D > 0.1, small-angle above 20°, es-range, assembly-jam, self-locking, missing inputs, hard limits.

## Implementation comparison
| Aspect | Spec / textbook | Implementation | Agrees? |
|---|---|---|---|
| Cantilever K_y | 2/3 (guides 0.67) | 2/3, noted in the step text | Yes |
| Taper integrals | Euler-Bernoulli, J closed form | Closed form for a ≥ 0.02, Simpson below | Yes |
| Peak strain in deep taper | t* = (2a−1)/a | Same, P uses φ_max | Yes |
| Deflection force | b·h²·Es·ε/(6·l·φ_max) | Same | Yes |
| Annular strain and force | max(K_ann·ε_nom, inner), force from ε_nom | Same | Yes |
| Torsion | γ = φ·r/l_eff, T with n | Same | Yes |
| γ_perm | ε_perm·(1+ν) | Same (maximum shear strain basis) | Yes, see -03 |
| Es(ε) lookup strain | design strain, ring: ε_nom | eps, ring: eps_nom | Yes |
| Joining forces and jam | μ·tan α ≥ 1 | Tolerance 1e-9 | Yes |
| Self-locking | α′ ≥ arctan(1/μ), π/2 at μ = 0 | Tolerance 1e-12 | Yes |
| Units | mm, N, MPa, N·m | SI inside, display units correct | Yes |
| Large tip deflection | Spec says small deflection | No guard | No, see -01 |
| Allowable strain by material | Brief asks for it | Not shipped, user input (Addendum B §B5) | By design |

## Findings
| ID | Severity | Finding | Evidence | Suggested fix (for Track S) |
|---|---|---|---|---|
| P1-mech.snap-fit-01 | Medium | No guard for large tip deflection. Beam theory is linear. The strain and the force are both under-predicted when y/l is large. | l 20, h 1.2, b 6, y 8.2, Es 1800, ε_perm 4 %: product ε = 3.69 %, u = 0.92, P = 4.78 N, no warning. Exact elastica (fixed-direction tip load) gives ε about 4.00 % (+8.5 %), u about 1.0, P about 5.8 N (+22 %). At y/l = 0.3: strain +4 %, force +10 %. At y/l = 0.2: +1.8 %, +4.3 %. | Warn when y/l > 0.3 and state the error. Or apply an elastica correction. Name the limit in the sheet text. |
| P1-mech.snap-fit-02 | Low | In torsion mode the output "eps" is labelled "Maximum strain". It is the equivalent tensile strain γ/(1+ν), not the surface strain. | ka-torsion: eps = 1.087 % while γ = 1.5 %. | Relabel to "Equivalent tensile strain" for the torsion mode. |
| P1-mech.snap-fit-03 | Low | The separation force grows without limit as α′ approaches arctan(1/μ). There is no warning before the lock angle. | P = 8.53 N, α′ = 63.43°. W′ = 102 N at μ 0.40, 209 N at μ 0.45, 526 N at μ 0.48, 98 796 N at μ 0.50 (0.005° below the lock). | Warn when 1 − μ·tan α′ < 0.1: the separation force is very sensitive to μ. |
| P1-mech.snap-fit-04 | Info | γ_perm = ε_perm·(1+ν) is the maximum shear strain basis. A von Mises basis gives 1.155 times more. The sheet is conservative by 15 % for the torsion bar. | Pure shear: equivalent strain 0.866·γ/(1+ν). | None needed. State the basis in the step text. |
| P1-mech.snap-fit-05 | Info | The annular strain uses the mean diameter. The guide may use another diameter. The inner-surface check covers the difference on the safe side. | ka-annular-inner-surface. | Owner to compare with the printed guide. |

## Notes for the owner's manual test
- Constant section: l 15, h 2, b 6, y 1, Es 2400, ε_perm 4 %, μ 0.3, α 30°, α′ 60°. Expect ε = 1.3333 %, P = 8.5333 N, W = 9.0551 N, W′ = 36.096 N, arctan(1/μ) = 73.30°.
- Tip thickness h/2 gives K_y = 1.0904. Tip width b/4 gives K_y = 0.8552. Compare with your printed guide (1.09 and 0.86). The constant section uses 2/3, not 0.67.
- Set α′ = 75° with μ = 0.3. Expect "self-locking (infinite)" and a warning. Set μ = 1 and α = 45°. Expect "jams".
- Try y/l above 0.3 and compare with a hand estimate or a finite-element run. The sheet will be low (finding -01).
- Annular, K_ann 1.2, D 30, ΔD 0.4, h 1.5, b 5, Es 2400: expect ε = 1.6 %, P = 1507.96 N. Torsion bar, one end versus both ends: the ends-held bar has twice the strain and four times the force.
