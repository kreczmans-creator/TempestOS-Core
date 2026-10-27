# Shaft under combined bending and torsion (`mech.shaft-stress@1.1.1`)

**In plain terms.** A rotating shaft is bent by the loads it carries and twisted by the torque it transmits. The bending stress and the twisting shear stress peak together at the surface, and the two failure theories in common use combine them differently: the maximum-shear-stress theory is the more conservative, the von Mises theory the closer to test. The factor of safety is the yield strength over the combined stress, raised at a shoulder or keyway by the stress-concentration factors.

## Scope

Calculates the bending and torsional stresses in a solid round shaft, with stress-concentration factors, and the factors of safety against yield. It covers static loading only, not fatigue or hollow shafts.

The sheet is ported from the legacy `calc.shaft-combined-stress` module (ADR-0167). `S_y` fills from a released material record.

## Method

- Bending stress, with its stress concentration: `σ = K_t·32M/(πd³)`.
- Torsional shear stress, with its stress concentration: `τ = K_ts·16T/(πd³)`.
- Maximum shear stress (Tresca): `τ_max = √((σ/2)² + τ²)`.
- von Mises equivalent stress: `σ' = √(σ² + 3τ²)`.
- Factors of safety against yield, the lower governing: `n_T = (S_y/2)/τ_max`; `n_VM = S_y/σ'`.

## Inputs

| Symbol | Meaning | Unit | Group |
|---|---|---|---|
| `d` | Shaft diameter | mm | Geometry |
| `S_y` | Yield strength | MPa | Material |
| `M` | Bending moment (magnitude) | kN·m | Loads |
| `T` | Torque (magnitude) | kN·m | Loads |
| `K_t` | Bending stress-concentration factor (1 for none) | none | Factors |
| `K_ts` | Torsional stress-concentration factor (1 for none) | none | Factors |
| `n_req` | Required factor of safety | none | Requirements |

## Assumptions

- The shaft is a solid round section, linear elastic, under static loading.

## Outputs

`sigma`, `tau`, `tau_max`, `sigma_vm`, `n_T`, `n_VM` and `n_min`.

## Ports

| Port | Symbol | Unit | Meaning |
|---|---|---|---|
| `shaft.stress_shear_max` | `tau_max` | MPa | Maximum shear stress at the surface (Tresca) |
| `shaft.stress_von_mises` | `sigma_vm` | MPa | von Mises equivalent stress at the surface |
| `shaft.safety_yield_min` | `n_min` | none | Governing factor of safety against yield |

## Checks

- Factor of safety against the required factor: the required factor `n_req` against the governing factor `n_min`.
- Rule `no-load` (block): the bending moment and the torque cannot both be zero.

## Limitations

The sheet checks static strength only. Fatigue is the job of `mech.fatigue-miner`. It has no hollow shafts and no axial load.

**1.1.1.** Declares the unit changes of 1.1.0 (ADR-0177 review): `M` and `T` were in N·m before 1.1.0, so a bare stored number from an earlier version is read in N·m, never kN·m. No calculated value changes.

## References

- Budynas, *Shigley's Mechanical Engineering Design*, §5-4 and §5-5, 10th ed.: maximum-shear-stress and distortion-energy theories.
- Budynas, *Shigley's Mechanical Engineering Design*, §7-4, 10th ed.: shaft design for stress.

## Test cases

The legacy module's three hand calculations (`ka-legacy-1`, `ka-legacy-2`, `ka-legacy-3`).

**Changes in 1.1.0 (C-2).** `M` and `T` are entered in kN·m (the same values as before in N·m); the inputs are in groups; the check shows the required and the governing factor; three ports are declared. No result changes.

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** 40 mm shaft in bending and torsion ([sheet](../../worked-examples/mechanical/mech.shaft-stress/shaft-40mm/sheet.txt)).

```text
sigma = 1 × 32 × 500 N·m / (π × (40 mm)^3) = 79.577 MPa
tau = 1 × 16 × 800 N·m / (π × (40 mm)^3) = 63.662 MPa
tau_max = sqrt((79.577 MPa / 2)^2 + (63.662 MPa)^2) = 75.073 MPa
sigma_vm = sqrt((79.577 MPa)^2 + 3 × (63.662 MPa)^2) = 135.98 MPa
n_T = 350 MPa / 2 / 75.073 MPa = 2.3311
n_VM = 350 MPa / 135.98 MPa = 2.5739
n_min = min(2.3311, 2.5739) = 2.3311
Governing: PASS  Factor of safety against the required factor: 2 vs 2.3311  utilisation 0.86  « Shigley §7-4
```
<!-- hand-arithmetic:end -->
