# E4 — Conductor sizing and overload coordination (`elec.conductor-sizing@1.1.1`)

**In plain terms.** A cable must be big enough to carry the circuit's current without overheating, and the device protecting it must trip before the cable is damaged. The worksheet takes the load current, picks the smallest standard device that carries it, finds the smallest cable whose rated capacity, once reduced for temperature and grouping, carries the device rating, and checks that the device really protects the cable.

**Scope.** One circuit: the design current, the smallest preferred device rating that carries it, the smallest conductor whose tabulated capacity (the user's own record) carries that rating once the correction factors are applied, and the overload coordination checks. The context choice (BS EN 60204-1 machine wiring, the default, or BS 7671 installation) changes the clause citations and, for a gG fuse, MCCB or MPCB entered without `I2`, whether the I₂ condition may be deemed satisfied (BS 7671 installation) or `I2` must be entered (machine wiring); no other number differs. **Independent check demanded** (DA-12); every numeric input outside its range is a hard FAIL (hard limit = stated range). Ships with E5 (`elec.fault-protection`).

**Method.**

- `I_b = P/(√3·U·cosφ·η)` three-phase, `P/(U·cosφ·η)` single-phase, with `P` the **rated output (shaft) power** and η converting it to input; `P/U` DC, where `P` is the electrical power drawn (power factor and efficiency not applied); or the linked `I_b_in`, which replaces the computed value.
- `I_n` = smallest rating in the `ratings` record with `I_n ≥ I_b`.
- `C_c = 0.725` for a semi-enclosed fuse only (1.45 divided by an assumed fusing factor of 2, derived), else 1.
- `I_t,req = I_n/(C_a·C_g·C_i·C_c)`. `C_a` is entered, or with `derive_Ca` derived as `√[(θ_p − θ_a)/(θ_p − θ_ref)]`, labelled "derived approximation — compare with the licensed table" (θ_p 70 °C for PVC, 90 °C for XLPE, the entered `theta_p` for "user"; θ_ref default 30 °C).
- `CSA` = smallest cross-section in the `ampacity` record with `I_t ≥ I_t,req` (row order does not matter). With the AWG system the row carries the gauge n and the area is the derived geometry `d = 0.127·92^((36 − n)/39) mm`, `A = π d²/4`; no AWG capacity is supplied.
- `I_z = I_t·C_a·C_g·C_i` (the factors actually applied; C_c is not part of it).
- Checks (BS 7671 reg. 433.1.1; BS EN 60204-1 cl. 12.4 and 7.2 in the machine context): `I_b ≤ I_n`, `I_n ≤ I_z`, `I₂ ≤ 1.45·I_z`. For MCB types B, C, D `I₂ = 1.45·I_n` is inherent. An entered `I2` (datasheet) is always checked. For a semi-enclosed fuse `I₂ = 2·I_n` by default (reg. 433.1.202). In the **BS 7671 installation context** a gG fuse to BS 88 without `I2` shows no `I₂`: the condition is deemed satisfied when `I_n ≤ I_z` (reg. 433.1.201, cited by number; shown as a check with its real ratio `I_n/I_z`), and an MCCB or MPCB without `I2` is treated the same with a warning that this presumes `I₂` not above 1.45·`I_n`. In the **machine-wiring context** a gG fuse, MCCB or MPCB without `I2` is **blocked** (`I2:`): an IEC 60269-1 gG fuse has `I₂ = 1.6·I_n`, not 1.45·`I_n`, so the BS 88 deeming rule is not carried over (Green G-B-02, G-B-13). The two regulation numbers 433.1.201 and 433.1.202 are cited as given to me and are to be checked against the owner's copy.

**Inputs.** `context`, `supply`, `P` 0.01–500 kW (rated output power), `U` 12–1000 V, `cos_phi` 0.5–1, `eta` 0.5–1, optional `I_b_in` (A); `L` 0.1–500 m, `theta_a` −10 to 70 °C, `insulation` (PVC 70 °C / XLPE 90 °C / user), optional `theta_p` 50–250 °C, optional `method` (descriptive key such as B2, C, E), `N_g` 1–20 grouped circuits, `system` (metric / AWG); `device` (MCB type B, C, D, gG fuse, semi-enclosed fuse, MCCB, MPCB), optional `I2` (MCCB, MPCB; semi-enclosed fuse default 2·`I_n`); `C_a`, `C_g`, `C_i` 0.1–1.5 (default 1; record kind `cable.correction-factors`), `derive_Ca`, `theta_ref`; tables `ratings` (`I_n`; record kind `device.ratings`) and `ampacity` (`CSA` mm² or `AWG` gauge, and `I_t` A; record kind `cable.ampacity`). **No ampacity, rating or correction-factor value ships**: the tables are empty until the user enters them from their own licensed copy. `N_g` is carried to the sheet; `C_g` is not looked up from it, and `C_g = 1` with more than one circuit raises a warning.

**Outputs.** `I_b`, `I_n`, `C_c`, `C_a`, `I_t_req`, `CSA`, `I_z`, `L`, `u_Ib = I_b/I_n`, `u_In = I_n/I_z`, `I2` and `u_I2 = I₂/(1.45 I_z)` (MCB, semi-enclosed fuse, or an entered `I2`), `device_code` (1 MCB type B, 2 type C, 3 type D, 4 fuse, MCCB or MPCB) and `gauge` (AWG system). **Ports.** `I_b` (A), `I_n` (A), `CSA` (mm²), `I_z` (A), `L` (m), `device_code` → E3 (CSA, length), E5 (CSA, length, rated current and, through `device_code`, the device: E5 is blocked if the device chosen there is not the one sized here), E2. **Checks.** `I_b ≤ I_n`, `I_n ≤ I_z`, `I₂ ≤ 1.45 I_z`, each PASS/FAIL with its utilisation. **Blocks** (each starts with the symbol and a colon): `ampacity:` empty table (enter your own licensed capacities, record kind `cable.ampacity`), rows given in the other conductor system (cross-sections with AWG selected, or gauges with metric: the message names the mismatch) or a non-integer AWG gauge ("AWG gauge must be a whole number"); `I2:` a gG fuse, MCCB or MPCB without `I2` in the machine-wiring context; `ratings:` empty table; `I_n:` no rating of at least `I_b`; `CSA:` no conductor carries `I_t,req`; `theta_p:`, `theta_a:` or `C_a:` when a derived factor cannot be formed.

**Limitations.** Single circuit, one installation method per run; the factors are applied as a product and are the user's; the derived `C_a` is a temperature-difference approximation only; the I²t/short-circuit and voltage-drop checks are E5 and E3; E5 accepts a line conductor of 0.5 mm² and above, so a selected CSA below 0.5 mm² (AWG 21 and finer) cannot be linked to E5; no cable ampacity, rating factor or standard table is embedded (§A5).

**Test cases.** All analytic and Confirmed, recomputed in Python. The capacities (1.5→12.3, 2.5→17.7, 4→24.1, 6→31.9, 10→44.6 A; AWG 14→11.2, 12→16.9, 10→26.3, 8→37.4 A; and, in the behaviour tests, 11.4/16.2/23.7/30.8/43.1 A, AWG 18.4 and 27.3 A and C_g 0.58, all invented) and the factors (C_a 0.91, C_g 0.77) are synthetic values invented for these tests, not taken from any table; the ratings 6, 10, 16, 20, 25, 32, 40, 50, 63 A are the typical preferred series. `ka-three-phase-mcb-c` (θ_a 35 °C, two circuits; I_b 14.1507 A, I_n 16 A, I_t,req 22.8343 A, 4 mm², I_z 16.8869 A), `ka-single-phase` (I_b 15.2555 A, 2.5 mm², I_z 17.7 A), `ka-dc` (I_b 25 A, I_t,req 27.4725 A, 6 mm², I_z 29.029 A; a gG fuse in the machine context with `I2` = 40 A entered, u_I2 = 0.95030), `ka-semi-enclosed` (C_c 0.725, I_t,req 27.5862 A, 6 mm², I₂ 40 A), `ka-derived-ca` (C_a = √(30/40) = 0.866025), `ka-awg-10` (AWG 10 = 5.2612 mm², on a type C MCB). Not pinned — owner check: the Schneider *Electrical Installation Guide* chapter G worked examples, the IET *Electrical Installation Design Guide* examples and the Kollmorgen current-carrying-capacity note; no value from them is in source, tests or docs. The worked example `conveyor-motor-circuit` (4 kW rated output, type C MCB, machine panel) uses synthetic values entered with record-style provenance.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** 4 kW conveyor motor circuit in a machine panel, type C MCB ([sheet](../../worked-examples/electrical/elec.conductor-sizing/conveyor-motor-circuit/sheet.txt)).

```text
I_b = 4 kW / (sqrt(3) × 400 V × 0.82 × 0.85) = 8.2834 A
I_n = smallest rating ≥ 8.2834 A in the ratings record = 10 A
device_code = MCB type C = 2
C_c = MCB type C = 1
I_t_req = 10 A / (0.91 × 0.77 × 1 × 1) = 14.271 A
I_t = I_t,req = 14.271 A = 17.7 A
CSA = I_t,req = 14.271 A = 2.5 mm²
I_z = 17.7 A × 0.91 × 0.77 × 1 = 12.402 A
u_Ib = 8.2834 A / 10 A = 0.82834
u_In = 10 A / 12.402 A = 0.8063
I2 = 1.45 × 10 A = 14.5 A
u_I2 = 14.5 A / (1.45 × 12.402 A) = 0.8063
Governing: PASS  Design current within device rating (I_b ≤ I_n): u_Ib = 0.82834 vs u_Ib ≤ 1  utilisation 0.828  « BS EN 60204-1 cl. 7.2; BS 7671 reg. 433.1.1
```
<!-- hand-arithmetic:end -->
