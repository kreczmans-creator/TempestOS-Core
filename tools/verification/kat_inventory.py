"""List every known-answer case (KAT) declared in the calculator templates.

Verification aid only. Not part of the solution or CI.
Usage: python3 tools/verification/kat_inventory.py [--json]
"""
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
CALC = ROOT / "src" / "Tempest.Calc"
ID_RE = re.compile(r'public const string Id = "([^"]+)"')
CASE_RE = re.compile(
    r'new CalcTestCase\(\s*"([^"]+)",\s*"([^"]*)",\s*'
    r'new CalcReferenceSource\(\s*"((?:[^"\\]|\\.)*)",\s*(null|"(?:[^"\\]|\\.)*"),\s*"((?:[^"\\]|\\.)*)"\)',
    re.S,
)


def case_blocks(text):
    """Yield (match, block text up to the next case or end of the TestCases list)."""
    starts = [m for m in CASE_RE.finditer(text)]
    for i, m in enumerate(starts):
        end = starts[i + 1].start() if i + 1 < len(starts) else len(text)
        yield m, text[m.start():end]


def inventory():
    rows = []
    for path in sorted(CALC.glob("Tempest.Calc.*/*Calculator.cs")):
        text = path.read_text(encoding="utf-8")
        ident = ID_RE.search(text)
        if not ident:
            continue
        for m, block in case_blocks(text):
            # Status is the last argument of the case, before the closing parenthesis.
            head = block.split("\n        ],")[0]
            status = "Unconfirmed" if "CalcReferenceStatus.Unconfirmed" in head else "Confirmed"
            title = m.group(3)
            kind = "Analytic" if title.lower().startswith("analytic") else "Published"
            rows.append({
                "calculator": ident.group(1),
                "file": str(path.relative_to(ROOT)),
                "case": m.group(1),
                "title": m.group(2),
                "source": title,
                "edition": None if m.group(4) == "null" else m.group(4).strip('"'),
                "location": m.group(5),
                "status": status,
                "kind": kind,
            })
    return rows


if __name__ == "__main__":
    data = inventory()
    if "--json" in sys.argv:
        print(json.dumps(data, indent=1, ensure_ascii=False))
    else:
        for r in data:
            print(f'{r["calculator"]:28} {r["case"]:34} {r["status"]:11} {r["kind"]:9} {r["source"][:60]}')
        print(f"{len(data)} cases, {sum(r['status'] == 'Unconfirmed' for r in data)} unconfirmed")
