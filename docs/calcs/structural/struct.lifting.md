# Lifting lugs and spreader beams (`struct.lifting@1.2.0`)

**In plain terms.** A lifting lug is a plate with a hole through which a pin takes the weight of a load, and it can tear, crush or shear out at the hole. The worksheet takes the rated load, the sling angles and the lug geometry, finds the force each lug really sees, and checks each way the lug could fail against design factors you supply from your own copy of the code. It is a design calculation, never a thorough examination, and it stays switched off in the product until the owner enables it.

## Scope

Calculates the capacity and utilisation of a pin-loaded lifting lug and of a spreader beam by the BTH-1 approach, the EN 13155 proof route or both. It is a design calculation, not a thorough examination under LOLER or PUWER.

## Method

- Sling force: F_v = WLL/n_eff (n_eff = n_lugs, at most 3); F_s = F_v/(cos θ·cos β); F_ip = F_s·cos β; F_op = F_s·sin β; N_beam = F_v·tan θ; M_op = F_op·h
- Horizontal beam, automatic 6° tilt: pitch (θ ± 6°, load redistributed, the sling at θ − 6° takes 2·F_v·sin(θ + 6°)/sin 2θ, and the force along the tilted beam is F_v·sin(θ + 6°)/cos θ); roll (β + 6°); the larger F_s governs, F_ip, F_op and N_beam are the envelopes
- Lug section geometry: side = min(b_e, r_lug − D_h/2); b_e,eff = min(side, 4t_p, 0.6·(f_u/f_y)·√(D_h/side)·side); a_eff = a (default r_lug − D_h/2); A_t = 2·t_p·b_e,eff; A_fr = [1.13·a_eff + 0.92·side/(1 + side/D_h)]·t_p; φ = 55·D_p/D_h (degrees); A_v = 2·t_p·[a_eff + (D_p/2)(1 − cos φ)]; A_b = D_p·t_p
- Pin clearance: C_r = 1 − 0.275·√(1 − D_p²/D_h²)
- BTH-1 (3-3.3): F_a = min(f_y/n_y, f_u/n_u); design load D = N_d·k_svc·F_s; P_t = C_r·F_a·A_t; P_sp = C_r·F_a·A_fr; P_v = F_a·A_v/√3; P_b = k_b·F_a·A_b; u = D/P for each; dishing D_h/t_p ≤ dish_max
- EN 13155 (2× elastic, 3× no release): P_EN = min(Y/2, U/3) for each section, Y with f_y and U with f_u (shear with f/√3; fracture area without C_r); u = F_s/P_EN
- Lug capacity and proof load: WLL_cap = WLL/u_lug for the governing section of each mode (smaller of the modes used); beam: u_beam from N_beam on A_beam; proof load = k_proof·WLL

The scope in detail: A pin-loaded lifting lug (a plate with a bored hole, loaded by a pin) and the lug loads of a spreader or lifting beam: the sling force on each lug from the rated load, the sling angle and the out-of-plane angle; tension beyond the hole, single-plane fracture beyond the hole, shear-out on two planes, pin bearing and the dishing ratio; capacity and utilisation by the BTH-1 allowable-stress approach (clause 3-3.3, cited by number only), by the EN 13155 proof route (2× the rated load without permanent set, 3× without release, at most 16,000 cycles) or by both; the governing mode; a proof-load value; and a marking and certification checklist as text.

Formulas in the author's notation (the template method text carries them). Angles in degrees at the interface; N, mm, MPa inside. The geometry and capacity relations follow the failure modes of the published course example the sheet is checked against (below); only their clause numbers are cited, never their text.

- *Loads.* `n_eff = min(n_lugs, 3)`; `F_v = WLL/n_eff` (vertical load on each lug); the sling leaves the lug at `θ` in the lug plane and `β` out of it, so the sling force is `F_s = F_v/(cos θ · cos β)`, `F_ip = F_s cos β` (= `F_v/cos θ`), `F_op = F_s sin β`, and the force along the beam axis (the axial force in a spreader beam, for S7) is `N_beam = F_v tan θ`; out-of-plane moment `M_op = F_op·h` when the lever arm `h` is given.
- *Tilt of a horizontal beam (EN 13155).* With the `beam` switch the automatic 6° tilt is applied and the larger sling force governs:
  - *Pitch* (about a horizontal axis across the beam): the slings leave at `θ + 6°` and `θ − 6°`. The slings meet at the hook, so their horizontal components are equal and opposite and the vertical components carry the load; the sling at `θ − 6°` carries `F_s,pitch = 2 F_v sin(θ + 6°)/sin 2θ` (in the lug plane; divided by `cos β` for the resultant). If `θ ≤ 6°` the tilted beam can put the whole load on one vertical sling and `F_s,pitch = WLL/cos β`. A single lug has no other lug to take load off it, so the pitch case is the nominal one.
  - *Roll* (about the beam's own axis): the out-of-plane angle grows by 6°: `F_s,roll = F_v/(cos θ · cos(β + 6°))`.
  - *Force along the beam.* The hook–lug–lug triangle is rigid with the beam, so in the pitch case each sling stays at `θ` to the beam's normal and sends `F·sin θ` along the beam: at the more loaded lug `N_beam,pitch = F_v·sin(θ + 6°)/cos θ` (not the horizontal component in the global frame, which is smaller). Check: the two slings' axial forces differ by the load's component along the beam, `(F₁ − F₂)·sin θ = W·sin 6°`. For `θ ≤ 6°` (the whole load on one vertical sling) it is `W·sin 6°` along the tilted beam, which is also what the formula gives at `θ = 6°`.
  - `F_s` is the maximum of the nominal, pitch and roll cases; `F_ip` and `F_op` are the envelopes over the cases, and `N_beam` is the larger of the level `F_v tan θ` and the pitched `N_beam,pitch`.
- *Geometry.* In the sheet's names `b_e` is the distance from the hole edge to the plate edge on each side across the load line and `a` the end distance along it (default `r_lug − D_h/2`). `side = min(b_e, r_lug − D_h/2)`; effective width `b_e,eff = min(side, 4t_p, 0.6·(f_u/f_y)·√(D_h/side)·side)`; `A_t = 2·t_p·b_e,eff` (tension beyond the hole); `A_fr = [1.13·a + 0.92·side/(1 + side/D_h)]·t_p` (single-plane fracture beyond the hole); `φ = 55·D_p/D_h` (degrees); `A_v = 2t_p[a + (D_p/2)(1 − cos φ)]` (two shear planes); `A_b = D_p·t_p` (bearing).
- *BTH-1 mode (3-3.3).* One allowable stress `F_a = min(f_y/n_y, f_u/n_u)` for every section; pin-clearance factor `C_r = 1 − 0.275√(1 − D_p²/D_h²)`, applied as stated for `D_p/D_h < 1` with no cut-off (conservative; see the owner questions); design load `D = N_d·k_svc·F_s`; `P_t = C_r F_a A_t`, `P_sp = C_r F_a A_fr`, `P_v = F_a A_v/√3` (√3 is the distortion-energy shear ratio, derived), `P_b = k_b F_a A_b`; `u = D/P` for each; dishing `D_h/t_p ≤ dish_max`. Capacity `cap_bth = WLL/max(u)`.
- *EN 13155 mode.* The four sections with `Y = f_y·A` (shear `f_y/√3·A_v`) and `U = f_u·A` (shear `f_u/√3·A_v`), the fracture area taken without `C_r`: `P_EN = min(Y/2, U/3)` (no permanent set at 2× the rated load, no release at 3×); `u = F_s/P_EN`; the governing route is named ("2×WLL elastic" or "3×WLL no release"). Capacity `cap_en = WLL/max(u)`. The notch-stress route of EN 13001-3-1 (cited) is not implemented; this is a nominal-section proof.
- *Both.* Every check of both modes is listed; the capacity is `WLL/lug_util` with `lug_util` the larger utilisation, i.e. the smaller of the two capacities.
- *Spreader beam.* With the `beam` switch and `A_beam`: axial utilisation `N_d k_svc N_beam/(A F_a)` (BTH-1) and/or `max(2N_beam/(A f_y), 3N_beam/(A f_u))` (EN 13155), larger of the two for both modes; beam buckling is S7's (link `N_beam`).
- *Governing.* `util = max(lug_util, u_beam, u_dish)` (the port `lifting.utilisation`); `capacity = WLL/lug_util` in kN (`lifting.capacity`); `governing_mode` text, e.g. "BTH-1: bearing" or "EN 13155 (2×WLL elastic): double-plane shear"; a failed dishing limit is named in it. The capacity does not depend on `WLL` (it is the rated load the lug would carry); utilisation is proportional to it.
- *Proof load.* `proof_load = k_proof·WLL`.

## Inputs

| Symbol | Description | Unit | Default | Usual range | Group | Old symbols |
|---|---|---|---|---|---|---|
| `mode` | Design mode | — | both | BTH-1, EN 13155, both | Setup | — |
| `n_lugs` | Number of lugs sharing the load (a fourth is not relied on: three share) | — | 2 | 1, 2, 3, 4 | Setup | — |
| `beam` | The lugs are on a horizontal spreader or lifting beam (adds the automatic 6° pitch and roll tilt cases) | — | 0 | — | Setup | — |
| `t_p` | Lug plate thickness | mm | 20 | 8 to 100 | Geometry | `t` |
| `Dh` | Hole diameter | mm | 50 | 16 to 300 | Geometry | — |
| `Dp` | Pin diameter (not larger than the hole) | mm | 45 | 12 to 300 | Geometry | — |
| `r_lug` | Lug end radius about the hole centre (the plate is twice that wide at the hole) | mm | 65 | 20 to 300 | Geometry | `R` |
| `be` | Edge distance from the hole edge to the plate edge on each side, across the load line (limited to r_lug − D_h/2; the effective width for tension is reduced further by 4t_p and by f_u/f_y) | mm | 35 | 10 to 200 | Geometry | — |
| `a` | End distance from the hole edge to the plate edge along the load line (optional; blank = r_lug − D_h/2, the round end) | mm | blank | 10 to 300 | Geometry | — |
| `h` | Distance from the hole centre to the weld or base, for the out-of-plane moment (optional; blank = no moment given) | mm | blank | 10 to 1500 | Geometry | — |
| `A_beam` | Gross cross-section area of the spreader or lifting beam, for its axial utilisation (optional; blank = lug utilisation only) | cm² | blank | 10 to 10000 | Geometry | — |
| `f_y` | Yield strength of the lug plate for its thickness | MPa | 355 | 235 to 960 | Material | — |
| `f_u` | Ultimate tensile strength of the lug plate | MPa | 470 | 340 to 1100 | Material | — |
| `WLL` | Rated load (working load limit) of the appliance, carried by all its lugs | kN | 150 | 5 to 5000 | Loads | — |
| `theta` | Sling angle θ from the vertical, measured in the plane of the lug (the out-of-plane angle β is applied on top: F_s = F_v/(cos θ·cos β)) | deg | 30 | 0 to 45 | Loads | — |
| `beta` | Out-of-plane angle β of the sling out of the lug plane | deg | 0 | 0 to 15 | Loads | — |
| `n_cycles` | Load cycles in the life of the lug (the 2×/3× route covers up to 16,000, and beyond that a fatigue assessment is needed and the sheet does not run) | — | 1000 | 1 to 10000 | Conditions | — |
| `design_category` | Design Category the design factor belongs to (optional; blank = no label printed) | — | blank | — | Factors | — |
| `service_class` | Service Class the service factor belongs to (optional; blank = no label printed) | — | blank | — | Factors | — |
| `Nd` | Design factor of the Design Category (optional; blank = BTH-1 mode is blocked; user record) | — | blank | 1 to 3 | Factors | — |
| `k_svc` | Service-class factor on the load (optional; blank = 1.0; user record) | — | blank | 1 to 2 | Factors | — |
| `n_y` | Divisor on the yield strength in the allowable stress (optional; blank = BTH-1 mode is blocked; user record) | — | blank | 1 to 3 | Factors | — |
| `n_u` | Divisor on the ultimate tensile strength in the allowable stress (optional; blank = BTH-1 mode is blocked; user record) | — | blank | 1.5 to 5 | Factors | — |
| `k_b` | Bearing multiple of the allowable stress on the projected pin area (optional; blank = BTH-1 mode is blocked; user record) | — | blank | 1 to 2 | Factors | — |
| `dish_max` | Dishing-ratio limit on the hole diameter over the thickness (optional; blank = the dishing check is not made; user record) | — | blank | 5 to 60 | Requirements | — |
| `k_proof` | Proof-load factor on the rated load (optional; blank = no proof load given; user record) | — | blank | 1 to 2 | Requirements | — |
| `attest_name` | Competent person attesting the design (not the author; required to check and issue) (optional) | — | blank | — | Competent-person attestation (required to check and issue) | — |
| `attest_qualification` | Their qualification and standing (optional) | — | blank | — | Competent-person attestation (required to check and issue) | — |
| `attest_statement` | Attestation: what the competent person has reviewed (at least 20 characters) (optional) | — | blank | — | Competent-person attestation (required to check and issue) | — |

## Assumptions

1. This is a design calculation, not a thorough examination. Basis: It checks the lug plate at the pin hole against the user's chosen criteria and nothing else. The owner and the competent person keep the LOLER reg 9 and PUWER duties.
2. The sheet screens four sections by nominal stress: tension beyond the hole, single-plane fracture, shear-out and bearing. Basis: There is no stress-concentration factor, no fatigue (at most 16,000 cycles) and no weld, bolt or beam check. The notch-stress route of EN 13001-3-1 is cited, not implemented.
3. The load on the hole is the resultant sling force F_s whatever the out-of-plane angle, which is conservative. Basis: The out-of-plane component and its moment are outputs for the weld and base checks: F_op = F_s·sin β.
4. The lugs share the load equally, and a fourth lug is not relied on. Basis: This is a conservative convention. The share of a statically indeterminate set is not computed, so replace the sharing with the actual share if it is known.
5. A horizontal beam adds an automatic 6° tilt as a pitch case and a roll case. Basis: Pitch: the sling angles are θ ± 6° with the load redistributed between the lugs by statics, and the slings meet at the hook. Roll: the out-of-plane angle grows by 6°. N_beam is the force along the beam axis, and in the pitch case the more loaded lug sends F_v·sin(θ + 6°)/cos θ along it. With θ ≤ 6° the pitch case puts the whole load on one lug. With more than two lugs the two-lug redistribution is applied to each lug's share. Owner's specification (6° tilt of a horizontal beam); EN 13155.
6. The allowable stress is the one lesser value min(f_y/n_y, f_u/n_u) for every BTH-1 section. Basis: This follows the published worked example the sheet is checked against. The pin-clearance factor C_r is applied as stated from D_p/D_h < 1 with no cut-off. Conservative; owner checklist.
7. TempestOS does not authenticate signatures, so any person can switch to another name. Basis: The platform asks for no password, so the author, the two checkers and the competent person are as entered into this installation. ADR-0173; the feature stays off until this is addressed.
8. The design factor, service-class factor, divisors, bearing multiple, dishing limit and proof-load factor are the user's records. Basis: They come from the user's own licensed copy of the code. None is shipped and none has a default, and the text and tables of ASME BTH-1 and EN 13155 are not shipped.
9. The owner compares the equations in the spec document with BTH-1 and EN 13155 before the feature is enabled. Basis: The single-plane fracture and effective-width relations and the constants 1.13 and 0.92 follow the published course example. The review checklist of the owner test pack lists the comparison.

## Outputs

| Symbol | Description | Unit |
|---|---|---|
| `governing_mode` | Governing mode and section | — |
| `capacity` | Rated load the lug carries in the governing mode | kN |
| `util` | Governing utilisation of the lug and the spreader | — |
| `lug_util` | Governing lug utilisation | — |
| `cap_bth` | Capacity, BTH-1 mode | kN |
| `u_bth` | Utilisation, BTH-1 mode | — |
| `F_s` | Sling force on each lug | kN |
| `F_ip` | In-plane component | kN |
| `F_op` | Out-of-plane component | kN |
| `N_beam` | Horizontal component per lug | kN |
| `M_op` | Out-of-plane moment | kN·m |
| `side` | Edge distance on each side of the hole | mm |
| `be_eff` | Effective width beyond the hole | mm |
| `a_eff` | End distance along the load line | mm |
| `Cr` | Pin-clearance factor C_r | — |
| `phi` | Shear-plane angle φ | deg |
| `A_v` | Shear area | mm² |
| `A_t` | Tension area beyond the hole | mm² |
| `A_fr` | Fracture area beyond the hole | mm² |
| `A_b` | Bearing area | mm² |
| `Fa` | Allowable stress | MPa |
| `D_bth` | Design load on each lug | kN |
| `P_t` | Tension beyond the hole | kN |
| `P_sp` | Single-plane fracture | kN |
| `P_v` | Double-plane shear | kN |
| `P_b` | Bearing | kN |
| `u_t` | Utilisation, tension | — |
| `u_sp` | Utilisation, fracture | — |
| `u_v` | Utilisation, shear | — |
| `u_b` | Utilisation, bearing | — |
| `dish` | Dishing ratio | — |
| `u_dish` | Dishing utilisation | — |
| `proof_load` | Proof-load value | kN |
| `sheet_notice` | Status of this sheet | — |
| `checklist` | Marking and certification checklist | — |

## Ports

| Port | Output symbol | Description | Unit | Old symbols |
|---|---|---|---|---|
| `lifting.capacity` | `capacity` | Rated load the lug carries in the governing mode | kN | — |
| `lifting.mode_governing` | `governing_mode` | The governing mode and section | — | — |
| `lifting.utilisation` | `util` | Governing utilisation of the lug and the spreader beam | — | — |
| `lifting.force_sling` | `F_s` | Sling force on each lug (resultant), for the pin, shackle and sling | kN | — |
| `lifting.force_in_plane` | `F_ip` | In-plane component of the sling force, for the weld and bolt group checks | kN | — |
| `lifting.force_out_of_plane` | `F_op` | Out-of-plane component of the sling force, for the weld and bolt group checks | kN | — |
| `lifting.force_beam_axial` | `N_beam` | Axial force along the spreader beam at the more loaded lug, the larger of the level and the 6° pitched case | kN | — |

## Checks

Checks the sheet makes:

- Design load against the tension resistance beyond the hole (BTH-1)
- Design load against the single-plane fracture resistance (BTH-1)
- Design load against the double-plane shear resistance (BTH-1)
- Design load against the bearing resistance (BTH-1)
- Dishing ratio against its limit (BTH-1)
- Sling force against the tension resistance beyond the hole (2×WLL elastic)
- Sling force against the single-plane fracture resistance (2×WLL elastic)
- Sling force against the double-plane shear resistance (2×WLL elastic)
- Sling force against the bearing resistance (2×WLL elastic)
- Sling force against the tension resistance beyond the hole (3×WLL no release)
- Sling force against the single-plane fracture resistance (3×WLL no release)
- Sling force against the double-plane shear resistance (3×WLL no release)
- Sling force against the bearing resistance (3×WLL no release)
- Beam axial utilisation against its limit

Applicability rules:

| Rule | Severity | Effect | Condition |
|---|---|---|---|
| `pin` | Block | Conservative | The pin cannot be larger than the hole. |
| `radius` | Block | Conservative | The lug end radius must exceed the hole radius. |
| `strength` | Block | Conservative | f_u cannot be below f_y. |
| `factors` | Block | Conservative | BTH-1 mode needs the user-record factors N_d, n_y, n_u and k_b. |
| `clearance` | Warning | Conservative | A pin much smaller than the hole reduces C_r. |
| `be` | Warning | Conservative | b_e is limited to r_lug − D_h/2. |
| `be-eff` | Warning | Conservative | The effective width beyond the hole is below the edge distance. |
| `tilt` | Warning | Conservative | A horizontal beam adds the 6° tilt case. |
| `sharing` | Warning | Conservative | Four lugs: three are taken to share the load. |
| `angle` | Warning | Conservative | A sling angle above 60° from the vertical, with the tilt, is outside the usual range. |
| `k-svc` | Warning | Unconservative | No service-class factor entered: 1.0 used. |
| `dishing` | Warning | Unconservative | No dishing limit entered: the dishing check is not made. |
| `proof` | Warning | Conservative | No proof-load factor entered: no proof load given. |
| `beam-area` | Warning | Unconservative | No beam area entered: the spreader utilisation is the lug utilisation only. |
| `attestation` | Warning | Conservative | The competent-person attestation and the second check are required to check and issue. |

## Limitations

**Status.** Behind the `lifting` feature flag (`ADR-0173`): composed into the build but withheld from the catalogue, the Worksheets tree, the Ctrl+K entries, every service path and every pack until the machine's administrator switches it on: on Windows the policy value `HKLM\SOFTWARE\Policies\TempestOS\Features` `Lifting` = DWORD 1, elsewhere `"Lifting": true` in the root-owned `/etc/tempestos/features.json` (the file and its directory writable by no one but root). No other source is read: not the per-user install folder, an `appsettings.json`, the environment, the command line or the current directory (`ADR-0173`). The older lifting-lug and pin-joint module (`calc.lifting-lug-pin-joint`, Engineering Calculators) is withheld by the same flag, down to the engine's run-by-id path (`ADR-0173`). Issue needs a competent-person attestation (`AttestAsync`, by a signed-in person other than the authors) and a second, independent check. **The flag stays off until the owner has compared the equations below with BTH-1 and EN 13155+A1 (owner test pack, S8 review checklist, §B10 item 6).**

**This sheet is a design calculation, not a thorough examination.** It does not replace the thorough examination and inspection of lifting equipment by a competent person (LOLER reg 9), the marking, test and certification the regulations and the standards require (LOLER reg 7), the planning of the lifting operation, the weld, bolt and beam checks on the linked sheets, or the rating of the pin, shackle and sling (PUWER reg 4, reg 5). The sheet prints this statement in its scope, as the output `sheet_notice`, and in the marking and certification checklist (`checklist`).

**Signatures are not authenticated by TempestOS.** The names of the author, the checkers and the competent person on the sheet are the sign-in names of this installation (recorded beside the typed name and statement); TempestOS does not verify that anyone is who they say, that the attester is competent, or that the second checker is independent in fact. That is the owner's and the company's to establish. The sheet prints the notice in its scope, in an assumption and as the output `sheet_notice`.

**Governance.**

- *Attestation.* A template that requires one is attested with `AttestAsync` (the Desktop form's **Attest** button), which records the competent person under the **signed-in principal**, not under typed text: the attester must be a known signed-in person who is none of the calculation's authors (the creator, anyone who set or changed an input, or any member of its pack), compared by sign-in id and by the display name the principal directory gives it (on Windows the sign-in id is a SID, which no typed name resembles). The name, qualification and statement typed (statement at least 20 characters) are display text: they are written into the three attestation inputs, with the attester as their source, and stored with the principal and the time. A later change to any input voids the attestation. A first checker must be someone other than the attester, and an attestation is not required to *reject* a calculation.
- *Second check.* Issue demands a **second Checked signature** (`CountersignAsync` / `CountersignPackAsync`, the form's **Countersign** button) by a signed-in person who is none of the authors, not the attester and not the first checker (by sign-in or typed name); the statement cannot be empty (at least 20 characters). A rejection is recorded, refuses Issue, and lets the checked calculation be revised. The issued sheet header reads "checker; second check: name"; the attestation prints in the inputs table; the issued snapshot records that the lifting feature was enabled and where.
- *Evidence.* The calculation's Evidence record is managed by its worksheet (`ADR-0173`): the generic Evidence Check, Issue and Revise acts refuse it, and its editor offers "Open worksheet" instead.

Nominal-stress screening of four sections on a plate loaded through a pin; no stress concentration, no fatigue (cycles limited to 16,000 by a hard limit), no buckling of the lug, no weld, bolt or beam check (linked sheets), no pin, shackle or sling rating; equal sharing between the lugs with a fourth not relied on; one pin diameter and a round-ended or straight-ended lug (through `a`); the horizontal-beam tilt is the 6° pitch and roll of EN 13155 applied one at a time, not both together. The design factor, service-class factor, allowable-stress divisors, bearing multiple, dishing limit and proof-load factor are the user's records; none is shipped, none has a default (ASME BTH-1 and EN 13155 text and tables are never shipped). **Signatures are not authenticated by TempestOS** (above).

**Owner questions (§B10 item 6, to be answered before the flag is enabled).** These are choices the sheet makes that the standards, not the author, must settle:

1. *Signatures.* TempestOS does not authenticate signatures or competence (above). Is the company's own procedure for who may attest, check and countersign, and for how that is evidenced, adequate for the sheet to be issued from?
2. *Four lugs.* The sheet takes `n_eff = min(n_lugs, 3)`: with four lugs three are assumed to share the load, a fourth is not relied on. Is that the rule BTH-1 and the company's lifting procedure intend for a statically indeterminate four-lug set, or is a different share required?
3. *Dishing.* The dishing ratio is taken as `D_h/t_p` against a user-record limit `dish_max`. Is that the definition the code uses (the code may define it on the pin or hole and the plate thickness differently)?
4. *`C_r` cut-off.* `C_r` is applied as `1 − 0.275√(1 − (D_p/D_h)²)` for every `D_p/D_h < 1`, with no cut-off. The course example and the code may take `C_r = 1` for a close-fitting pin (a ratio at or above a stated limit); omitting the cut-off is conservative. Is the cut-off to be added, and at what ratio?
5. *Tilt.* The 6° tilt is applied as a pitch case and a roll case taken one at a time, and the larger sling force governs. Does EN 13155 require both at once, or a different angle?
6. *Allowable stress.* One value `min(f_y/n_y, f_u/n_u)` is used for every BTH-1 section, as in the course example; does BTH-1 take separate allowables for yield-governed and fracture-governed sections?

**Not pinned (owner check).** The BTH-1 clause numbers other than 3-3.3 and the EN 13155 clause numbers (the sheet says "clause to be confirmed"); the LOLER and PUWER regulation numbers in the checklist (reg 7, 9; reg 4, 5) are from memory of the Regulations and to be confirmed.

### Changes by version

**1.2.0 (C-2 consistency).** The lug plate thickness `t` is now `t_p` (alias `t`, because a bare `t` means time), and the lug end radius `R` is now `r_lug` (alias `R`, because a bare `R` means a resistance). Every check shows its demand and capacity with units: the design load against each BTH-1 resistance, the sling force against each EN 13155 resistance, the dishing ratio against its limit, and the beam utilisation against the limit 1 (new step `u_limit`). The inputs are grouped Setup, Geometry, Material, Loads, Conditions, Factors and Requirements, with the optional hints in the standard form, and every numeric input has a usual range inside its unchanged hard range. The seven ports have public names `lifting.*`. The scope is shorter and still names the design-calculation notice, LOLER and PUWER. The messages, assumptions and references follow the sheet standard. The LOLER and PUWER references now cite the HSE books L113 and L22 that print the regulations, and the owner confirms their editions. The standards BTH-1, EN 13155 and EN 13001-3-1 keep their form, because the year of the owner's copy is not recorded. The rule `be-eff` is now declared. No calculated value changes.

## References

- ASME BTH-1, cl. 3-3.3: Pin-connected plates (design of lifting lugs).
- BS EN 13155: Non-fixed load lifting attachments: 2× elastic, 3× no release (clause to be confirmed).
- BS EN 13001-3-1, cl. 5: Proof of static strength: the calculation route cited, not implemented.
- Health and Safety Executive, Safe use of lifting equipment (L113), reg. 7 and 9, 2nd ed.: LOLER 1998: marking of lifting equipment; thorough examination and inspection.
- Health and Safety Executive, Safe use of work equipment (L22), reg. 4 and 5, 4th ed.: PUWER 1998: suitability and maintenance of work equipment.

## Test cases

**Test cases.** Illustrative user-record factors `N_d` 1.8, `n_y` 1.6, `n_u` 2.2, `k_b` 1.2, `dish_max` 40, `k_proof` 1.5 are not values from any standard.

- Analytic, **Confirmed** (recomputed in python, `s8_ref2`, from the equations above): `ka-bth1-hand` (two lugs, 150 kN, 30°/5°: every section, bearing governs), `ka-en13155-hand` (EN 13155 mode, f_y 235 / f_u 360: the 2× elastic route governs the shear-out), `ka-both-beam-tilt` (four lugs sharing as three, 400 kN, with the 6° pitch and roll cases and a 120 cm² beam), `ka-both-shear-governs` (both modes, BTH-1 shear-out governs).
- **Secondary, Confirmed by recompute:** `ka-pdhonline-s106`, the PDHonline course S106 worked example (course notes, a secondary source; not a standard). Inputs as published: plate 1.25 in thick, hole 1.25 in, pin 0.75 in, side distance 1.125 in, end distance 1.125 in, `F_u` 58 ksi, `F_y` 36 ksi, allowable stress `min(F_u/5, F_y/3) = 11.6 ksi` (so `n_y` 3, `n_u` 5, `N_d = k_b = 1`), unit rated load. All six published values are pinned: `C_r = 0.78`, `φ = 33°`, `A_v = 2.964 in²`, tension 25.448 kip, bearing 10.875 kip, double-plane shear 19.849 kip and single-plane fracture 20.539 kip. The python recompute reproduces them to the rounding of the published three decimals (tension 25.4475, single-plane 20.5388, shear 19.84899, bearing 10.875, `A_v` 2.96375). The case is a check of the *equations of this sheet against the course*, not against BTH-1.

**Worked example.** `docs/worked-examples/structural/struct.lifting/lifting-beam-lug-pair/`: two lugs on a 150 kN horizontal lifting beam at 30° and 2° (with the 6° pitch and roll cases), both modes, all inputs with provenance, with the feature flag on in the test harness only. The attestation inputs are left blank (an example is not an issued sheet).

- `ka-bth1-hand`: BTH-1 mode, two lugs at 30° and 5° out of plane, 150 kN: every section hand-recomputed. Source: Analytic: the equations of the spec document (docs/calcs/structural/struct.lifting.md) evaluated independently in python (s8_ref2); the design factors are illustrative user-record values, not values from any standard, struct.lifting spec 1.1.0.
- `ka-en13155-hand`: EN 13155 mode, two lugs at 20°, 100 kN, f_y 235 / f_u 360: the 2×WLL elastic route governs the shear-out. Source: Analytic: 2× the rated load without permanent set (capacity at f_y) and 3× without release (capacity at f_u) on the four sections of the spec, evaluated independently in python (s8_ref2), struct.lifting spec 1.1.0, EN 13155 mode.
- `ka-both-beam-tilt`: Both modes on a horizontal beam: four lugs (three share), 400 kN at 25° and 3°, with the automatic 6° tilt (pitch redistribution and roll), 120 cm² beam. Source: Analytic: the spec equations with F_v = 400/3 = 133.333 kN; nominal, pitch (sling angles 31° and 19°, F_s = 2·F_v·sin 31°/sin 50°/cos 3°) and roll (β + 6° = 9°) cases evaluated independently in python (s8_ref2); the pitch case governs, struct.lifting spec 1.1.0, both modes.
- `ka-both-shear-governs`: Both modes, shear-out governs: the geometry of the EN 13155 case with the BTH-1 user-record factors. Source: Analytic: the spec equations, evaluated independently in python (s8_ref2), struct.lifting spec 1.1.0, both modes.
- `ka-pdhonline-s106`: PDHonline S106 worked example: 1.25 in plate, 1.25 in hole, 0.75 in pin, F_u 58 ksi, F_y 36 ksi, allowable stress min(F_u/5, F_y/3). Source: PDHonline course S106 (Design/Evaluation of Overhead Lifting Lugs), worked example, Failure Modes 1.1, 2, 3.1 and 4.1 (course notes: a secondary source).

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Lifting beam, two lugs at 30° and 2° out of plane, 150 kN rated load, BTH-1 and EN 13155 modes with the automatic 6° tilt cases (feature flag on in the test harness only) ([sheet](../../worked-examples/structural/struct.lifting/lifting-beam-lug-pair/sheet.txt)).

```text
n_eff = min(2, 3) = 2
F_v = 150 kN / 2 = 75 kN
F_s_nom = 75 kN / (cos 30° × cos 2°) = 86.655 kN
F_s_pitch = 2 × 75 kN × sin(36°) / sin(60°) / cos(2°) = 101.87 kN
F_s_roll = 75 kN / (cos 30° × cos 8°) = 87.454 kN
F_s = governing case: pitch = 101.87 kN
F_ip = 101.87 kN × cos β = 101.81 kN
F_op = 101.87 kN × sin β = 12.171 kN
N_beam = 50.904 kN, the larger of the level and the pitched case = 50.904 kN
M_op = 12.171 kN × 180 mm = 2.1908 kN·m
side = min(40, 75 − 55/2) mm = 40 mm
a_eff = 75 − 55/2 mm = 47.5 mm
phi = 55 × 50 / 55 = 50 deg
be_eff = min(40, 100, 38.339) mm = 38.339 mm
… 35 more step(s) on the sheet
Governing: PASS  Design load against the bearing resistance (BTH-1): 183.37 kN vs 320.45 kN  utilisation 0.57  « BTH-1 3-3.3
```
<!-- hand-arithmetic:end -->
