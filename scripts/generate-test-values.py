#!/usr/bin/env python3
"""Build docs/testing/v0.26.0-test-values.md from the worked examples.

For every worked example (docs/worked-examples/<discipline>/<template>/<example>/) it writes the inputs exactly as the
sheet lists them (symbol, description, value, unit, table rows) and the expected outputs from the example README, so a
tester can type them into the Desktop form or import inputs.json and compare. Run from the repository root:
    python3 scripts/generate-test-values.py

An example whose printed expected value is a hand calculation, or needs the tester's judgement, is marked "For your review". The list is
the one the guided test keeps (the ForReview set in tests/Tempest.Calc.Tests.Library/ManualTesting/GuidedAutoCompareTests.cs), read from that
file, so a regeneration keeps the marks in step with the guided test (Addendum C, C-8; DECISIONS DC-12).
"""
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
WE = ROOT / "docs" / "worked-examples"
OUT = ROOT / "docs" / "testing" / "v0.26.0-test-values.md"
DISCIPLINES = [("mechanical", "Mechanical"), ("structural", "Structural"), ("building-services", "Building services"),
               ("electrical", "Electrical"), ("fluid-power", "Fluid power")]
REVIEW_SOURCE = ROOT / "tests" / "Tempest.Calc.Tests.Library" / "ManualTesting" / "GuidedAutoCompareTests.cs"

# Why an example is on the list, where the reason is not "a hand calculation". Every other example on the list gets the hand-calculation text.
REVIEW_REASON = {
    "mech.iso286-fit/h7-g6-25mm": "The README prints the standard's rounded deviation es (-7 µm) and clearance c_min (7 µm). The engine's formula gives -7.2853 and 7.2853 µm. Decide which one you accept.",
    "fp.accumulator/clamp-circuit-bladder-accumulator": "The README prints the pre-charge p0 in a bare `bar`, which names no basis. The form shows `bar a` (absolute). Confirm that the printed value is the absolute pre-charge.",
}
HAND_REASON = ("The printed expected value is a hand calculation or a published figure, held to the example's own verification tolerance (1 % by default). "
               "The form shows what the engine computed, so a line can differ from the printed digits.")


def review_set() -> set[str]:
    """The example ids (template/example) in the ForReview set of the guided test's auto-compare tests."""
    if not REVIEW_SOURCE.exists():
        raise SystemExit(f"{REVIEW_SOURCE.relative_to(ROOT)} is missing: the list of examples for review is read from it")
    text = REVIEW_SOURCE.read_text(encoding="utf-8")
    block = re.search(r"ForReview\s*=\s*new\([^)]*\)\s*\{(.*?)\n\s*\};", text, re.S)
    if not block:
        raise SystemExit("GuidedAutoCompareTests.cs: the ForReview set was not found")
    ids = set(re.findall(r'"example:([^"]+)"', block.group(1)))
    if not ids:
        raise SystemExit("GuidedAutoCompareTests.cs: the ForReview set holds no example")
    return ids


def differing_lines(readme: str) -> list[str]:
    """The expected-output lines whose printed value differs from the computed value in the README (the hand-calculation lines)."""
    lines = readme.splitlines()
    try:
        h = next(i for i, l in enumerate(lines) if l.startswith("| Output | Expected"))
    except StopIteration:
        return []
    out = []
    for l in lines[h + 2:]:
        if not l.startswith("|"):
            break
        parts = [p.strip() for p in l.strip("|").split("|")]
        if len(parts) > 2 and parts[1].split()[0] != parts[2].split()[0]:
            out.append(f"{parts[0]} (printed {parts[1]}, computed {parts[2]})")
    return out


def section(text: str, number: int) -> list[str]:
    lines = text.splitlines()
    start = next((i for i, l in enumerate(lines) if re.match(rf"^{number}\. ", l)), None)
    if start is None:
        return []
    end = next((i for i in range(start + 1, len(lines)) if re.match(r"^\d+\. [A-Z]", lines[i])), len(lines))
    return lines[start + 1:end]


def cell(s: str) -> str:
    return s.strip().replace("|", "\\|")


def inputs_table(sheet: str) -> list[str]:
    rows = [l for l in section(sheet, 2) if l.strip()]
    if not rows:
        return ["_No inputs section found in sheet.txt._"]
    out = ["| Symbol | Description | Value | Unit |", "|---|---|---|---|"]
    for l in rows[1:]:  # first row is the header
        parts = [p.strip() for p in l.strip().split(" | ")]
        if len(parts) < 4:
            continue
        sym, desc, val, unit = parts[0], parts[1], parts[2], parts[3]
        if re.match(r"^\w+\[\d+\]$", sym):
            sym = "&nbsp;&nbsp;" + sym  # a table row, shown under its table
        out.append(f"| `{cell(sym)}` | {cell(desc)} | {cell(val)} | {cell(unit)} |")
    return out


def expected_table(readme: str) -> list[str]:
    lines = readme.splitlines()
    try:
        h = next(i for i, l in enumerate(lines) if l.startswith("| Output | Expected"))
    except StopIteration:
        return ["_No expected-output table in the README._"]
    out = ["| Output | Expected |", "|---|---|"]
    for l in lines[h + 2:]:
        if not l.startswith("|"):
            break
        parts = [p.strip() for p in l.strip("|").split("|")]
        out.append(f"| {parts[0]} | {parts[1]} |")
    return out


def main() -> int:
    examples = []
    for disc, _ in DISCIPLINES:
        for readme in sorted((WE / disc).glob("*/*/README.md")):
            examples.append((disc, readme.parent.parent.name, readme.parent))
    templates = sorted({(d, t) for d, t, _ in examples})

    md = ["# TempestOS v0.26.0 — test values for every worksheet calculator", "",
          f"Generated by `scripts/generate-test-values.py` from the {len(examples)} worked examples of the {len(templates)} worksheet templates "
          "(docs/worked-examples). Every value here is the one the build's automated tests check: the worked-example tests "
          "re-run each example and compare these outputs.", "",
          "**How to use a set.** In the Desktop open Modules → Calculations → Worksheets and pick the template. Either import the "
          "example's `inputs.json` (Import inputs… on the worksheet form; the path is given) or type the inputs below. Run it. "
          "Each expected output must match to the digits shown. The governing check must read as stated. A table input lists its "
          "rows under it (for example `sizes[1]`).", "",
          "Units are as the form shows them. `bar a` and `bar g` are absolute and gauge bar. A value marked `default` is the form's "
          "own default: leave it as it opens.", "", "## Contents", ""]
    for disc, title in DISCIPLINES:
        ts = [t for d, t in templates if d == disc]
        if ts:
            md.append(f"- **{title}:** " + ", ".join(f"[`{t}`](#{t.replace('.', '')})" for t in ts))
    md.append("")

    review = review_set()
    known = {f"{t}/{folder.name}" for _, t, folder in examples}
    unknown = sorted(review - known)
    if unknown:
        raise SystemExit("GuidedAutoCompareTests.cs lists examples that do not exist: " + ", ".join(unknown))
    md += [f"## Examples for your review ({len(review)})", "",
           "These examples are marked **For your review** under their heading. For each one the expected value is a hand calculation, a "
           "published figure or a judgement, so you decide it. Do not log a fault when the form differs from the printed digits by less than "
           "the example's own tolerance (1 % by default), and judge a larger difference yourself. The Guided test also leaves these examples to you.", ""]
    md += [f"- `{r}`" for r in sorted(review)] + [""]

    for disc, title in DISCIPLINES:
        ts = [t for d, t in templates if d == disc]
        if not ts:
            continue
        md += [f"## {title}", ""]
        for t in ts:
            md += [f"### {t}", ""]
            for d, tt, folder in examples:
                if d != disc or tt != t:
                    continue
                readme = (folder / "README.md").read_text(encoding="utf-8")
                sheet = (folder / "sheet.txt").read_text(encoding="utf-8") if (folder / "sheet.txt").exists() else ""
                heading = readme.splitlines()[0].lstrip("# ").strip()
                version = re.search(r"Template: (\S+@\S+)", sheet)
                governing = next((l for l in readme.splitlines() if l.startswith("Governing check:")), "")
                governing = governing.split(" Files:")[0]
                rel = folder.relative_to(ROOT).as_posix()
                md += [f"#### {heading}", "",
                       f"Template `{version.group(1) if version else t}`. Import file: `{rel}/inputs.json`. Reference sheet: `{rel}/sheet.pdf`.", ""]
                key = f"{t}/{folder.name}"
                if key in review:
                    why = REVIEW_REASON.get(key, HAND_REASON)
                    diff = differing_lines(readme)
                    md += [f"> **For your review.** {why}" + (" Lines that differ from the computed value: " + "; ".join(diff) + "." if diff and key not in REVIEW_REASON else ""), ""]
                md += ["**Inputs**", ""] + inputs_table(sheet) + [""]
                md += ["**Expected outputs**", ""] + expected_table(readme) + [""]
                if governing:
                    md += [f"**{governing}**", ""]
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text("\n".join(md).rstrip() + "\n", encoding="utf-8")
    print(f"{OUT.relative_to(ROOT)}: {len(templates)} templates, {len(examples)} examples")
    return 0


if __name__ == "__main__":
    sys.exit(main())
