#!/usr/bin/env python3
"""Build docs/calcs/glossary.md from docs/calcs/glossary-intro.md and docs/calcs/glossary-data.json.

glossary-data.json is written from the live catalogue by the contract tests (every input, table column and output symbol of every
worksheet template, with its quantity type, units, descriptions and templates), together with the precision table and the reserved
symbols of the sheet standard:

    TEMPEST_WRITE_GLOSSARY=1 dotnet test tests/Tempest.Calc.Tests.Calculators --filter GlossaryTests
    python3 scripts/generate-glossary.py

`GlossaryTests` fails when the data file no longer matches the catalogue, and when glossary.md lacks a symbol a template uses or lists it
under another quantity type, so a template cannot add or change a symbol without the glossary changing in the same commit. Run from the
repository root. With --check the script writes nothing and exits 1 when glossary.md is not what it would write.
"""
import json
import pathlib
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
CALCS = ROOT / "docs" / "calcs"
DATA = CALCS / "glossary-data.json"
INTRO = CALCS / "glossary-intro.md"
OUT = CALCS / "glossary.md"


def cell(text: str) -> str:
    """A table cell: no pipe, no line break."""
    return " ".join(text.replace("|", "/").split())


def shorten(descriptions: list[str], limit: int = 3, width: int = 70) -> str:
    cut = [d if len(d) <= width else d[: width - 1] + "…" for d in descriptions[:limit]]
    more = len(descriptions) - limit
    return cell("; ".join(cut) + (f"; … ({len(descriptions)} descriptions)" if more > 0 else ""))


def build() -> str:
    data = json.loads(DATA.read_text(encoding="utf-8"))
    out: list[str] = [INTRO.read_text(encoding="utf-8").rstrip("\n"), ""]

    out += ["## Quantity types: units and decimals", "",
            "The unit a quantity type is written in, and the decimals it is shown to (display only; the calculation keeps full precision). "
            "`CalcDisplay.Rules` is the one table of decimals and `CalcUnitStandard` the one table of units; `GlossaryTests` holds this table to them. "
            "A value is never shown with fewer than two or more than five significant figures. A utilisation is shown to 2 decimals and, above 1, rounded up.", "",
            "| Quantity type | Symbols | Standard unit | Decimals by unit | Exceptions |", "|---|---|---|---|---|"]

    for t in data["quantityTypes"]:
        units = ", ".join(t["units"]) or "—"
        decimals = "; ".join(f"{u} {d}" for u, d in t["decimals"].items())
        exceptions = "; ".join(t["exceptions"]) or "—"
        out.append(f"| {cell(t['quantity'])} | {cell(t['symbols']) or '—'} | {cell(units)} | {cell(decimals)} | {cell(exceptions)} |")

    out += ["", "Plain numbers: a utilisation, a safety factor, an efficiency and a power factor are shown to 2 decimals; any other plain number to five significant figures. "
            "A percentage is a quantity in `%`, shown to 1 decimal.", ""]

    out += ["## Reserved symbols", "",
            "A bare symbol that means one kind of quantity wherever it appears. Another quantity takes a qualified symbol. `GlossaryTests` and the contract "
            "rules `C10` to `C18` hold every template to this list.", "",
            "| Symbol | Means | Audit row |", "|---|---|---|"]
    out += [f"| `{r['symbol']}` | {cell(r['meaning'])} | {r['row']} |" for r in data["reserved"]]
    out.append("")

    clashes = [s for s in data["symbols"] if len(s["meanings"]) > 1]
    out += [f"## Symbols with more than one meaning today ({len(clashes)})", "",
            "Each of these symbols is used for different quantities in different templates. Phases B and C resolve every one: give each quantity its own symbol "
            "(keeping the old symbol as an input or port alias), or make the templates agree. Status `RESOLVE` marks the rows in the full table below.", ""]

    for s in clashes:
        out.append(f"- `{s['symbol']}`: " + "; ".join(f"{cell(m['quantity'])} ({', '.join(t.split('.', 1)[1] for t in m['templates'][:4])}{', …' if len(m['templates']) > 4 else ''})" for m in s["meanings"]))

    out += ["", f"## All symbols ({len(data['symbols'])} symbols in {data['templates']} templates)", "",
            "One row per symbol and quantity type. Status `ok` is a symbol with one meaning; `RESOLVE` a symbol with several (phases B and C).", "",
            "| Symbol | Quantity type | Units | Descriptions in the templates | Templates | Status |", "|---|---|---|---|---|---|"]

    for s in data["symbols"]:
        status = "ok" if len(s["meanings"]) == 1 else f"RESOLVE ({len(s['meanings'])} meanings)"

        for m in s["meanings"]:
            symbol = cell(s["symbol"].replace("`", ""))
            out.append(f"| `{symbol}` | {cell(m['quantity'])} | {cell(', '.join(m['units'])) or '—'} | {shorten(m['descriptions'])} | {', '.join(m['templates'])} | {status} |")

    return "\n".join(out) + "\n"


def main() -> int:
    text = build()

    if "--check" in sys.argv:
        current = OUT.read_text(encoding="utf-8") if OUT.exists() else ""

        if current != text:
            print(f"{OUT.relative_to(ROOT)} is not what glossary-data.json and glossary-intro.md produce; run scripts/generate-glossary.py.")
            return 1

        return 0

    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
