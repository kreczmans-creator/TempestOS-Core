# v0.26.0 risk-based test plan and defect triage (Track P, P6)

Date: 2026-10-24. Base: `release/v0.26.0` @ 0d265eeb (Addendum A-3 merged). Author: Addendum C Track P.
This file ranks every known or suspected defect and sets the owner's test order.
Track S fixes Critical and High findings in phase C-1. Track P reports only and changed no product code.

## Sources
- P1 re-derivation reports: `docs/verification/rederivation/<id>.md` (25 calculators plus the drive-train chain).
- Each P1 report had a first pass and a senior independent review. The "Reviewed" severity governs.
- P2 known-answer sources: `docs/verification/kat-sources.md`. P5 consistency audit: `docs/verification/consistency-audit.md`.
- Colour-team reports under `docs/reviews/` (open items only, listed below).
- `python3 tools/verification/findings.py --min High` regenerates the finding list from the reports.

## Severity scale
- **Critical:** wrong result in normal use; unconservative; could lead to an unsafe design.
- **High:** wrong result in plausible use; or a missing guard lets an unconservative result through.
- **Medium:** wrong only in edge cases; conservative error over 5 %; misleading output or label.
- **Low:** wording, display or small inconsistency; conservative error under 5 %.

## Summary
All 87 known-answer cases (KATs) pass in the product and in the independent scripts.
The KATs are mostly analytic and narrow, so they miss the defects below.
Every calculator Critical and High finding was reproduced with the probe tool. The Desktop finding comes from code reading only.

| Calculator | Critical | High | Medium | Low | Info | Rejected |
|---|---|---|---|---|---|---|
| elec.fault-protection | 0 | 5 | 3 | 3 | 2 | 1 |
| mech.bolt-group | 0 | 3 | 2 | 1 | 1 | 0 |
| mech.bolted-joint | 0 | 1 | 3 | 4 | 1 | 0 |
| mech.shaft | 0 | 1 | 5 | 1 | 2 | 1 |
| chain.drive-train | 2 | 1 | 3 | 3 | 1 | 0 |
| struct.section-properties | 1 | 0 | 2 | 2 | 1 | 0 |
| struct.connection-ec3 | 0 | 4 | 1 | 6 | 1 | 0 |
| struct.en1993-member | 0 | 2 | 3 | 4 | 1 | 0 |
| struct.frame-2d | 0 | 2 | 2 | 2 | 0 | 0 |
| struct.beam-formulas | 0 | 0 | 1 | 3 | 1 | 0 |
| mech.weld-group | 0 | 1 | 2 | 1 | 1 | 0 |
| elec.motor-sizing | 0 | 2 | 4 | 3 | 1 | 0 |
| bs.psychrometrics | 1 | 1 | 0 | 6 | 2 | 0 |
| mech.iso286-fit | 0 | 3 | 2 | 4 | 1 | 0 |
| mech.press-fit | 0 | 1 | 1 | 1 | 2 | 0 |
| mech.thick-cylinder | 0 | 1 | 0 | 1 | 2 | 0 |
| mech.bearing-life | 0 | 2 | 0 | 1 | 2 | 0 |
| elec.load-budget | 0 | 3 | 4 | 2 | 2 | 0 |
| elec.voltage-drop | 0 | 1 | 2 | 3 | 1 | 0 |
| bs.pipe-pump | 0 | 3 | 3 | 3 | 2 | 0 |
| bs.sealed-system | 0 | 2 | 0 | 4 | 2 | 0 |
| elec.enclosure-heat | 0 | 0 | 1 | 4 | 2 | 0 |
| bs.duct-sizing | 0 | 0 | 1 | 3 | 2 | 0 |
| bs.heat-exchanger | 0 | 0 | 1 | 2 | 3 | 0 |
| struct.load-combinations | 0 | 0 | 3 | 0 | 1 | 0 |
| mech.tolerance-stack | 0 | 0 | 0 | 4 | 2 | 0 |
| desktop.calc-sheet-view | 0 | 1 | 1 | 0 | 0 | 0 |
| **Total** | **4** | **40** | **50** | **71** | **39** | **2** |

## Four Critical findings (fix before any owner use)
1. **P1-struct.section-properties-01.** A tube or hollow rectangle inside a composite section counts its hole as solid. A 200 × 10 tube reads 568.6 cm²; the correct area is 59.7 cm². The coordinator reproduced it.
2. **P1-bs.psychrometrics-01.** Cooling with dehumidification swaps sensible and latent load. At 28 °C 50 % to 12 °C, the sheet shows 8.9 kW sensible and 16.4 kW latent; physics gives about 16.3 kW and 8.9 kW. Total load is right; SHR and coil selection are wrong. The coordinator reproduced it.
3. **P1-chain.drive-train-01.** The drive-train pack binds motor RMS torque to the shaft as steady torque. Shaft yield and key checks never see peak torque. Fatigue ignores the reversing torque (chain n_f 2.01 passes; reversing cycle gives 1.39).
4. **P1-chain.drive-train-02.** The same binding applies no gear ratio or efficiency. A shaft after a gearbox sees T·i·η, about 17 times more in the reviewed example. It is Critical only when i > 1; a direct-drive shaft is unaffected.

## Recommended test order for the owner
The addendum's liability order is E4/E5, S8, F2, S6, S7, M3, M4.
E4, S6 (Addendum A) and S7, S8, F2 (Addendum B) are not yet on `release/v0.26.0`.
Track P will add them as they merge. For the calculators present, test in this order:

| Order | Item | Why here | Manual test |
|---|---|---|---|
| 1 | E5 fault protection | Electrical safety; 5 High (resistivity, let-through, curve end) | `manual-tests/elec.fault-protection.md` |
| 2 | M3 bolt group, bolted joint | Liability list; 4 High (linear check, ΣA x′y′, collinear, assembly stress) | `mech.bolt-group.md`, `mech.bolted-joint.md` |
| 3 | M4 shaft and the drive-train pack | Liability list; 2 Critical in the chain; Rayleigh critical speed | `mech.shaft.md`, `chain.drive-train.md` |
| 4 | S1 section properties | Critical; feeds S3 and S4 | `struct.section-properties.md` |
| 5 | S4 EC3 connections | 4 High (oversize holes, single-lap cap, M12 clearance, legacy picker) | `struct.connection-ec3.md` |
| 6 | S3 member checks | 2 High (k factors default 1.0, stale shear-area dimensions) | `struct.en1993-member.md` |
| 7 | S2 frame and beam formulas | 2 High (couple on a member, blank load position) | `struct.frame-2d.md`, `struct.beam-formulas.md` |
| 8 | M6 weld group | 1 High (I_uxy ignored) | `mech.weld-group.md` |
| 9 | E1 motor sizing | 2 High (holding torque, braking torque) | `elec.motor-sizing.md` |
| 10 | B2 psychrometrics | Critical, lower physical liability | `bs.psychrometrics.md` |
| 11 | M2 fits, press fit, thick cylinder | 5 High (18 mm step, N9, p/r–u, hollow-shaft bore, collapse) | `mech.iso286-fit.md`, `mech.press-fit.md`, `mech.thick-cylinder.md` |
| 12 | M5 bearing life | 2 High (Y default 0, no Y₁) | `mech.bearing-life.md` |
| 13 | E6 load budget, E3 voltage drop | 4 High (starting peak, blanks as 1, duty on currents, resistivity) | `elec.load-budget.md`, `elec.voltage-drop.md` |
| 14 | B1 pipe and pump, B6 sealed system | 5 High (NPSH gaps, trim law, vessel margins; B6 needs the owner's BS EN 12828) | `bs.pipe-pump.md`, `bs.sealed-system.md` |
| 15 | E2, B3, B5, S5, M1 | No Critical or High | remaining files |
| any | Desktop sheet import | 1 High (seeded unit not converted); test with every JSON import | `docs/verification/rederivation/desktop.calc-sheet-view.md` |

## Critical and High findings, ranked (44)
| Rank | ID | Calculator | Reviewed | First pass | Finding (first sentence; detail in the report) |
|---|---|---|---|---|---|
| 1 | P1-chain.drive-train-01 | chain.drive-train | Critical | Critical | E1 T_rms is bound to M4 T, which M4 treats as steady torque. |
| 2 | P1-chain.drive-train-02 | chain.drive-train | Critical | Critical | E1 torques are at the motor shaft. |
| 3 | P1-struct.section-properties-01 | struct.section-properties | Critical | High | A tube or hollow-rectangle part in a composite gives a wrong section with no warning. |
| 4 | P1-bs.psychrometrics-01 | bs.psychrometrics | Critical | High | In cooling with dehumidification, Q_sensible and Q_latent are exchanged. |
| 5 | P1-elec.fault-protection-01 | elec.fault-protection | High | High | Default ρ₂₀ is the pure-copper value. |
| 6 | P1-elec.fault-protection-02 | elec.fault-protection | High | High | The line-conductor check (u_line, labelled reg. |
| 7 | P1-elec.fault-protection-03 | elec.fault-protection | High | Medium | The CPC check covers only the far-end minimum current. |
| 8 | P1-elec.fault-protection-09 | elec.fault-protection | High | Low | The rule "curve must bracket the fault current" is declared Block. |
| 9 | P1-elec.fault-protection-R1 | elec.fault-protection | High | High | When I_f lies above the highest curve current, the code uses I_f²·0.1 s. |
| 10 | P1-mech.bolt-group-01 | mech.bolt-group | High | High | The EN 1993-1-8 linear check is evaluated only at the bolt with the highest circular utilisation. |
| 11 | P1-mech.bolt-group-02 | mech.bolt-group | High | High | Tension from M_x and M_y ignores ΣA x′y′. |
| 12 | P1-mech.bolt-group-03 | mech.bolt-group | High | High | When ΣA y′² is zero, the M_x term is dropped silently. |
| 13 | P1-mech.bolted-joint-R1 | mech.bolted-joint | High | — | The assembly stress (VDI 2230 R7) is only a warning. |
| 14 | P1-mech.shaft-01 | mech.shaft | High | High | The Rayleigh sum uses \|y\| for the weights. |
| 15 | P1-chain.drive-train-05 | chain.drive-train | High | High | A screw or rack drive puts axial thrust on the bearings. |
| 16 | P1-struct.connection-ec3-01 | struct.connection-ec3 | High | Medium | Oversize or slotted holes are accepted with no reduction and no warning. |
| 17 | P1-struct.connection-ec3-03 | struct.connection-ec3 | High | Medium | The cap F_b,Rd ≤ 1.5 f_u d t/γ_M2 for single-lap joints with one bolt row (cl. |
| 18 | P1-struct.connection-ec3-04 | struct.connection-ec3 | High | High | ADR-0169 offers α_v f_ub for threads in the plane but multiplies by the gross shank area. |
| 19 | P1-struct.connection-ec3-R1 | struct.connection-ec3 | High | — | The default M12 hole is d + 2 = 14 mm. |
| 20 | P1-struct.en1993-member-02 | struct.en1993-member | High | Medium | Default k_yy = k_zz = 1.0 is unconservative where Annex B gives k > 1, and no warning says so. |
| 21 | P1-struct.en1993-member-R1 | struct.en1993-member | High | — | The shear-area helper inputs h, b, t_w, t_f and r carry UC 203x203x46 defaults. |
| 22 | P1-struct.frame-2d-01 | struct.frame-2d | High | High | A concentrated couple on a member makes the moment diagram jump. |
| 23 | P1-struct.frame-2d-02 | struct.frame-2d | High | Medium | A point or couple load with a blank position "a" goes to the start of the member with no warning. |
| 24 | P1-mech.weld-group-01 | mech.weld-group | High | High | The out-of-plane law ignores the product I_uxy. |
| 25 | P1-elec.motor-sizing-01 | elec.motor-sizing | High | High | The dwell runs at zero torque. |
| 26 | P1-elec.motor-sizing-02 | elec.motor-sizing | High | High | The peak-torque check uses T_pk only. |
| 27 | P1-bs.psychrometrics-R1 | bs.psychrometrics | High | High | Cooling with dehumidification accepts a leaving state wetter than the entering state. |
| 28 | P1-mech.iso286-fit-01 | mech.iso286-fit | High | High | A nominal size of exactly 18 mm is placed in the 18 to 30 mm step instead of 10 to 18 mm. |
| 29 | P1-mech.iso286-fit-02 | mech.iso286-fit | High | High | Hole N above IT8 uses ES = −5D^0.34. |
| 30 | P1-mech.iso286-fit-05 | mech.iso286-fit | High | Medium | The spec and code comment say the formula values differ from the tables by about 1 µm. |
| 31 | P1-mech.press-fit-01 | mech.press-fit | High | High | The shaft yield check uses the von Mises stress at the shaft outer surface. |
| 32 | P1-mech.thick-cylinder-01 | mech.thick-cylinder | High | Medium | Under external pressure on a moderately thin wall, the check passes although elastic collapse governs. |
| 33 | P1-mech.bearing-life-01 | mech.bearing-life | High | Medium | The defaults X = 1, Y = 0 remove F_a from P without any notice. |
| 34 | P1-mech.bearing-life-02 | mech.bearing-life | High | Medium | The branch F_a/F_r ≤ e has no Y₁. |
| 35 | P1-elec.load-budget-01 | elec.load-budget | High | High | The starting peak uses the demand-weighted apparent power of the starting load. |
| 36 | P1-elec.load-budget-03 | elec.load-budget | High | Medium | Blank optional cells (cosφ, η, d, k, inrush) become 1 with no notice. |
| 37 | P1-elec.load-budget-R1 | elec.load-budget | High | High | The duty d (share of time on) scales the bus currents and the PSU rating. |
| 38 | P1-elec.voltage-drop-01 | elec.voltage-drop | High | Medium | The default ρ_20 = 0.01724 gives conductor resistances 5 to 11 % below the BS EN 60228 class 2 maximum values. |
| 39 | P1-bs.pipe-pump-01 | bs.pipe-pump | High | Medium | A custom fluid with NPSH_r entered gives no NPSH_a, no NPSH check and no notice. |
| 40 | P1-bs.pipe-pump-02 | bs.pipe-pump | High | Medium | D_ratio applies Q ∝ D³ and H ∝ D². |
| 41 | P1-bs.pipe-pump-R1 | bs.pipe-pump | High | High | NPSH_a includes only segments marked "suction". |
| 42 | P1-bs.sealed-system-02 | bs.sealed-system | High | Medium | The final pressure may sit just under the safety valve setting. |
| 43 | P1-bs.sealed-system-03 | bs.sealed-system | High | Medium | The vessel formula has no water reserve (seal) volume. |
| 44 | P1-desktop.calc-sheet-view-01 | desktop.calc-sheet-view | High | High | A seeded quantity whose unit is not in the form's pick list keeps its number and takes the default unit, so the value silently changes scale. |

## Medium findings (50)
Fix in C-1 when each takes under 2 hours; otherwise list for the owner.

| Rank | ID | Calculator | Reviewed | First pass | Finding (first sentence; detail in the report) |
|---|---|---|---|---|---|
| 1 | P1-elec.fault-protection-04 | elec.fault-protection | Medium | Medium | When I_f is below the tripping current, the adiabatic checks still use 0.1 s and report pass with the "conservative" warning. |
| 2 | P1-elec.fault-protection-05 | elec.fault-protection | Medium | Medium | t_req is a free input (0.1 to 5 s, default 0.4 s). |
| 3 | P1-elec.fault-protection-06 | elec.fault-protection | Medium | Medium | θ for Z_s and θ_i for the adiabatic check are separate inputs. |
| 4 | P1-mech.bolt-group-04 | mech.bolt-group | Medium | Medium | v_max and t_max are values at the governing bolt. |
| 5 | P1-mech.bolt-group-R1 | mech.bolt-group | Medium | — | The -03 guard idea (Σ A y′² = 0) misses collinear bolts on a sloped line. |
| 6 | P1-mech.bolted-joint-01 | mech.bolted-joint | Medium | Low | Thread torsion uses W_p = π/16 d_3³. |
| 7 | P1-mech.bolted-joint-02 | mech.bolted-joint | Medium | Medium | After the joint separates, F_S,max, yield, fatigue and stripping use the pre-separation linear law. |
| 8 | P1-mech.bolted-joint-R2 | mech.bolted-joint | Medium | — | F_M is labelled the maximum preload, but μ_G, μ_K and K are not labelled as minimum values. |
| 9 | P1-mech.shaft-02 | mech.shaft | Medium | Medium | A shoulder is checked on the side with the smaller outer diameter. |
| 10 | P1-mech.shaft-03 | mech.shaft | Medium | Medium | Defaults x_T1 = 0 and x_T2 = 400 mm suit the 400 mm example only. |
| 11 | P1-mech.shaft-04 | mech.shaft | Medium | Medium | The "mass carried" column adds no weight to reactions, moment or deflection. |
| 12 | P1-mech.shaft-05 | mech.shaft | Medium | Low | Key bearing uses the key yield strength only. |
| 13 | P1-mech.shaft-R1 | mech.shaft | Medium | — | The sheet cites Shigley 11th ed. |
| 14 | P1-chain.drive-train-03 | chain.drive-train | Medium | Medium | E1 n_pk feeds M5 n. |
| 15 | P1-chain.drive-train-06 | chain.drive-train | Medium | Medium | M4's radial loads are user rows, not linked to the torque. |
| 16 | P1-chain.drive-train-07 | chain.drive-train | Medium | Medium | The seat-fit step uses M2 defaults H7/g6, a clearance fit. |
| 17 | P1-struct.section-properties-03 | struct.section-properties | Medium | Medium | J of channel, angle and tee ignores the root fillet. |
| 18 | P1-struct.section-properties-04 | struct.section-properties | Medium | Low | A self-crossing polygon, or a hole outside the outline, ends in a cryptic engine block. |
| 19 | P1-struct.connection-ec3-02 | struct.connection-ec3 | Medium | Medium | Block tearing offers V_eff,1 only. |
| 20 | P1-struct.en1993-member-01 | struct.en1993-member | Medium | Medium | High-shear reduction multiplies the full bending resistance by (1 − ρ), for both axes. |
| 21 | P1-struct.en1993-member-06 | struct.en1993-member | Medium | Low | Tension checks gross-section yielding only. |
| 22 | P1-struct.en1993-member-R2 | struct.en1993-member | Medium | — | The member interaction divides the moments by M_c,Rd, which uses γ_M0. |
| 23 | P1-struct.frame-2d-03 | struct.frame-2d | Medium | Medium | The large-deflection warning compares each element deflection with its own length. |
| 24 | P1-struct.frame-2d-R1 | struct.frame-2d | Medium | Medium | A blank load magnitude runs as zero with no warning. |
| 25 | P1-struct.beam-formulas-01 | struct.beam-formulas | Medium | Medium | The propped cantilever reports the position of maximum deflection from the wrong end. |
| 26 | P1-mech.weld-group-02 | mech.weld-group | Medium | Medium | The "collinear" block fires for any horizontal or vertical line, even with no bending. |
| 27 | P1-mech.weld-group-03 | mech.weld-group | Medium | Medium | A diagonal collinear group is not blocked. |
| 28 | P1-elec.motor-sizing-03 | elec.motor-sizing | Medium | Medium | For the S-curve, P_pk = T_pk·ω_pk pairs the peak torque (mid-acceleration) with the peak speed (end of acceleration). |
| 29 | P1-elec.motor-sizing-04 | elec.motor-sizing | Medium | Medium | A blank screw lead is replaced by the default 10 mm with no notice. |
| 30 | P1-elec.motor-sizing-R1 | elec.motor-sizing | Medium | Medium | The cycle holds one move and one dwell. |
| 31 | P1-elec.motor-sizing-R2 | elec.motor-sizing | Medium | Medium | Peak and rated torque are single numbers. |
| 32 | P1-mech.iso286-fit-03 | mech.iso286-fit | Medium | Medium | Hole K at IT8 loses the k deviation, because the shaft k formula returns 0 for grade 8. |
| 33 | P1-mech.iso286-fit-04 | mech.iso286-fit | Medium | Medium | Letter t silently returns the s deviation when the step mean is 24 mm or less. |
| 34 | P1-mech.press-fit-R1 | mech.press-fit | Medium | — | Torque capacity and force use the nominal interference as entered. |
| 35 | P1-elec.load-budget-02 | elec.load-budget | Medium | Medium | DC loads count in S_dem and S_T,req but their AC-side input is missing from I_3ph and I_1ph. |
| 36 | P1-elec.load-budget-04 | elec.load-budget | Medium | Medium | The PSU rating covers DC loads only. |
| 37 | P1-elec.load-budget-05 | elec.load-budget | Medium | Medium | Phi (→ E2 enclosure) includes the heat of every row, including motors in the field. |
| 38 | P1-elec.load-budget-06 | elec.load-budget | Medium | Low | Peukert gives a capacity credit when t_auto exceeds H_rate, with no notice. |
| 39 | P1-elec.voltage-drop-02 | elec.voltage-drop | Medium | Medium | A blank A_c or ρ_20 is replaced by 35 mm² and copper with no notice. |
| 40 | P1-elec.voltage-drop-03 | elec.voltage-drop | Medium | Low | The mV/A/m route ignores I_z. |
| 41 | P1-bs.pipe-pump-03 | bs.pipe-pump | Medium | Medium | The system curve scales friction with Q². |
| 42 | P1-bs.pipe-pump-04 | bs.pipe-pump | Medium | Medium | The duty point test looks only at the sign at the two curve ends. |
| 43 | P1-bs.pipe-pump-R2 | bs.pipe-pump | Medium | Medium | NPSH_a uses suction friction at the design flow Q. |
| 44 | P1-elec.enclosure-heat-01 | elec.enclosure-heat | Medium | Medium | The A_eff step is labelled "IEC TR 60890 approach", but it uses plain face areas. |
| 45 | P1-bs.duct-sizing-01 | bs.duct-sizing | Medium | Medium | The velocity method on a rectangular or flat-oval duct does not reach the target velocity. |
| 46 | P1-bs.heat-exchanger-01 | bs.heat-exchanger | Medium | Medium | The isothermal arrangement forces C_r = 0 but cannot name the constant-temperature stream. |
| 47 | P1-struct.load-combinations-01 | struct.load-combinations | Medium | Low | The default ξ is 0.85 (EN recommended). |
| 48 | P1-struct.load-combinations-02 | struct.load-combinations | Medium | Low | The recommended ψ_0 values differ from the UK NA for roofs (category H) and wind. |
| 49 | P1-struct.load-combinations-03 | struct.load-combinations | Medium | Info | K_FI multiplies γ_Q only, as the spec says. |
| 50 | P1-desktop.calc-sheet-view-02 | desktop.calc-sheet-view | Medium | Medium | A seed with an unknown unit text shows the default unit and runs; the engine path refuses it. |

## Known-answer source issues (P2)
- **ka-shigley-stiffness (M3):** contradicted. Shigley's frustum relation uses the bolt diameter in both places; the sheet mixes bolt and hole diameter. The senior review upheld it. Owner to confirm the equation number in the 11th edition.
- **ka-shigley-7-1 (M4):** provisionally contradicted. An open lecture deck of Example 7-1 shows other material values. The deck may be wrong; the owner's copy decides.
- Five cases stay unconfirmed and need the owner's books or datasheet: Incropera 11.1, Shigley screw stack, SCI P358 fin plate, the Designers' Guide to EN 1990, Phoenix Contact QUINT4 10 A derating.

## Open colour-team items (not P1 findings)
| ID | Severity | Item | Status |
|---|---|---|---|
| Y3-03 / RR-03 | Medium | Frame mechanism test by relative pivot can pass a near-mechanism with an extreme stiffness ratio | Open (Red) |
| RR-01 | Low | Windows DirectWrite may reject a subset font; PDF may exceed 200 KB | Open (Grey CI decides) |
| RR-04 | Low | Blue Book and ASHRAE values read from mirrors, not printed sources | Open (owner); P2 confirmed 20 of 21 secondary cases |
| R-22 | Low | Orphan Draft Evidence after a crash mid-Create | Accepted (D-32) |
| B-19 | Medium | Failed condition check shows "—" for utilisation | Justified; wording for v0.26.1 |
Note: the G-02 fix added the linear EN 1993-1-8 bolt check, but P1-mech.bolt-group-01 shows it runs only at one bolt. Treat that as a partial regression of G-02.

## Consistency items that can mislead (from P5)
Track S fixes these in C-2. The top five are:
1. S1 names the I-section major axis x; S3 names it y.
2. Symbols clash across linked sheets (`theta`, `T`, `t_f`, `eps`, `h`, `P`, `p_i`).
3. Linked quantities use different units (N·m vs kN·m, m³/s vs m³/h).
4. Percent appears as a unit, a 0–100 number and a 0–1 fraction.
5. Sheets cite internal codes (E4, S6, §A5) an engineer cannot resolve.
