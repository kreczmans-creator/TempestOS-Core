# WP 7.0A — Recommended v0.7 Candidate Work Packages

## Status

Complete. **This is a recommendation, not an approval.** No Work
Package number below is authorised for implementation. Per this Work
Package's own controlling instruction, defining implementation Work
Packages beyond recommendation is out of scope — actual scoping still
requires its own Architecture, Planning, and Contract Review phase,
mirroring `v0.6.0`'s own precedent, and Engineering Review of `WP 7.0A`
itself before `WP 7.0B` begins.

## Basis

`WP7.0A Future Capability Summary.md` identifies five capabilities as
Ready, Medium-or-higher Strategic Value, and not gated on an
unfulfilled external trigger: `FCR-0001`, `FCR-0003`, `FCR-0004`,
`FCR-0005`, `FCR-0006`. All five already appear as candidates `C1`–`C4`
in `docs/releases/v0.7.0/WorkPackages.md`, produced immediately after
`v0.6.0`'s own release. This document formalises that same set as the
recommended `v0.7` basis, having now independently re-derived it via the
full 28-entry register rather than only the four items `WP 6.8`'s own
retrospective happened to name.

## Recommended Grouping

Three candidate Work Packages are recommended, grouped by shared
subject matter and shared risk profile — not four, since `FCR-0003` and
`FCR-0004` share one deployment-scenario trigger and one Work Package's
own natural scope:

### Candidate WP A — Plugin & Registration Trust Isolation

**Capabilities:** `FCR-0001` (`C3` in `WorkPackages.md`).

**Rationale.** The enforcement mechanism (`IPermissionEvaluator`,
`ADR-0044`) already exists; this Work Package would apply it to three
already-identified call sites. Self-contained, no dependency on any
other candidate.

**Caution.** `FCR-0001`'s own register entry gates this on a real
trigger (third-party plugin support) per Security Principle 7 — this
candidate should not proceed unless that trigger, or an equivalent
genuine need, is confirmed at Architecture/Planning time, not assumed
here.

### Candidate WP B — REST API Authentication & Transport Security

**Capabilities:** `FCR-0003`, `FCR-0004` (`C4` in `WorkPackages.md`).

**Rationale.** Both share the identical trigger (a deployment scenario
beyond a trusted local network) and are natural to design and implement
together — TLS configuration is close to meaningless without a real
authentication mechanism to protect, and vice versa.

**Caution.** Per `Security Roadmap.md` item 6/7's own standing
instruction, this should be its own dedicated architecture Work Package
with an explicit threat-model addendum, exactly as `WP 6.1` and `WP 6.3`
each were — not folded into an unrelated feature Work Package.

### Candidate WP C — Engineering Foundation Governance Closeout

**Capabilities:** `FCR-0005`, `FCR-0006` (`C1`/`C2` in
`WorkPackages.md`).

**Rationale.** Both are self-contained, low-risk, architecture/tooling
corrections with no dependency on each other or on Candidates A/B — a
natural single Work Package closing out the specific findings `WP 6.8`
disclosed, mirroring how `WP 5.2` bundled several small, related
architecture corrections into one Work Package.

## Sequencing Suggestion

No hard dependency exists between Candidates A, B, and C — they could
proceed in any order, or in parallel, once each is individually
approved. Candidate C carries the lowest risk and narrowest scope, and
would be a reasonable first Work Package of the Engineering Foundation
phase's own implementation stage if a single starting point is wanted;
Candidates A and B each carry a real "is the trigger genuinely met"
question that Architecture/Planning should resolve explicitly before
either begins, per each candidate's own Caution above.

## What This Document Does Not Do

It does not assign a Work Package number (`WP 7.1`, `WP 7.0B`, or
otherwise) to any candidate above — Work Package numbering is an
Architecture/Planning-phase decision, not a recommendation-phase one.
It does not authorise implementation of any candidate. It does not
expand any candidate's own scope beyond what `Future Capability
Register.md` already records for `FCR-0001`, `FCR-0003`, `FCR-0004`,
`FCR-0005`, and `FCR-0006`.

## Related Documents

`docs/governance/Future Capability Register.md`; `WP7.0A Future
Capability Summary.md`; `docs/releases/v0.7.0/WorkPackages.md`.
