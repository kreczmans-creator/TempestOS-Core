# elec.motor-sizing — independent re-derivation (Track P, P1)
Calculator: Motor and actuator sizing, version 1.0.0. Source file: src/Tempest.Calc/Tempest.Calc.Electrical/MotorSizingCalculator.cs. Spec: docs/calcs/electrical/elec.motor-sizing.md.
Script: tools/verification/elec.motor-sizing.py (exit 0 = all KATs match; 109 rows: 13 KAT rows and 96 extra rows).
Verdict: MATCH WITH FINDINGS (reviewed: two High, four Medium including R1 and R2)

All 13 KAT values match. All 8 extra probes match to better than 1e-9. Two High findings concern unconservative checks for vertical and downhill loads. One Medium finding covers peak power.

## Method re-derived
- Travel per motor radian k: screw p/(2π i); belt D/(2 i); rack D/(2 i). Unit m/rad. Source: Hughes & Drury, Electric Motors and Drives, drive-selection chapter (inertia and torque referred through a ratio).
- Load force F = m g (sin θ + μ cos θ) + F_p, with g = 9.80665 m/s². The KAT value 49.03 N confirms this g.
- Reflected inertia J_refl = m k²/η (linear) or J_L/(i² η) (rotary). Load torque T_load = F k/η or T_L/(i η).
- Trapezoid: t_acc = f_acc t_move, ω_pk = θ_m/(t_move − t_acc), α = ω_pk/t_acc. Unit rad/s².
- S-curve with sine acceleration: α_pk = (π/2) α_trap, because ∫ sine over t_acc equals 2/π of the peak times t_acc.
- Torque T = J_total α + T_load, with J_total = J_refl + J_mech + J_motor. Deceleration T_dec = −J_total α + T_load.
- RMS torque T_rms = √(Σ T_i² t_i / (t_move + t_dwell)). For the sine phase I integrated the square in closed form. The cross terms cancel between acceleration and deceleration.
- Inertia ratio (J_refl + J_mech)/J_motor. The KAT confirms that J_mech is included.

## Known-answer cases
| Case | Symbol | KAT expected | Independent | Rel. diff | KAT tol | Result |
|---|---|---|---|---|---|---|
| ka-ball-screw | v_pk (m/s) | 1.5 | 1.5 | 1.5e-16 | 1e-9 | PASS |
| ka-ball-screw | n_pk (rpm) | 9000 | 9000 | 2.0e-16 | 1e-9 | PASS |
| ka-ball-screw | J_refl (kg·m²) | 1.4072e-4 | 1.40724e-4 | 2.8e-5 | 1e-4 | PASS |
| ka-ball-screw | T_acc (N·m) | 2.4923 | 2.49223 | 2.6e-5 | 1e-4 | PASS |
| ka-ball-screw | T_load (N·m) | 0.08670 | 0.086710 | 1.1e-4 | 1e-3 | PASS |
| ka-ball-screw | T_pk (N·m) | 2.579 | 2.57894 | 2.2e-5 | 1e-3 | PASS |
| ka-ball-screw | T_rms (N·m) | 1.4402 | 1.44020 | 1.3e-6 | 1e-3 | PASS |
| ka-ball-screw | J_ratio | 1.2036 | 1.20362 | 1.6e-5 | 1e-3 | PASS |
| ka-geared-index-table | n_pk (rpm) | 750 | 750 | 1.5e-16 | 1e-6 | PASS |
| ka-geared-index-table | J_refl (kg·m²) | 5.882e-3 | 5.88235e-3 | 6.0e-5 | 1e-3 | PASS |
| ka-geared-index-table | T_pk (N·m) | 5.0126 | 5.01269 | 1.8e-5 | 1e-3 | PASS |
| ka-geared-index-table | T_rms (N·m) | 2.2417 | 2.24174 | 1.9e-5 | 1e-3 | PASS |
| ka-geared-index-table | J_ratio | 11.76 | 11.7647 | 4.0e-4 | 2e-3 | PASS |

No KAT value disagrees with physics. The geared KAT inertia ratio 11.76 is the rounded form of 11.7647.

## Extra probes (product vs independent)
Product values are 10 significant figures from the probe tool. The script compares 12 symbols per case: T_load, J_refl, T_acc, T_pk, T_dec, T_rms, J_ratio, n_pk, P_pk, u_peak, u_rms, u_speed. The worst relative difference in every case is below 3e-10.

| Probe | Inputs (short) | Symbol | Product | Independent | Rel. diff | Result |
|---|---|---|---|---|---|---|
| E1 belt, trapezoid | D 100 mm, i 3, η 0.95, 20 kg, 10° up, F_p 30 N | T_rms | 1.159574 N·m | 1.159574 | <1e-9 | PASS |
| E2 rack, S-curve, downhill | D 60 mm, i 5, η 0.8, 200 kg, −20° | T_dec | −8.687705 N·m | −8.687705 | <1e-9 | PASS |
| E3 direct rotary, S-curve, f_acc 0.5 | J_L 0.02, T_L 1.5 N·m, 2 rev in 0.8 s | T_pk | 4.432897 N·m | 4.432897 | <1e-9 | PASS |
| E4 gearbox rotary | i 25, η 0.7, J_L 3, T_L 40 N·m | T_rms | 1.327121 N·m | 1.327121 | <1e-9 | PASS |
| E5 screw, vertical | 500 kg, 90°, 1 s move, 10 s dwell | T_pk | 9.636403 N·m | 9.636403 | <1e-9 | PASS |
| E6 near zero | m 0, F_p 5 N, 0.1 mm in 10 ms | T_acc | 0.848230 N·m | 0.848230 | <1e-9 | PASS |
| E7 very large | 1e5 kg, 100 m in 600 s, 100 mm lead | T_rms | 3088.313 N·m | 3088.313 | <1e-9 | PASS |
| E8 ball-screw KAT, S-curve | KAT inputs, S-curve | T_rms | 1.599383 N·m | 1.599383 | <1e-9 | PASS |

Mechanism choices screw, belt, rack, geared and direct all ran. Both profiles ran. E3 raised the triangular and inertia-ratio warnings as expected.

## Implementation comparison
| Aspect | Spec / textbook | Implementation | Agrees? |
|---|---|---|---|
| k, J_refl, T_load, v_pk, α | As above | Same formulas; unit conversions correct (mm, rpm, kW) | Yes |
| g | 9.80665 m/s² | 9.80665 | Yes |
| T_rms, trapezoid and S-curve | Closed-form integral | Same closed forms | Yes |
| Dwell torque | Holding torque m g sin θ·k when no brake holds the load | Dwell at zero torque always | No, see -01 |
| Peak torque check | Largest of \|T_pk\| and \|T_dec\| | u_peak uses T_pk only (signed) | No, see -02 |
| P_pk | Maximum of T(t)·ω(t) | T_pk·ω_pk for both profiles | Partly, see -03 |
| Efficiency on a negative load | Overhauling load: F k η | F k/η for all signs | No, see -05 |
| Missing screw lead | Block (rule "mechanism-input") | Silent default 10 mm | No, see -04 |
| Overhauling warning | Negative load torque or load driving the motor | Fires whenever T_dec < 0 | No, see -06 |
| Speed check | Spec says "with SF" | u_speed = n_pk/n_max, no SF | Differs, see -07 |

## Findings
| ID | Severity | Reviewed | Finding | Evidence | Suggested fix (for Track S) |
|---|---|---|---|---|---|
| P1-elec.motor-sizing-01 | High | High (confirmed). Probe: 500 kg vertical, 10 s dwell gives T_rms 2.625 N·m and u_rms 0.656 pass. With holding torque m·g·k = 7.80 N·m, u_rms is 1.97. | The dwell runs at zero torque. A vertical or inclined axis needs holding torque m g sin θ·k during the dwell unless a brake holds it. The RMS check is unconservative. | E5: 500 kg, θ = 90°, 1 s move, 10 s dwell. Product T_rms 2.625 N·m. With 7.804 N·m holding torque it is 7.890 N·m (3.0 times). With T_rated 5 N·m, T_peak 15 N·m the product passes u_rms 0.656. The true value is 1.97. | Add a holding torque m g sin θ·k in the dwell. Offer a "brake holds the load" flag that sets it to zero. Warn when θ ≠ 0 and t_dwell > 0. |
| P1-elec.motor-sizing-02 | High | High (confirmed). Probe: rack, 200 kg, −20°, S-curve, T_peak 6 N·m gives T_dec −7.757 N·m but u_peak 0.096 pass. SF·\|T_dec\|/T_peak is 1.62. A downward move on a vertical axis is normal use. | The peak-torque check uses T_pk only. With a negative load torque (downhill or overhauling) \|T_dec\| is larger and is ignored. P_pk also ignores braking power. | E2: T_pk 0.0078 N·m, T_dec −8.688 N·m. With T_rated 5.5 and T_peak 6 N·m all three checks pass (u_peak 0.0016). The true value is SF·8.688/6 = 1.81. P_pk shows 1.4 W; \|T_dec\|·ω_pk is 1551 W (an upper bound for the S-curve). | Use u_peak = SF·max(\|T_pk\|, \|T_dec\|)/T_peak. Report the braking power. |
| P1-elec.motor-sizing-03 | Medium | Medium (confirmed). Conservative by 53 %, and the label says peak power. | For the S-curve, P_pk = T_pk·ω_pk pairs the peak torque (mid-acceleration) with the peak speed (end of acceleration). It is too high. | E8: product 3771 W. True maximum of T(t)·ω(t) is 2458 W (ratio 1.53). | Scan T(t)·ω(t) over the sine phase, or relabel the output as an upper bound. |
| P1-elec.motor-sizing-04 | Medium | Medium (confirmed). Probe: no p_screw gives no notice at all, unlike other defaulted inputs. | A blank screw lead is replaced by the default 10 mm with no notice. The block rule "mechanism-input" for the lead cannot fire. | Run the ball-screw KAT without p_screw: identical results, no lead notice. The code tests Default is null, but the default is 10 mm. | Make p_screw have no default for the screw mechanism, or add a notice when the default is used. |
| P1-elec.motor-sizing-05 | Low | Low (confirmed). Once 02 is fixed, \|T_dec\| governs and its error is conservative. | The efficiency divides a negative load torque. An overhauling load gives F k η. T_pk is then too small (unconservative) and \|T_dec\| too large. | E2: product T_load −4.340 N·m. With η on the overhauling side it is −2.778 N·m, so T_pk is 1.570 N·m (product 0.008) and T_dec −7.125 N·m (product −8.688). | Use F k η when F < 0 (for the load-driven direction). Document the choice. |
| P1-elec.motor-sizing-06 | Low | Low (confirmed). | The "overhauling" warning fires on every normal move, because T_dec is negative whenever J α exceeds T_load. The text says the load drives the motor. | ka-ball-screw inputs give the warning. T_dec is −2.406 N·m on a horizontal axis. | Warn on T_load < 0 only, or on net regeneration with the load driving. |
| P1-elec.motor-sizing-07 | Low | Low (confirmed). | Inputs for other mechanisms still act or still raise notices. Direct rotary uses i. Linear mechanisms raise "T_L was not entered". | Direct rotary with i = 5: n_pk 1000 rpm, against 200 rpm for i = 1. | Force i = 1 for direct rotary. Do not notice unused inputs. |
| P1-elec.motor-sizing-08 | Info | Info (confirmed). | The spec lists "speed (with SF)". The speed check has no safety factor. The v_pk step label says "(trapezoid)" for both profiles. | E8: u_speed 0.9 with SF 1.25. | Align the spec or the code. Fix the label. |
| P1-elec.motor-sizing-R1 | Medium | Medium (new) | The cycle holds one move and one dwell. The return stroke is not modelled. On an inclined or vertical axis, or with a one-way process force, the return move needs other torques. No notice says so. | Vertical axis, 500 kg: the up move gives T_load +8.67 N·m. The down move gives about −8.67 N·m. Both belong in one thermal cycle, but the sheet holds only one. | Add an optional return move with its own load sign, or warn when θ ≠ 0 or F_p ≠ 0. |
| P1-elec.motor-sizing-R2 | Medium | Medium (new) | Peak and rated torque are single numbers. The motor must give T_pk at ω_pk at the end of acceleration. A servo's peak torque falls near its top speed. The check can pass a motor that cannot reach the point. | ka-ball-screw: T_pk 2.58 N·m is needed at 9000 rpm with n_max 10 000 rpm. The check compares it with the stall peak of 6 N·m. | Add an optional peak torque at n_pk, or warn when u_speed exceeds about 0.7. |

## Notes for the owner's manual test
- Run the ball-screw KAT: expect v_pk 1.5 m/s, T_pk 2.579 N·m and T_rms 1.440 N·m. Note the overhauling warning (-06).
- Set θ = 90°, m = 500 kg, t_move = 1 s, s = 0.2 m, dwell 10 s, T_rated 5 N·m, T_peak 15 N·m. Expect T_load 8.671 N·m, T_pk 9.636 N·m, T_rms 2.625 N·m (low by a factor 3, see -01).
- Set θ = −20°, rack D 60 mm, i 5, η 0.8, m 200 kg, S-curve, s 1.5 m, t_move 2 s. Expect T_dec −8.688 N·m with u_peak near zero (-02).
- Switch to S-curve on the ball-screw KAT. Expect T_pk 4.0015 N·m, T_rms 1.5994 N·m and P_pk 3771 W (-03).
- For a hand check of the S-curve: α_pk = (π/2)·α_trap = 8883 rad/s² for the KAT.

## Independent review
- Reviewer: senior reviewer (Claude), 2026-10-24.
- I re-ran the script: all rows pass. It is an independent Python derivation, not a copy of the C#.
- I reproduced 01, 02 and 04 with the probe. The numbers for 02 differ from the report, because my inputs differ. The defect is the same.
- I read Run line by line. The S-curve RMS closed form and the cross-term cancellation are right.
- Changes: all original severities stand. R1 and R2 added.
- Chain note: T_rms and n_pk are motor-shaft values. The drive-train pack feeds them to the shaft unchanged. See docs/verification/rederivation/chain.drive-train.md.
- Residual doubt: the holding torque in 01 ignores static friction and screw back-driving efficiency. Both would lower it a little.
