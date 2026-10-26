"""Regenerates the verification dashboard's embedded snapshot (Addendum C, C-4).

The dashboard (Calculations -> Verification) must work in an installed build, where docs/ is not present. So the data that
lives in documents is read here, once, and written to a JSON file that the Library assembly embeds:

    src/Tempest.Calc/Tempest.Calc.Library/Dashboard/verification-snapshot.json

Inputs (read only, never edited):
  docs/verification/rederivation/*.md     Track P re-derivation reports: verdict line and the finding rows, through
                                          tools/verification/findings.py (imported, not run; its output format is the contract)
  docs/testing/v0.26.0-known-defects.md   the status of each finding id: Fixed / Still open / Owner decision / Not a defect
  docs/testing/kat-results.json           the last real run of every KAT and worked example (written by KatResultsTests)
  docs/worked-examples/**/README.md       how many worked examples each template and chain has; a chain's check counts

This script and tools/verification/findings.py are hashed with the documents (the "generator" group), so changing how they read
a document makes the snapshot stale too.

A test (VerificationSnapshotStaleTests) hashes the same inputs and fails when the snapshot was made from older ones.

Usage (from anywhere):
    python3 scripts/generate-verification-snapshot.py            write the snapshot
    python3 scripts/generate-verification-snapshot.py --check    exit 1 if the snapshot on disk is not what this would write
"""
import hashlib
import importlib.util
import json
import pathlib
import re
import sys

sys.dont_write_bytecode = True  # importing findings.py must not leave a __pycache__ in tools/verification

ROOT = pathlib.Path(__file__).resolve().parent.parent
OUT = ROOT / "src" / "Tempest.Calc" / "Tempest.Calc.Library" / "Dashboard" / "verification-snapshot.json"
REDERIVATION = ROOT / "docs" / "verification" / "rederivation"
DEFECTS = ROOT / "docs" / "testing" / "v0.26.0-known-defects.md"
KAT_RESULTS = ROOT / "docs" / "testing" / "kat-results.json"
EXAMPLES = ROOT / "docs" / "worked-examples"
SCHEMA = 1


def normalised(path):
    """File bytes with CRLF folded to LF, so a Windows checkout hashes the same as a Linux one."""
    return path.read_bytes().replace(b"\r\n", b"\n")


def digest(files):
    """SHA-256 over 'relative/path\\n<sha256 of the normalised file>\\n' for each file, in ordinal path order.
    VerificationSnapshotStaleTests computes the same value in C#; change both together."""
    h = hashlib.sha256()
    for path in sorted(files, key=lambda p: p.relative_to(ROOT).as_posix()):
        h.update((path.relative_to(ROOT).as_posix() + "\n" + hashlib.sha256(normalised(path)).hexdigest() + "\n").encode("utf-8"))
    return h.hexdigest()


def source_files():
    return {
        "rederivation": sorted(REDERIVATION.glob("*.md")),
        "knownDefects": [DEFECTS],
        "katResults": [KAT_RESULTS],
        "workedExamples": sorted(EXAMPLES.rglob("README.md")),
        # The code that reads the documents is an input too: a change to how findings are parsed or classified changes the snapshot.
        "generator": [pathlib.Path(__file__).resolve(), ROOT / "tools" / "verification" / "findings.py"],
    }


# ---------------------------------------------------------------- known defects

SECTION = re.compile(r"^###\s+(Critical|High|Medium)\b")
ID_CELL = re.compile(r"^[a-z][a-z0-9.\-]*-[A-Za-z0-9]+$")
ID_START = re.compile(r"^`?([a-z][a-z0-9.\-]*-[A-Za-z0-9]+)`?")
BULK_LINE = re.compile(r"^Still open, [^(]*\((\d+)\):\s*(.*)$")


def bulk_ids(text):
    """The ids of a bulk line: comma-separated at bracket depth 0 (a note in brackets may hold commas), each item's id taken from its
    start (backticks dropped, a trailing bracketed note ignored). An item that does not start with an id is an error, never skipped."""
    items = []
    depth = 0
    current = ""
    for ch in text.strip().rstrip("."):
        if ch == "(":
            depth += 1
        elif ch == ")":
            depth = max(0, depth - 1)
        if ch == "," and depth == 0:
            items.append(current)
            current = ""
        else:
            current += ch
    items.append(current)
    ids = []
    for item in (i.strip() for i in items):
        if not item:
            continue
        m = ID_START.match(item)
        if not m:
            raise SystemExit(f"known-defects: '{item[:40]}' in a 'Still open' line does not start with a finding id")
        ids.append(m.group(1))
    return ids


def cells(line):
    body = line.strip()
    body = body[1:] if body.startswith("|") else body
    body = body[:-1] if body.endswith("|") else body
    return [c.strip().replace("\\|", "|") for c in re.split(r"(?<!\\)\|", body)]


def classify(decision):
    text = decision.replace("*", "").strip().lower()
    if text.startswith("not a defect"):
        return "not-a-defect"
    if "owner decision" in text:
        return "owner-decision"
    if text.startswith("fixed"):
        return "fixed"
    if text.startswith("still open") or text.startswith("partly fixed"):
        return "open"  # "Partly fixed" is not Fixed: the finding is still listed as open
    raise SystemExit(f"known-defects: cannot classify the decision '{decision}'")


def read_defects(path=None):
    """Finding id -> {severity, status, decision}. The summary table the document prints is a cross-check (SystemExit when the rows
    parse to other counts). <path> is the document; a test passes its own."""
    rows = {}
    summary = {}
    section = None
    lines = (path or DEFECTS).read_text(encoding="utf-8").splitlines()
    for line in lines:
        m = SECTION.match(line)
        if m:
            section = m.group(1)
            continue
        if line.startswith("### "):
            section = None
            continue
        if section and line.startswith("|"):
            c = cells(line)
            if not c or not ID_CELL.match(c[0]):
                continue
            decision = c[1] if section == "Medium" else c[2]
            rows[c[0]] = {"severity": section, "status": classify(decision), "decision": re.sub(r"\s+", " ", decision.replace("*", ""))[:60]}
        m = re.match(r"^\|\s*(Critical|High|Medium)\s*\|\s*(\d+)\s*\|\s*(\d+)\s*\|\s*(\d+)\s*\|\s*(\d+)\s*\|\s*(\d+)\s*\|", line)
        if m:
            summary[m.group(1)] = [int(m.group(i)) for i in range(2, 7)]
    # "Still open, code unchanged (35): id, id, ..." and "Still open, listed for the owner (1): id (a note)." list Medium rows that are
    # not in a table: any "Still open, <words> (n): ..." line is read the same way.
    for line in lines:
        m = BULK_LINE.match(line)
        if m:
            label = re.sub(r"\s+", " ", line.split("(")[0].strip())
            ids = bulk_ids(m.group(2))
            if len(ids) != int(m.group(1)):
                raise SystemExit(f"known-defects: the '{label}' line says {m.group(1)} ids and lists {len(ids)}")
            for i in ids:
                if i in rows:
                    raise SystemExit(f"known-defects: {i} is in a table and in the '{label}' line")
                rows[i] = {"severity": "Medium", "status": "open", "decision": label[:60]}
    # Cross-check against the document's own summary table.
    counted = {}
    for r in rows.values():
        counted.setdefault(r["severity"], [0, 0, 0, 0, 0])
        k = {"fixed": 0, "owner-decision": 1, "open": 2, "not-a-defect": 3}[r["status"]]
        counted[r["severity"]][k] += 1
        counted[r["severity"]][4] += 1
    for sev, printed in summary.items():
        if counted.get(sev) != printed:
            raise SystemExit(f"known-defects: the {sev} rows parsed as {counted.get(sev)} but the summary table prints {printed} (fixed, owner decision, still open, not a defect, total)")
    return rows


# ---------------------------------------------------------------- Track P re-derivation


def load_findings():
    spec = importlib.util.spec_from_file_location("trackp_findings", ROOT / "tools" / "verification" / "findings.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)  # defines functions only; its __main__ block does not run
    return module.collect()


def verdict_of(path):
    for line in path.read_text(encoding="utf-8").splitlines():
        if line.startswith("Verdict:"):
            return re.sub(r"\s+", " ", line[len("Verdict:"):]).strip()
    return None


def result_code(verdict):
    if verdict is None:
        return "none"
    v = verdict.upper()
    if v.startswith("MATCH WITH FINDINGS"):
        return "match-with-findings"
    if v.startswith("MATCH"):
        return "match"
    if v.startswith("VALUES TRANSFER CORRECTLY"):
        return "values-transfer"
    return "finding"


def rederivation_id(report_id):
    """chain.<x> reports review the pack template pack.<x>; every other report is named for its template id."""
    return "pack." + report_id[len("chain."):] if report_id.startswith("chain.") else report_id


def defect_id(item_id):
    """The prefix a known-defects id carries: a pack template's findings are filed under chain.<x>."""
    return "chain." + item_id[len("pack."):] if item_id.startswith("pack.") else item_id


# ---------------------------------------------------------------- worked examples

CHECKS_ROW = re.compile(r"^\|[^|]*\|[^|]*\|[^|]*\|\s*(\d+) of (\d+) PASS\s*\|\s*$")


def read_examples():
    """Template id -> worked example count; pack template id -> {examples, checks, checksPassed}."""
    per_template = {}
    packs = {}
    for readme in sorted(EXAMPLES.rglob("README.md")):
        rel = readme.relative_to(EXAMPLES).parts
        if len(rel) == 4:  # <discipline>/<template id>/<slug>/README.md
            per_template[rel[1]] = per_template.get(rel[1], 0) + 1
        elif len(rel) == 3 and rel[0] == "packs":  # packs/<slug>/README.md
            checks = passed = 0
            for line in readme.read_text(encoding="utf-8").splitlines():
                m = CHECKS_ROW.match(line)
                if m:
                    passed += int(m.group(1))
                    checks += int(m.group(2))
            packs["pack." + rel[1]] = {"examples": 1, "checks": checks, "checksPassed": passed}
    return per_template, packs


# ---------------------------------------------------------------- assemble


def build():
    defects = read_defects()
    findings = load_findings()
    per_template, packs = read_examples()
    results = json.loads(KAT_RESULTS.read_text(encoding="utf-8"))
    kat = {t["id"]: t for t in results["templates"]}
    pack_runs = {p["id"]: p for p in results.get("packs", [])}
    for t in kat.values():
        if not (0 <= t["katsPassed"] <= t["kats"]) or (t["katsPassed"] == t["kats"]) != (not t["failedKats"]):
            raise SystemExit(f"kat-results: {t['id']} says {t['katsPassed']} of {t['kats']} pass and lists {len(t['failedKats'])} failed cases")
    for p in pack_runs.values():
        if p["passed"] != (not p["failures"]):
            raise SystemExit(f"kat-results: {p['id']} says passed={p['passed']} and lists {len(p['failures'])} failures")

    items = {}

    def item(item_id):
        return items.setdefault(item_id, {})

    for template_id, count in per_template.items():
        item(template_id)["workedExamples"] = count
    for pack_id, data in packs.items():
        item(pack_id)["workedExamples"] = data["examples"]
        item(pack_id)["exampleChecks"] = data["checks"]
        item(pack_id)["exampleChecksPassed"] = data["checksPassed"]
    for template_id, t in kat.items():
        item(template_id)["kat"] = {k: t[k] for k in ("version", "kats", "katsPassed", "failedKats", "examplesPassed", "failedExamples")}
    for pack_id, p in pack_runs.items():
        item(pack_id)["packRun"] = {k: p[k] for k in ("steps", "bindings", "passed", "failures")}

    for report in sorted(REDERIVATION.glob("*.md")):
        report_id = report.stem
        if report_id.startswith("desktop."):
            continue  # the Desktop form is not a template; its defects are listed in the known-defects document only
        entry = item(rederivation_id(report_id))
        verdict = verdict_of(report)
        entry["rederivation"] = {"result": result_code(verdict), "verdict": (verdict or "")[:300], "findings": []}

    triaged = set()
    for f in findings:
        if f["calculator"].startswith("desktop."):
            continue
        finding_id = f["id"][3:] if f["id"].startswith("P1-") else f["id"]
        triage = defects.get(finding_id)
        # The known-defects document is the authority for Critical, High and Medium findings: its severity and its status win. A
        # finding it does not list was closed by Track P's own re-check ("Fixed" in the last status column of its report), or is
        # Low or Info, which the document does not triage.
        severity = triage["severity"] if triage else f["reviewed"]
        status = triage["status"] if triage else ("fixed" if f["status"] == "Fixed" else None)
        if triage:
            triaged.add(finding_id)
        elif status is None and severity in ("Critical", "High", "Medium"):
            raise SystemExit(f"{finding_id} is {severity} and not Fixed in its report, but the known-defects document does not list it")
        item(rederivation_id(f["calculator"]))["rederivation"]["findings"].append({"id": finding_id, "severity": severity, "defect": status})

    # Known-defect rows that no Track P report row names (a finding id the reports do not carry) are kept on their template, as
    # defect rows only, so an open defect is never lost for want of a matching report line.
    for finding_id, d in sorted(defects.items()):
        if finding_id in triaged or finding_id.startswith("desktop."):
            continue
        owner = finding_id.rsplit("-", 1)[0]
        target = "pack." + owner[len("chain."):] if owner.startswith("chain.") else owner
        if target not in items or "rederivation" not in items[target]:
            raise SystemExit(f"known-defects: {finding_id} names '{owner}', which has no re-derivation report")
        items[target]["rederivation"]["findings"].append({"id": finding_id, "severity": d["severity"], "defect": d["status"]})

    for entry in items.values():
        if "rederivation" in entry:
            entry["rederivation"]["findings"].sort(key=lambda x: x["id"])

    sources = {name: digest(files) for name, files in source_files().items()}
    totals = {}
    for d in defects.values():
        totals.setdefault(d["severity"], {})
        totals[d["severity"]][d["status"]] = totals[d["severity"]].get(d["status"], 0) + 1

    return {
        "schema": SCHEMA,
        "generatedBy": "scripts/generate-verification-snapshot.py",
        "sources": sources,
        "defectTotals": {k: dict(sorted(v.items())) for k, v in sorted(totals.items())},
        "items": dict(sorted(items.items())),
    }


def text(snapshot):
    return json.dumps(snapshot, indent=1, ensure_ascii=False, sort_keys=False) + "\n"


if __name__ == "__main__":
    wanted = text(build())
    if "--check" in sys.argv:
        current = OUT.read_bytes().decode("utf-8").replace("\r\n", "\n") if OUT.exists() else ""
        if current != wanted:
            print(f"{OUT.relative_to(ROOT)} is stale; run python3 scripts/generate-verification-snapshot.py", file=sys.stderr)
            sys.exit(1)
        print("verification snapshot is current")
    else:
        OUT.parent.mkdir(parents=True, exist_ok=True)
        OUT.write_bytes(wanted.encode("utf-8"))
        print(f"wrote {OUT.relative_to(ROOT)}: {len(wanted):,} bytes")
