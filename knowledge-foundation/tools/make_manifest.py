#!/usr/bin/env python3
"""Regenerate MANIFEST.json from the package tree.

Run at every release, before tools/validate.py. The versioned manifests
(MANIFEST-vX.Y.Z.json) are historical records and are never regenerated.
"""
import json, os, datetime

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

def main() -> None:
    files = []
    for r, dirs, names in os.walk(ROOT):
        dirs[:] = [d for d in dirs if not d.startswith(".")]
        for n in names:
            if n.startswith("."):
                continue
            files.append(os.path.relpath(os.path.join(r, n), ROOT).replace(os.sep, "/"))
    files.sort()
    version = open(os.path.join(ROOT, "VERSION")).read().strip()
    manifest = {
        "package": "TempestOS Engineering Knowledge Foundation",
        "version": version,
        "generated": datetime.date.today().isoformat(),
        "generator": "tools/make_manifest.py — regenerated at every release, "
                     "reconciled by tools/validate.py",
        "file_count": len(files),
        "files": files,
    }
    with open(os.path.join(ROOT, "MANIFEST.json"), "w") as fh:
        json.dump(manifest, fh, indent=1)
    print(f"MANIFEST.json regenerated: v{version}, {len(files)} entries")

if __name__ == "__main__":
    main()
