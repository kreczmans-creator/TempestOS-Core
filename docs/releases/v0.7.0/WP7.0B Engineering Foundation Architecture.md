# WP 7.0B — Engineering Foundation Architecture

## Status

Complete. Architecture and planning only — no production code, no
implementation, no ADR (an ADR would require an actual design decision;
this document identifies *what* needs designing, not *how*).

## Purpose

`docs/governance/Future Capability Register.md`, as established by `WP
7.0A`, had exactly two entries in the Engineering Discipline categories
(`FCR-0027` Requirements Engine, `FCR-0028` Project Engine) and zero for
the other seven. This left an open question this Work Package's own
controlling instruction names directly: **what is the minimum
engineering foundation required before discipline-specific Engineering
Modules begin?** This document answers that question using only
architectural necessity reasoning grounded in `Capability Categories.md`
and `Threat Model.md` — not invented discipline capability.

## Method

A discipline-specific capability (a structural load calculation, an
HVAC duct-sizing tool) was deliberately **not** designed or even named
here — doing so would repeat exactly the invention `WP 7.0A` declined.
Instead, this document asks: *what would any such capability,
whichever discipline eventually needs it, structurally require to exist
at all?* Five answers emerged, each cross-checked against whether an
existing `v0.6.0` platform service already provides it (in which case it
is not a new capability) or a genuine gap exists.

## The Five Engineering Foundation Capabilities

### 1. `FCR-0029` — Engineering Data Model & Document Management Foundation

**Why this is foundational.** Every future Engineering Module —
Requirements, Project, or any eventual discipline module — needs a
place to store its own domain entities (a requirement, a project
record, a material specification, a verification result) and their
relationships (revisions, references, cross-links). Without a shared
data model, each module independently invents its own storage shape,
repeating the exact anti-pattern `ADR-0041` already resolved once for
Settings/Audit (a shared Persistence abstraction, not reinvented per
service).

**Why it is not already solved.** `IPersistenceStore` (`WP 6.4`)
provides key-value storage with no native query/filter capability
(`FCR-0007`) and no entity-relationship concept at all — sufficient for
Settings and Audit's own flat records, not sufficient for a document
with revisions and cross-references.

### 2. `FCR-0030` — Units & Quantities Framework

**Why this is foundational.** Every Engineering Discipline category
`Capability Categories.md` establishes (Mechanical, Structural,
Electrical, Building Services/HVAC, Materials, Manufacturing) operates
on dimensioned physical quantities. A platform with no shared
representation for "this number is 4.2, in newtons" invites exactly the
unit-conversion defect class that has caused real, documented failures
in other engineering software industry-wide.

**Why it is not already solved.** Nothing in TempestOS today represents
a dimensioned quantity — every existing platform service (Settings,
Audit, Reporting) operates on primitive types or opaque strings, never
a physical quantity.

### 3. `FCR-0032` — Engineering Calculation Framework

**Why this is foundational.** A structural load calculation, an HVAC
sizing calculation, an electrical load calculation — each is, at the
platform level, an instance of the same underlying shape: take
dimensioned inputs, apply a formula, produce a dimensioned output,
record what was calculated and why. The Command Framework already
proved the value of "one dispatch mechanism, not reinvented per
consumer" (`ADR-0037`/`ADR-0038`) for a structurally similar problem.

**Why it is not already solved.** No calculation-execution abstraction
exists in TempestOS today; a Reporting or Audit-style pipeline (data in,
formatted or recorded output) is not the same shape as a calculation
(dimensioned inputs, a formula, a dimensioned output that itself may
feed a downstream calculation).

**Note on sequencing.** `FCR-0032` is rated High engineering effort and
depends on `FCR-0030` — it should not begin before `FCR-0030` lands,
per Part 2's dependency graph in `WP7.0B Capability Dependency
Report.md`.

### 4. `FCR-0031` — Materials Framework

**Why this is foundational** for the `Materials` and `Manufacturing`
categories specifically (not for Mechanical/Structural/Electrical/HVAC
directly, though those disciplines would consume material data
indirectly): a Materials Engineering module cannot exist without some
shared material-specification and traceability capability, and
Manufacturing/Quality both depend on materials data being consistent
and traceable.

**Why it is not already solved.** No material-data concept exists
anywhere in TempestOS today, dormant code included.

### 5. `FCR-0033` — Verification & Validation Framework

**Why this is foundational** for the `Quality` category and strengthens
`FCR-0027` directly: `Threat Model.md` assumption 1 names "verification
records" explicitly as engineering IP TempestOS will eventually manage.
A requirement without a verification record against it is only half the
traceability chain `FCR-0027`'s own description already names.

**Why it is not already solved.** Audit (`WP 6.5`) records *who did
what, when* — a durable, queryable, append-only log of platform
actions. It does not record *whether a specification was met* — a
categorically different kind of record, with its own pass/fail
semantics and its own relationship to a requirement, not an action.

## What This Document Does Not Claim

- It does not claim these five capabilities are sufficient to build a
  real Mechanical, Structural, Electrical, or HVAC module — only that
  they are architecturally necessary *before* one begins. Each real
  discipline module will likely need discipline-specific capability
  beyond this foundation, not yet identified (see `WP7.0B Engineering
  Discipline Assessment.md`).
- It does not scope, design, or estimate implementation detail for any
  of the five — each still requires its own Architecture Work Package,
  per this project's standing discipline.
- It does not classify any of the five under `ADR-0013` (Platform
  Service vs. Module) — that decision is explicitly deferred to each
  capability's own future Architecture Work Package, exactly as
  `FCR-0027`/`FCR-0028` already deferred it in `WP 7.0A`.

## Recommended Internal Sequencing

1. `FCR-0029` (Engineering Data Model) — nothing else in this set can
   proceed meaningfully without it.
2. `FCR-0030` (Units & Quantities) — independent of `FCR-0029`, can
   proceed in parallel.
3. `FCR-0031` (Materials Framework) — depends on both 1 and 2.
4. `FCR-0032` (Calculation Framework) — depends on 2.
5. `FCR-0033` (Verification & Validation) — depends on `FCR-0027`
   (Requirements Engine) existing first, and on 1.

## Related Documents

`docs/governance/Future Capability Register.md` (`FCR-0029`–`FCR-0033`
full entries); `WP7.0B Capability Dependency Report.md`; `docs/
governance/Capability Categories.md`; `VISION.md`; `ADR-0013`;
`ADR-0037`, `ADR-0038`, `ADR-0041`.
