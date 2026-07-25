# Engineering Lifecycle

## What This Document Is

The canonical, stage-by-stage engineering process for TempestOS —
formalised at the close of the Foundation phase (`WP 4.5B`), after twenty-
two Work Packages had already, independently, converged on the same
shape. This document does not invent a new process; it names the one
already in continuous use since `WP 2.1`, so it can be followed
deliberately rather than merely rediscovered by imitation. It elaborates
`docs/academy/06 Engineering Standards/Engineering Governance.md`'s own
§1 (Work Package Lifecycle) into a fuller pipeline — read Governance §1
first if you have not already; this document assumes it.

## The Lifecycle

```
Idea
  ↓
Investigation
  ↓
Architecture
  ↓
ADR
  ↓
Rejected Designs
  ↓
Implementation
  ↓
Testing
  ↓
Architecture Review
  ↓
Academy
  ↓
Governance
  ↓
Release
  ↓
Maintenance
```

Not every stage produces a durable artefact on every pass — a Work
Package that implements an already-fully-decided architecture (a `WP 4.2`
implementation phase, say) moves through Architecture/ADR/Rejected
Designs quickly because a prior Work Package already did that work; a
Work Package that finds no genuine alternative worth rejecting produces
no Rejected Designs entry. The stages are not a bureaucratic checklist to
pad — they are the questions a well-run Work Package answers, in this
order, whether the answer to a given stage is "here is the new artefact"
or "already answered, nothing new needed here."

---

### 1. Idea

**Why this stage exists.** Every Work Package starts as a stated need —
a capability the platform lacks, a gap a prior retrospective's own
"Future Evolution" section named, or a risk `Risks.md` flagged as needing
resolution. Naming the idea explicitly, before doing anything else,
keeps the next ten stages honestly scoped to it rather than drifting.

**Expected outputs.** A stated objective — see any Work Package's own
brief, or `docs/releases/v0.4.0/WorkPackages.md`'s own per-Work-Package
"Objective" section.

**Review criteria.** Is the idea real (a genuine gap or need), and is its
scope stated narrowly enough to actually finish?

**Definition of completion.** The idea can be stated in one or two
sentences without hand-waving.

### 2. Investigation

**Why this stage exists.** An idea's premise can be wrong. `WP 4.4C`
assumed `IEventBus` already existed; investigating the actual repository
first found it did not, and the Work Package stopped rather than
building against a false premise. Investigation is the stage that catches
this before any design work is wasted on it.

**Expected outputs.** A confirmed (or corrected) understanding of what
currently exists, checked directly against the repository — not assumed
from a prior document's own claim, which may itself be stale.

**Review criteria.** Was the current repository state actually checked,
or was the idea's own premise taken on faith?

**Definition of completion.** The Work Package's actual starting point is
known and stated, matching what the repository currently contains.

### 3. Architecture

**Why this stage exists.** FOUNDATION.md's first non-negotiable:
architecture precedes implementation, for anything non-trivial. A
component's responsibilities, explicit non-responsibilities, failure
behaviour, and state machine (if it has one) are designed before its
first line of production code — not discovered afterward by reading what
got built.

**Expected outputs.** An architecture document (`docs/architecture/`) or
a design-only Work Package retrospective, following the same rigour as
`Runtime Host Architecture.md` or `Background Services Architecture.md`.

**Review criteria.** Does the design state what the component owns, what
it explicitly does not own, and how it fails? Does it reuse an existing,
proven pattern where one applies, per Reuse Before Invention?

**Definition of completion.** The design is written, reviewed, and
approved before implementation begins — a genuinely separate Work Package
phase where the risk profile warrants it (`WP 2.7A`/`WP 2.7B`, `WP
4.2`/`WP 4.2` implementation, `WP 4.5` architecture/`WP 4.5`
implementation).

### 4. ADR

**Why this stage exists.** A decision that was not the only reasonable
choice, that would be expensive to reverse, or that establishes a
convention future Work Packages depend on needs its reasoning to survive
longer than the person who made it (Engineering Governance §5).

**Expected outputs.** One or more files in `docs/adr/`, numbered
sequentially, following the Status/Context/Decision/Consequences/Future
Considerations template.

**Review criteria.** Does the decision actually meet one of §5's five
criteria? (Routine implementation detail with no genuine alternative does
not need an ADR merely because a choice was technically made.)

**Definition of completion.** The ADR is Accepted, cross-referenced from
the architecture document(s) it governs, and indexed in
`docs/governance/Architecture/ADR Register.md`.

### 5. Rejected Designs

**Why this stage exists.** The mirror image of an ADR: an alternative
seriously considered and declined, recorded at the moment it is declined
so the reasoning survives as well as the decision it produced
(Engineering Governance §10). Without this, a future contributor may
re-propose and re-investigate an alternative already carefully ruled out.

**Expected outputs.** One or more numbered entries in
`docs/architecture/Rejected Designs.md`, each naming what was considered,
why it was rejected, its reversibility, and (where applicable) its
revisit trigger.

**Review criteria.** Was the alternative genuinely considered, not a
straw man invented to pad the log?

**Definition of completion.** The entry is written and indexed in
`docs/governance/Architecture/Rejected Designs Register.md`.

### 6. Implementation

**Why this stage exists.** The stage every prior one exists to make safe
— by the time implementation begins, the hard questions (what this
component owns, how it fails, what alternative was rejected and why)
already have written answers, so implementation is realising a decision,
not making one.

**Expected outputs.** Production code under `src/`, matching the
approved architecture exactly — "implement the design exactly; do not
extend it; do not introduce speculative capability" is the standing
instruction for this stage (see any implementation Work Package's own
brief, e.g. `WP 4.5`'s).

**Review criteria.** Does the code match the approved design? If
implementation surfaces a genuine need to revisit the architecture, does
the Work Package stop and report rather than redesigning silently mid-
implementation (the explicit instruction this document's own governing
Work Package brief, and several before it, state)?

**Definition of completion.** The code builds cleanly (0 warnings, 0
errors) and matches the architecture document(s) it implements.

### 7. Testing

**Why this stage exists.** A design that is merely asserted to work is
not the same claim as one demonstrated to work. TempestOS consistently
prefers proving a claim against the real implementation over asserting it
from code inspection — "sequential dispatch," for example, is proven with
an in-flight-concurrency counter, not merely by reading call order.

**Expected outputs.** Tests under `tests/`, covering at minimum whatever
category list the Work Package's own brief or `Testing.md` names for it,
preferring real implementations over mocks (the one recurring exception:
a level-recording `ILogger`, to observe log output).

**Review criteria.** Does every test category the brief named have at
least one identifiable, correctly-named test? Does the full suite pass,
including every pre-existing test, not only the new ones?

**Definition of completion.** `dotnet test` reports 100% pass, verified
stable across multiple consecutive runs, with the pre-existing test count
undiminished.

### 8. Architecture Review

**Why this stage exists.** Individual Work Packages can each be locally
correct while a milestone's worth of them, taken together, drifts —
periodic, dedicated review catches this at a coarser grain than any
single Work Package's own scope allows. TempestOS has run this stage
formally twice (`WP 4.2D`, `WP 4.4F`) and a third time, for governance
specifically (`WP 4.5A`'s own Audit Report).

**Expected outputs.** A milestone review document (see `docs/releases/
v0.4.0/Platform Services Architecture Review.md`) naming every stale
cross-reference or drifted claim found, and confirming (or correcting)
that the whole milestone remains internally consistent.

**Review criteria.** Was every document changed since the last review
actually re-read, or was the review a rubber stamp?

**Definition of completion.** A consolidated finding list exists, every
finding is corrected or explicitly deferred with an owner, and the review
has its own Academy retrospective.

### 9. Academy

**Why this stage exists.** Source code and tests show *how* something
works; they cannot show *why* it works this way instead of one of the
others considered. The Academy is where that reasoning is taught, not
merely filed (Engineering Governance §6).

**Expected outputs.** A Work Package retrospective (`docs/academy/03 Work
Packages/`), following the 13-section template, plus any concept guide
or Engineering Principle document that changed as a result.

**Review criteria.** Does the retrospective explain *why*, name rejected
alternatives with real reasoning, and honestly record any mistake found
and fixed along the way, rather than presenting a tidied-up final state
as if it were reached directly?

**Definition of completion.** The retrospective exists, is indexed in
`docs/governance/Documentation/Academy Register.md`, and no "Future
Evolution" prediction it makes is later left stale once the predicted
change actually happens.

### 10. Governance

**Why this stage exists.** An individual document can be excellent while
the aggregate picture — how many ADRs exist, does every platform service
have a test and a retrospective — remains genuinely unknown without a
structured, cross-cutting index (`docs/governance/Governance
Philosophy.md`).

**Expected outputs.** Updates to whichever governance register(s) the
Work Package's own subject matter touches — a new platform service
updates the Platform Services Register and the Traceability Matrix; a new
ADR updates the ADR Register; and so on.

**Review criteria.** Does the register's own Cross-Reference Check still
hold after the update, or does the update introduce a discrepancy between
the register and the source document it indexes?

**Definition of completion.** Every register the Work Package touched is
updated in the same commit, not a follow-up pass, and its own Cross-
Reference Check passes.

### 11. Release

**Why this stage exists.** A Work Package's own completion is not the
same event as a release being cut — `docs/releases/v0.4.0/
ReleaseChecklist.md`'s own release-level gates (every Work Package's
Acceptance Criteria met, `CHANGELOG.md` current, `Risks.md` reviewed) are
checked once, deliberately, at the point of tagging, not folded silently
into every Work Package along the way.

**Expected outputs.** A `CHANGELOG.md` entry for every Work Package as it
lands (continuous); a release notes document and a `VERSION` bump, only
at the point of actually tagging (occasional).

**Review criteria.** Does `CHANGELOG.md` reflect every landed change
already, so the release-tagging Work Package has nothing left to
reconstruct from memory?

**Definition of completion.** For an individual Work Package: its
`CHANGELOG.md` entry exists. For an actual release: every
`ReleaseChecklist.md` gate passes.

### 12. Maintenance

**Why this stage exists.** A document, register, or test that is correct
today decays the moment the system it describes changes and it does not
change with it — this is the recurring theme across FOUNDATION.md,
Engineering Governance §6, and `Governance Philosophy.md` alike, applied
here as its own explicit lifecycle stage rather than left implicit.

**Expected outputs.** Whatever a *later* Work Package's own stages 9–10
above produce, applied back onto this Work Package's own artefacts when
they are the ones that have gone stale — exactly what `WP 4.5B` itself
did on finding `WorkPackages.md`'s and `ReleasePlan.md`'s own status
lines stale.

**Review criteria.** When a later Work Package finds this one's own
documentation stale, is it corrected in the open (as this document's own
governing Work Package did), or left for a future contributor to
rediscover the hard way?

**Definition of completion.** There is no single "done" for this stage —
it recurs for the life of the artefact, which is the point.

---

## Cross-Reference

Every stage above maps directly onto Engineering Governance's own
sections: Idea/Investigation → §1; Architecture/ADR/Rejected Designs →
§1, §5, §10; Implementation/Testing → §2, §3; Architecture Review → §2
(Technical Review), extended to periodic milestone form; Academy → §6;
Governance → this document's own new addition, formalising what `WP
4.5A` built; Release → §7; Maintenance → §6's own "maintained asset, not
a one-time deliverable" framing, generalised beyond just the Academy.

## Related Documents

`docs/academy/06 Engineering Standards/Engineering Governance.md`;
`docs/governance/Governance Philosophy.md`; `docs/governance/Future Work
Package Guidelines.md`; `docs/academy/Contributor Learning Path.md`.
