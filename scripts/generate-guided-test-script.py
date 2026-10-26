#!/usr/bin/env python3
"""Build the guided test script the app ships (Addendum C, C-5).

The script is the structured, versioned list of steps the in-app "Guided test" walks the owner through. It is generated from
the same sources the owner's paper pack uses:

* one step per worked example (docs/worked-examples/<discipline>/<template>/<example>/): its inputs.json, the inputs table
  of sheet.txt, and the README "Output | Expected" table and "Governing check" line (see scripts/generate-test-values.py);
* the manual steps of the release's runbook, docs/testing/v<VERSION>-runbook.md (named from the VERSION file, so a new release's runbook is
  picked up without editing this script): the smoke test (section 4), the linked packs (6), imports (7),
  lifting (8) and legacy records (9).

The result is written to src/Tempest.Calc/Tempest.Calc.Library/ManualTesting/guided-test-script.json, which the library embeds
(the docs folder is not present in an installed build). A test (GuidedTestScriptStalenessTests) fails when the shipped script
no longer matches the docs. Run from the repository root:

    python3 scripts/generate-guided-test-script.py          # rewrite the script
    python3 scripts/generate-guided-test-script.py --check  # exit 1 when the shipped script is stale
"""
import hashlib
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
WE = ROOT / "docs" / "worked-examples"
RELEASE = (ROOT / "VERSION").read_text(encoding="utf-8").strip()
RUNBOOK = ROOT / "docs" / "testing" / f"v{RELEASE}-runbook.md"
OUT = ROOT / "src" / "Tempest.Calc" / "Tempest.Calc.Library" / "ManualTesting" / "guided-test-script.json"
DISCIPLINES = [("mechanical", "Mechanical"), ("structural", "Structural"), ("building-services", "Building services"),
               ("electrical", "Electrical"), ("fluid-power", "Fluid power")]

# The runbook's pack bullets name a chain; the pack folder holding its README is stated here, so a renamed bullet is a loud error.
PACKS = {
    "drive train": "drive-train",
    "hydraulic power unit (chain 1)": "hydraulic-power-unit",
    "actuated linkage (chain 5)": "actuated-linkage",
    "electronics enclosure (chain 10)": "electronics-enclosure",
}

# Caps the C# loader enforces too (GuidedTestScript.Max*).
MAX_STEPS = 400
MAX_EXPECTED = 200
MAX_INPUT_ROWS = 400


def lf(text: str) -> str:
    return text.replace("\r\n", "\n").replace("\r", "\n")


def plain(text: str) -> str:
    """Markdown emphasis, code ticks and links removed: the app shows plain text."""
    text = re.sub(r"\[([^\]]+)\]\([^)]*\)", r"\1", text)
    text = text.replace("**", "").replace("`", "")
    return text.strip()


def section(text: str, number: int) -> list[str]:
    """The lines of '## <number>. ...' up to the next '## ' or '---' rule."""
    lines = text.splitlines()
    start = next((i for i, l in enumerate(lines) if re.match(rf"^## {number}\. ", l)), None)
    if start is None:
        raise SystemExit(f"runbook: section {number} not found")
    end = next((i for i in range(start + 1, len(lines)) if lines[i].startswith("## ") or lines[i].strip() == "---"), len(lines))
    return lines[start + 1:end]


def items(lines: list[str], marker: str) -> tuple[list[str], list[list[str]]]:
    """Splits lines into (intro lines, top-level list items); an item keeps its indented continuation, code blocks and sub-bullets."""
    pattern = re.compile(marker)
    intro: list[str] = []
    found: list[list[str]] = []
    in_fence = False

    for line in lines:
        if line.strip().startswith("```"):
            in_fence = not in_fence
            if found:
                found[-1].append(line)
            continue

        if not in_fence and pattern.match(line):
            found.append([pattern.sub("", line, count=1)])
        elif found:
            found[-1].append(line)
        else:
            intro.append(line)

    return intro, found


def item_lines(item: list[str]) -> list[str]:
    """One item as display lines: blank lines and fence marks dropped, sub-bullets kept as '- text', everything else plain."""
    out = []
    in_fence = False

    for line in item:
        if line.strip().startswith("```"):
            in_fence = not in_fence
            continue

        if not line.strip():
            continue

        if in_fence:
            out.append(line.strip())
            continue

        sub = re.match(r"^\s+[-*] (.*)$", line)
        numbered = re.match(r"^\s+\d+\. (.*)$", line)
        out.append(("- " + plain(sub.group(1))) if sub else ("- " + plain(numbered.group(1))) if numbered else plain(line))

    return out


def title_of(text: str, limit: int = 80) -> str:
    first = re.split(r"(?<=[.:;])\s", plain(text), maxsplit=1)[0].rstrip(".:;")
    return first if len(first) <= limit else first[:limit - 1].rstrip() + "…"


def pass_line(lines: list[str]) -> str | None:
    for l in lines:
        m = re.match(r"^\*\*Pass:\*\*\s*(.*)$", l.strip())
        if m:
            text = plain(m.group(1))
            return text[:1].upper() + text[1:]
    return None


def manual_steps(runbook: str) -> list[dict]:
    steps: list[dict] = []

    # 4. Smoke test: one step per numbered item.
    lines = section(runbook, 4)
    _, found = items([l for l in lines if not l.startswith("**Pass:**")], r"^\d+\.\s+")
    passes = pass_line(lines)
    for n, item in enumerate(found, 1):
        body = item_lines(item)
        steps.append({"id": f"smoke-{n}", "kind": "Smoke", "section": "4", "title": f"Smoke test {n}: {title_of(item[0])}",
                      "instructions": body, "passWhen": passes})

    # 6. Linked chains and packs: the four named chains, each with the common four-point script.
    lines = section(runbook, 6)
    _, common = items(lines, r"^\d+\.\s+")
    common_text = [item_lines(i)[0] for i in common][:4]
    if len(common_text) != 4:
        raise SystemExit("runbook: section 6 should state the four pack steps")

    _, bullets = items([l for l in lines if re.match(r"^- ", l)], r"^- ")
    for bullet in bullets:
        name = plain(bullet[0]).rstrip(";.").strip()
        if name not in PACKS:
            raise SystemExit(f"runbook: pack '{name}' has no folder in generate-guided-test-script.py PACKS")
        folder = PACKS[name]
        readme = f"docs\\worked-examples\\packs\\{folder}\\README.md"
        if not (WE / "packs" / folder / "README.md").exists():
            raise SystemExit(f"runbook: {readme} is missing")
        steps.append({"id": f"pack-{folder}", "kind": "Pack", "section": "6", "title": f"Linked chain: {name}",
                      "instructions": [*(f"{i + 1}. {t}" for i, t in enumerate(common_text)), f"Pack README: {readme}"],
                      "passWhen": "Every link carries the value in the pack README, a changed upstream input marks every downstream step STALE, and the pack checks and issues after the links are updated."})

    # 7. Imports and records: one step per top-level bullet.
    _, found = items(section(runbook, 7), r"^- ")
    for item in found:
        head = plain(item[0])
        key = re.match(r"^(W\d)\b", head)
        steps.append({"id": f"import-{key.group(1).lower() if key else len(steps)}", "kind": "Import", "section": "7",
                      "title": title_of(re.sub(r"^(W\d [^:]+):.*$", r"\1", head)), "instructions": item_lines(item), "passWhen": None})

    # 8. Lifting: one step per numbered item (with its commands).
    lines = section(runbook, 8)
    _, found = items(lines, r"^\d+\.\s+")
    for n, item in enumerate(found, 1):
        steps.append({"id": f"lifting-{n}", "kind": "Lifting", "section": "8", "title": f"Lifting {n}: {title_of(item[0])}",
                      "instructions": item_lines(item), "passWhen": None})

    # 9. Legacy records: the bullets, each with the section's setup paragraph.
    lines = section(runbook, 9)
    intro = [plain(l) for l in lines if l.strip() and not l.startswith("- ")]
    _, found = items(lines, r"^- ")
    for n, item in enumerate(found, 1):
        steps.append({"id": f"legacy-{n}", "kind": "Legacy", "section": "9", "title": f"Legacy records {n}: {title_of(item[0])}",
                      "instructions": [*item_lines(item), *intro], "passWhen": None})

    return steps


def table_after(lines: list[str], header_start: str) -> list[list[str]]:
    try:
        h = next(i for i, l in enumerate(lines) if l.startswith(header_start))
    except StopIteration:
        return []
    rows = []
    for l in lines[h + 2:]:
        if not l.startswith("|"):
            break
        rows.append([p.strip() for p in re.split(r"(?<!\\)\|", l.strip().strip("|"))])
    return rows


def split_number(expected: str) -> tuple[str, str]:
    m = re.match(r"^(\S+)\s*(.*)$", expected.strip())
    return (m.group(1), m.group(2).strip()) if m else (expected.strip(), "")


def inputs_rows(sheet: str) -> list[dict]:
    lines = sheet.splitlines()
    start = next((i for i, l in enumerate(lines) if re.match(r"^2\. ", l)), None)
    if start is None:
        return []
    rows = []
    for l in lines[start + 2:]:
        if re.match(r"^\d+\. [A-Z]", l):
            break
        if not l.strip():
            continue
        parts = [p.strip() for p in l.strip().split(" | ")]
        if len(parts) < 4:
            continue
        rows.append({"symbol": parts[0], "description": parts[1], "value": parts[2], "unit": parts[3]})
    return rows


def governing_of(lines: list[str], where: str) -> dict | None:
    """The README's 'Governing check:' line as a dict, or None for an example that states 'Governing check: none'.

    A line that is neither is an error, not a silent 'none': the app compares the form's governing check with this, so an unparsed line would
    quietly turn "the form must show this check" into "the form must show none"."""
    line = next((l for l in lines if l.startswith("Governing check:")), None)
    if line is None:
        raise SystemExit(f"{where}: the README has no 'Governing check:' line (write 'Governing check: none' for an example with no check)")
    if re.match(r"^Governing check: none\b", line):
        return None
    m = re.match(r"^Governing check: (.*?) — (PASS|FAIL) \(utilisation ([^)]+)\)", line)
    if not m:
        raise SystemExit(f"{where}: cannot parse the 'Governing check:' line {line!r}; expected 'Governing check: <name> — PASS|FAIL (utilisation <n>)' or 'Governing check: none'")
    return {"name": m.group(1), "outcome": m.group(2), "utilisation": m.group(3)}


def example_step(disc: str, template: str, folder: pathlib.Path) -> dict:
    readme = lf((folder / "README.md").read_text(encoding="utf-8"))
    sheet = lf((folder / "sheet.txt").read_text(encoding="utf-8")) if (folder / "sheet.txt").exists() else ""
    lines = readme.splitlines()
    title = lines[0].lstrip("# ").strip()
    title = re.sub(r"\s*\(`[^`]+`\)\s*$", "", title)
    version = re.search(r"Template: (\S+)@(\S+)", sheet)

    expected = []
    for row in table_after(lines, "| Output | Expected"):
        value, unit = split_number(row[1])
        computed, _ = split_number(row[2]) if len(row) > 2 else ("", "")
        expected.append({"symbol": row[0].strip("`"), "value": value, "unit": unit, "computed": computed})

    governing = governing_of(lines, f"{template}/{folder.name}")

    inputs = json.loads(lf((folder / "inputs.json").read_text(encoding="utf-8")))
    example = folder.name
    return {
        "id": f"example:{template}/{example}", "kind": "Example", "section": "5", "title": title,
        "discipline": disc, "templateId": template, "exampleId": example,
        "templateVersion": version.group(2) if version else None,
        "inputsPath": folder.relative_to(ROOT).as_posix() + "/inputs.json",
        "inputs": inputs_rows(sheet), "inputsJson": json.dumps(inputs, ensure_ascii=False, separators=(",", ":")),
        "expected": expected, "governing": governing,
    }


def build() -> dict:
    runbook = lf(RUNBOOK.read_text(encoding="utf-8"))
    steps: list[dict] = []
    steps += manual_steps(runbook)

    for disc, _ in DISCIPLINES:
        for readme in sorted((WE / disc).glob("*/*/README.md")):
            steps.append(example_step(disc, readme.parent.parent.name, readme.parent))

    # A manual step that pastes a worked example's inputs.json (the smoke test) opens that example's form and shows its expected values.
    by_path = {s["inputsPath"].replace("/", "\\"): s["id"] for s in steps if s["kind"] == "Example"}
    for s in steps:
        if s["kind"] == "Example":
            continue
        for line in s["instructions"]:
            m = re.search(r"docs\\worked-examples\\\S+?\\inputs\.json", line)
            if m and m.group(0) in by_path:
                s["exampleRef"] = by_path[m.group(0)]

    ids = [s["id"] for s in steps]
    if len(set(ids)) != len(ids):
        raise SystemExit("duplicate step ids")
    if len(steps) > MAX_STEPS:
        raise SystemExit(f"{len(steps)} steps exceed the cap of {MAX_STEPS}")
    for s in steps:
        if len(s.get("expected", [])) > MAX_EXPECTED or len(s.get("inputs", [])) > MAX_INPUT_ROWS:
            raise SystemExit(f"step {s['id']} exceeds a cap")

    release = RELEASE
    content = json.dumps(steps, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
    digest = hashlib.sha256(content.encode("utf-8")).hexdigest()
    return {
        "format": 1,
        "scriptVersion": f"{release}+{digest[:8]}",
        "release": release,
        "generatedFrom": f"docs/testing/v{release}-runbook.md, docs/worked-examples (scripts/generate-guided-test-script.py)",
        "steps": steps,
    }


def render(script: dict) -> str:
    """Header fields first, then one step per line, so a change to one example is a one-line diff."""
    head = {k: v for k, v in script.items() if k != "steps"}
    lines = ",\n".join(" " + json.dumps(step, ensure_ascii=False, separators=(",", ":")) for step in script["steps"])
    return json.dumps(head, ensure_ascii=False, indent=1)[:-2] + ',\n "steps": [\n' + lines + "\n ]\n}\n"


def main() -> int:
    text = render(build())

    if "--check" in sys.argv:
        current = lf(OUT.read_text(encoding="utf-8")) if OUT.exists() else ""
        if current != text:
            print(f"{OUT.relative_to(ROOT)} is stale; run python3 scripts/generate-guided-test-script.py", file=sys.stderr)
            return 1
        print("guided test script is current")
        return 0

    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(text, encoding="utf-8", newline="\n")
    script = json.loads(text)
    kinds: dict[str, int] = {}
    for s in script["steps"]:
        kinds[s["kind"]] = kinds.get(s["kind"], 0) + 1
    print(f"{OUT.relative_to(ROOT)}: {len(script['steps'])} steps {kinds}, version {script['scriptVersion']}, {len(text) // 1024} KB")
    return 0


if __name__ == "__main__":
    sys.exit(main())
