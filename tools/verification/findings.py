"""Collect every P1 finding with its original and reviewed severity.

Verification aid only. Usage: python3 tools/verification/findings.py [--min High] [--open] [--csv]
"""
import csv
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
REPORTS = ROOT / "docs" / "verification" / "rederivation"
RANK = {"Critical": 0, "High": 1, "Medium": 2, "Low": 3, "Info": 4, "Rejected": 9}


def cells(line):
    body = line.strip()[1:-1] if line.strip().endswith("|") else line.strip()[1:]
    return [c.strip().replace("\\|", "|") for c in re.split(r"(?<!\\)\|", body)]


def level(text):
    """The first severity word in a cell; 'Rejected' wins when present."""
    t = text.replace("*", "")
    if t.lower().startswith("rejected") or "reject" in t.lower().split(":")[0]:
        return "Rejected"
    found = [(t.find(k), k) for k in RANK if k != "Rejected" and k in t]
    # A cell like "High → Critical" means the reviewed value is the last one named.
    if "→" in t:
        tail = t.split("→")[-1]
        found = [(tail.find(k), k) for k in RANK if k != "Rejected" and k in tail] or found
    return min(found)[1] if found else t[:20]


def collect():
    rows = []
    for path in sorted(REPORTS.glob("*.md")):
        header = None
        for line in path.read_text(encoding="utf-8").splitlines():
            if line.startswith("| ID "):
                header = cells(line)
                continue
            if header and line.startswith("| P1-"):
                c = dict(zip(header, cells(line)))
                original = level(c.get("Severity", ""))
                reviewed = level(c["Reviewed"]) if c.get("Reviewed") else original
                if reviewed not in RANK:  # not yet reviewed ("—"): keep the first-pass severity
                    reviewed = original
                # The last "Status @ <sha>" column is the latest re-check.
                status = [v for k, v in c.items() if k.startswith("Status")]
                status = status[-1] if status else "Open"
                status = "Fixed" if status.lower().startswith("fixed") else ("Open" if not status else status)
                rows.append({
                    "status": status,
                    "calculator": path.stem,
                    "id": c["ID"],
                    "original": original,
                    "reviewed": reviewed,
                    "finding": re.sub(r"\s+", " ", c.get("Finding", ""))[:300],
                })
            elif header and not line.startswith("|"):
                header = None
    return rows


if __name__ == "__main__":
    data = collect()
    floor = sys.argv[sys.argv.index("--min") + 1] if "--min" in sys.argv else "Info"
    data = [r for r in data if RANK.get(r["reviewed"], 5) <= RANK[floor]]
    if "--open" in sys.argv:
        data = [r for r in data if r["status"] != "Fixed"]
    data.sort(key=lambda r: (RANK.get(r["reviewed"], 5), r["calculator"], r["id"]))
    if "--csv" in sys.argv:
        w = csv.DictWriter(sys.stdout, fieldnames=list(data[0]))
        w.writeheader()
        w.writerows(data)
    else:
        for r in data:
            print(f'{r["reviewed"]:9} {r["original"]:9} {r["status"][:12]:12} {r["id"]:36} {r["finding"][:120]}')
        counts = {}
        for r in data:
            counts[r["reviewed"]] = counts.get(r["reviewed"], 0) + 1
        print(counts)
