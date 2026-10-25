# Calc pack — Envelope moisture (`pack.envelope-moisture`)

**Chain.** Room heat loss (`bs.room-heat-loss`) → Wall condensation; Room air state (`bs.psychrometrics`) → Wall condensation.

**Purpose.** Takes the U-value of a wall from the room heat-loss step and the temperature and humidity of the room air from the psychrometrics step to the Glaser condensation check of the wall: the condensate on each interface month by month, the annual balance, the peak against the owner's limit, and the surface temperature factor. The wall's layers are typed in both the heat-loss and the Glaser steps (no per-layer link exists) and the Glaser step warns if its U-value departs from the heat-loss step's by more than 2 %. The monthly climate is the user's own record: the example's is invented.

A solid brick wall (215 mm) lined inside with 80 mm of mineral wool and 12.5 mm plasterboard, rendered outside, in a room held at 21 °C and 50 % relative humidity. The heat-loss step finds the wall's U-value (0.3991 W/m²K, 89 % of its 0.45 target); the psychrometrics step gives the room air state, and its dry-bulb (21 °C) and relative humidity (50 %, as a percentage quantity) are the Glaser step's internal conditions. The Glaser step checks the same wall, with a 0.2 mm vapour control layer behind the plasterboard (μ 20000, an invented figure for a taped but imperfect membrane), over twelve months of an INVENTED maritime climate: the vapour pressure touches saturation only at the interface between the mineral wool and the brick, condensate collects from December to February (peak 9.8 g/m² in January, against an invented 500 g/m² limit), and the wall is dry again in March; the surface temperature factor (0.905) is far above the 0.609 the room needs at 80 % critical humidity. **The wall's layers are typed in both the heat-loss and the Glaser steps** (the heat-loss step offers no per-layer output to link); the one value they share, the wall's U-value, is linked from the heat-loss step to the Glaser step, which compares it with 1/R_T of its own layers and warns if they differ by more than 2 % (here 0.02 %: the vapour control layer, 0.2 mm, is the only difference, and it is thermally negligible). **That comparison is of the thermal resistance only: it cannot see a swapped layer order or a wrong vapour resistance factor in the twice-typed layers, either of which changes the Glaser result, so the layers must be checked against each other by eye.** **The monthly climate, the vapour resistance factors, the surface resistances, the critical humidity and the limit are the owner's records; the example's are invented and are not data from ISO 13788 or any other standard.** A timber or breathable build-up is outside the Glaser method (EN 15026 applies).

**Issue.** Project “Studio 3 external wall”, pack “Internally insulated wall moisture calc pack”, checked once as a bundle and issued under issue reference **`ENV-MOI-001`** to Client Co in one transaction. After issue every step is read-only and carries that reference. Checked against SPEC-ENV-02 and DRG-ENV-210 rev B.

## Steps

| Step | Calculator | Revision | Checks |
|---|---|---|---|
| Room heat loss | `bs.room-heat-loss` | A | 1 of 1 PASS |
| Room air state | `bs.psychrometrics` | A | none |
| Wall condensation | `bs.glaser` | A | 3 of 3 PASS |

## Links

Every link is pinned to the upstream revision named in the issued sheet (`link: <step> rev <n> · <output>`); changing an input of a step makes every step that takes from it, directly or through other steps, stale, and the pack cannot be checked or issued until each is updated in order.

| From | Output | To | Input | Value carried |
|---|---|---|---|---|
| Room heat loss | `U_1` | Wall condensation | `U_ref` | 0.399135 W/(m^2*K) |
| Room air state | `t_1_out` | Wall condensation | `theta_int` | 21 degC |
| Room air state | `phi_1` | Wall condensation | `RH_int` | 50 % |

## Inputs and provenance

Every input of every step carries a provenance on the issued sheet: the figures chosen for this example cite an (invented) specification or drawing, an input left at the sheet's default is recorded as an assumption, and a linked input names the calculation and output it comes from. Project, drawing, specification and issue references are illustrative; they are not real documents.

## Files

- `pack-issue-sheet.txt` — the pack's issue sheet (index, members, revisions) as stored on the Evidence record.
- `b4-bs-room-heat-loss.sheet.txt` — Room heat loss, the issued sheet.
- `b2-bs-psychrometrics.sheet.txt` — Room air state, the issued sheet.
- `b7-bs-glaser.sheet.txt` — Wall condensation, the issued sheet.
- `inputs.json` — a record of every step's inputs as issued, links included: one JSON object keyed by step, each value that step's list of inputs. It is not a file that one worksheet imports whole (a worksheet takes the inputs of one calculation: copy one step's list out of it to load that step). Links and attached-file references in it name the original calculations and their Evidence, so they do not resolve in another project.

Generated by `ChainPackWorkedExampleTests` in `tests/Tempest.Calc.Tests.Library/Chains`. The PDFs are rendered separately.
