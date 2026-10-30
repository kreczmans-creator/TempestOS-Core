# Lifting lug and pin joint (`mech.lifting-lug@1.2.0`)

**In plain terms.** A lifting lug is a plate with a hole and a pin through it, and it can fail five ways: the plate can tear across the narrowest section beside the hole, the pin can crush the hole, the plate can shear out from the hole to its end, the pin can shear through, and the pin can bend between the cheek plates that hold it. Each check is a load over an area, or a moment over a section modulus, compared with what the material allows; the largest ratio governs, and it is often the pin in bending.

**Status: gated, off by default.** The sheet is behind the `lifting` feature flag like S8 (`struct.lifting`) and the legacy module it replaces (`ADR-0173`): composed into the build but withheld from the catalogue, the Worksheets tree, every service path and every pack until the machine's administrator switches the feature on (the policy value `Lifting` on Windows, the root-owned `/etc/tempestos/features.json` elsewhere), and the engine refuses the id `mech.lifting-lug` while it is off (`CalculationFeatureGate`). A lifting design calculation, it also demands what S8 demands at Issue: a competent-person attestation (the inputs `attest_name`, `attest_qualification`, `attest_statement`, by someone who is none of its authors), an independent first check and a second independent check (`RequiresAttestation`, `IndependentCheck`, `RequiresSecondCheck`). The sheet prints as an assumption and as the output `sheet_notice` that it is **a design calculation, not a thorough examination** (LOLER reg 9) and that **signatures are not authenticated by TempestOS**. Migrated legacy lifting records open as read-only archive sheets whatever the flag says (reading is not running); the legacy engine definition `calc.lifting-lug-pin-joint` is refused by id while the flag is off.

## Scope

Calculates the net-section tension, bearing, tear-out, pin shear and pin bending of a pinned lifting lug between two cheek plates under a load along the lug. It is a design calculation, not a thorough examination.

## Method

- Net-section tension, beside the hole: σ_t = F_lug/((W − d_h)·t_lug)
- Bearing of the pin on the lug: σ_br = F_lug/(d_p·t_lug)
- Tear-out shear beyond the hole, on two planes: τ_to = F_lug/(2·a·t_lug)
- Pin shear, in double shear: τ_p = F_lug/(2·π·d_p²/4)
- Pin bending moment, from the lever arm across the lug, the gap and the cheek plate: M = (F_lug/2)·(t_lug/4 + g + t_s/2)
- Pin bending stress: σ_b = 32·M/(π·d_p³)
- Utilisation of each mode, its stress over the allowable entered: u = σ/σ_allow

Ported from the legacy `calc.lifting-lug-pin-joint` (ADR-0167). No design factor is applied: the allowables entered carry it.

## Inputs

| Symbol | Description | Unit | Default | Usual range | Group |
|---|---|---|---|---|---|
| `F_lug` (was `P`) | Load on the lug (along the lug) | kN | 50 | 0.01 to 100000 | Loads |
| `t_lug` (was `t`) | Lug thickness | mm | 20 | 1 to 500 | Lug |
| `W` | Lug width at the hole | mm | 100 | 5 to 2000 | Lug |
| `d_h` | Hole diameter | mm | 32 | 2 to 1000 | Lug |
| `a` | Edge distance, hole edge to lug end (load direction) | mm | 40 | 1 to 2000 | Lug |
| `d_p` | Pin diameter | mm | 30 | 2 to 1000 | Pin |
| `t_s` | Cheek plate thickness (each) | mm | 12 | 1 to 500 | Pin |
| `g` | Clearance, lug face to cheek plate (each side) | mm | 2 | 0 to 100 | Pin |
| `f_t` | Allowable tensile stress, lug | MPa | 150 | 1 to 2000 | Allowables |
| `f_br` | Allowable bearing stress, lug | MPa | 200 | 1 to 3000 | Allowables |
| `f_v` | Allowable shear stress, lug | MPa | 90 | 1 to 2000 | Allowables |
| `f_b_pin` | Allowable bending stress, pin | MPa | 250 | 1 to 3000 | Allowables |
| `f_v_pin` | Allowable shear stress, pin | MPa | 150 | 1 to 2000 | Allowables |
| `attest_name` | Competent person attesting the design (not the author; required to check and issue) |  | optional |  | Competent-person attestation (required to check and issue) |
| `attest_qualification` | Their qualification and standing |  | optional |  | Competent-person attestation (required to check and issue) |
| `attest_statement` | Attestation: what the competent person has reviewed (at least 20 characters) |  | optional |  | Competent-person attestation (required to check and issue) |

## Assumptions

1. The load acts along the lug's axis through the hole centre. Basis: No out-of-plane or side load.
2. The pin bears uniformly over the lug thickness and over each cheek plate. Basis: Pin-bending idealisation for a lug between two cheek plates.
3. Each cheek plate reacts half the load. Basis: Pin-bending idealisation for a lug between two cheek plates.
4. The pin is a close fit, with d_p at least 0.9 d_h. Basis: Uniform-bearing idealisation.
5. This is a design calculation, not a thorough examination. Basis: The sheet checks the lug and pin at the stated loads against the allowables entered. LOLER reg 9 and PUWER duties stay with the owner and the competent person.

## Outputs

| Symbol | Description |
|---|---|
| `sigma_t` | Net-section tension |
| `sigma_br` | Bearing stress |
| `tau_to` | Tear-out shear |
| `tau_p` | Pin shear |
| `M_p` | Pin bending moment |
| `sigma_b` | Pin bending stress |
| `u_t` | Net-section utilisation |
| `u_br` | Bearing utilisation |
| `u_to` | Tear-out utilisation |
| `u_p` | Pin shear utilisation |
| `u_b` | Pin bending utilisation |
| `sheet_notice` | Status of this sheet |

The five stresses, the pin bending moment `M_p` (now in kN·m, the same value as before) and the five utilisations. The sheet also prints `sheet_notice`, the status of the sheet. The five utilisations and `M_p` are ports.

## Ports

A port keeps its output symbol; a link, a pack binding or an inputs file written with the symbol or an alias still resolves.

| Public name | Symbol | Unit | Description |
|---|---|---|---|
| `lug.utilisation_tension` | `u_t` |  | Net-section tension utilisation |
| `lug.utilisation_bearing` | `u_br` |  | Bearing utilisation |
| `lug.utilisation_tear_out` | `u_to` |  | Tear-out utilisation |
| `lug.utilisation_pin_shear` | `u_p` |  | Pin shear utilisation |
| `lug.utilisation_pin_bending` | `u_b` |  | Pin bending utilisation |
| `lug.moment_pin` | `M_p` | kN·m | Pin bending moment |

## Checks

- Net-section tension against the allowable tensile stress
- Pin bearing against the allowable bearing stress
- Tear-out shear against the allowable shear stress
- Pin double shear against the allowable pin shear stress
- Pin bending stress against the allowable pin bending stress

Applicability rules:

| Rule | Severity | Effect | Condition |
|---|---|---|---|
| `geometry` | Block | Conservative | The lug must be wider than the hole and the pin must fit the hole. |
| `loose-pin` | Block | Conservative | A pin smaller than 0.9 of the hole concentrates bearing; the uniform-bearing method does not apply. |

## Limitations

No out-of-plane load, no dished or stiffened lugs, no design factor applied (the allowables carry it). A pin looser than 0.9 d_h blocks.

## Changes

**1.2.0 (C-2, phase B).** Two inputs are renamed, and the old symbols stay as aliases (an inputs file, a CSV, a saved draft and a link that use them still load): `P` is `F_lug` (a force is F) and `t` is `t_lug` (a bare `t` is a time; a thickness is `t_<part>`). The load is grouped under `Loads`. The pin bending moment `M_p` is shown in kN·m instead of N·m (the same value). The five checks show a stress against its allowable. The sheet declares six ports. The scope is two sentences; the status of the sheet is kept in this page and in the sheet's own notice. The arithmetic and the two known-answer cases are unchanged.

**1.1.0 (from the 1.0.1 port).** Adds the governance and the notice only: the feature flag, the attestation, the two independent checks and the sheet notice. The arithmetic and the two known-answer cases are unchanged.

## References

- ASME BTH-1, Ch. 3: Pinned connections.
- Budynas and Nisbett, Shigley's Mechanical Engineering Design, Ch. 3, 11th ed.: Shear, bearing and bending stresses.

## Test cases

The legacy module's two hand calculations (`ka-legacy-pass`, `ka-legacy-fail`) to five significant figures.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** Lifting lug, 50 kN — passes, pin bending governs ([sheet](../../worked-examples/mechanical/mech.lifting-lug/lug-50kN-passes/sheet.txt)).

```text
k_fit = 30 mm / 32 mm = 0.9375
A_net = (100 mm − 32 mm) × 20 mm = 1360 mm²
sigma_t = 50 kN / 1360 mm² = 36.765 MPa
A_br = 30 mm × 20 mm = 600 mm²
sigma_br = 50 kN / 600 mm² = 83.333 MPa
A_to = 2 × 40 mm × 20 mm = 1600 mm²
tau_to = 50 kN / 1600 mm² = 31.25 MPa
A_p = 2 × π × (30 mm)^2 / 4 = 1413.7 mm²
tau_p = 50 kN / 1413.7 mm² = 35.368 MPa
M_p = 50 kN / 2 × (20 mm / 4 + 2 mm + 12 mm / 2) = 0.325 kN·m
Z_p = π × (30 mm)^3 / 32 = 2650.7 mm³
sigma_b = 0.325 kN·m / 2650.7 mm³ = 122.61 MPa
u_t = 36.765 MPa / 150 MPa = 0.2451
u_br = 83.333 MPa / 200 MPa = 0.41667
… 3 more step(s) on the sheet
Governing: PASS  Pin bending stress against the allowable pin bending stress: 122.6 MPa vs 250 MPa  utilisation 0.49
```
<!-- hand-arithmetic:end -->
