# WP 9.5A — Manufacturing Workspace — Lessons Learned

## Purpose

Records what went well, what was harder than expected, and what a
future Work Package facing a similar situation should know going in.

## What Went Well

**The Kind-keyed Workspace extension model generalised a sixth time,
and this time proved something new: it can span more than one
discipline at once.** Every prior real-discipline Work Package built its
own facet/view providers for its own Kind(s) exclusively. This Work
Package is the first whose own scope named Domain Kinds
(`WorkInstruction`/`Inspection`) that are simultaneously subtypes of a
different, already-Workspace-integrated discipline's own base type
(`Document`/`VerificationActivity`) — and the existing providers, being
already generic over their own `Kind` parameter, worked unmodified the
moment they were constructed with a different Kind string. Recognising
this early — by reading `DocumentsPropertyFacetProvider`'s/
`VerificationActivityPropertyFacetProvider`'s own actual constructor
shape before assuming new Manufacturing-specific versions were needed —
avoided building two files of genuinely duplicate code.

**Two "already works, zero new code" claims about existing commands
were proven empirically, not just asserted.** `Mechanical.SetBomLineCommand`'s
and `Verification.RecordVerificationResultCommand`'s own documented
Kind-agnosticism had been *stated* by their own prior Work Packages'
documentation, but never actually exercised against a foreign Kind
before this one. Writing a dedicated test dispatching each against a
live `"ManufacturingOperation"`/`"Inspection"` — rather than trusting
the prior documentation's own claim — is a small extra step that turns
a plausible assertion into a verified fact, and is now itself a
reusable precedent for future disciplines claiming reuse of an existing
command.

## What Was Harder Than Expected

**Catching a cross-sample-module ordering defect during planning,
before any code existed, rather than after a test failure.** The
initial implementation plan (approved before implementation began)
listed `EngineeringVerificationWorkspaceSampleModule` as a fifth
cross-sample-module dependency, extending `WP 9.3A`'s own five-module
precedent by one — a natural-looking continuation of an established
pattern. Recalculating the two modules' own literal ordinal Id strings
(`tempest.samples.workspacemanufacturing` vs.
`tempest.samples.workspaceverification`) during implementation-prep
reasoning — not during coding, and not by a failing test — showed `m` <
`v`, meaning Manufacturing's own module would initialise *before*
Verification's, making the dependency unsatisfiable at DI-resolution
time had it been kept. Worth remembering directly: an established
pattern's own *shape* ("depend on the four/five prior sample modules")
does not automatically transfer when the *new* module's own literal Id
happens to sort differently from its predecessor's — the ordinal
position has to be rechecked for each new module individually, not
assumed to continue working because it worked for the last several
Work Packages in a row.

**Deciding whether `ManufacturingKpiCards` should reuse
`EngineeringCockpit.FormatCoverage`, once its own zero-denominator text
was found to be discipline-mismatched.** The instinctive choice was to
reuse the existing helper regardless — every prior discipline's own KPI
card set had, without exception. Inspecting its own zero-case text
(`"— (no requirements yet)"`, hardcoded, already inaccurately reused
twice by `WP 9.2A`/`WP 9.3A`) surfaced a small but real design fork: fix
the shared helper now (touching already-shipped `WP 9.2A`/`WP 9.3A`
code, outside this Work Package's own scope), reuse it anyway and
compound the existing inaccuracy a third time, or write a small, local,
accurate alternative and disclose the finding rather than silently
absorbing it. The third option was chosen (`TD-33`) — worth remembering:
finding a small, pre-existing inaccuracy in shared code a Work Package
is about to reuse is itself useful information, even when fixing it
outright is correctly judged out of scope.

## Process Observations

This Work Package's own controlling instruction closes with "await
Product Owner instruction before `WP 9.9.0` Release Preparation" —
skipping `WP 9.6A` through `WP 9.8A` entirely, a range never named or
reserved anywhere in this repository's own governance history. Unlike
`WP 9.3A`'s own disclosed controlling-instruction artifact (a genuine
copy-paste error referring to an already-complete Work Package), this
skip carries no internal contradiction — the Product Owner is free to
sequence Work Packages as instructed, and no prior record commits this
repository to filling that range before release wrap-up. Recorded here
as a plain observation, following the identical "disclose, do not
silently modify" discipline `WP 9.3A` applied to a genuine error,
applied here to a deliberate instruction that merely looks unusual.

Like `WP 9.3A`'s own Lessons Learned, this Work Package's own one
genuine new Technical Debt finding (`TD-33`) was found by direct code
inspection during design, not by a failing test — unlike `WP 9.3A`'s own
`TD-32`, which needed nine failing tests to surface. Worth recording as
a data point in its own right: reading a shared helper's own actual
implementation before reusing it a third time, rather than assuming its
prior two uses proved it fully general, caught this one before it ever
reached a test run at all.

## Recommendation for Future Work Packages

Before extending an established "constructor-inject the N prior sample
modules" pattern by one more module, recompute the new module's own
literal ordinal Id string against every dependency's own Id directly —
do not assume a pattern that has held for several consecutive Work
Packages will continue to hold for the next one without checking. Before
reusing a shared Cockpit/Workspace helper a third or later time, read
its own actual implementation for hardcoded, discipline-specific text or
assumptions the first two call sites happened not to expose — and when
one is found, disclose it as debt rather than either compounding it
silently or fixing shared, already-shipped code outside the current Work
Package's own scope.

## Related Documents

`WP9.5A Implementation Report.md`; `WP9.5A Technical Debt Assessment.md`
(`TD-33`); `ADR-0091`; `WP9.3A Lessons Learned.md`; `WP9.2A Lessons
Learned.md`; `WP9.4A Lessons Learned.md`.
