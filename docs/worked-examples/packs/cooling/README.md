# Calc pack — Enclosure cooling (`pack.cooling`)

**Chain.** Panel load budget (`elec.load-budget`) → Enclosure heat; Enclosure heat (`elec.enclosure-heat`) → Cooling-water air coil; Cooling-water air coil (`bs.heat-exchanger`) → Cooling-water pipework and pump.

**Purpose.** Takes the heat a control panel dissipates, finds what the enclosure cannot lose naturally, sizes the water-cooled air coil that removes it at the enclosure's temperature limit and the pipework and pump that carry the cooling water. The coil duty is sensible only: when the water enters below the dew point of the enclosure air the coil condenses water (a latent load and a drain this chain does not size), and the coil step warns when its optional dew point is entered. The pump head covers pipe friction only unless the coil's water-side loss, valves and plant are entered as the outlet head h_out of the pipework step (the step warns while it is 0). The pipework step is on water, so a glycol mixture reaches it as a specific heat and mass flow only: for a glycol cold side give the pipework step the custom fluid with the mixture's density and viscosity (the step warns when the specific heat is not water's). A hot ambient at or above the enclosure's temperature limit does not stop the chain: the enclosure step then gives the duty the unit must remove, the heat inside plus the heat the walls gain from the ambient, P_cool = P + h·A_eff·(T_amb − T_max), and the coil is sized for it (no ventilation airflow or fan duty is given in that case).

The heat a drive panel dissipates is a linked load of its enclosure; the heat the enclosure cannot lose naturally is the required duty of a water-cooled air coil. The coil's hot side is the enclosure air, entering at the enclosure's temperature limit (E2 `T_max`, linked) with c_p 1006 J/(kg·K), the conventional dry-air value E2 itself uses; the air flow through the coil (0.4 kg/s) and U = 60 W/(m²·K) are the coil's own figures, and no intermediate water loop inside the enclosure is modelled. The coil's duty, cold-side temperature rise, mean cold-side temperature and cold-side specific heat (linked, so a glycol mixture changes the flow) give the cooling-water pipework and pump their flow and fluid. The enclosure step's own check, natural dissipation holding the limit, FAILS by design: that is the reason the coil is needed, and the duty it hands over is exactly the shortfall.

**Issue.** Project “Motor control centre 3”, pack “Drive panel cooling calc pack”, checked once as a bundle and issued under issue reference **`MCC-COOL-001`** to Client Co in one transaction. After issue every step is read-only and carries that reference. Checked against SPEC-MCC-03 and DRG-MCC-210/215 rev B.

## Steps

| Step | Calculator | Revision | Checks |
|---|---|---|---|
| Panel load budget | `elec.load-budget` | A | none |
| Enclosure heat | `elec.enclosure-heat` | A | 0 of 1 PASS |
| Cooling-water air coil | `bs.heat-exchanger` | A | 1 of 1 PASS |
| Cooling-water pipework and pump | `bs.pipe-pump` | A | 1 of 1 PASS |

## Links

Every link is pinned to the upstream revision named in the issued sheet (`link: <step> rev <n> · <output>`); changing an input of a step makes every step that takes from it, directly or through other steps, stale, and the pack cannot be checked or issued until each is updated in order.

| From | Output | To | Input | Value carried |
|---|---|---|---|---|
| Panel load budget | `Phi` | Enclosure heat | `P_add` | 3510 W |
| Enclosure heat | `P_cool` | Cooling-water air coil | `Q_req` | 3.4224 kW |
| Enclosure heat | `T_max` | Cooling-water air coil | `T_h_in` | 45 degC |
| Cooling-water air coil | `Q` | Cooling-water pipework and pump | `Phi_load` | 3422.4 W |
| Cooling-water air coil | `dT_c` | Cooling-water pipework and pump | `dT_load` | 3.27502 K |
| Cooling-water air coil | `T_c_m` | Cooling-water pipework and pump | `T` | 23.6375 degC |
| Cooling-water air coil | `cp_c` | Cooling-water pipework and pump | `cp_load` | 4180 J/(kg*K) |

## Inputs and provenance

Every input of every step carries a provenance on the issued sheet: the figures chosen for this example cite an (invented) specification or drawing, an input left at the sheet's default is recorded as an assumption, and a linked input names the calculation and output it comes from. Project, drawing, specification and issue references are illustrative; they are not real documents.

## Files

- `pack-issue-sheet.txt` — the pack's issue sheet (index, members, revisions) as stored on the Evidence record.
- `e6-elec-load-budget.sheet.txt` — Panel load budget, the issued sheet.
- `e2-elec-enclosure-heat.sheet.txt` — Enclosure heat, the issued sheet.
- `b5-bs-heat-exchanger.sheet.txt` — Cooling-water air coil, the issued sheet.
- `b1-bs-pipe-pump.sheet.txt` — Cooling-water pipework and pump, the issued sheet.
- `inputs.json` — a record of every step's inputs as issued, links included: one JSON object keyed by step, each value that step's list of inputs. It is not a file that one worksheet imports whole (a worksheet takes the inputs of one calculation: copy one step's list out of it to load that step). Links and attached-file references in it name the original calculations and their Evidence, so they do not resolve in another project.

Generated by `ChainPackWorkedExampleTests` in `tests/Tempest.Calc.Tests.Library/Chains`. The PDFs are rendered separately.
