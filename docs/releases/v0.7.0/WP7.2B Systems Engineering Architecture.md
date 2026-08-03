# WP 7.2B — Systems Engineering Architecture

## Status

Architecture only. No production code accompanies this document.

## Purpose

Defines the architectural boundaries for the seven Systems Engineering
capability areas this Work Package's own controlling instruction names
(Requirements Management, Traceability, Allocation, Verification
Integration, Compliance Support, Engineering Evidence, Digital Thread),
and establishes a clear, three-layer distinction — **Engineering Core →
Systems Engineering Foundation → Engineering Discipline Modules** —
so a future discipline module's own architecture phase knows precisely
which layer it builds on and which layer it must not reach into.

## The Three-Layer Model

```
┌─────────────────────────────────────────────────────┐
│  Engineering Discipline Modules  (none exist yet)    │
│  Mechanical · Structural · Electrical · HVAC ·       │
│  Manufacturing · Materials-specific · Quality-specific│
│  — each classified per ADR-0013 when identified,     │
│  each consuming the Systems Engineering Foundation   │
│  and Engineering Core, never bypassing either        │
└───────────────────────▲───────────────────────────────┘
                         │ consumes
┌───────────────────────┴───────────────────────────────┐
│  Systems Engineering Foundation  (this Work Package)   │
│  Requirements · Requirement Hierarchy/Collections ·    │
│  Allocation · Traceability · Verification Integration  │
│  — cross-cutting, discipline-agnostic, consumed by     │
│  every future discipline module identically            │
└───────────────────────▲───────────────────────────────┘
                         │ consumes
┌───────────────────────┴───────────────────────────────┐
│  Engineering Core  (WP 7.1A–WP 7.1F, certified)        │
│  Engineering Data Model · Units & Quantities ·         │
│  Materials · Calculation · Verification                │
└───────────────────────▲───────────────────────────────┘
                         │ consumes
┌───────────────────────┴───────────────────────────────┐
│  Platform Core  (v0.6.0, certified)                    │
│  Identity · Audit · Settings · Reporting · REST API ·  │
│  Export/Import · Notifications · Licensing · Runtime   │
└─────────────────────────────────────────────────────────┘
```

**The boundary rule, stated once, applied throughout:** a layer consumes
only the layer(s) beneath it, never a layer above or beside it at the
same tier — the identical downward-only discipline `ADR-0023` already
enforces platform-wide, extended here one layer further. The Systems
Engineering Foundation is itself evidence this extension generalises
cleanly: it consumes the Engineering Core exactly as Materials/
Calculation/Verification consume the Engineering Data Model, and it will
be consumed by future discipline modules exactly as it itself consumes
the Engineering Core — the same pattern, repeated one layer up, not a
new architectural idea.

**Why "Systems Engineering Foundation," not "Requirements Module":** this
layer is not a single discipline's own private concern — every future
Engineering Discipline module (Mechanical, Structural, Electrical, HVAC,
Manufacturing, and any future Materials- or Quality-specific module)
will want to state requirements, allocate them, trace them, and record
their verification, in exactly the same shape. Naming this layer after
one discipline would misrepresent its own cross-cutting role — the same
reasoning `WP7.0B Engineering Foundation Architecture.md` applied when
naming the Engineering Core "Foundation," not "Systems Engineering
Utilities."

## Capability Area 1 — Requirements Management

**Boundary:** owns requirement identity, statement, categorisation, and
lifecycle status. Does not own what satisfies a requirement (a design
element in a future discipline module) or what demonstrates it (a
`VerificationRecord`) — both are referenced, never contained. See
`WP7.2B Requirements Domain Model.md` for the complete concept
definitions.

## Capability Area 2 — Traceability

**Boundary:** the Systems Engineering Foundation provides the
*mechanism* (typed, directed references between requirements and
between a requirement and anything else with an `IEngineeringDocument`
Id), reusing `IEngineeringDocumentStore.LinkAsync`/`GetReferencesAsync`
directly. It does not provide a discipline-specific traceability
*policy* (which link types a given regulated standard requires, for
example) — that is named only at the industry-neutral, architectural-
implication level in `WP7.2B Standards Mapping.md`, never implemented
here.

## Capability Area 3 — Allocation

**Boundary:** allocation is architecturally a specialised traceability
relationship — a requirement linked to whatever it is allocated to. The
Systems Engineering Foundation does not know, and must not need to know,
what an allocation *target* actually is: a future Mechanical component,
a future Electrical circuit, or (absent any such document yet) an open
string identifier. This is the single most load-bearing discipline-
neutrality decision in this architecture — see `WP7.2B Requirements
Domain Model.md` §5 (Requirement Allocation) for the full design.

## Capability Area 4 — Verification Integration

**Boundary:** the Systems Engineering Foundation calls
`IVerificationService.RecordAsync` directly against a requirement's own
document Id; it does not wrap, extend, or duplicate `IVerificationRecord`,
`VerificationOutcome`, or `VerificationContext` in any way. A
requirement's own "has this been demonstrated" question is answered
entirely by Verification — the Systems Engineering Foundation's own
contribution is *what* gets verified (a requirement) and *how it is
found* (traceability), never *how verification itself works*.

## Capability Area 5 — Compliance Support

**Boundary:** "compliance support" means providing the generic
architectural capacity a future compliance need (a specific standard, a
specific regulator) could be built on — traceability, evidence
retention, baseline/collection management, revision history — never a
specific standard's own compliance logic. `WP7.2B Standards Mapping.md`
enumerates exactly what generic capacity each named standard family
would need, confirming this Foundation's own design already provides
the load-bearing pieces (traceability, revisioning, evidence
aggregation) without building any standard-specific behaviour.

## Capability Area 6 — Engineering Evidence

**Boundary:** "Requirement Evidence" (`WP7.2B Requirements Domain
Model.md` §8) is an aggregation — a read-side view collecting a
requirement's own linked `VerificationRecord.Evidence` entries, linked
`CalculationRecord`s, and linked supporting documents into one coherent
account. It is not a new storage location; every underlying fact it
presents is already recorded by an Engineering Core framework. This
mirrors exactly how `CalculationRecord<TResult>`'s own provenance
(Principle 20) is not a bolted-on field but the record itself — evidence
here is composition, not a new mechanism.

## Capability Area 7 — Digital Thread

**Boundary:** see `WP7.2B Digital Thread Architecture.md` for the
complete design. In summary here: the digital thread is the traversable
path connecting a requirement to every other engineering fact related to
it (an allocation, a calculation, a verification, its evidence, its
reporting, its export, its audit trail) — entirely a *read-side,
traversal* capability over links the Systems Engineering Foundation and
Engineering Core already create as an ordinary consequence of their own
normal operation. The digital thread is not a new write path, a new
storage mechanism, or a new service — it is a name for what
`GetReferencesAsync`, followed transitively, already provides.

## Cross-Cutting Constraint: No Discipline-Specific Behaviour

Every one of the seven capability areas above is checked against this
Work Package's own explicit instruction — "It shall not introduce
discipline-specific engineering behaviour" — and confirmed clean: no
area references a specific engineering calculation, a specific material
property, a specific design methodology, or a specific regulatory
clause. Where a discipline-specific concept would otherwise be needed
(what an allocation target *is*, what a requirement's own constraint
*means*), this architecture deliberately stops at an open, generic
reference — the same discipline `Tempest.Core.Verification`'s own
Principle 28 already established ("Verification is independent of
presentation"), applied here to disciplines instead of presentation.

## Related Documents

`ADR-0013`; `ADR-0023`; `WP7.0B Engineering Foundation Architecture.md`
(the precedent this document's own three-layer framing extends);
`WP7.2B Requirements Platform Architecture.md`; `WP7.2B Digital Thread
Architecture.md`; `WP7.2B Requirements Domain Model.md`; `WP7.2B
Standards Mapping.md`.
