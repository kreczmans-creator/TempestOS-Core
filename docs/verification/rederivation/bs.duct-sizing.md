# bs.duct-sizing — independent re-derivation (Track P, P1)
Calculator: Duct sizing, version 1.0.0. Source file: src/Tempest.Calc/Tempest.Calc.BuildingServices/DuctSizingCalculator.cs (friction helpers in Fluids/Water.cs). Spec: docs/calcs/building-services/bs.duct-sizing.md.
Script: tools/verification/bs.duct-sizing.py (exit 0 = all KATs match).
Verdict: MATCH WITH FINDINGS

The checkout was on branch claude/tempestos-v0260-test-readiness-s4jwh1, not release/v0.26.0. I did not switch branches.

## Method re-derived
- Dry air: ρ = p/(R T) with R = 287.055 J/(kg K). Viscosity by Sutherland's law (S = 110.4 K).
- Velocity v = 4Q/(πD²) for round ducts; Re = ρ v D/μ.
- Friction rate Δp/L = f ρ v²/(2D) (Darcy–Weisbach). Laminar f = 64/Re; otherwise Colebrook–White solved to full convergence (brentq).
- Equal friction: D solved so that Δp/L equals the target. Velocity method: D = √(4Q/(πv)).
- Rectangular equivalent diameter D_e = 1.30 (ab)^0.625/(a + b)^0.25. Flat oval D_e = 1.55 A^0.625/P^0.25 with A = πb²/4 + b(a − b) and P = πb + 2(a − b). Source: ASHRAE Handbook, Fundamentals, 2017, Ch. 21.
- Friction in a rectangular or oval duct is the friction in a round duct of diameter D_e at the same flow, so v_e = 4Q/(πD_e²).
- Fitting loss = ΣC ρ v²/2 at the actual section velocity Q/A. Section loss = friction + fitting + equipment.
- Sizes rounded up to a multiple of the user step. Index-run total = sum over flagged sections.

## Known-answer cases
| Case | Symbol | KAT expected | Independent | Rel. diff | KAT tol | Result |
|---|---|---|---|---|---|---|
| ka-round-given | v_s1 | 7.958 | 7.95775 | 3.2e-05 | 1e-3 | PASS |
| ka-round-given | f_s1 | 0.01714 | 0.017137 | 1.9e-04 | 5e-3 | PASS |
| ka-round-given | dpL_s1 | 1.633 | 1.63334 | 2.1e-04 | 5e-3 | PASS |
| ka-round-given | dp_index | 16.33 | 16.3334 | 2.1e-04 | 5e-3 | PASS |
| ka-rectangular-equivalent | De_s1 | 457.3 | 457.012 | 6.3e-04 | 2e-3 | PASS |
| ka-rectangular-equivalent | v_s1 | 4.444 | 4.44444 | 1.0e-04 | 1e-3 | PASS |
| ka-rectangular-equivalent | dpL_s1 | 0.556 | 0.55875 | 4.9e-03 | 1e-2 | PASS (half of tol) |
| ka-velocity-method | D_s1 | 350 | 350 | 0 | 1e-9 | PASS |
| ka-velocity-method | v_s1 | 5.197 | 5.1969 | 2.0e-05 | 1e-3 | PASS |
| ka-velocity-method | dp_fit_s1 | 19.47 | 19.512 | 2.2e-03 | 5e-3 | PASS |

KATs matched: 3 of 3 cases (10 of 10 expected values). The product's own run is the same. Three expected values are loose; see finding 02.

## Extra probes (product vs independent)
All rows are in the script. Product values were read from the Probe tool. Differences are below 1e-5 unless stated (the two Sutherland forms differ by 5e-5).
| Probe | Inputs (short) | Symbol | Product | Independent | Rel. diff | Result |
|---|---|---|---|---|---|---|
| P1 | round, 0.3 m³/s, 1.0 Pa/m, step 50 | D, Δp/L | 300 mm, 0.72275 Pa/m | same | 8e-06 | PASS |
| P2, P17 | same, step 10 and step 1 | D | 290 mm; 281 mm | same | 0 | PASS |
| P3 | rect 0.8 m³/s, a = 500, b solved | b, D_e, Δp/L | 300 mm, 419.98 mm, 0.8468 Pa/m | same | 7e-06 | PASS |
| P4 | rect, a blank | a × b | 650 × 250 mm | same rule | 0 | PASS (rule is a product choice, 03) |
| P5, P15 | flat oval 800 × 400; given as 400 × 800 | D_e, Δp/L | 591.50 mm, 0.15823 Pa/m | same | 8e-06 | PASS |
| P5b | oval, a = 600, b solved | b, D_e | 300 mm, 443.62 mm | same | 1e-10 | PASS |
| P6c | velocity method, rect, a = 500, 6 m/s, step 10 | b; v | 190 mm; 5.263 m/s | 170 mm; 5.882 m/s (area rule) | 12 %; 10 % | FINDING 01 |
| P6d | velocity method, oval, a = 500, step 10 | b; v | 200 mm; 5.470 m/s | 190 mm; 5.730 m/s | 5 %; 5 % | FINDING 01 |
| P7 | D lands exactly on 350 mm | D | 350 mm | 350 mm | 0 | PASS |
| P8, P8b | Q = 0.001 (Re 211) and 0.05 m³/s (Re 10 568) in 400 mm | f | 0.30279; 0.030790 | 0.30280; 0.030791 | 5e-05 | PASS |
| P9 | 40 °C, 80 kPa | Δp/L | 1.2669 Pa/m | 1.2669 | 7e-06 | PASS |
| P13, P13b | ε = 0 and 3 mm | f | 0.015469; 0.034815 | same | 1e-05 | PASS |
| P14, P14b | −20 °C and 200 °C | Δp/L | 1.8325; 1.1444 Pa/m | same | 8e-06 | PASS |
| P10 | 100 m³/s at 0.3 Pa/m | D | 3250 mm | 3250 mm | 0 | PASS |
| P11 | 3 sections, 2 on index run | dp_index, dp_all | 60.770; 130.246 Pa | same | 2e-06 | PASS |
| P18 | rect 1000 × 100 | Δp/L | 4.4179 Pa/m | 4.4179 | 5e-06 | PASS (no aspect-ratio note, 03) |
| P12 | 400 m³/s at 0.3 Pa/m; 1e-4 m³/s at 5 Pa/m | block | engine message | D = 5508 mm; 11 mm | n/a | FINDING 04 |

Units were also checked: L/s, m³/h, cfm, in, inH₂O and kPa inputs give the same result as SI inputs.

## Implementation comparison
| Aspect | Spec / textbook | Implementation | Agrees? |
|---|---|---|---|
| Air density | p/(R T), dry | p/(287.055 T) | Yes (dry air is slightly conservative) |
| Viscosity | Sutherland | μ0 = 1.716e-5 at 273.15 K, S = 110.4 K | Yes; about 0.4 % under a reference value (06) |
| Colebrook / laminar | Re < 2300 laminar | same; warns below Re 4000 | Yes |
| Equal-friction sizing | solve D for target rate | bisection, 20 mm to 5000 mm | Yes inside that range (04) |
| Velocity method, round | D = √(4Q/πv) | same | Yes |
| Velocity method, rect and oval | area = Q/v | equivalent round D at v; b from D_e | No (01) |
| D_e rectangular and oval | ASHRAE Ch. 21 | same | Yes |
| Friction through D_e | v_e = 4Q/(πD_e²) | same | Yes |
| Reported velocity and fitting loss | actual Q/A | same | Yes |
| Rounding | up to step | ceil(x/step − 1e-9) | Yes (no false round-up at exact multiples) |
| Totals | index run | default of a blank index flag is "yes" | Yes; branches must be unflagged (05) |
| Guards | positive sizes, shape check | present; velocity and Re warnings | Yes; no aspect-ratio note (03) |

## Findings
| ID | Severity | Finding | Evidence | Suggested fix (for Track S) |
|---|---|---|---|---|
| P1-bs.duct-sizing-01 | Medium | The velocity method on a rectangular or flat-oval duct does not reach the target velocity. It sizes the equivalent round duct at the target velocity, then solves b from D_e. A rectangle of that D_e has more area than the round duct. The duct comes out about 5 to 12 % too large, with velocity below target. The error is conservative for pressure and noise, but not what the method states. | Q = 0.5 m³/s, a = 500 mm, 6 m/s, step 10 mm. Product b = 190 mm, v = 5.26 m/s. Area rule b = 170 mm, v = 5.88 m/s. Flat oval: 200 mm, 5.47 m/s against 190 mm, 5.73 m/s. | For rect and oval ducts, size b from the area Q/v. Keep D_e only for the friction calculation. |
| P1-bs.duct-sizing-02 | Low | Three KAT expected values are loose and hidden by the tolerance. De 457.3 mm: the relation gives 457.01 mm. Δp/L 0.556: the relation gives 0.5588 (0.5 %). dp_fit 19.47: the value was built with ρ ≈ 1.2015; the tool uses 1.2041 and gives 19.51. The KAT text calls 350 mm a standard size. I recall EN 1506 sizes as 315 and 400 mm (not confirmed against the standard). | Script KAT rows. | Recompute the expected values from the relations. Reword "standard size" as "next 50 mm step". |
| P1-bs.duct-sizing-03 | Low | When a is blank the tool chooses a = 1.5 D_e rounded up, with no note in the spec. When only b is given, a is still chosen by sizing. There is no warning for a high aspect ratio. A flat oval entered with a < b is swapped silently. | Probe P4: a blank gives 650 × 250. Probe P16: b = 300 given gives a = 650. Probe P18: 1000 × 100 (10:1) runs with no note. Probe P15. | Document the default aspect ratio. Warn above about 4:1 (or the limit you adopt). Note the swap. |
| P1-bs.duct-sizing-04 | Low | Equal-friction sizing searches only 20 mm to 5000 mm. Valid inputs outside that range give the block "The bracket does not contain a sign change; no root was found." | Probe P12: 400 m³/s at 0.3 Pa/m (needs 5.5 m); 1e-4 m³/s at 5 Pa/m (needs 11 mm). Both are inside the hard input limits. | Widen the bracket, or return a clear message such as "the size is outside 20 mm to 5000 mm". |
| P1-bs.duct-sizing-05 | Info | A blank "index" flag counts the section on the index run. A branch left unflagged is added to the index-run total and the pressure-drop check. This errs on the safe side. | Probe P11: dp_all differs from dp_index only when a section is unflagged. | Mention the default in the column description. |
| P1-bs.duct-sizing-06 | Info | Sutherland's law gives 18.13 µPa·s at 20 °C. A typical reference value is about 18.2 µPa·s. The effect on f is below 0.1 %. | Script row for air viscosity. | None needed. |

## Notes for the owner's manual test
- 1 m³/s in a 400 mm round duct, 10 m, ε 0.09 mm, 20 °C: expect 7.958 m/s, f 0.01714, 1.633 Pa/m, 16.33 Pa.
- Rectangular 600 × 300 at 0.8 m³/s: expect D_e 457.0 mm, 4.444 m/s and about 0.559 Pa/m.
- Velocity method, 0.5 m³/s at 6 m/s, step 50 mm, round: expect 350 mm and 5.197 m/s.
- Velocity method with a = 500 mm rectangular and step 10 mm: the tool gives b = 190 mm; area sizing gives 170 mm (finding 01).
- Enter 400 m³/s at 0.3 Pa/m with equal friction: expect the unclear engine block (finding 04).
