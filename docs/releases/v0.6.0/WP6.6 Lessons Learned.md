# WP 6.6 — Licensing Framework — Lessons Learned

## 1. A Failure Classification Inherited From One Failure Model Does Not Automatically Transfer Correctly to a New Kind of Failure

`ADR-0013`'s Host-fatal classification was designed around technical
faults — a corrupt configuration entry, a module discovery defect.
Applying it verbatim to Licensing, a business/entitlement condition,
would have silently conflated "no one has ever supplied a license"
(this platform's own normal, unrestricted-but-uncapable default state)
with "a license was supplied and it is broken" (a genuine, actionable
operator error). Both look identical if you only ask "is this valid or
invalid" without asking what each concrete non-valid state actually
means. **Lesson: reusing an existing failure classification for a new
category of failure requires re-examining what "invalid" concretely
means in the new context, not just applying the label mechanically.**

## 2. Prove a Behavioural Default Is Safe by Running the Suite That Depends On It, Not by Reasoning About It

It would have been easy to argue "obviously a missing license file
shouldn't be Host-fatal, that would be silly" and move on. Instead, the
actual claim — that this resolution regresses none of this platform's
own existing tests — was verified by running all 24 pre-existing
`TempestHost`-building test files, unmodified, after implementing the
change, and confirming zero failures. **Lesson: when a design decision
has an implicit claim about backward compatibility, that claim is a
testable fact, not a rhetorical one — test it.**

## 3. A Genuinely Novel Timing Constraint Is Usually Not Actually Novel

`ILicenseValidator` needing to exist before the DI container felt, at
first glance, like a new kind of problem this codebase had never faced.
It was not: `PlatformVersionProvider` already established the
"deliberately a leaf, no constructor dependencies" pattern for an
earlier timing constraint, and `IPlatformVersionProvider`/
`IDiagnosticsProvider` already established the "Composition-Root-
constructed, `AddInstance`-registered" pattern for a service the
container itself cannot build. Combining the two, rather than
inventing a third mechanism, resolved Licensing's own construction
timing with zero new infrastructure. **Lesson: before treating a
timing or lifecycle constraint as novel, check whether this codebase's
own history already solved the same *shape* of problem, even under a
different name.**

## 4. "Expose Capability Only" Is a Discipline That Has to Be Checked, Not Just Stated

The brief's own instruction — "shall expose capability only... shall
not implement commercial policy" — is easy to state and easy to
violate quietly (a tempting shortcut: "just add a `PricingTier` enum
while we're at it"). Discipline here meant `ILicenseProvider` ends at
`HasCapability(string)` and nothing more; no tier concept, no pricing
concept, no subscription concept exists anywhere in
`Tempest.Core.Licensing`, confirmed by direct inspection, not merely by
intention. **Lesson: a scope boundary stated in a brief is a claim
until it's verified against the actual shipped code — the same
discipline this session has applied to every prior Work Package's own
dependency and layering claims.**

## 5. This Was the Last Feature Work Package — A Different Kind of "Done" Than Every Prior One

Every prior Work Package this release closed with "the next Work
Package may proceed." This one closes with "no further feature Work
Package remains — only certification." That changes what "done" means
for its own repository review: rather than merely checking this Work
Package's own scope, it is worth explicitly confirming that no register
this release depends on has been left in a state that would surprise
`WP 6.8`'s own closing audit. **Lesson: the last Work Package before a
closing-review milestone should explicitly check whether it is leaving
a clean, fully-disclosed starting point for that review, not just a
clean starting point for "whatever comes next."**

## Related Documents

`WP6.6 Implementation Report.md`; `WP6.6 Engineering Review Report.md`;
`WP6.6 Platform Integration Demonstration.md`; `WP6.6 Platform Impact
Assessment.md`; `WP6.6 Technical Debt Assessment.md`; `WP6.6 Future
Capability Recommendations.md`; `docs/academy/03 Work
Packages/WP6.6-licensing-framework-implementation.md`; `ADR-0013`;
`ADR-0023`; `ADR-0050`.
