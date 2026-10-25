# M6 — Lifting lug and pin joint (`mech.lifting-lug@1.1.0`) — GATED, OFF BY DEFAULT

**In plain terms.** A lifting lug is a plate with a hole and a pin through it, and it can fail five ways: the plate can tear across the narrowest section beside the hole, the pin can crush the hole, the plate can shear out from the hole to its end, the pin can shear through, and the pin can bend between the cheek plates that hold it. Each check is a load over an area, or a moment over a section modulus, compared with what the material allows; the largest ratio governs, and it is often the pin in bending.

**Status.** Behind the `lifting` feature flag like S8 (`struct.lifting`) and the legacy module it replaces (`ADR-0173`): composed into the build but withheld from the catalogue, the Worksheets tree, every service path and every pack until the machine's administrator switches the feature on (the policy value `Lifting` on Windows, the root-owned `/etc/tempestos/features.json` elsewhere), and the engine refuses the id `mech.lifting-lug` while it is off (`CalculationFeatureGate`). A lifting design calculation, it also demands what S8 demands at Issue: a competent-person attestation (the inputs `attest_name`, `attest_qualification`, `attest_statement`, by someone who is none of its authors), an independent first check and a second independent check (`RequiresAttestation`, `IndependentCheck`, `RequiresSecondCheck`). The sheet prints in its scope, as an assumption and as the output `sheet_notice` that it is **a design calculation, not a thorough examination** (LOLER reg 9) and that **signatures are not authenticated by TempestOS**. Version 1.1.0 (from the 1.0.1 port) adds only this governance and the notice; the arithmetic and the two known-answer cases are unchanged. Migrated legacy lifting records open as read-only archive sheets whatever the flag says (reading is not running); the legacy engine definition `calc.lifting-lug-pin-joint` is refused by id while the flag is off.

**Scope.** One lug between two cheek plates, load along the lug, close-fitting pin (d_p ≥ 0.9·d_h). Ported from the legacy `calc.lifting-lug-pin-joint` (ADR-0167). Clause numbers only (ASME BTH-1 Ch. 3).

**Method.** `σ_t = P/((W − d_h)t)`; `σ_br = P/(d_p t)`; `τ_to = P/(2at)`; `τ_p = P/(2πd_p²/4)`; `M = (P/2)(t/4 + g + t_s/2)`, `σ_b = 32M/(πd_p³)`; each over its allowable.

**Inputs.** `P`, `t`, `W`, `d_h`, `a`, `d_p`, `t_s`, `g`, allowables `f_t`, `f_br`, `f_v`, `f_b_pin`, `f_v_pin`. **Outputs.** the five stresses, `M_p`, the five utilisations. **Checks.** net section, bearing, tear-out, pin shear, pin bending.

**Limitations.** No out-of-plane load, no dished or stiffened lugs, no design factor applied (allowables carry it); a pin looser than 0.9·d_h blocks.

**Test cases.** The legacy module's two hand calculations (`ka-legacy-pass`, `ka-legacy-fail`) to five significant figures.

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
M_p = 50 kN / 2 × (20 mm / 4 + 2 mm + 12 mm / 2) = 325 N·m
Z_p = π × (30 mm)^3 / 32 = 2650.7 mm³
sigma_b = 325 N·m / 2650.7 mm³ = 122.61 MPa
u_t = 36.765 MPa / 150 MPa = 0.2451
u_br = 83.333 MPa / 200 MPa = 0.41667
… 3 more step(s) on the sheet
Governing: PASS  Pin bending: u_b = 0.49043 vs u_b ≤ 1  utilisation 0.49
```
<!-- hand-arithmetic:end -->
