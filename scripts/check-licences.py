#!/usr/bin/env python3
"""Offline, deterministic NuGet licence check (white-team finding W-B-01).

Collects every package ID from every packages.lock.json under the repository
(direct and transitive; first-party "Project" entries are skipped) and checks
each against scripts/licence-allowlist.json:

  * the ID must be listed (otherwise: add it, with its licence read from the
    package's nuspec, in the same commit that adds the PackageReference);
  * its recorded SPDX licence expression must be built only from the allowed
    set (MIT, BSD-2-Clause, BSD-3-Clause, Apache-2.0). "A OR B" passes if
    either side is allowed, "A AND B" needs both;
  * a package outside the allowed set passes only if it has an entry in the
    allow-list's "exceptions" array with the same licence and a reason. Every
    exception is printed as a WARNING on every run.

No network, no build, no NuGet cache: it reads only files in the repository.
Exit status: 0 = pass, 1 = violation, 2 = the check itself could not run.

Usage: python3 scripts/check-licences.py [--root DIR] [--allowlist FILE]
"""
import argparse
import json
import os
import re
import sys

SKIP_DIRS = {"node_modules", "bin", "obj"}


def find_locks(root):
    locks = []
    for dirpath, dirnames, filenames in os.walk(root):
        # Skip hidden dirs (.git, .claude worktrees, ...) and build output.
        dirnames[:] = sorted(d for d in dirnames if not d.startswith(".") and d not in SKIP_DIRS)
        if "packages.lock.json" in filenames:
            locks.append(os.path.join(dirpath, "packages.lock.json"))
    return sorted(locks)


def collect_ids(locks, root):
    """Return {lowercase id: (display id, sorted list of lock files)}."""
    found = {}
    for path in locks:
        rel = os.path.relpath(path, root).replace(os.sep, "/")
        with open(path, encoding="utf-8-sig") as fh:
            doc = json.load(fh)
        for _tfm, entries in doc.get("dependencies", {}).items():
            for pkg_id, info in entries.items():
                if info.get("type") == "Project":
                    continue
                display, files = found.setdefault(pkg_id.lower(), (pkg_id, set()))
                files.add(rel)
    return {k: (d, sorted(f)) for k, (d, f) in found.items()}


_TOKEN = re.compile(r"\(|\)|[^\s()]+")


def expression_ok(expr, allowed):
    """Evaluate an SPDX expression: every leaf must be in `allowed` (OR: any side)."""
    tokens = _TOKEN.findall(expr)
    pos = 0

    def parse_or():
        nonlocal pos
        value = parse_and()
        while pos < len(tokens) and tokens[pos].upper() == "OR":
            pos += 1
            right = parse_and()
            value = value or right
        return value

    def parse_and():
        nonlocal pos
        value = parse_atom()
        while pos < len(tokens) and tokens[pos].upper() == "AND":
            pos += 1
            right = parse_atom()
            value = value and right
        return value

    def parse_atom():
        nonlocal pos
        if pos >= len(tokens):
            raise ValueError("unexpected end of expression")
        tok = tokens[pos]
        pos += 1
        if tok == "(":
            value = parse_or()
            if pos >= len(tokens) or tokens[pos] != ")":
                raise ValueError("missing ')'")
            pos += 1
            return value
        if tok == ")" or tok.upper() in ("AND", "OR"):
            raise ValueError("unexpected '%s'" % tok)
        return tok in allowed  # case-sensitive: SPDX ids are

    value = parse_or()
    if pos != len(tokens):
        raise ValueError("unexpected '%s'" % tokens[pos])
    return value


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--root", default=os.path.dirname(here), help="repository root (default: parent of scripts/)")
    ap.add_argument("--allowlist", default=os.path.join(here, "licence-allowlist.json"))
    args = ap.parse_args()

    try:
        with open(args.allowlist, encoding="utf-8") as fh:
            allow = json.load(fh)
        allowed = set(allow["allowedLicences"])
        packages = {k.lower(): (k, v) for k, v in allow["packages"].items()}
        exceptions = {e["id"].lower(): e for e in allow.get("exceptions", [])}
    except (OSError, ValueError, KeyError) as exc:
        print("::error::Cannot read the licence allow-list %s: %s" % (args.allowlist, exc))
        return 2

    locks = find_locks(args.root)
    if not locks:
        print("::error::No packages.lock.json found under %s; refusing to pass an empty check." % args.root)
        return 2
    ids = collect_ids(locks, args.root)

    problems, warnings = [], []
    for key in sorted(ids):
        display, files = ids[key]
        where = files[0] + (" (+%d more)" % (len(files) - 1) if len(files) > 1 else "")
        if key not in packages:
            problems.append("%s is not in %s (used by %s). Read its licence from the package nuspec and add it." % (display, os.path.basename(args.allowlist), where))
            continue
        licence = packages[key][1]
        try:
            ok = expression_ok(licence, allowed)
        except ValueError as exc:
            problems.append("%s has an unparseable licence expression '%s' (%s)." % (display, licence, exc))
            continue
        if ok:
            continue
        exc_entry = exceptions.get(key)
        if exc_entry and exc_entry.get("licence") == licence and exc_entry.get("reason"):
            warnings.append("%s is %s (not allowed) but has an explicit exception: %s" % (display, licence, exc_entry["reason"]))
        else:
            problems.append("%s has licence '%s', outside the allowed set {%s} and with no matching exception (used by %s)." % (display, licence, ", ".join(sorted(allowed)), where))

    stale = sorted(v[0] for k, v in packages.items() if k not in ids)
    stale_exc = sorted(e["id"] for k, e in exceptions.items() if k not in ids)
    for name in stale:
        warnings.append("%s is in the allow-list but in no packages.lock.json (stale entry; safe to remove)." % name)
    for name in stale_exc:
        warnings.append("Exception for %s matches no package in any packages.lock.json (stale; remove it)." % name)

    print("Licence check: %d lock file(s), %d distinct NuGet package(s); allowed: %s" % (len(locks), len(ids), ", ".join(sorted(allowed))))
    for w in warnings:
        print("::warning::" + w)
    for p in problems:
        print("::error::" + p)
    if problems:
        print("FAILED: %d package(s) violate the licence policy." % len(problems))
        return 1
    print("PASSED: every package is allow-listed with an allowed licence (%d exception(s) flagged above)." % len([w for w in warnings if "explicit exception" in w]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
