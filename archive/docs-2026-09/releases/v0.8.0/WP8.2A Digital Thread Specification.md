# WP 8.2A — Engineering Domain Architecture — Digital Thread Specification

## Purpose

The complete traceability chain every Engineering Object participates
in, extending `WP7.2B Digital Thread Architecture.md`'s own founding
claim — **"the digital thread is not a new mechanism"** — from the
Requirements/Verification/Calculation chain already proven, to the
full canonical object set this Work Package defines.

## 1. The Digital Thread Is Traversal, Not Storage

Restated, unchanged, as platform-wide architecture: the Digital Thread
is a **read-side composition** over the single, already-shipped
reference mechanism (`DocumentReference`/`LinkAsync`/
`GetReferencesAsync`) — never a new index, cache, or graph-storage
technology. Any Engineering Object is "in the Digital Thread" simply by
having at least one `DocumentReference` connecting it to another
Engineering Object; no object opts in or out, and no object requires
special registration to participate.

## 2. The Full Canonical Chain

The controlling instruction's own named chain, extended with every
family from `Canonical Object Catalogue.md`, expressed as relationship
hops (`Relationship Catalogue.md`):

```
Requirement
  --allocatedTo/derivesFrom-->  Assembly / Part
  --calculatedBy-->             Calculation Result
                                    --references-->  Material
  --documentedBy-->             CAD Model  --source for-->  Drawing
  --verifiedBy-->                Verification Result
                                    --basedOnCalculation-->  Calculation Result
  --manufacturedBy-->           Manufacturing Operation
                                    --documentedBy-->  Work Instruction
  (Inspection, as a Verification Activity)
                                    --verifiedBy-->  Verification Result
  (Acceptance, expressed as an Approval)
                                    --approvedBy-->  Approval
  (Release, expressed as lifecycle state + Baseline)
                                    Baseline --composed of--> frozen revisions
  Evidence
     (composed traversal over every Verified By / Calculated By /
      Documented By / Approved By link reachable from the Requirement)
```

Every arrow above is an ordinary `Relationship Catalogue.md` entry;
none is a new mechanism. "Manufacture," "Inspection," "Acceptance," and
"Release," named as distinct stages in the controlling instruction, are
each realised through the existing vocabulary — a Manufacturing
Operation object, a Verification Result produced by an Inspection
Verification Activity, an Approval object, and a Baseline/lifecycle
state, respectively — never four new traversal primitives.

## 3. Evidence Traversal (Concrete Recipe)

Extending `IRequirementEvidence`'s own shipped recipe
(`GetEvidenceAsync`) to the full canonical chain, the same three-step
shape applies uniformly to any Engineering Object, not only
Requirement:

1. `GetReferencesAsync(objectId)` — every direct relationship the
   object participates in, in either direction.
2. For each `verifiedBy` reference — the linked Verification Result's
   own `Criteria`/`Evidence`/`LinkedDocumentIds`.
3. For each `calculatedBy`/`basedOnCalculation` reference — the linked
   Calculation Result's own `Assumptions`/`Validation`.

This recipe terminates naturally (the graph is not designed to be
acyclic by construction, but every real chain named in §2 is a DAG in
practice) — a traversal implementation should bound recursion depth
defensively, the same disclosed limitation `ADR-0065` already names for
the Workspace's own Digital Thread panel ("shows only one hop... tracing
a longer chain requires repeated manual navigation").

## 4. Allowable Links

**Every relationship in `Relationship Catalogue.md` is allowable
between any two Engineering Objects, regardless of family** —
Kind-agnostic at both ends, unchanged from `Engineering Principle 31`.
There is no platform-enforced "Requirement may only link to Assembly"
rule; the discipline is social/procedural (a Requirements Author would
not, in practice, write a `manufacturedBy` link from a Requirement),
not structural. This is a deliberate choice, not an oversight: enforcing
per-Kind relationship constraints would require a closed Kind registry
this platform has explicitly never built (`Engineering Domain
Architecture.md` §0 — "no closed enum... consistency is
convention-based").

## 5. Forbidden Links

Exactly one structural constraint exists, inherited unchanged from the
shipped mechanism: **a `DocumentReference` may not target a
`SourceDocumentId` equal to its own `TargetDocumentId`** (no
self-referential link) — the one validation `IEngineeringDocumentStore.LinkAsync`
already performs today. No other link is structurally forbidden;
anything else "forbidden" (a `Requirement` linking `verifiedBy` to
another `Requirement` rather than a `VerificationRecord`, for instance)
is a **validation warning**, not a rejected write — see `Validation
Specification.md` §Relationship Constraints.

## 6. Traceability Rules

1. **Forward traceability** — from a Requirement, follow `allocatedTo`/
   `derivesFrom` outward to what realises it.
2. **Backward traceability** — from any object, follow the same links
   inward (`GetReferencesAsync` is symmetric; direction is a property
   of the stored link, not of which end initiates the query).
3. **Evidence completeness** is a per-object judgement, never a
   platform-enforced gate — a Requirement may legitimately reach
   `Released` status with thin evidence; the Digital Thread makes that
   thinness *visible* (an empty or short evidence traversal), it does
   not *prevent* it. This mirrors `RequirementStatus`'s own shipped
   independence from `VerificationOutcome` (`Engineering Principle 29`)
   extended platform-wide.
4. **No orphan detection is built here** — `WP8.0C Engineering Cockpit
   Specification.md` §2 already names "a specific, actionable callout
   for orphaned objects" as a target Cockpit capability; this
   specification supplies the traversal primitive (§3) a future
   Work Package would compose that callout from, not the callout
   itself.

## Related Documents

`WP8.2A Engineering Domain Architecture.md`; `WP8.2A Relationship
Catalogue.md`; `WP8.2A Canonical Object Catalogue.md`; `WP8.2A
Validation Specification.md`; `docs/releases/v0.7.0/WP7.2B Digital
Thread Architecture.md`; `IRequirementsService.GetEvidenceAsync`;
`ADR-0065`.
