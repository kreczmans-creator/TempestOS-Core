# bs.pipe-pump — independent re-derivation (Track P, P1)
Calculator: Pipe pressure drop and pump duty, version 1.0.0 (re-checked at 1.1.0, see the last section). Source file: src/Tempest.Calc/Tempest.Calc.BuildingServices/PipeFlowCalculator.cs (helpers in Fluids/Water.cs). Spec: docs/calcs/building-services/bs.pipe-pump.md.
Script: tools/verification/bs.pipe-pump.py (exit 0 = all KATs match; 86 rows at e283c07e).
Verdict: MATCH WITH FINDINGS (after independent review: 3 High, 3 Medium open)

The checkout was on branch claude/tempestos-v0260-test-readiness-s4jwh1, not release/v0.26.0. I did not switch branches, as the brief forbids it.

## Method re-derived
- Velocity v = Q / (πD²/4) in m/s; Re = ρ v D / μ. Source: Çengel and Cimbala, Fluid Mechanics, Ch. 8.
- Darcy–Weisbach: h_f = f (L + L_eq)/D · v²/2g + K v²/2g, in m, with g = 9.80665 m/s².
- Laminar f = 64/Re for Re < 2300. Colebrook–White solved to full convergence (scipy brentq) above that.
- Swamee–Jain explicit form f = 0.25 / [log10(ε/3.7D + 5.74/Re^0.9)]² as the cross-check.
- Pressure drop Δp = ρ g h_f; Hagen–Poiseuille Δp = 32 μ L v / D² checked directly for the laminar KAT.
- System curve H = H_static + k Q², with k = Σh_f / Q² at the design flow. H_static = Σdz + h_out.
- Duty point: intersection of the piecewise-linear pump curve with the system curve. Shaft power = ρ g Q H / η.
- Affinity: Q ∝ n D³, H ∝ n² D² (as in the spec).
- NPSH_a = p_atm/ρg + z_s − h_f,suction − p_v/ρg, in m.
- Water: Kell (1975) density; IAPWS viscosity values (log-spline through 0–100 °C points); Wagner–Pruss (2002) saturation pressure. These are different sources from the product's viscosity and vapour-pressure forms.

## Known-answer cases
| Case | Symbol | KAT expected | Independent | Rel. diff | KAT tol | Result |
|---|---|---|---|---|---|---|
| ka-laminar-oil | v_s1 | 0.14147 | 0.141471 | 7.5e-06 | 1e-4 | PASS |
| ka-laminar-oil | Re_s1 | 7.852 | 7.85164 | 4.5e-05 | 1e-3 | PASS |
| ka-laminar-oil | f_s1 | 8.151 | 8.15116 | 2.0e-05 | 1e-3 | PASS |
| ka-laminar-oil | h_f_total | 6.654 | 6.65417 | 2.6e-05 | 1e-3 | PASS |
| ka-laminar-oil | dp_total | 57.946 | 57.9465 | 9.4e-06 | 1e-3 | PASS |
| ka-turbulent-water | v_s1 | 1.0 | 1.0 | 4e-09 | 1e-6 | PASS |
| ka-turbulent-water | Re_s1 | 1e5 | 1e5 | 4e-09 | 1e-6 | PASS |
| ka-turbulent-water | f_s1 | 0.022175 | 0.0221745 | 2.1e-05 | 5e-4 | PASS |
| ka-turbulent-water | h_f_total | 1.1306 | 1.13059 | 1.2e-05 | 1e-3 | PASS |
| ka-turbulent-water | f_sj_s1 | 0.022342 | 0.0223424 | 1.8e-05 | 1e-3 | PASS |
| ka-duty-point | H_static | 10 | 10 | 0 | 1e-9 | PASS |
| ka-duty-point | H_duty | 17.67 | 17.6674 | 1.5e-04 | 1e-2 | PASS |
| ka-duty-point | Q_duty | 46.3 | 46.3326 | 7.0e-04 | 1e-2 | PASS |
| ka-duty-point | Q_duty2 | 40.1 | 40.1056 | 1.4e-04 | 1e-2 | PASS |

KATs matched: 3 of 3 cases (14 of 14 expected values). The product's own KAT run also passes 3 of 3. No KAT value disagrees with physics.
The spec text calls the turbulent point "Moody point f = 0.02237". Colebrook gives 0.022175 (see P1-bs.pipe-pump-05).

## Extra probes (product vs independent)
Full rows are in the script. Product values were read from the Probe tool. Water-property tolerance is 0.2 to 1 %.
| Probe | Inputs (short) | Symbol | Product | Independent | Rel. diff | Result |
|---|---|---|---|---|---|---|
| A | 60 °C, 2 segments, suction + discharge, n 0.9, D 0.95 | h_f_total (m) | 16.5235 | 16.5242 | 4e-05 | PASS |
| A | same | Q_duty / Q_duty2 (m³/h) | 56.729 / 39.828 | 56.728 / 39.828 | 1e-05 | PASS |
| A | same | P_shaft2 (kW) | 5.9054 | 5.9054 | 6e-06 | PASS |
| A | same, p_atm 98 kPa, z_s 2 m | NPSH_a (m) | 9.6078 | 9.6075 | 4e-05 | PASS |
| B | 100 °C, z_s 0, NPSH_r 3 m | NPSH_a (m) | -0.5287 | -0.5285 | 4e-04 | PASS (check fails, correct) |
| B2 | 95 °C, lift 4 m | NPSH_a (m) | -2.7477 | -2.7477 | 3e-07 | PASS |
| C1–C5 | custom fluid, Re 2299, 2301, 3999, 4001, 1000 | f, h_f | see script | exact | below 1e-9 | PASS |
| D | Q = 0.01 m³/h, DN25 | f (laminar) | 0.45480 | 0.45393 | 1.9e-03 | PASS (viscosity fit) |
| E | Q = 1e5 m³/h, DN2000 | h_f (m) | 41.314 | 41.314 | 1.6e-05 | PASS |
| H | 0 °C water | f | 0.018872 | 0.018909 | 2.0e-03 | PASS |
| I | smooth pipe, Re 35 180 | f | 0.022627 | 0.022617 | 4.4e-04 | PASS |
| U | ε = 5 mm, L_eq 25 m | h_f (m) | 17.461 | 17.461 | 4e-06 | PASS |
| S | p_atm 90 kPa, suction K = 3 | NPSH_a (m) | 11.5535 | 11.5535 | 2e-06 | PASS |
| T | 80 °C, lift 2 m | NPSH_a (m) | 3.2787 | 3.2783 | 1e-04 | PASS |
| V | n = 1.3 | Q_duty2 (m³/h) | 62.800 | 62.801 | 3e-05 | PASS |
| J | D ratio 0.8 | Q_duty2 / P_shaft2 | 22.996 m³/h / 1.0912 kW | 22.996 / 1.0911 (D³ law) | 1e-05 | PASS |
| J | D ratio 0.8, trim law Q ∝ D, H ∝ D² | Q_duty2 / P_shaft2 | 22.996 / 1.0912 | 32.999 / 1.8665 | 30 % / 42 % | FINDING 02 |
| W | oil, Re 159, Q² system curve | Q_duty (m³/h) | 5.467 | 5.467 (same law) | 4e-07 | PASS |
| W | oil, exact laminar h_f ∝ Q | Q_duty / power | 5.467 / 145 | 5.878 / 154 | 7 % / 5.5 % | FINDING 03 |
| R | pump curve crosses system curve twice | duty point | "no crossing" | 2 crossings, 41.5 m³/h | n/a | FINDING 04 |

## Implementation comparison
| Aspect | Spec / textbook | Implementation | Agrees? |
|---|---|---|---|
| Darcy–Weisbach with K and L_eq | h_f as above | same form, g = 9.80665 | Yes |
| Laminar limit | f = 64/Re below 2300 | Re < 2300 laminar, warned | Yes |
| Colebrook | converged solution | fixed point from Swamee–Jain, tolerance 1e-12, cap 100 | Yes (matches brentq to 1e-10) |
| Transition 2300–4000 | warn | warns; Colebrook used | Yes (f jumps 73 % at Re 2300, see 06) |
| Water density | Kell 1975 | same formula | Yes |
| Water viscosity | IAPWS | Vogel fit, 0.0294 exp(507.88/(T−149.3)) | Within 0.93 % (see 08) |
| Vapour pressure | Hyland–Wexler over water | same coefficients | Yes (within 0.02 % of Wagner–Pruss) |
| System curve | H_static + k Q² | same | Yes; invalid for laminar flow (03) |
| Duty point | intersection | bisection on end-point sign change | Fails for two crossings (04) |
| Affinity | Q ∝ n D³, H ∝ n² D² | same | Matches the spec; D³ is not the trim law, which the input label implies (02) |
| NPSH_a | formula above | same, suction segments only, at the design flow | Yes for water; absent for custom fluid (01); no friction if no segment is flagged (R1); not at duty flow (R2) |
| NPSH check | NPSH_r ≤ NPSH_a | utilisation NPSH_r/NPSH_a | Pass/fail correct; value negative if NPSH_a < 0 (07) |
| Water range guard | 0–100 °C | blocks outside | Yes |
| Units | m³/h, mm, kPa, m | converted to SI; outputs correct | Yes |

## Findings
| ID | Severity | Reviewed | Finding | Evidence | Suggested fix (for Track S) | Status @ e283c07e |
|---|---|---|---|---|---|---|
| P1-bs.pipe-pump-01 | Medium | High. Reproduced (probe G). Glycol circuits use the custom fluid. The NPSH check vanishes silently while the sheet still passes. | A custom fluid with NPSH_r entered gives no NPSH_a, no NPSH check and no notice. The user may assume the check passed. | Probe G: custom ρ 1050, μ 3 mPa·s, z_s −8 m, NPSH_r 3 m. Outputs contain no NPSH_a and no NPSH check. | Add a vapour-pressure input for custom fluids, or warn that NPSH is not computed when NPSH_r is given. | Open. Probe G: a custom fluid with NPSH_r 3 m still gives no NPSH_a, no NPSH check and no notice. |
| P1-bs.pipe-pump-02 | Medium | High. Reproduced (probe J). The input is labelled for impeller diameter, so a trim is plausible use. Shaft power is 42 % low; a motor sized from it is too small. Owner to confirm the trim law to adopt (Q ∝ D or Q ∝ D²); both give more than Q ∝ D³. | D_ratio applies Q ∝ D³ and H ∝ D². That is the law for geometrically similar pumps. The label says "impeller diameter", and users will apply it to a trimmed impeller (Q ∝ D, H ∝ D²). The shaft power is then under-predicted. | Probe J, D ratio 0.8: product Q_duty2 22.996 m³/h and P_shaft2 1.091 kW; trim law gives 32.999 m³/h and 1.866 kW. | Relabel as "similar-pump scale ratio", or add a choice for the trim law. State the law on the output. | Open. Probe J: D ratio 0.8 still gives Q_duty2 22.996 m³/h and P_shaft2 1.091 kW (Q ∝ D³). |
| P1-bs.pipe-pump-03 | Medium | Medium (confirmed). Reproduced (probe W). Laminar pumping is an edge case in building services. | The system curve scales friction with Q². This is wrong for laminar flow, where h_f ∝ Q. The laminar warning does not mention it. The duty flow is 7 % low and shaft power 5.5 % low (unconservative). In turbulent flow the error is small (0.3 % on Q_duty for the KAT case). | Probe W: oil μ 200 mPa·s, Re 159, DN50, 100 m. Product Q_duty 5.467 m³/h; exact 5.878 m³/h. | Scale laminar segments with Q¹, or extend the laminar warning: "the system curve and duty point are approximate". | Open. Probe W: oil at Re 159 still gives Q_duty 5.467 m³/h from the Q² law. |
| P1-bs.pipe-pump-04 | Medium | Medium (confirmed). Reproduced (probe R). Two crossings give a false FAIL, which is safe. Three crossings give an arbitrary root. | The duty point test looks only at the sign at the two curve ends. A curve that crosses the system curve twice is reported as "no crossing" and the check fails. With three crossings the root found is arbitrary. | Probe R: pump points (0,8), (10,14), (40,18), (70,2); static 10 m. The independent scan finds crossings at about 3.4 and 41.5 m³/h. The product warns "no-duty". | Scan the curve for all sign changes. Report the stable crossing, or list all and warn when there is more than one. | Open. Probe R: two crossings still give the "no-duty" warning and a failed duty check. |
| P1-bs.pipe-pump-05 | Low | Low (confirmed) | The spec says the turbulent KAT is "Moody point f = 0.02237". Colebrook gives 0.022175 at Re 1e5 and ε/D 0.001. The KAT value is right; the spec text is wrong. The duty KAT note says f ≈ 0.0198; the value is 0.0199. | Script ka-turbulent-water: independent f = 0.0221745. | Correct the spec text. | Open. The spec text still says "Moody point f = 0.02237" for the turbulent case. |
| P1-bs.pipe-pump-06 | Info | Info (confirmed) | The friction factor jumps 73 % at Re = 2300 (0.0278 to 0.0480), so Δp jumps from 26.2 to 45.2 kPa. Both sides carry a warning. | Probes C1 and C2. | None needed. Optionally note the step in the laminar and transition warnings. | Open (Info). The friction code is unchanged, so the step at Re 2300 remains. |
| P1-bs.pipe-pump-07 | Low | Low (confirmed) | When NPSH_a is negative, the utilisation shows a negative number (u = −5.67). The pass/fail result is correct (FAIL). | Probe B: 100 °C, NPSH_r 3 m, NPSH_a −0.53 m. | Show the utilisation as infinity or n/a when NPSH_a ≤ 0, and warn that the fluid is at its boiling point. | Open. Probe B: 100 °C still gives NPSH_a −0.529 m and u −5.674. |
| P1-bs.pipe-pump-08 | Info | Info (confirmed) | The Vogel viscosity fit differs from IAPWS by −0.93 % at 0 °C, +0.19 % at 20 °C and +0.69 % at 100 °C. The effect on h_f is under 0.3 %. | Script viscosity comparison. | None needed. | Open (Info). The viscosity fit in Water.cs is unchanged. |
| P1-bs.pipe-pump-09 | Low | Low (confirmed) | The notice for the defaulted p_atm shows "101.3 kPa", but 101.325 kPa is used. Display only. | Probe C1 notice list. | Show the default with three decimals. | Open. Probe C1: the notice still shows 101.3 kPa for the default p_atm. |
| P1-bs.pipe-pump-R1 | High | High (new in review) | NPSH_a includes only segments marked "suction". The side column is optional and blank means discharge. With NPSH_r given and no suction segment, NPSH_a omits all friction. There is no notice. | Water 60 °C, 30 m³/h, one 60 m DN65 segment (ΣK 6), lift 3 m, NPSH_r 3 m. Side blank: NPSH_a 5.44 m, check passes. Side = suction: NPSH_a −2.15 m, check fails. | Warn when NPSH_r is given and no segment is marked suction. Consider making the side column required. | Open. Probe: 60 m suction run, side blank, still gives NPSH_a 5.440 m and a pass. Marked suction it gives −2.151 m. |
| P1-bs.pipe-pump-R2 | Medium | Medium (new in review) | NPSH_a uses suction friction at the design flow Q. When a pump curve is entered, the pump runs at Q_duty. If Q_duty exceeds Q, NPSH_a at duty is lower and the check is unconservative. | Water 60 °C, design 30 m³/h, 10 m DN65 suction (K 4), lift 3 m, pump (0,30) (20,28) (40,22) (60,10). Duty 38.54 m³/h. Product NPSH_a 3.21 m passes against 3 m. At duty flow NPSH_a is 1.78 m and fails. | When a duty point exists, also compute NPSH_a at Q_duty and check the larger flow. Say which flow NPSH_r refers to. | Open. Probe: NPSH_a is still taken at the design flow (3.210 m) while the duty flow is 38.54 m³/h. |
| P1-bs.pipe-pump-C1 | Low | Low (confirmed). Reproduced: no T_shaft, n_shaft or P_shaft_design and no notice. A link to E1 is refused loudly, so nothing unsafe passes. | Shaft torque and speed need n_pump. Without it the sheet offers no T_shaft, n_shaft or P_shaft_design, and no notice says so. A link to E1 then has nothing to bind. | Probe: 594.5 W, 10 K, 70 °C, no n_pump. Q_design and P_hyd appear. T_shaft and n_shaft are absent. No notice. | Warn when a heat load is given and n_pump is blank. Or say on the sheet that torque needs n_pump. | Open (new). |
| P1-bs.pipe-pump-C2 | Medium | Medium (confirmed). Unconservative in normal use for small pumps: the default η 0.7 lowers the torque handed to E1. A default notice names it. Circulator efficiency values: owner to confirm. | Shaft power and torque use eta_pump, default 0.7, for any pump size. Small circulators have far lower overall efficiency (recalled; owner to confirm). The torque handed to E1 is then low. | Pack reference case: eta 0.7 gives T_shaft 2.037e-3 N·m. Eta 0.2 gives 7.13e-3 N·m, 3.5 times more. | Warn when the default eta_pump is used and the shaft power is under about 1 kW. Or require eta_pump with n_pump. | Open (new). |

## Notes for the owner's manual test
- Water 20 °C, DN80, 60 m, ε 0.045 mm, ΣK 8, rise 10 m, 30 m³/h: expect f 0.0199, h_f 3.21 m, duty 46.3 m³/h at 17.67 m.
- Custom fluid ρ 1000, μ 1 mPa·s, DN100, ε 0.1 mm, 28.274 m³/h: expect Re 1e5, f 0.022175 and h_f 1.1306 m per 100 m.
- Put water at 100 °C on a suction line with NPSH_r 3 m: expect NPSH_a near −0.5 m and a failed check.
- Enter oil with Re below 2300 and a pump curve: expect the duty flow to read about 7 % low (finding 03).
- Enter a D ratio of 0.8 and compare the new duty flow with the trim law (finding 02).

## Independent review
- Reviewer: senior reviewer (Claude), 2026-10-24.
- I re-ran the script (exit 0) and read it. It is independent: Colebrook by brentq, Kell density, IAPWS viscosity points, Wagner–Pruss vapour pressure.
- I reproduced findings 01, 02, 03 and 04 with the probe (inputs G, J, W, R). I read the Run and Duty methods line by line.
- Findings 01 and 02 are raised to High. Both let an unconservative result through in plausible use without a notice.
- Findings 03 and 04 stay Medium.
- New finding R1 (High): NPSH_a ignores all friction unless a segment is flagged suction.
- New finding R2 (Medium): NPSH_a is evaluated at the design flow, not at the duty flow.
- The script now carries rows for R1 and R2 (69/69 rows pass, exit 0).
- Residual doubt: the trim law for finding 02 is a practice choice. The owner should state the one to adopt.

## Re-check after A7a (2026-10-24)
- Source changed: version 1.1.0 adds the heat-load flow Q = Φ/(ρ c_p ΔT). It adds n_pump with P_shaft_design, T_shaft and n_shaft, and 15 declared output ports. Q is now optional.
- Friction, duty-point, affinity and NPSH code is unchanged. I re-ran probes G, J, W, R, B, C1, R1 and R2. All eleven findings are still Open.
- Heat-load flow: the spec's case (12 kW, 20 K, 70 °C) gives 0.5277375 m³/h and matches. The pack reference case matches to 6e-11.
- Shaft torque T = P/ω matches the independent script within 4e-3. Three cases ran, with and without a pump curve.
- New defects: C1 (Low) and C2 (Medium). The closed-loop static head is recorded once, in P1-chain.wet-heating-02.
- The script now runs 86 rows (69 before) and exits 0.
