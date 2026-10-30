# Press and shrink fit (`mech.press-fit@1.3.0`)

**In plain terms.** Pressing a shaft into a slightly smaller hole stretches the hub and squeezes the shaft until the two meet at a common radius. That interference sets up a contact pressure, found by treating hub and shaft as thick cylinders. The pressure times friction over the contact area is the torque and axial force the joint can carry without slipping. The same pressure makes the hub's hoop stress highest at its bore, which is checked against yield. Heating the hub changes the interference.

## Scope

Calculates the interface pressure, stresses, assembly force and friction torque of a shaft pressed or shrunk into a hub, and the shrink temperature. It uses the Lamé thick-cylinder relations and ignores end effects.

## Method

- Radial interference: δ_r = δ/2.
- Interface pressure, the hub and the shaft as thick cylinders: p_int = δ_r / [ (R/E_h)·((r_o² + R²)/(r_o² − R²) + ν_h) + (R/E_s)·((R² + r_i²)/(R² − r_i²) − ν_s) ].
- Stresses at the hub bore: σ_θ = p_int·(r_o² + R²)/(r_o² − R²); σ_r = −p_int.
- Stresses at the shaft surface: σ_θ = −p_int·(R² + r_i²)/(R² − r_i²); σ_r = −p_int.
- Stress at the bore of a hollow shaft: σ_θ = −2·p_int·R²/(R² − r_i²); σ_r = 0.
- Surface smoothing on assembly (when Rz is entered): δ_eff = max(δ − 0.8·(Rz_h + Rz_s), 0), p_eff = p_int·δ_eff/δ.
- Axial force and torque the fit holds by friction, on p_eff: F = μ·p_eff·2πR·L; T = μ·p_eff·2πR²·L. The stresses stay at the nominal p_int (the larger interference of the fit).
- Temperature difference to assemble by heating the hub, with assembly clearance c_a: ΔT = (δ + c_a)/(α·2R).

`p_int` is the interface pressure (it was `p`, which the standard keeps for no quantity; the port `press.pressure_interface` keeps `p` as an alias). `C_h` and `C_s` are the hub and shaft compliance terms of the working.

## Inputs

| Symbol | Description | Unit | Default | Usual range | Group |
|---|---|---|---|---|---|
| `d` | Interface (nominal) diameter | mm | 50 | 1 to 1000 | Geometry |
| `delta` | Diametral interference | µm | 40 | 1 to 500 | Geometry |
| `d_o` | Hub outer diameter | mm | 100 | 1 to 2000 | Geometry |
| `d_i` | Shaft bore diameter (0 for a solid shaft) | mm | 0 | 0 to 1000 | Geometry |
| `L` | Engagement length | mm | 60 | 1 to 2000 | Geometry |
| `Rz_h` | Hub bore roughness Rz (optional; blank = the smoothing is not applied) | µm | optional | 0 to 100 | Geometry |
| `Rz_s` | Shaft surface roughness Rz (optional; blank = the smoothing is not applied) | µm | optional | 0 to 100 | Geometry |
| `E_h` | Hub Young's modulus | GPa | 207 | 10 to 450 | Material |
| `nu_h` | Hub Poisson's ratio |  | 0.292 | 0.1 to 0.45 | Material |
| `E_s` | Shaft Young's modulus | GPa | 207 | 10 to 450 | Material |
| `nu_s` | Shaft Poisson's ratio |  | 0.292 | 0.1 to 0.45 | Material |
| `S_y_h` | Hub yield strength | MPa | 350 | 50 to 2000 | Material |
| `S_y_s` | Shaft yield strength | MPa | 350 | 50 to 2000 | Material |
| `alpha_h` | Hub thermal expansion coefficient | 1/K | 1.2e-05 | 5e-06 to 3e-05 | Material |
| `mu` | Coefficient of friction at the interface |  | 0.15 | 0.05 to 0.4 | Factors |
| `c_a` | Assembly clearance wanted for shrink fitting | µm | 20 | 0 to 500 | Requirements |

## Assumptions

1. Both parts are long cylinders in plane stress, and the interface pressure is uniform along the engagement. Basis: The Lamé thick-cylinder relations apply, and end effects are neglected.
2. The interference is the nominal value entered. Basis: Use the minimum interference of the fit for the torque and the force, and the maximum for the stresses.
3. Surface roughness smooths on assembly and takes about 0.8·(Rz_h + Rz_s) off the interference when Rz is entered. Basis: DIN 7190-1 gives the allowance; enter Rz to apply it to the torque and the force.
4. The friction coefficient is as entered and the same for assembly and for torque transmission. Basis: Enter it with its source, because it dominates the force and torque results.
5. Yield is checked by von Mises stress at the hub bore, the shaft surface and a hollow shaft's bore. Basis: The axial stress is zero, and the larger of the two shaft values governs.

## Outputs

| Symbol | Description |
|---|---|
| `p_int` | Interface pressure |
| `sigma_t_hub` | Hub hoop stress at bore |
| `sigma_t_shaft` | Shaft hoop stress at surface |
| `sigma_vm_hub` | Hub von Mises stress |
| `sigma_vm_shaft` | Shaft von Mises stress at the surface |
| `sigma_vm_shaft_bore` | Shaft von Mises stress at the shaft bore |
| `sigma_vm_shaft_max` | Largest shaft von Mises stress (surface or bore) |
| `delta_eff` | Effective interference after smoothing |
| `p_eff` | Gripping interface pressure |
| `F_a` | Assembly force |
| `T` | Torque capacity |
| `dT` | Shrink-fit temperature rise |

The torque `T` is shown in kN·m since 1.3.0 (it was N·m); its value in SI is unchanged.

## Ports

A port keeps its output symbol; a link, a pack binding or an inputs file written with the symbol or an alias still resolves.

| Public name | Symbol | Unit | Description |
|---|---|---|---|
| `press.pressure_interface` | `p_int` (also `p`) | MPa | Interface pressure from the interference |
| `press.torque_capacity` | `T` | kN·m | Torque the joint carries by friction |
| `press.force_assembly` | `F_a` | kN | Axial assembly (press) force |
| `press.temperature_rise_hub` | `dT` | K | Hub temperature rise for shrink fitting |
| `press.stress_von_mises_hub` | `sigma_vm_hub` | MPa | Hub von Mises stress at the bore |
| `press.stress_von_mises_shaft` | `sigma_vm_shaft_max` | MPa | Largest shaft von Mises stress, surface or bore |

## Checks

- Hub von Mises stress against yield strength
- Largest shaft von Mises stress against yield strength

Applicability rules:

| Rule | Severity | Effect | Condition |
|---|---|---|---|
| `thin-hub` | Warning | Conservative | Hub wall thinner than 10 % of the interface diameter: the thick-cylinder relation still holds but assembly distortion is likely. |
| `bore-geometry` | Block | Conservative | The shaft bore must be smaller than the interface, and the interface smaller than the hub outer diameter. |

## Limitations

Plane stress, uniform pressure and no end effects. Friction is as entered. The surface-finish loss of interference applies only when `Rz_h` and `Rz_s` are entered.

## Changes

**1.3.0 (C-2, phase B).** The interface pressure `p` is `p_int` (alias `p` on the port `press.pressure_interface`). The torque `T` is shown in kN·m. The inputs are grouped (`Geometry`, `Material`, `Factors`, `Requirements`) and the two roughness inputs carry the `(optional; blank = ...)` hint. The sheet declares six ports (`press.pressure_interface`, `press.torque_capacity`, `press.force_assembly`, `press.temperature_rise_hub`, `press.stress_von_mises_hub`, `press.stress_von_mises_shaft`). The two checks are named "Hub von Mises stress against yield strength" and "Largest shaft von Mises stress against yield strength". The bore-geometry block names the one input to change. The scope, the assumptions and the references follow the standard. No calculated value changes.

**1.2.0 (mech.press-fit-R1).** New optional inputs `Rz_h` and `Rz_s` (µm). With a roughness entered the interference that grips is `δ_eff = max(δ − 0.8·(Rz_h + Rz_s), 0)` (DIN 7190-1: the roughness peaks flatten on assembly), the gripping pressure is `p_eff = p_int·δ_eff/δ`, and the axial force `F_a` and the torque `T` use `p_eff`. The stresses and the yield checks stay at the nominal `p_int` (the larger interference of the fit). Default case (δ 40 µm, Rz 4 µm on each part): `δ_eff` 33.6 µm, `T` 2194.8 N·m before, 1843.6 N·m now (−16 %). With no roughness entered the numbers are unchanged and every run carries the assumption that the smoothing is not applied, that the torque needs the minimum interference of the fit and the stresses the maximum. New outputs `delta_eff` and `p_eff`.

**1.1.0.** New outputs `sigma_vm_shaft_bore` and `sigma_vm_shaft_max`: the hollow shaft's bore, where the hoop stress `2·p_int·R²/(R² − r_i²)` is largest and the radial stress zero, and the larger of the two. Example: d 50, bore 25, hub 100 mm, δ 120 µm: surface 216.6 MPa, bore 397.4 MPa, u 1.136 where the check read 0.619 and passed. A solid shaft is unchanged.

## References

- Budynas and Nisbett, Shigley's Mechanical Engineering Design, §3-16, §3-17, 11th ed.: Press and shrink fits; stresses in pressurised cylinders.
- Young, Budynas and Sadegh, Roark's Formulas for Stress and Strain, Ch. 13, Table 13.5, 8th ed.: Thick-walled cylinders under pressure.

## Test cases

`ka-steel-steel-solid` (`p_int` 62.1 MPa, hub hoop 103.5 MPa, F 87.8 kN, T 2195 N·m, ΔT 100 K, analytic); `ka-hollow-shaft` (`p_int` 49.6 MPa, analytic).

<!-- hand-arithmetic:begin generated from the worked-example sheet by SpecHandArithmeticTests; do not edit -->
**Worked example, by hand.** 50 mm solid steel shaft in a 100 mm steel hub ([sheet](../../worked-examples/mechanical/mech.press-fit/solid-steel-shaft/sheet.txt)).

```text
R = 50 mm / 2 = 25 mm
r_o = 100 mm / 2 = 50 mm
r_i = 0 mm / 2 = 0 mm
delta_r = 40 µm / 2 = 20 µm
C_h = 25 mm / 207 GPa × (((50 mm)^2 + (25 mm)^2) / ((50 mm)^2 − (25 mm)^2) + 0.292) = 0.23655 mm/GPa
C_s = 25 mm / 207 GPa × (((25 mm)^2 + (0 mm)^2) / ((25 mm)^2 − (0 mm)^2) − 0.292) = 0.085507 mm/GPa
p_int = 20 µm / (0.23655 mm/GPa + 0.085507 mm/GPa) = 62.1 MPa
sigma_t_hub = 62.1 MPa × ((50 mm)^2 + (25 mm)^2) / ((50 mm)^2 − (25 mm)^2) = 103.5 MPa
sigma_r_hub = −62.1 MPa = -62.1 MPa
sigma_t_shaft = −62.1 MPa × ((25 mm)^2 + (0 mm)^2) / ((25 mm)^2 − (0 mm)^2) = -62.1 MPa
sigma_r_shaft = −62.1 MPa = -62.1 MPa
sigma_vm_hub = sqrt((103.5 MPa)^2 − 103.5 MPa × (-62.1 MPa) + (-62.1 MPa)^2) = 144.9 MPa
sigma_vm_shaft = sqrt((-62.1 MPa)^2 − (-62.1 MPa) × (-62.1 MPa) + (-62.1 MPa)^2) = 62.1 MPa
sigma_vm_shaft_bore = 62.1 MPa
… 6 more step(s) on the sheet
Governing: PASS  Hub von Mises stress against yield strength: 144.9 MPa vs 355 MPa  utilisation 0.41
```
<!-- hand-arithmetic:end -->
