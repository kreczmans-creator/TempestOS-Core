# chain.steel-support — independent review of the linked chain (Track P, P1)
Pack: `pack.steel-support`, "Steel support". Source file: src/Tempest.Calc/Tempest.Calc.Library/CalcPackTemplates.cs. Example inputs: CalcPackExamples.cs (SteelS5, SteelS2, SteelS3, SteelM3, SteelS4A, SteelS4B, SteelS6). Chain tests: tests/Tempest.Calc.Tests.Library/Chains/SteelSupportChainTests.cs.
Steps: S5 struct.load-combinations → S2 struct.frame-2d → S3 struct.en1993-member (column) and M3 mech.bolt-group → S4a struct.connection-ec3 (arm joint) → S6 struct.base-plate. S4b struct.connection-ec3 (holding-down bolts) feeds S6.
Script: none. I fed each step by hand with the probe. The inputs are in /tmp/claude-0/recheck3/ (drv.py, steel.py, st_*.json). Release 298ade36 (A7b/A8).
Verdict: VALUES TRANSFER CORRECTLY; FIVE MEDIUM (-01 TO -05), EIGHT LOW, THREE INFO (first pass, not yet reviewed)

Every binding moves the right quantity. Sign conventions agree at all three boundaries: S2 to S3, S2 to S6 and S4b to S6. The per-bolt and group values go to the right inputs.
The problems are four. The pack hangs on S2's numbering and nothing checks it. M3 holds typed copies of S4a's resistances. The arm joint is checked at the bolts only. The anchorage check cannot fail.

## Method re-derived
- S5 gives the governing downward ULS effect E_d,min = −(1.35 G + 1.5 Q). For G 12 kN and Q 8 kN this is −28.2 kN. Source: EN 1990 eq. 6.10. See docs/verification/rederivation/struct.load-combinations.md.
- Cell link: S5 E_d,min fills S2 `loads` row 1, column `F_y` (kN, negative is down). The pack counts rows from 0.
- S2 gives reactions and member forces for the combination "ULS". Reactions are up positive. Member N is tension positive. The `_abs` ports are magnitudes.
- Cantilever column 3 m, arm 1 m. Hand values are R1_y = 28.2 kN and R1_x = −5 kN. R1_m = 5 × 3 + 28.2 × 1 = 43.2 kN·m. The column carries 43.2 kN·m at its base. The arm carries 28.2 kN·m at node 2.
- S3 takes N_Ed with tension positive. The compression N_e1 = −28.2 kN therefore keeps its sign. M_y,Ed is the greatest |M| of member 1. V_Ed is the greatest |V|.
- M3 bolt group, elastic: v = V/n for pure shear. Tension from a moment about the centroid: t = M y/Σy². Source: see docs/verification/rederivation/mech.bolt-group.md.
- Per-bolt values v_max and t_max go to S4a `F_v_Ed` and `F_t_Ed`. The group shear V_e2 goes to S4a `V_Ed` for block tearing.
- S4a: F_v,Rd = α_v f_ub A_s/γ_M2, F_t,Rd = 0.9 f_ub A_s/γ_M2, combined F_v/F_v,Rd + F_t/(1.4 F_t,Rd) ≤ 1. Source: EN 1993-1-8 Table 3.4. See docs/verification/rederivation/struct.connection-ec3.md.
- S6 takes N_Ed with compression positive, M_Ed and V_Ed unsigned. Compression positive agrees with S2's up-positive R1_y.
- S6 holding-down bolts: F_t,Rd per bolt, A_s, f_ub, f_yb and F_1,vb,Rd come from S4b. Anchor shear is F_2,vb,Rd = α_bc f_ub A_s/γ_M2 with α_bc = 0.44 − 0.0003 f_yb. Source: EN 1993-1-8 cl. 6.2.2. See docs/verification/rederivation/struct.base-plate.md.
- A table-cell link or input link takes the upstream output in the input's unit and pins the revision. It is stale when the revision moves or the value differs by more than 1e-9 relative.

## Known-answer cases
The chain has no known-answer case of its own. SteelSupportChainTests holds four tests. I ran the Chains test classes: 38 pass in all.

| Test | What it asserts | What it does not assert |
|---|---|---|
| Combined_Actions_Reach_The_Frame_The_Column_The_Arm_Joint_And_The_Base_Plate | Each bound value equals its source in SI. v_max = V/6. t_max = M·0.15/(4 × 0.15²). F_b,Rd of S4b is 25/20 of S4a. All checks pass. | Whether S2's numbering is the one the links assume (-01). Whether M3's resistances still match S4a (-02). |
| A_Heavier_Load_Makes_Every_Step_Below_S5_Stale_Blocks_The_Pack_And_Updating_Carries_It_Through | A heavier load makes S2, S3, M3, S4a and S6 stale. Updating in order clears it. | An update out of order. S4b staying current. |
| Revising_The_Holding_Down_Bolts_Makes_The_Base_Plate_Stale_And_Updating_Carries_The_New_Bolt_Data_Through | A thicker plate in S4b raises F_b,Rd in S6. F_t,Rd stays. S4a is not stale. | That S6's plate thickness follows S4b's (-07). |
| The_Chain_Checks_And_Issues_As_One_Bundle_With_The_Anchorage_Report_Filed_As_Evidence | The pack checks and issues with the PROFIS placeholder as Evidence. The sheet names the cell link under its row. | What the placeholder proves (-04). |

## Extra probes (chain fed by hand)
Base case: the pack's example inputs from CalcPackExamples.cs. S2 is a 3 m cantilever column with a 1 m arm. A 5 kN horizontal action acts at node 2. M3 has six M20 bolts in two columns 120 mm apart and three rows 150 mm apart. S4a and S4b are M20 8.8 in S275. S6 is a 350 × 300 × 25 plate on C25/30.

| Probe | Step and inputs (short) | Symbol | Chain value | Correct or comparison value | Effect |
|---|---|---|---|---|---|
| T0 | S5 G 12 kN, Q 8 kN | E_d,min | −28.2 kN | −(1.35 × 12 + 1.5 × 8) | S5 right |
| T0 | S2 with the cell link | R1_y / R1_x / R1_m | 28.2 / −5 kN / 43.2 kN·m | 28.2; −5; 15 + 28.2 | Cell link and statics right |
| T0 | S2 | N_e1 / V_e1 / M_abs_e1 / V_e2 / M_abs_e2 | −28.2 / 5 / 43.2 / 28.2 / 28.2 | Hand values above | Ports right |
| T0 | S3 with N −28.2 kN, M 43.2 kN·m, V 5 kN | u_N / u_M_y / u_V / u_member | 0.0135 / 0.2448 / 0.0144 / 0.2892 | 43.2/176.435 = 0.2448; 497 cm³ × 355 MPa | Transfer and signs right |
| T0 | M3 with F_y 28.2 kN, M_x 28.2 kN·m | v_max / t_max | 4.7 / 47.0 kN | 28.2/6 = 4.7; 28.2e6 × 150/90000 = 47.0 | Per-bolt values right |
| T0 | S4a with F_v 4.7, F_t 47, V 28.2 kN | F_v,Rd / F_t,Rd / u_vt / u_block | 94.001 / 141.002 kN / 0.2881 / 0.0364 | 4.7/94.001 + 47/(1.4 × 141.002) = 0.2881 | Bolt checks right |
| T0 | S4b M20 8.8, 25 mm plate | F_t,Rd / A_s / F_b,Rd / f_ub / f_yb | 141.002 kN / 244.79 mm² / 248.48 kN / 800 / 640 MPa | 198.79 × 25/20 = 248.48 | Ports right |
| T0 | S6 with N 28.2, M 43.2, V 5 kN | T_Ed / F_t,Ed per bolt / u_bt / u_V | 175.21 kN / 87.60 kN / 0.621 / 0.031 | 43.2/0.2311 − 28.2 × 0.0961/0.2311 = 175.2 | Base plate right for the data given |
| T1 | S2 renumbered: arm is member 1, column member 2 | S3 N_Ed / u_M_y | 0 / 0.1598 | −28.2 kN / 0.2448 | Column check on the arm, 35 % low, no notice; see -01 |
| T2 | S4a changed to M16 | S4a F_t,Rd / u_t; M3 u_t | 90.24 kN / 0.5208; M3 stays 0.3333 | M3 with T_R 90.24: 0.5208 | M3 not stale and not updated; see -02 |
| T3 | S4b class 10.9 | f_yb / S6 | 900 MPa / Block "f_yb must be at most 640 MPa" | 10.9 anchors are common | Clear block; see -09 |
| T4 | S6 profis_ref blank, then the placeholder name | check "Anchorage designed externally" | Warning when blank; pass with any text | A placeholder proves nothing | See -04 |
| T5 | S6 x_b 150 mm, m left at 28 mm | T_Rd | 282.0 kN, unchanged | m should follow x_b | See -07 |
| T6 | S2 plus 5 kN horizontal at node 3 | N_e2 / M3 t_max | 5 kN / 47.0 kN, unchanged | N_e2 is not bound | See -08 |
| T7 | M3 pivot at the bottom row, hand | top-bolt tension | 37.6 kN | Centroid method 47.0 kN | Centroid is conservative here; see -12 |
| T8 | G = 12 kN column and arm self-weight, hand | ΔN_Ed | about +2.4 kN (+8.6 %) | 46 kg/m × 9.81 × 4 m × 1.35 | See -10 |

## Implementation comparison
| Aspect | Engineering need | Pack | Agrees? |
|---|---|---|---|
| S5 E_d,min → S2 F_y: sign and unit | Downward design load, negative, in kN | kN to kN, negative | Yes |
| S5 value: governing case | Maximum and minimum vertical load | Only the downward maximum, E_d,min | Partly, see -11 |
| S2 N_e1 → S3 N_Ed: sign | Compression negative in S3 | Both tension positive | Yes |
| S2 M_abs_e1, V_e1 → S3: value | Greatest magnitude along the column | Greatest |M| and |V| of member 1 | Yes (conservative as a single value) |
| S2 → S3: member identity | The column is member 1 | Fixed by the example numbering | No check, see -01 |
| S2 V_e2, M_abs_e2 → M3: value | Shear and moment at the joint face | Values at node 2 axis, 0.1 m further out | Yes; conservative by 11 % |
| S2 N_e2 → M3 N: axial force in the arm | Arm axial force acts on the end plate | Not bound; M3 N stays 0 | Partly, see -08 |
| M3 v_max, t_max → S4a: per bolt | Force on the worst bolt | Per bolt; same governing bolt in this pattern | Yes here; see propagation table |
| S2 V_e2 → S4a V_Ed: group | Group shear for block tearing | Group value | Yes (concentric only, connection-ec3-02) |
| S4a resistances → M3 V_R, T_R | One source for the resistances | Typed copies; no link, no stale | No, see -02 |
| Arm joint scope | Bolts, end plate, column flange and web, weld | Bolts only | Partly, see -03 |
| S2 R1_y, R1_m_abs, R1_x_abs → S6: sign | Compression positive; M and V unsigned | Same | Yes |
| S4b → S6: per bolt or group | F_t,Rd per bolt; S6 multiplies by n_t | Per bolt | Yes |
| S4b F_b,Rd → S6 F_1,vb,Rd | Bearing of the anchor on the plate | Bound; F_2,vb,Rd governs here | Yes; oversize holes unreduced (-06) |
| Anchorage | Concrete failure modes designed | PROFIS report as Evidence, any text passes | No, see -04 |
| Stale handling | Judged like input links | Same rules; transitive; S4b a separate branch | Yes (-13) |

## Findings
| ID | Severity | Reviewed | Finding | Evidence | Suggested fix (for Track S) |
|---|---|---|---|---|---|
| P1-chain.steel-support-01 | Medium | — | The links depend on S2's numbering, and nothing checks it. If member 1 is not the column, S3 checks the wrong member without a notice. The pack text states the numbering, but a user who adds a node or reorders members breaks it silently. Unconservative. | T1: swap the element ids so the arm is member 1. S3 receives N_Ed 0 and M_y 28.2 kN·m. u_M_y falls from 0.2448 to 0.1598 (35 % low). S2 raises no notice. | Bind by a named member, not by an index. Or warn when member 1 is not vertical. Or check that the member bound to S3 is the one with the largest |N|. |
| P1-chain.steel-support-02 | Medium | — | M3 holds typed copies of the bolt resistances V_R (94.0 kN) and T_R (141.0 kN). S4a, which computes them, comes after M3, so a link cannot run backwards. A change in S4a's bolt does not make M3 stale. M3 then reports a wrong utilisation. | T2: M16 in S4a gives F_t,Rd 90.24 kN and u_t 0.5208. M3 still shows u_t 0.3333 (36 % low). The test checks the copies only at build time. | Put the bolt resistance step before M3 and link V_R and T_R. Or drop the utilisation from M3 and rely on S4a. |
| P1-chain.steel-support-03 | Medium | — | The arm joint is checked at the bolts only. The pack text says it designs the bracket's bolted joint. Four checks are missing. They are end-plate bending with prying, the weld, the column flange in bending and the column web. Passing bolts do not make the joint. | M3 leaves the prying levers a and b blank, so t_max = 47.0 kN has no prying. The S4a checks are bolt shear, bearing, tension, combined and block tearing. No end plate or column step exists. | Add the T-stub checks of EN 1993-1-8 cl. 6.2.4 and 6.2.6. Or reword the pack text to "bolts of the joint". Enter a and b so prying is included. |
| P1-chain.steel-support-04 | Medium | — | The anchorage check cannot fail. It passes for any non-blank report reference, including the placeholder. The concrete failure modes of the anchors are the ones that usually govern, and none is computed. The S6 u_max of 0.62 covers the steel parts only. | T4: profis_ref "PROFIS-BP1-PLACEHOLDER.txt" gives a pass. Blank gives only a warning. The check text reads "bolt tension 87.603 kN and shear 0 kN per anchor designed in PROFIS". | Show the check as "not assessed" until the report carries a real result. Pass the anchor forces to the report as required inputs. State in the pack that u_max excludes the anchorage. |
| P1-chain.steel-support-05 | Medium | — | Three bolt-group defects reach the chain when the pattern is not symmetric or the load is eccentric. v_max and t_max belong to the governing bolt, not to the worst bolt for each. Tension from two moments drops a term. S4a then takes the wrong F_v,Ed or F_t,Ed. | Propagation of mech.bolt-group-02, -03, -04 and R1. The example is symmetric, so every bolt carries 4.7 kN of shear and the top bolts carry 47.0 kN. T0 matches the hand values. | Fix the bolt group once. Then add an eccentric-load case to the chain test. Warn on the pack that the pattern must be symmetric until then. |
| P1-chain.steel-support-06 | Low | — | Oversize holes are accepted without a reduction in S4. Base-plate holes for holding-down bolts are normally oversize. F_b,Rd is then up to 20 % high. It does not govern here, because F_2,vb,Rd (38.85 kN) is far lower. | T0: S4b F_b,Rd 248.48 kN, S6 F_vb,Rd 38.85 kN. Propagation of struct.connection-ec3-01. With the 0.8 factor F_b,Rd is 198.8 kN, still above 38.85 kN. | Fix connection-ec3-01. Add a hole-type choice to S4b. |
| P1-chain.steel-support-07 | Low | — | The T-stub geometry in S6 (m and l_eff) is typed and sits at its defaults. It does not follow x_b, the column size or the plate width. A change of x_b leaves the plate resistance unchanged. The defaults happen to fit the example. | T5: x_b 150 mm gives T_Rd 282.0 kN, the same as 135 mm. m stays 28 mm. | Derive m from x_b, h and the weld size. Or warn when m differs from x_b − h/2. |
| P1-chain.steel-support-08 | Low | — | The arm's axial force is not bound. M3 `N` stays 0. The example has no axial force in the arm, because the 5 kN acts at the column top. A horizontal load at the arm tip would pull on the end plate and the chain would not see it. | T6: a 5 kN horizontal load at node 3 gives N_e2 = 5 kN. M3 t_max stays 47.0 kN. | Bind S2 N_e2 to M3 N. Or warn on the pack that the arm must carry no axial force. |
| P1-chain.steel-support-09 | Low | — | A class 10.9 holding-down bolt cannot be built. S4b gives f_yb 900 MPa and S6 accepts at most 640 MPa. The result is a clear block, not a wrong number. It is P1-struct.base-plate-03 seen through the chain. | T3: S4b class 10.9 gives f_yb 900 MPa. S6 blocks "f_yb must be at most 640 MPa; 900 MPa was given". | Fix base-plate-03 so S6 accepts the true f_yb. Or say in the pack text that only up to class 8.8 is covered. |
| P1-chain.steel-support-10 | Low | — | The frame carries no self-weight of the column and the arm. Equipment and maintenance loads only. A 203 × 203 × 46 column and arm weigh about 1.8 kN. At 1.35 that adds about 8.6 % to N_Ed. It errs on the safe side for compression. | T8, hand value: 46 kg/m × 9.81 m/s² × 4 m × 1.35 = 2.4 kN on 28.2 kN. S5 has no self-weight action. | Add a self-weight action to S5 and a gravity row to S2. Or state in the pack that self-weight is excluded. |
| P1-chain.steel-support-11 | Low | — | Only one combination travels. S5 hands over E_d,min, the largest downward effect. The favourable case, with only permanent load, never reaches S2 or S6. In this geometry the arm eccentricity makes the largest load govern the anchor tension. A support with a small eccentricity could be governed by the favourable case. | S5 also gives E_d,max = −12 kN. By hand the base-plate tension rises with the vertical load here (1 m arm against a 0.23 m lever). | Hand over both E_d,max and E_d,min. Add a second combination to S2. Or state the assumption in the pack text. |
| P1-chain.steel-support-12 | Low | — | The same section and the same geometry are typed several times. S2 takes A and I. S3 takes A, W, I and shear helpers. S6 takes h, b, t_f and t_w. All describe the column section. M3 holds the bolt coordinates, S4a the pitches p_1 and p_2. S4b and S6 repeat the plate thickness and edge distances. Nothing links or compares them. The example is consistent. | S2 A 58.7 cm², I 4568 cm⁴ match S3's defaults. M3 rows 150 mm apart match S4a p_1 150. S4b t 25 mm matches S6 t_p. | List the repeated inputs in the pack text. Warn when a repeated value differs. |
| P1-chain.steel-support-13 | Low | — | The column check leaves out lateral-torsional buckling, and the minor-axis moment is typed 0. The column is a cantilever with a free top, where lateral-torsional buckling can govern. S3 warns "no-ltb", so this is disclosed. The u_member of 0.29 is an in-plane result only. | T0: S3 prints the warning "lateral-torsional buckling is not checked". M_z,Ed is 0 in the example. | Add the 6.3.2 check or state the limit in the pack text. Offer an out-of-plane eccentricity input. |
| P1-chain.steel-support-14 | Info | — | The bindings are right in kind, unit and sign. N_e1 keeps its compression sign into S3. R1_y keeps its up-positive sign into S6. Magnitudes go where the target takes unsigned values. Per-bolt values go to per-bolt inputs. | T0: S3 N_Ed −28.2 kN. S6 N_Ed +28.2 kN. S4a F_t,Ed 47.0 kN. S6 F_t,Rd 141.0 kN per bolt, 282.0 kN for two. | None. |
| P1-chain.steel-support-15 | Info | — | The centroid method is conservative for this end-plate joint. The top bolts get 47.0 kN. A pivot at the bottom row gives 37.6 kN. The joint moment is taken at the node, 0.1 m beyond the column face, which adds about 11 %. | T7, hand values: pivot at the bottom row, Σd² = 225000 mm², 28.2e6 × 300/225000 = 37.6 kN. Joint at 0.898 m instead of 1 m. | None. Note mech.bolt-group-06 for large eccentricities. |
| P1-chain.steel-support-16 | Info | — | Stale handling works. A heavier load makes every step below S5 stale. S4b is a separate branch and stays current when S5 or S2 change. A revision of S4b makes only S6 stale. The reverse dependency of -02 is the one gap. | SteelSupportChainTests (2nd and 3rd tests) pass. The first-row cell link rides on the cell provenance. | None beyond -02. |

## Earlier findings that propagate
I read the findings of the calculator reports. This table says which reach the chain. Rows here are not findings.

| Report and finding | Reaches the chain? | How |
|---|---|---|
| struct.frame-2d-01 (High) | In kind | A couple on a member hides one side of the moment jump, and M_abs_e<n> inherits it (frame-2d-D1). The example has no couple. |
| struct.frame-2d-02, R1 (blank guards) | No here | The example fills every cell. A blank "a" or F_y would run as 0 with no warning. |
| struct.frame-2d-03, -04, -05 | No | They concern false warnings, absolute deflection and a KAT note. |
| struct.load-combinations-01, -02 (Low) | Yes, if the UK NA is meant | The default ξ and ψ are EN recommended values. S5 hands the lower value to S2. |
| struct.load-combinations-03, -04 | No | They concern K_FI and an example text. |
| struct.en1993-member-02 (Medium) | Yes | k_yy = k_zz = 1 is unconservative where Annex B gives more. The example has u_member 0.29, so the margin is wide. |
| struct.en1993-member-01, -03 | No here | High-shear reduction is not reached (u_V 0.014). |
| struct.en1993-member-04 to -06, R2, R3 | No | They concern tension, net section, gamma factors and web buckling. |
| struct.en1993-member-R1 | Yes, if changed | The shear-area helpers carry UC 203 defaults. A changed section needs them changed too. See -12. |
| mech.bolt-group-02 to -04, R1 | Yes, if asymmetric | See -05. |
| mech.bolt-group-05 | Yes | Prying is ignored when a and b are blank. See -03. |
| mech.bolt-group-06 (Info) | Slightly | The centroid pivot is conservative here. See -15. |
| mech.bolt-group-01 (High) | No here | The linear check is evaluated at the governing bolt. All top bolts are alike. |
| struct.connection-ec3-01 (High) | Yes | Oversize holes in S4b. See -06. |
| struct.connection-ec3-02 (Medium) | Yes, slightly | Block tearing is concentric only. u_block is 0.036, so it does not matter here. |
| struct.connection-ec3-03 (High) | No | The single-lap cap applies to lap joints with one row. |
| struct.connection-ec3-R1 | No | M12 only. The example uses M20. |
| struct.connection-ec3-05 to -09, R2, R3, C1 | No | They concern spacing guards, stress area text and the legacy picker. |
| struct.base-plate-03 (Low) | Yes | See -09. |
| struct.base-plate-R1 (Medium) | Yes | One f_y at every plate thickness. The 25 mm plate at 275 MPa is 3.8 % high if EN 10025-2 gives 265 MPa. The utilisations are low. |
| struct.base-plate-01, -02, -04 to -07 | No | They concern CHS columns, z_C_in, messages and defaults. The example uses I/H and a blank z_C_in. |

## Notes for the owner's manual test
- Build S5 with G 12 kN and Q 8 kN, 6.10. Expect E_d,min −28.2 kN.
- Build S2 with the nodes, members and loads of the example. Link S5 to the first load row. Expect R1_y 28.2 kN, R1_m 43.2 kN·m and R1_x −5 kN.
- Link S2 to S3. Expect N_Ed −28.2 kN, M_y,Ed 43.2 kN·m, V_Ed 5 kN and u_member 0.289.
- Build M3 and link V_e2 and M_abs_e2. Expect v_max 4.7 kN and t_max 47.0 kN. Link both to S4a and expect u_vt 0.288.
- Build S4b and link its five outputs to S6. Expect S6 T_Ed 175.2 kN, F_t,Ed 87.6 kN per bolt and u_max 0.621.
- Swap the member ids in S2 and look at S3 (-01). Change S4a to M16 and look at M3 (-02).
- Enter the placeholder reference in S6 and note that the anchorage check passes (-04).

## First-pass notes
- Reviewer: P1 chain pass (Claude), 2026-10-24. This is a first pass. The Reviewed column is "—" until the independent review.
- I read the pack, its examples, the instantiation and dry-run code, and the four chain tests. I ran the Chains test classes (38 pass).
- I fed S5, S2, S3, M3, S4a, S4b and S6 by hand with the probe. I checked each against a hand calculation.
- The self-weight in -10 and the pivot-at-bottom value in -15 are hand values. The probe has no self-weight load for this model.
- Residual doubt: -03 depends on how much of the joint the owner expects the pack to cover.
- Residual doubt: -04 depends on whether the PROFIS report is meant to carry its own check.
