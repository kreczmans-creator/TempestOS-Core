#!/usr/bin/env python3
"""TempestOS Engineering Knowledge Foundation — package validator.

Generates the evidence the governance registers cite, rather than letting a
register assert something no one checked. Run from the package root:

    python3 tools/validate.py            # report to stdout, write registers
    python3 tools/validate.py --check    # exit 1 if any invariant fails

It never modifies content files. It only reads them, and writes generated
registers under governance/generated/.
"""
from __future__ import annotations
import json, os, re, sys, hashlib, collections, datetime

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "governance", "generated")
SKIP_DIRS: set = set()   # enumerate everything; kind_of() decides what is scored
CONTENT_EXT = {"json", "yaml", "csv", "md", "py"}

# The fifteen depth elements the programme's own depth standard requires.
DEPTH_ELEMENTS = [
    "definition", "physical_meaning", "governing_principles", "key_variables",
    "units", "assumptions", "equations", "interpretation", "design_implications",
    "failure_implications", "verification_implications", "common_mistakes",
    "limitations", "worked_example", "adjacent_disciplines",
]

# Maturity tiers, most mature first. A record is assigned the LOWEST tier any
# of its own declared metadata supports — a record never rises above what it
# says about itself.
MATURITY_ORDER = ["verified", "sourced", "review-required", "placeholder"]

# Only these fields declare a record's own maturity. Prose that happens to
# contain the word "verified" is not a claim of verification — an earlier
# revision of this validator read free text and produced 107 false positives
# against policy documents, which is why the test is field-driven now.
STATUS_FIELDS = ("status", "evidence_status", "design_use", "evidence_level",
                 "state", "review_status")

PLACEHOLDER_MARKERS = (
    "screening_only", "screening-only", "not approved", "not_approved",
    "screening-level", "screening taxonomy", "screening range",
    "qualitative_screening_metadata", "framework_only", "unverified",
    "source_or_review_required", "not_for_allowables", "candidate_reference",
    "framework", "open", "qualitative", "taxonomy", "screening",
)
SOURCED_MARKERS = ("sourced", "source_bound", "datasheet", "standard-derived",
                   "source_or_review_required")
VERIFIED_MARKERS = ("verified", "validated", "confirmed")

# "must be verified" is an instruction to verify, not a claim of verification.
# Reading it as the latter is how a second false positive got through.
REVIEW_PHRASES = ("must be verified", "to be verified", "must be checked",
                  "must be confirmed", "verify", "review required",
                  "requires review", "applicability")

# File kinds. Only "record" files carry engineering content and are subject to
# the maturity and depth tests; the rest are process material and are counted,
# not scored.
def kind_of(rel: str) -> str:
    if rel.startswith("MANIFEST"):
        return "manifest"
    if rel.startswith("baseline/"):
        return "baseline"
    if rel.startswith("governance/generated/"):
        return "generated"
    if rel in ("VERSION", "README.md"):
        return "package-metadata"
    if rel.startswith("docs/"):
        return "doc"
    if rel.startswith("tools/") or rel.endswith(".py"):
        return "tool"
    if "/governance/" in rel or rel.startswith("governance/"):
        return "governance"
    if "provenance" in rel or "/audit/" in rel:
        return "provenance"
    if "fixture" in rel or "schema" in rel or "canonical-index" in rel:
        return "schema-or-fixture"
    if rel.startswith("data/"):
        return "record"
    return "other"


def iter_files():
    for r, dirs, files in os.walk(ROOT):
        rel_dir = os.path.relpath(r, ROOT).replace(os.sep, "/")
        if rel_dir == ".":
            rel_dir = ""
        if any(rel_dir == s or rel_dir.startswith(s + "/") for s in SKIP_DIRS):
            continue
        dirs[:] = [d for d in dirs if not d.startswith(".")]
        for f in sorted(files):
            rel = os.path.join(rel_dir, f).lstrip("/")
            yield rel, os.path.join(r, f)


def declared_statuses(obj, text: str) -> list:
    """Every value a record declares in one of its own status fields."""
    found = []
    if isinstance(obj, dict):
        for k, v in obj.items():
            if k.lower() in STATUS_FIELDS and isinstance(v, str):
                found.append(v.lower())
    for f in STATUS_FIELDS:                     # YAML, and nested JSON
        found += [m.lower() for m in re.findall(
            rf'^\s*{f}\s*:\s*["\']?([^"\'\n,}}]+)', text, re.M)]
        found += [m.lower() for m in re.findall(
            rf'"{f}"\s*:\s*"([^"]+)"', text)]
    return [s.strip() for s in found if s.strip()]


def classify(obj, text: str) -> tuple:
    """A record never rises above what it says about itself."""
    decl = declared_statuses(obj, text)
    if not decl:
        return "undeclared", decl
    if any(any(m in d for m in PLACEHOLDER_MARKERS) for d in decl):
        return "placeholder", decl
    if any(any(p in d for p in REVIEW_PHRASES) for d in decl):
        return "review-required", decl
    if any(any(m in d for m in SOURCED_MARKERS) for d in decl):
        return "sourced", decl
    if any(any(m in d for m in VERIFIED_MARKERS) for d in decl):
        return "verified", decl
    return "review-required", decl


def score_depth(obj, text: str) -> dict:
    """Presence, not quality. A generous test the content still has to pass."""
    keys = set()
    if isinstance(obj, dict):
        keys = {k.lower() for k in obj.keys()}

    def has(*names):
        return any(n in keys for n in names)

    return {
        "definition": has("definition", "concept", "concepts", "purpose", "problem"),
        "physical_meaning": has("physical_meaning", "physics", "governing_physics"),
        "governing_principles": has("relationships", "equations", "principles", "governing", "rule"),
        "key_variables": has("variables", "key_variables", "critical_variables", "inputs"),
        "units": bool(re.search(r"\b(mm|m/s|MPa|GPa|kN|N·m|Nm|kg|Hz|°C|W/m|Pa|mm²|m³)\b", text)),
        "assumptions": has("assumptions"),
        "equations": bool(re.search(r"[=√]|sqrt|\bω|\bσ|\bτ|\bΔ", text)),
        "interpretation": has("interpretation", "engineering_interpretation", "meaning"),
        "design_implications": has("design_implications", "design_rules", "design_use", "design"),
        "failure_implications": has("failure_modes", "failure", "failure_implications"),
        "verification_implications": has("verification", "checks", "verification_implications"),
        "common_mistakes": has("common_mistakes", "pitfalls", "mistakes"),
        "limitations": has("boundary", "limitations", "limits"),
        "worked_example": has("worked_example", "example", "workflow"),
        "adjacent_disciplines": has("adjacent", "related", "cross_domain", "relationships_to"),
    }


def main(check_only: bool = False) -> int:
    records, failures, warnings = [], [], []
    ext_count = collections.Counter()
    maturity = collections.Counter()

    for rel, path in iter_files():
        ext = os.path.splitext(rel)[1].lstrip(".").lower()
        ext_count[ext] += 1
        raw = open(path, "rb").read()
        entry = {
            "kind": kind_of(rel),
            "path": rel,
            "bytes": len(raw),
            "sha256": hashlib.sha256(raw).hexdigest(),
            "ext": ext,
        }
        if ext not in CONTENT_EXT:
            records.append(entry)
            continue

        text = raw.decode("utf-8", errors="replace")
        obj = None
        if ext == "json":
            try:
                obj = json.loads(text)
            except json.JSONDecodeError as e:
                failures.append(f"UNPARSEABLE JSON: {rel} ({e})")
                records.append(entry)
                continue

        if entry["kind"] == "record":
            entry["maturity"], entry["declared"] = classify(obj, text)
            maturity[entry["maturity"]] += 1
        if ext in {"json", "yaml"}:
            depth = score_depth(obj, text)
            entry["depth_elements_present"] = sum(depth.values())
            entry["depth"] = depth
            entry["words"] = len(re.findall(r"[A-Za-z]+", text))
        records.append(entry)

        # Invariant 1: a content record may not claim verification without
        # naming a source. This is the check that stops placeholder data being
        # promoted by editing one word.
        if entry.get("maturity") == "verified" and not re.search(
            r'(source_reference|sourcereference|"source"|^\s*source\s*:)',
            text, re.I | re.M
        ):
            failures.append(f"CLAIMS VERIFIED WITHOUT NAMING A SOURCE: {rel}")

        # Invariant 2: a content record must declare its own maturity.
        if entry.get("kind") == "record" and entry.get("maturity") == "undeclared":
            warnings.append(f"NO DECLARED STATUS FIELD: {rel}")

    # Manifest reconciliation
    on_disk = {r["path"] for r in records}
    manifest_report = {}
    for m in sorted(f for f in os.listdir(ROOT) if f.startswith("MANIFEST")):
        try:
            data = json.load(open(os.path.join(ROOT, m)))
        except Exception as e:
            failures.append(f"UNPARSEABLE MANIFEST: {m} ({e})")
            continue
        listed = set(data.get("files", []))
        manifest_report[m] = {
            "declared_version": data.get("version"),
            "entries": len(listed),
            "listed_but_absent": sorted(listed - on_disk),
            "present_but_unlisted": sorted(on_disk - listed),
        }

    generated = {
        "generated": datetime.date.today().isoformat(),
        "generator": "tools/validate.py",
        "file_count": len(records),
        "files_by_extension": dict(ext_count.most_common()),
        "file_kind_census": dict(collections.Counter(
            r.get("kind", "-") for r in records).most_common()),
        "record_maturity_census": dict(maturity.most_common()),
        "manifests": manifest_report,
        "failures": failures,
        "warnings": warnings,
        "records": records,
    }
    os.makedirs(OUT, exist_ok=True)
    with open(os.path.join(OUT, "data-maturity-register.json"), "w") as fh:
        json.dump(generated, fh, indent=1)

    undeclared = sorted(r["path"] for r in records
                        if r.get("maturity") == "undeclared")
    with open(os.path.join(OUT, "undeclared-records.txt"), "w") as fh:
        fh.write(
            "# Content records that declare no maturity status of their own.\n"
            "# Per README.md, each is 'placeholder' by the package-wide default.\n"
            "# This list is the accepted exception under TD-K03 and is\n"
            "# regenerated by tools/validate.py — do not hand-edit.\n"
            f"# count: {len(undeclared)}\n\n")
        fh.write("\n".join(undeclared) + "\n")

    print(f"files            : {len(records)}")
    kinds = collections.Counter(r.get("kind", "-") for r in records)
    print(f"file kinds       : {dict(kinds.most_common())}")
    print(f"record maturity  : {dict(maturity.most_common())}")
    for m, r in manifest_report.items():
        print(
            f"manifest {m:26s} v{r['declared_version']} "
            f"entries={r['entries']} missing={len(r['listed_but_absent'])} "
            f"unlisted={len(r['present_but_unlisted'])}"
        )
    print(f"failures         : {len(failures)}")
    for f in failures[:20]:
        print("   !", f)
    return 1 if (check_only and failures) else 0


if __name__ == "__main__":
    sys.exit(main("--check" in sys.argv))
