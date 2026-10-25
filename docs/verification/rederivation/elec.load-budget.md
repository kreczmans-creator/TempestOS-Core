# elec.load-budget — independent re-derivation (Track P, P1)
Calculator: Load budget, PSU, transformer and battery, version 1.2.0 (1.0.0 at first review). Source file: src/Tempest.Calc/Tempest.Calc.Electrical/LoadBudgetCalculator.cs. Spec: docs/calcs/electrical/elec.load-budget.md.
Script: tools/verification/elec.load-budget.py (exit 0 = all KATs match; 84 checks, 17 KAT rows and 67 extra rows).
Verdict: MATCH WITH FINDINGS (reviewed: three High, four Medium)

The arithmetic matches all four KATs. The High findings are the starting peak with duty below 1 (01), silent defaults (03), and duty applied to currents and the PSU (R1). The Medium findings concern missing terms and labels.

## Method re-derived
- Demand per load P·d·k. Apparent power P·d·k/cosφ, with DC loads at power factor 1. Sums are arithmetic.
- Bus currents: I_3φ = S_3φ/(√3·U), I_1φ = S_1φ/U, I_DC = P_DC/U_DC. Units: kW, kVA, V, A.
- Heat: Φ = Σ(1 − η)·P·d·k, with P the electrical input.
- PSU: P_PSU,req = margin·P_DC/f_derate. Utilisation is required over selected.
- Transformer: S_T,req = margin·S_dem. Starting peak S_peak = S_dem + max((m − 1)·S_i), compared with k_T·S_T.
- Battery: I_b = Σ backed P·d·k/(U_b·η_conv). C = I_b·t/(DoD·η_b·k_temp·k_age).
- Peukert: Linden's Handbook of Batteries, 5th ed., relation t = H·(C/(I·H))^k. Solved for rated capacity it gives C = I·H·(t/H)^(1/k). I did not check the chapter number.
- In my blind script I took S_i as the demand-weighted apparent power, as the KAT does. The KAT cannot tell the two conventions apart, because the pump has d = k = 1. See P1-01.

## Known-answer cases
| Case | Symbol | KAT expected | Independent | Rel. diff | KAT tol | Result |
|---|---|---|---|---|---|---|
| ka-mixed-buses | P_dem (kW) | 9.12 | 9.12 | 0 | 1e-09 | PASS |
| ka-mixed-buses | S_dem (kVA) | 10.73958 | 10.73958 | 1.9e-07 | 1e-06 | PASS |
| ka-mixed-buses | I_3ph (A) | 14.72031 | 14.72031 | 5.1e-08 | 1e-06 | PASS |
| ka-mixed-buses | I_1ph (A) | 1.830664 | 1.830664 | 2.1e-07 | 1e-06 | PASS |
| ka-mixed-buses | I_DC (A) | 5 | 5 | 0 | 1e-09 | PASS |
| ka-mixed-buses | Phi (kW) | 0.927 | 0.927 | 2.4e-16 | 1e-09 | PASS |
| ka-mixed-buses | P_PSU_req (W) | 150 | 150 | 0 | 1e-09 | PASS |
| ka-mixed-buses | S_T_req (kVA) | 13.42448 | 13.42448 | 1.8e-07 | 1e-06 | PASS |
| ka-mixed-buses | S_peak (kVA) | 54.85723 | 54.85723 | 1.6e-08 | 1e-06 | PASS |
| ka-mixed-buses | C_req (A·h) | 32.89474 | 32.89474 | 9.6e-08 | 1e-06 | PASS |
| ka-battery | I_b (A) | 7 | 7 | 0 | 1e-09 | PASS |
| ka-battery | C_req (A·h) | 51.16959 | 51.16959 | 1.3e-08 | 1e-06 | PASS |
| ka-battery | u_batt | 0.9303562 | 0.9303562 | 7.0e-09 | 1e-06 | PASS |
| ka-peukert | Ah (A·h) | 26.15321 | 26.15321 | 1.1e-08 | 1e-06 | PASS |
| ka-peukert | C_req (A·h) | 43.01515 | 43.01515 | 5.7e-08 | 1e-06 | PASS |
| ka-psu-derating | P_PSU_req (W) | 333.3333 | 333.3333 | 1.0e-07 | 1e-06 | PASS |
| ka-psu-derating | u_PSU | 1.388889 | 1.388889 | 8.0e-08 | 1e-06 | PASS |

All 17 rows match. Two cases are Unconfirmed: ka-peukert and ka-psu-derating. The Peukert form agrees with the relation as I know it. I could not check the Phoenix Contact datasheet, so the factor 0.75 at 70 °C stays unverified.

## Extra probes (product vs independent)
| Probe | Inputs (short) | Symbol | Product | Independent | Rel. diff | Result |
|---|---|---|---|---|---|---|
| x1 | 11 kW pump, cosφ 0.85, d 0.5, m 6 | S_dem / I_3ph | 6.4706 kVA / 9.3395 A | same | <1e-10 | PASS |
| x1 | same | S_peak | 38.824 kVA | 38.824 (product convention) | <1e-10 | PASS, but see P1-01 |
| x2 | same pump, d 1 | S_peak | 77.647 kVA | 77.647 | <1e-10 | PASS |
| x3 | DC only, 4 kW, η 0.9, 2 h | I_DC / P_PSU_req | 166.67 A / 5000 W | same | <1e-9 | PASS |
| x3 | same | C_req | 548.25 A·h | 548.25 A·h | 2e-10 | PASS |
| x3 | same | I_3ph / I_1ph | 0 A / 0 A | 0 A / 0 A | 0 | PASS, but see P1-02 |
| x4 | Peukert, 10 A, t 72 h, H 20 h, k 1.3 | Ah | 535.74 A·h | 535.74 A·h | 2e-10 | PASS |
| x5 | Peukert, 10 A, t 1 h, H 20 h, k 1.3 | Ah | 19.964 A·h | 19.964 A·h | 5e-11 | PASS |
| x6 | AC3 5 kW, other cells blank | S_dem / I_3ph | 5 kVA / 7.2169 A | same (cosφ = 1) | <1e-10 | PASS, but see P1-03 |
| x7 | DC 200 W, 55 °C, f = 1 | P_PSU_req | 250 W | 250 W | 0 | PASS |
| x8 | Pump and fan, S_T 15 kVA, k_T 3, margin 1.5 | u_T / u_inrush | 1.0199 / 1.2070 | same | <1e-9 | PASS |
| x9 | η_conv 0.5, U_b 2 V, DoD 0.05, 10 h | C_req | 31 579 A·h | 31 579 A·h | 2e-10 | PASS |
| x10 | 200 kW, cosφ 0.5, η 0.5, m 20, 12 V | I_3ph / Phi | 19 245 A / 100 kW | same | <1e-9 | PASS |
| x11 | 500 W in W, cosφ 0.9 | I_1ph | 2.4155 A | 2.4155 A | 8e-11 | PASS |
| x12 | k_p given, H_rate blank | Ah | 20 A·h (Peukert off, warning) | 20 A·h | 0 | PASS |
| x13 | 168 W DC backed, 4 h, PSU 240 W, 55 A·h | u_PSU / u_batt | 0.875 / 0.8373 | same | <1e-9 | PASS |

The product blocks an empty loads table. It warns when only one of k_p and H_rate is given. It warns when the ambient exceeds 40 °C and f_derate is 1.

## Implementation comparison
| Aspect | Spec / textbook | Implementation | Agrees? |
|---|---|---|---|
| Demand and apparent power | P·d·k, P·d·k/cosφ | Same; DC at power factor 1 | Yes |
| Bus currents | Per supply | AC loads only in I_3ph and I_1ph; DC in I_DC | Partly; see P1-02 |
| Heat | Σ(1 − η)·P·d·k | Same, all rows | Yes; see P1-05 |
| PSU | margin·P_DC/f | Same, DC loads only | Partly; see P1-04 |
| Transformer | margin·S | Same | Yes |
| Starting peak | S + max((m − 1)·S_i) | S_i is P·d·k/cosφ | Formula yes; physics no, see P1-01 |
| Battery | I_b·t/(DoD·η_b·k_temp·k_age) | Same | Yes |
| Peukert | I_b·H·(t/H)^(1/k_p) | Same, no limit on t against H | Yes; see P1-06 |
| Blank optional cells | Not stated | Taken as 1 (cosφ, η, d, k, inrush) | Silent; see P1-03 |
| Ranges | Spec lists them | Hard limits match the spec | Yes |

## Findings
| ID | Severity | Reviewed | Finding | Evidence | Suggested fix (for Track S) | Status @ 614ddf89 | Status @ 0772fcf8 |
|---|---|---|---|---|---|---|---|
| P1-elec.load-budget-01 | High | High (confirmed). A load starts at its full rating whatever its duty. Duty below 1 is normal; the KAT fan has d 0.5. | The starting peak uses the demand-weighted apparent power of the starting load. A load starts at its full rating. When d·k is below 1, S_peak is too low and the transformer inrush check is unconservative. | 11 kW pump, cosφ 0.85, m 6, d 0.5, alone: product S_peak = 38.82 kVA. With d 1 it is 77.65 kVA. The load starts at full rating in both cases. The fully loaded start is S_dem − S_i,demand + m·S_i,full = 77.65 kVA, so the product is 50 % low. | Use S_peak = S_dem − P_i·d_i·k_i/cosφ_i + m_i·P_i/cosφ_i, and take the largest over non-DC rows. Add a KAT with d·k below 1 for the largest starter. | Fixed (499bfb01, DA-17). Probe: 11 kW pump, d 0.5, m 6 now gives S_peak 77.647 kVA. Method uses m·P/cosφ − S_i. | Fixed (499bfb01, DA-17), re-verified. Probe: 11 kW pump, d 0.5, m 6 gives S_peak 77.647 kVA. New port S_T_req_start 25.882 kVA now carries the start into the rating. |
| P1-elec.load-budget-02 | Medium | Medium (confirmed). Usually small in a control panel, but the bus currents are low with no notice. | DC loads count in S_dem and S_T,req but their AC-side input is missing from I_3ph and I_1ph. I_3ph feeds the cable calculation E4. The convention note says the PSU loss is a load row, but that row would double count in S_dem. | Only a 4 kW DC load: S_dem 4 kVA, S_T,req 5 kVA, I_3ph 0 A, I_1ph 0 A. | Add PSU efficiency and power factor inputs. Add the AC input of the PSU to I_1ph or I_3ph, and state which supply feeds the PSU. | Open. Probe: 4 kW DC alone still gives I_3ph 0 A and I_1ph 0 A. | Partly fixed (874bc235). New inputs psu_bus and eta_psu. Probe: 4 kW DC on the three-phase bus gives I_3ph 6.415 A and S_dem 4.444 kVA. The default is still 'not counted'; then I_3ph is 0 A and a warning shows. |
| P1-elec.load-budget-03 | Medium | High (upgraded). Three silent defaults are unconservative: cosφ 1 lowers current, η 1 hides heat, inrush 1 hides the start. This is a missing guard in normal data entry. | Blank optional cells (cosφ, η, d, k, inrush) become 1 with no notice. S_dem, currents and heat come out low for an AC row with only P. | AC3 5 kW, other cells blank: S_dem 5 kVA, I_3ph 7.22 A, Phi 0 kW. With cosφ 0.85 the current is 8.49 A, 18 % higher. | Warn per row for each defaulted cell. Make cosφ required for AC rows. | Open. Probe: AC3 5 kW with blank cells still gives I_3ph 7.22 A, Phi 0 kW, no per-cell notice. | Open. Probe: AC3 5 kW with blank cells still gives I_3ph 7.217 A, Phi 0 kW and no per-cell notice. |
| P1-elec.load-budget-04 | Medium | Medium (confirmed). | The PSU rating covers DC loads only. It leaves out battery charging power for battery-backed loads. The check can pass a PSU that cannot also charge the battery. | 168 W DC backed load, 4 h, battery 55 A·h, PSU 240 W: u_PSU 0.875, pass. A 0.1C charge at 24 V needs about 132 W extra. | Add an optional charge-current input, or warn when backed loads and t_auto are both set. | Open. Probe: u_PSU 0.875 still passes with no charge power. | Open. Probe: u_PSU 0.875 still passes with no battery charge power. |
| P1-elec.load-budget-05 | Medium | Medium (confirmed). Mostly conservative, but the label misleads and PSU loss is missing. | Phi (→ E2 enclosure) includes the heat of every row, including motors in the field. It also leaves out the PSU's own loss. The label "Heat dissipated (→ E2)" is misleading. | KAT mixed buses: Phi 0.927 kW, of which pump and fan give 0.915 kW. Only the controls row (0.012 kW) is a panel load, before PSU loss. | Add an "inside enclosure" flag per row. Add PSU loss to Phi. Reword the port label. | Open. Probe: Phi 0.762 kW still counts the motor row and omits PSU loss. | Partly fixed (874bc235). A per-row heat share h (blank: 1 − η) and heat_add (default 0) now exist. The port label is reworded. A motor row still counts 1 − η unless h is set. PSU loss is still absent from Phi. |
| P1-elec.load-budget-06 | Low | Medium (upgraded). Unconservative by 26 % with no notice. Peukert's credit beyond the rated hour rate is larger than real batteries give. It is limited to t_auto > H_rate, so it is an edge case. | Peukert gives a capacity credit when t_auto exceeds H_rate, with no notice. This is unconservative and needs data outside the rated range. | 10 A, t 72 h, H 20 h, k_p 1.3: Ah = 535.7 A·h against I·t = 720 A·h (26 % less). | Warn when t_auto > H_rate. Or limit Ah to at least I_b·t. | Open. Probe: Ah 535.74 A·h at 72 h on a 20 h rating, no t_auto notice. | Open. Probe: Ah 535.74 A·h at 72 h on a 20 h rating, with no t_auto notice. |
| P1-elec.load-budget-07 | Low | Low (confirmed). | Autonomy above 0 with no backed load gives C_req = 0 and no notice. | One unbacked DC load, t_auto 4 h: I_b 0 A, C_req 0 A·h. | Warn that no row is battery-backed. | Open. Probe: unbacked DC load with 4 h still gives C_req 0 with no notice. | Open. Probe: one unbacked DC load with 4 h still gives C_req 0 and no notice. |
| P1-elec.load-budget-08 | Low | Low (confirmed). | The inrush check treats the start as a thermal overload with default k_T = 3 and no source. It has no voltage-dip check. | Template default k_T 3. u_inrush = S_peak/(3·S_T). | Cite a source for k_T, or leave it empty. Add a note that voltage dip is not assessed. | Open. k_T default 3 unchanged. Probe gives u_inrush 0.647. | Open. Probe: k_T default 3 unchanged; u_inrush 0.647. No source cited. |
| P1-elec.load-budget-09 | Info | Info (confirmed). | η_b, DoD and Peukert all apply together. This may double count rate loss. It is conservative. | ka-peukert: 43.0 A·h against 26.2 A·h before derates. | Document it. | Open (Info). Derates unchanged. | Open (Info). Derates unchanged. |
| P1-elec.load-budget-10 | Info | Info (confirmed). | The PSU derating is a user-entered factor. The warning needs both ambient above 40 °C and f_derate = 1. A factor typed for another ambient raises no warning. | θ 55 °C, f = 1: warning appears. θ 55 °C, f = 0.9: no warning. | None, or add the datasheet curve as a record. | Open (Info). f_derate 0.9 at 55 °C still raises no ambient warning. | Open (Info). Probe: θ 55 °C with f_derate 0.9 still raises no ambient warning. |
| P1-elec.load-budget-R1 | High | High (new) | The duty d (share of time on) scales the bus currents and the PSU rating. A cable and a PSU must carry the load while it is on. The duty average is right only for energy (battery) and slow heating (transformer, enclosure). I_3ph feeds E4 as the design current I_b. | Probe: one 7.5 kW pump, cosφ 0.85, d 0.5 gives I_3ph 6.37 A. Its running current is 12.74 A. A 200 W DC brake at d 0.1 gives P_PSU,req 25 W, and a 30 W PSU passes. The KAT itself gives I_3ph 14.72 A against 16.71 A with the fan at full current. | Use P·k (no d) for I_3ph, I_1ph, I_DC and P_PSU,req. Keep P·d·k for the battery energy and the heat. State this in the method. | Open. Probe: 7.5 kW pump at d 0.5 still gives I_3ph 6.37 A; 200 W brake at d 0.1 still gives P_PSU,req 25 W. | Open. Probe: 7.5 kW pump at d 0.5 still gives I_3ph 6.368 A. A 200 W brake at d 0.1 still gives P_PSU,req 25 W. |
| P1-elec.load-budget-C1 | Low | New | A load with inrush 1 and duty or diversity below 1 is still counted at full rating in S_peak and is named as "starting". The undiversified start is a stated choice, but the label and the 1.0 default mislead. | Probe: 10 kW heater, cosφ 1, d 0.5, inrush 1. Step reads "5 + 5 (Heater starting)" and S_peak is 10 kVA against S_dem 5 kVA. | Count only rows with inrush above 1 as starters, or rename the step "largest load at full rating". | Open (new) | Open. Probe: 10 kW heater, d 0.5, inrush 1 still gives S_peak 10 kVA against S_dem 5 kVA. |

## Notes for the owner's manual test
- Load the KAT mixed-buses rows. Expect P_dem 9.12 kW, S_dem 10.740 kVA, I_3ph 14.72 A, Phi 0.927 kW, S_peak 54.86 kVA, C_req 32.89 A·h.
- Change the pump to d = 0.5 and remove the fan. Expect S_peak 38.8 kVA from the product. Your hand value for a full-rating start is 77.6 kVA (finding 01).
- Enter one DC load of 4 kW. Expect I_DC 166.7 A, P_PSU,req 5000 W, and I_3ph and I_1ph both 0 A (finding 02).
- Set k_p 1.2, H_rate 20 h, 4 h, 5 A. Expect Ah 26.15 A·h and C_req 43.02 A·h with the default derates.
- Set 70 °C, f_derate 0.75, 200 W DC, PSU 240 W. Expect P_PSU,req 333.3 W and u_PSU 1.389. Check 0.75 against the datasheet curve.

## Independent review
- Reviewer: senior reviewer (Claude), 2026-10-24.
- I re-ran the script: all rows pass. It re-derives simple arithmetic, so it shares the product's conventions by nature. It adopts the product's S_i convention on purpose and says so.
- I reproduced 01 and R1 with the probe, and read Run line by line.
- Changes: 03 raised to High, 06 raised to Medium. R1 added.
- R1 is a defect of the spec as well as the code. The spec sets the same P·d·k for every output.
- Residual doubt: whether k (diversity) should also leave the cable current depends on the owner's practice for a bus. I kept k in the fix.

## Re-check after DA-17/DA-18 and A-4/A-5 (2026-10-24)
- Source changed: version 1.1.0 (DA-17). The starting peak is now S_dem + max(m·P/cosφ − S_i).
- Standby loads with no demand are skipped in that peak.
- A-4 added a linked load (P_add, cosphi_add, eta_add, inrush_add) for the motor-circuit chain. An empty table is allowed when P_add is above 0.
- Fixed: 01, re-run with the probe (S_peak 77.647 kVA). Open: 02 to 10 and R1. R1 still scales currents and the PSU by duty.
- New: C1 (Low), a load with inrush 1 and duty below 1 is called "starting" in S_peak.
- Script updated: new starting-peak model and a corrected x1 value. New groups x14 to x16 cover the linked load, standby and C1.
- It runs 73 checks and exits 0.
- The earlier script passed x1 only because it used the old convention. It now checks the product value 77.647 kVA.

## Re-check after the Addendum A Blue round (0772fcf8)
- Source changed (874bc235): version 1.2.0. New inputs psu_bus, eta_psu, a per-row heat share and heat_add. New outputs S_T_req_start and S_T_min. P_add now takes E1's RMS power.
- A chosen PSU bus adds P_DC/η_PSU to that bus and to S_dem. With the default 'not counted' a warning appears.
- Probe at 0772fcf8: 01 stays Fixed. 02 and 05 are partly fixed. Nine rows stay Open, including R1 (High). No KAT value changed (4 of 4 pass).
- I tried the PSU on each bus, with a pump, and in a DC-only start. I also tried heat share 0 and 1, and heat_add.
- S_peak never falls below S_dem. No new defect found.
- Script updated: new cases x17 to x20 (PSU bus, start rating, heat share). It runs 84 checks and exits 0.
- New defects: none found.
