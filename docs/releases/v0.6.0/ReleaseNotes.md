# TempestOS v0.6.0 — "Platform Services"

**Release date:** Not yet applicable — no Work Package has started.
**Tag:** Not yet cut.
**Branch:** `feature/v0.6.0-platform-services`, cut from `main` at the
`v0.5.0` tag.

---

## Status

This document is a prepared skeleton, not a finished release note — it
exists so this release's own documentation structure is fixed before any
implementation begins, mirroring every prior release's own discipline
(`docs/releases/v0.4.0/Release Notes.md`, `docs/releases/v0.5.0/Release
Notes.md`). Each section below will be populated as its own Work Package
actually lands, exactly as `docs/releases/v0.4.0/CHANGELOG.md` and
`docs/releases/v0.5.0/CHANGELOG.md` were both written incrementally,
never in advance as predictions. **No Work Package in `docs/releases/
v0.6.0/WorkPackages.md` (`WP 6.0` through `WP 6.8`) has begun.**

## Overview

*To be written once this release's own scope is substantially delivered.*
Working premise, from `docs/releases/v0.6.0/WorkPackages.md`: `v0.6.0`
("Platform Services") is the first release since the Runtime Foundation
and the Platform Foundation to add genuinely new domain-facing
capability — Reporting, Permissions & Identity, Notifications, a REST
API, Settings, Audit, Licensing, and Export/Import — built on the stable
platform `v0.3.0`–`v0.5.0` established.

## Highlights

*To be written as Work Packages land.*

## Major Features

| Feature | What It Does | Work Package | Status |
|---|---|---|---|
| Reporting Framework | Structured, formatted output from platform data | `WP 6.0` | Not started |
| Permissions & Identity | Who is doing this, and are they allowed to | `WP 6.1` | Not started |
| Notification Framework | Tell a user, module, or external system something happened | `WP 6.2` | Not started |
| REST API | Invoke platform capability from outside the running process | `WP 6.3` | Not started |
| Settings Framework | User-changeable, runtime-mutable configuration | `WP 6.4` | Not started |
| Audit Framework | Durable, queryable record of who did what, when | `WP 6.5` | Not started |
| Licensing Framework | What capability is enabled, for whom, until when | `WP 6.6` | Not started |
| Export / Import | Portable, round-trippable platform data | `WP 6.7` | Not started |

## Engineering Improvements

*To be written as genuine findings occur — this section is never
written in advance.*

## Architecture

*To be written once this release's own ADRs exist.* Provisional count:
0 new ADRs as of this document's own preparation.

## Security

Three Work Packages in this release are named triggers in `docs/
security/Security Roadmap.md` for security design work that must precede
implementation, not follow it: `WP 6.1` (Permissions & Identity, item 6),
`WP 6.3` (REST API, item 7 — explicitly blocked on `WP 6.1`), and `WP 6.6`
(Licensing, item 8). `WP 6.8` (Platform Services Integration Review) will
confirm each was genuinely resolved, not deferred again, before this
release is considered candidate-ready. *Findings to be written as these
Work Packages land.*

## Documentation

*To be written as this release's own architecture documents and
governance register updates land.*

## Academy

*To be written as this release's own Work Package retrospectives land.*

## Governance

*To be written once `WP 6.8`'s own repository review completes.*

## Testing

| Metric | v0.5.0 | v0.6.0 (current) | Change |
|---|---|---|---|
| Automated tests | 552 | 552 | 0 (no Work Package has started) |
| Test failures | 0 | 0 | — |
| Build warnings | 0 | 0 | — |
| Build errors | 0 | 0 | — |

## Repository Metrics

*To be written once this release substantially progresses — see
`docs/governance/Quality/Repository Metrics Register.md` for the current,
authoritative snapshot in the meantime.*

## Known Limitations

*To be written as they are found or deliberately scoped out.*

## What's Next

Begin `WP 6.0` (Reporting Framework) — see `docs/releases/v0.6.0/
WorkPackages.md` for its own scope, and `PROJECT_STATUS.md` for current,
live status.

## Acknowledgements

*To be written at release close, mirroring every prior release's own
closing note.*
