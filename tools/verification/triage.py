"""Regenerate docs/verification/risk-triage.md from the P1 reports (Track P, P6).

Verification aid only. Usage: python3 tools/verification/triage.py <release-sha>
The narrative below is maintained here; the tables come from findings.py.
"""
import pathlib
import re
import subprocess
import sys

sys.path.insert(0, str(pathlib.Path(__file__).parent))
import findings  # noqa: E402

ROOT = pathlib.Path(__file__).resolve().parents[2]
OUT = ROOT / "docs" / "verification" / "risk-triage.md"
LEVELS = ["Critical", "High", "Medium", "Low", "Info"]

# Test order: (label, calculator ids, manual-test files, why here)
ORDER = [
    ("E4 conductor sizing", ["elec.conductor-sizing"], "elec.conductor-sizing.md", "Liability list; electrical safety"),
    ("E5 fault protection", ["elec.fault-protection"], "elec.fault-protection.md", "Liability list; electrical safety"),
    ("Motor-circuit pack (E1 → E6 → E4 → E3, E5, E2)", ["chain.motor-circuit"], "chain.motor-circuit.md", "Links E4 and E5 to their inputs"),
    ("M3 bolt group, bolted joint", ["mech.bolt-group", "mech.bolted-joint"], "mech.bolt-group.md, mech.bolted-joint.md", "Liability list"),
    ("M4 shaft and the drive-train pack", ["mech.shaft", "chain.drive-train"], "mech.shaft.md, chain.drive-train.md", "Liability list; two Critical in the chain"),
    ("S1 section properties", ["struct.section-properties"], "struct.section-properties.md", "Critical; feeds S3, S4 and S6"),
    ("S4 EC3 connections", ["struct.connection-ec3"], "struct.connection-ec3.md", "Steel connections"),
    ("S6 base plate", ["struct.base-plate"], "struct.base-plate.md", "Liability list"),
    ("S3 member checks", ["struct.en1993-member"], "struct.en1993-member.md", "Steel members"),
    ("S2 frame and beam formulas", ["struct.frame-2d", "struct.beam-formulas"], "struct.frame-2d.md, struct.beam-formulas.md", "Analysis feeding S3"),
    ("M6 weld group", ["mech.weld-group"], "mech.weld-group.md", "Welds"),
    ("E1 motor sizing", ["elec.motor-sizing"], "elec.motor-sizing.md", "Feeds both packs"),
    ("B2 psychrometrics", ["bs.psychrometrics"], "bs.psychrometrics.md", "Critical; lower physical liability"),
    ("M2 fits, press fit, thick cylinder", ["mech.iso286-fit", "mech.press-fit", "mech.thick-cylinder"], "mech.iso286-fit.md, mech.press-fit.md, mech.thick-cylinder.md", "Mechanical fits and vessels"),
    ("M5 bearing life", ["mech.bearing-life"], "mech.bearing-life.md", "Drive-train pack"),
    ("E6 load budget, E3 voltage drop", ["elec.load-budget", "elec.voltage-drop"], "elec.load-budget.md, elec.voltage-drop.md", "Motor-circuit pack"),
    ("B1 pipe and pump, B6 sealed system", ["bs.pipe-pump", "bs.sealed-system"], "bs.pipe-pump.md, bs.sealed-system.md", "B6 needs the owner's BS EN 12828"),
    ("B4 room heat loss", ["bs.room-heat-loss"], "bs.room-heat-loss.md", "Heating design"),
    ("E2, B3, B5, S5, M1", ["elec.enclosure-heat", "bs.duct-sizing", "bs.heat-exchanger", "struct.load-combinations", "mech.tolerance-stack"], "remaining files", "Lower liability"),
    ("Desktop sheet import (any calculator)", ["desktop.calc-sheet-view"], "rederivation/desktop.calc-sheet-view.md", "Seeded unit not converted"),
]


def first(text):
    text = text.replace("|", "\\|")
    m = re.match(r"(.+?[.!?])(\s|$)", text)
    return (m.group(1) if m else text)[:220]


def main(sha):
    rows = findings.collect()
    open_rows = [r for r in rows if r["status"] != "Fixed" and r["reviewed"] != "Rejected"]
    fixed = [r for r in rows if r["status"] == "Fixed"]
    pos = {c: i for i, (_, ids, _, _) in enumerate(ORDER) for c in ids}
    rank = findings.RANK

    def count(cid, lvl):
        return sum(1 for r in open_rows if r["calculator"] == cid and r["reviewed"] == lvl)

    calcs = sorted({r["calculator"] for r in rows}, key=lambda c: pos.get(c, 99))
    cnt = ["| Calculator | " + " | ".join(LEVELS) + " | Fixed |", "|---|" + "---|" * (len(LEVELS) + 1)]
    for c in calcs:
        cnt.append(f"| {c} | " + " | ".join(str(count(c, l)) for l in LEVELS) + f" | {sum(1 for r in fixed if r['calculator'] == c)} |")
    tot = {l: sum(1 for r in open_rows if r["reviewed"] == l) for l in LEVELS}
    cnt.append("| **Open total** | " + " | ".join(f"**{tot[l]}**" for l in LEVELS) + f" | **{len(fixed)}** |")

    order = ["| Order | Item | Open Critical / High | Why here | Manual test (docs/verification/manual-tests/) |", "|---|---|---|---|---|"]
    for i, (label, ids, files, why) in enumerate(ORDER, 1):
        ch = f"{sum(count(c, 'Critical') for c in ids)} / {sum(count(c, 'High') for c in ids)}"
        order.append(f"| {i} | {label} | {ch} | {why} | {files} |")

    def table(levels):
        sel = sorted([r for r in open_rows if r["reviewed"] in levels], key=lambda r: (rank[r["reviewed"]], pos.get(r["calculator"], 99), r["id"]))
        out = ["| Rank | ID | Calculator | Reviewed | First pass | Finding (first sentence; detail in the report) |", "|---|---|---|---|---|---|"]
        out += [f'| {i} | {r["id"]} | {r["calculator"]} | {r["reviewed"]} | {r["original"]} | {first(r["finding"])} |' for i, r in enumerate(sel, 1)]
        return "\n".join(out), len(sel)

    ch_table, n_ch = table({"Critical", "High"})
    med_table, n_med = table({"Medium"})
    fixed_lines = "\n".join(f'- {r["id"]} ({r["reviewed"]}): {first(r["finding"])}' for r in sorted(fixed, key=lambda r: (rank[r["reviewed"]], r["id"]))) or "- None."
    ncalc = len([c for c in calcs if not c.startswith(("chain.", "desktop."))])

    doc = f"""# v0.26.0 risk-based test plan and defect triage (Track P, P6)

Generated by `python3 tools/verification/triage.py` against `release/v0.26.0` @ {sha}.
This file ranks every open known or suspected defect and sets the owner's test order.
Track S fixes Critical and High findings in phase C-1. Track P reports only and changed no product code.

## Sources
- P1 re-derivation reports: `docs/verification/rederivation/<id>.md` ({ncalc} calculators, 2 packs, the Desktop sheet).
- Each P1 report had a first pass and a senior independent review. The "Reviewed" severity governs.
- Reports carry a "Status @ <sha>" column where the product changed after the first pass.
- P2 known-answer sources: `kat-sources.md`. P5 consistency audit: `consistency-audit.md`. Colour-team reports: `docs/reviews/`.

## Severity scale
- **Critical:** wrong result in normal use; unconservative; could lead to an unsafe design.
- **High:** wrong result in plausible use; or a missing guard lets an unconservative result through.
- **Medium:** wrong only in edge cases; conservative error over 5 %; misleading output or label.
- **Low:** wording, display or small inconsistency; conservative error under 5 %.

## Summary
All 107 known-answer cases (KATs) pass in the product and in the independent scripts.
The KATs are mostly analytic and narrow, so they miss the defects below.
Every calculator Critical and High finding was reproduced with the probe tool. The Desktop finding comes from code reading only.

{chr(10).join(cnt)}

## Four Critical findings (fix before any owner use)
1. **P1-struct.section-properties-01.** A tube or hollow rectangle inside a composite section counts its hole as solid. A 200 × 10 tube reads 568.6 cm²; the correct area is 59.7 cm². The coordinator reproduced it.
2. **P1-bs.psychrometrics-01.** Cooling with dehumidification swaps sensible and latent load. At 28 °C 50 % to 12 °C, the sheet shows 8.9 kW sensible and 16.4 kW latent; physics gives about 16.3 kW and 8.9 kW. Total load is right; SHR and coil selection are wrong. The coordinator reproduced it.
3. **P1-chain.drive-train-01.** The drive-train pack binds motor RMS torque to the shaft as steady torque. Shaft yield and key checks never see peak torque. Fatigue ignores the reversing torque.
4. **P1-chain.drive-train-02.** The same binding applies no gear ratio or efficiency. A shaft after a gearbox sees T·i·η. It is Critical only when i > 1; a direct-drive shaft is unaffected.

## Recommended test order for the owner
The addendum's liability order is E4/E5, S8, F2, S6, S7, M3, M4.
S7, S8 and F2 belong to Addendum B and are not yet on `release/v0.26.0`. Track P adds them as they merge.

{chr(10).join(order)}

## Open Critical and High findings, ranked ({n_ch})
{ch_table}

## Open Medium findings ({n_med})
Fix in C-1 when each takes under 2 hours; otherwise list for the owner.

{med_table}

## Fixed on release since the first pass ({len(fixed)})
{fixed_lines}

## Known-answer source issues (P2)
- **ka-shigley-stiffness (M3):** contradicted. Shigley's frustum relation uses the bolt diameter in both places; the sheet mixes bolt and hole diameter. The senior review upheld it. Owner to confirm the equation number in the 11th edition.
- **ka-shigley-7-1 (M4):** provisionally contradicted. An open lecture deck of Example 7-1 shows other material values. The deck may be wrong; the owner's copy decides.
- **ka-chs-moment (S6):** its expected t_req pins defect P1-struct.base-plate-01 and must change with the fix.
- Five cases stay unconfirmed and need the owner's books or datasheet: Incropera 11.1, Shigley screw stack, SCI P358 fin plate, the Designers' Guide to EN 1990, Phoenix Contact QUINT4 10 A derating.

## Open colour-team items (not P1 findings)
| ID | Severity | Item | Status |
|---|---|---|---|
| Y3-03 / RR-03 | Medium | Frame mechanism test by relative pivot can pass a near-mechanism with an extreme stiffness ratio | Open (Red) |
| RR-01 | Low | Windows DirectWrite may reject a subset font; PDF may exceed 200 KB | Open (Grey CI decides) |
| RR-04 | Low | Blue Book and ASHRAE values read from mirrors, not printed sources | Open (owner); P2 confirmed 20 of 21 secondary cases |
| R-22 | Low | Orphan Draft Evidence after a crash mid-Create | Accepted (D-32) |
| B-19 | Medium | Failed condition check shows "—" for utilisation | Justified; wording for v0.26.1 |

The G-02 fix added the linear EN 1993-1-8 bolt check, but P1-mech.bolt-group-01 shows it runs only at one bolt. Treat that as a partial regression of G-02.

## App gaps that block parts of the owner test (from P4)
- The Desktop app cannot reopen a saved worksheet calculation. Stale-upstream and revise steps are marked BLOCK as built.
- There is no pack create, check or issue screen. The owner builds each chain by hand.

## Consistency items that can mislead (from P5)
Track S fixes these in C-2. The top five are:
1. S1 names the I-section major axis x; S3 names it y.
2. Symbols clash across linked sheets (`theta`, `T`, `t_f`, `eps`, `h`, `P`, `p_i`).
3. Linked quantities use different units (N·m vs kN·m, m³/s vs m³/h).
4. Percent appears as a unit, a 0–100 number and a 0–1 fraction.
5. Sheets cite internal codes (E4, S6, §A5) an engineer cannot resolve.
"""
    OUT.write_text(doc, encoding="utf-8")
    print(f"wrote {OUT.relative_to(ROOT)}: open {tot}, fixed {len(fixed)}")


if __name__ == "__main__":
    sha = sys.argv[1] if len(sys.argv) > 1 else subprocess.run(["git", "rev-parse", "--short", "origin/release/v0.26.0"], capture_output=True, text=True, cwd=ROOT).stdout.strip()
    main(sha)
