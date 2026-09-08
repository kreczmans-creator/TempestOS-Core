# WP 9.2A — Engineering Calculations Workspace — Future Capability Assessment

## Purpose

Records candidate future capabilities this Work Package's own
implementation surfaced but deliberately did not build.

## `FCR-0051` — Concrete `ICalculationResult`/`IVerificationResult` Implementations

`TD-30` (this Work Package's own Technical Debt Assessment) discloses
that `EvidenceComposer`/`ITraceable.GetEvidenceAsync` resolves
structurally empty for every Calculation/Verification, platform-wide,
because neither Domain contract has ever been given a concrete
realisation. A future implementation would need a real, addressable
`IEngineeringObject` shape wrapping `CalculationRecord<TResult>` (which
is generic and has no `SubjectId`/fixed `Kind` today) — a genuine Domain
design question (does every `TResult` variant get one shared
non-generic wrapper type, keyed by `CalculationId`? does the Calculation
Framework itself grow this responsibility, or does `Tempest.Core.EngineeringDomain`?),
not a mechanical add. **Recommended once a real, demonstrated need for
composed, cross-discipline `IEvidence` (rather than direct relationship
reads, which already serve every scope item across four consecutive
Work Packages) exists** — building it speculatively now would guess at a
shape two more Engineering disciplines' own real Workspace integrations
might still inform.

## `FCR-0052` — Concrete Approval/Review Workflow

`TD-30` also discloses that `IApprovalGate`/`IApproval`/`IReview`/
`IReviewGate` have never been given a concrete realisation — this Work
Package's own "Calculation Approval State" scope item is satisfied by
`LifecycleState` alone (`Approved`/`Released` → "Yes"), not a governed,
queryable sign-off record naming who approved what, when, against which
evidence. A real implementation would give Engineering Calculations (and
every other discipline naming "Approval" in its own future scope) a
genuine governance record, not a status reading. **Recommended once a
real, demonstrated need for auditable approval provenance exists** —
today's `LifecycleState` reading already satisfies every KPI/facet this
Work Package's own controlling instruction names, and a governance
record's own shape (single approver? review panel? evidence bundle
required?) is a real design question best answered once a concrete
consumer states its own requirements.

## `FCR-0053` — Recalculate Resuming From a Previously-Executed Input

`TD-29` discloses that `RecalculateCalculationCommand` cannot offer a
parameterless "run it again" gesture, since `CalculationRecordDto<TResult>`
never retained the input that produced it. A future capability could
extend the Calculation Framework's own stored shape (a
`Tempest.Core.Calculations` change, deliberately out of this Work
Package's own "reuse, do not redesign execution" scope) to retain a
JSON-serialized input snapshot alongside the existing fields, or could
introduce a Workspace-layer-only "last input cache" keyed by target
object Id (never persisted, session-scoped only, avoiding any Domain
change at all). **Recommended once a real UI consumer of this Workspace
surface demonstrates the need** — no such consumer exists yet; every
invocation today is direct (tests) or would be through a future
presentation layer this Work Package's own scope does not build.

## Not Recommended: A Runtime Calculation Template Authoring UI

Considered directly during implementation: `ICalculationEngine.RegisterDefinition`'s
own XML documentation states it is "expected to be called only during
module initialisation," mirroring the Command Framework's own descriptor
registration model exactly. Building a runtime UI to author new
`ICalculationDefinition<TInput,TResult>` implementations would require
either a scripting/expression-evaluation engine (a substantial new
platform capability, well beyond "integrate the existing framework") or
code generation at runtime — neither is what this Work Package's own
controlling instruction asks for ("Reuse the existing Calculation
Framework. Do not redesign calculation execution"). **Not recommended**
— Templates remain a module-registration-time concept, exactly as
`WP 7.1D` originally designed.

## Not Recommended: A Dedicated `SafetyFactor`/`ApprovalRecord` Domain Type

Considered directly during implementation, for both `TD-30`'s own
Approval gap and the Safety Factor representation this Work Package's
own scope names. Both were judged better served, for now, by the
Calculation Framework's own already-general, open shapes
(`CalculationIntermediateResult` for Safety Factor; `LifecycleState` for
Approval) than by a new, narrowly-scoped Domain type this Work Package's
own controlling instruction ("no contract redesign") would forbid adding
unilaterally. **Not recommended** as a `WP 9.2A`-scoped addition — folded
into `FCR-0052`, above, as a question for whichever future Work Package
does take up governed Approval.

## Verdict

Three new candidates recorded (`FCR-0051`–`FCR-0053`); none built
speculatively ahead of genuine need; two further candidates considered
and explicitly not recommended, with reasoning recorded rather than
silently dropped.

## Related Documents

`docs/governance/Future Capability Register.md`; `ADR-0086`; `ADR-0087`;
`WP9.1A Future Capability Assessment.md` (`FCR-0048`–`FCR-0050`);
`WP9.2A Technical Debt Assessment.md` (`TD-29`, `TD-30`); `WP9.2A
Engineering Review Report.md`.
