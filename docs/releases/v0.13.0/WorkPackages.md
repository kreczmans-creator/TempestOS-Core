# TempestOS v0.13.0 — Work Packages

## Status

**In progress.** `v0.13.0` — "Trust & Deployment Hardening" — branch
`feature/v0.13.0`, cut from the released `v0.12.0` tag (`13a6ce3`), not
from any working branch, per this release's own explicit `WP 13.0.0`
Branch Establishment instruction — confirmed directly, `git merge-base
feature/v0.13.0 v0.12.0` resolves to `13a6ce3` exactly, the identical
commit `v0.12.0` itself points to. This document is created now, per
this project's own established convention ("each release's own
`WorkPackages.md` is created when that release's branch is cut,"
`WP11.4B Release Process Correction Report.md` §10), seeded from
`WP11.0B Architecture Roadmap.md` §3's own predicted `v0.13.0` table.

**This release is conditional, and the condition is now confirmed
triggered.** `WP11.0B Architecture Roadmap.md` §3 is explicit:
`v0.13.0` "only enters the plan if `WP 11.2A` / the Product Owner
commits `v1.0` scope to third-party plugins and/or REST deployment
beyond a trusted local network. Otherwise… this release is skipped
entirely — the plan collapses to `v0.11.0` → `v0.12.0` → `v1.0.0`."
Directly confirmed, at this Work Package's own commissioning: neither
trigger had fired as of `v0.12.0`'s own close (`src/Plugins/` remains
empty — `WP 12.9.1`'s own tracked marker `README.md` — and no
`v0.12.0` Work Package touched REST API scope). The Product Owner has
now explicitly made that commitment, confirmed directly, commissioning
this branch — recorded here as the trigger event itself, not assumed
or inferred from the branch's mere existence.

**Predicted scope**, per `WP11.0B Architecture Roadmap.md` §3: Work
Package, Scope, and Type columns reproduced verbatim; Status updated
from that table's own `Conditional` to `Not started` now that the
condition has fired — no `v0.13.0` Work Package has yet begun:

| Work Package | Scope | Type | Status |
|---|---|---|---|
| `WP 13.0A` | Plugin & Registration Trust Isolation Architecture (`A-3`/`FCR-0001`) | Architecture | Not started |
| `WP 13.0B` | Plugin & Registration Trust Isolation Implementation | Implementation | Not started |
| `WP 13.1A` | REST API Authentication & TLS Architecture (`A-4`/`FCR-0003`/`FCR-0004`) | Architecture | Not started |
| `WP 13.1B` | REST API Authentication & TLS Implementation | Implementation | Not started |
| `WP 13.9.0` | `v0.13.0` Release Preparation & Engineering Sign-Off | Verification only | Not started |

**Not re-derived or re-scoped here** — this document records the
roadmap's own predicted plan at the moment this branch was cut; the
first substantive Work Package (`WP 13.0A`, expected) is where a real,
independent architectural investigation of `A-3`/`FCR-0001` belongs,
mirroring exactly how `WP 12.0A` opened `v0.12.0`'s own first roadmap-
predicted item rather than re-litigating it here.

## Branch Discipline

Per this release's own explicit `WP 13.0.0` instruction — stricter
than every prior release's own convention (`v0.11.0`/`v0.12.0` each
used multiple parallel `feature/vX.Y.0-*` branches, one per Work
Package or Work Package pair):

- `feature/v0.13.0` is the **sole** integration branch for every
  `v0.13.0` Work Package. No additional feature branches without
  explicit, separate authorisation.
- Every Work Package commits **directly** to `feature/v0.13.0`.
- **Never rebase. Never squash.** History stays linear through
  sequential commits on this one branch.
- Merge commits only when integrating `feature/v0.13.0` back to `main`
  at this release's own close (mirroring Engineering Governance §7's
  own merge-commit-only rule, applied here one level earlier, to the
  Work-Package-to-branch relationship as well as the branch-to-`main`
  one).

## Work Packages

*(No Work Package has started. This table will grow one row per
completed Work Package, exactly as every prior release's own
`WorkPackages.md` does — see `docs/releases/v0.12.0/WorkPackages.md`
for the established format and density convention.)*

## Related Documents

`docs/releases/v0.11.0/WP11.0B Architecture Roadmap.md` §3 (this
release's own originally-predicted scope, now confirmed triggered);
`docs/releases/v0.12.0/WorkPackages.md` (the immediately preceding
release, and this document's own format precedent); `PROJECT_STATUS.md`;
`docs/governance/Future Capability Register.md` (`FCR-0001`–`FCR-0004`).
