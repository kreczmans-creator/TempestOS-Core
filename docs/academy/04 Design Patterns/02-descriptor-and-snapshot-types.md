# Descriptor and Snapshot Types

## 1. Introduction

Three of TempestOS's central types — `ModuleDescriptor`, `RuntimeModule`, and
`ModuleLifecycleStatus` — share a shape: each is an immutable, data-only record
representing "what we know, right now, about one module," produced by a specific
stage of the pipeline for consumption by the stages after it. This document
describes the pattern behind all three.

## 2. Purpose

To let information flow between pipeline stages (discovery → registration →
lifecycle) as safe, immutable values, without any stage being able to corrupt
data a previous stage produced, and without needing synchronisation to share
that data across threads.

## 3. Background

Each of these three types was introduced by a different work package, for a
different, specific need, but all three converged on the identical shape
independently: a small set of get-only properties, an internal-or-otherwise-
protected constructor, no behaviour beyond exposing data.

## 4. The Problem

A pipeline stage needs to hand its output to the next stage without either (a)
handing over a mutable object the next stage could accidentally corrupt, sending
bad data back upstream in effect, or (b) requiring every consumer to defensively
copy the data themselves, which just relocates the same problem.

## 5. The Design

- **`ModuleDescriptor`** (WP 2.1): `Id`, `Name`, `Version`, `ModuleType` — a
  record of what discovery found.
- **`RuntimeModule`** (WP 2.2): `Descriptor`, `State`, `RegisteredAt`,
  `FailureReason` — a record of what was registered, `internal` constructor.
- **`ModuleLifecycleStatus`** (WP 2.3): `Descriptor`, `State`, `FailureReason` —
  a *snapshot* of a module's lifecycle status at query time, `internal`
  constructor.

All three are `sealed`, all three expose only get-only properties, and two of
the three (`RuntimeModule`, `ModuleLifecycleStatus`) restrict construction to
`internal`, so only the one component authorised to produce them can create an
instance.

The distinction between `RuntimeModule` and `ModuleLifecycleStatus` is the most
instructive part of this pattern: they carry almost identical data, but
`RuntimeModule` is a permanent record fixed at registration time, while
`ModuleLifecycleStatus` is a fresh snapshot, regenerated every time
`ModuleLifecycleManager.Modules` is queried, of state that genuinely does change
underneath it. The pattern isn't "make everything immutable" — it's "make the
value type callers hold immutable, regardless of whether the underlying reality
it describes is itself changing somewhere else."

## 6. Alternatives Considered

**One shared base class** (`ModuleRecord`, say) that `RuntimeModule` and
`ModuleLifecycleStatus` both inherit from, given how similar their shapes are.
Considered and rejected — see the Composition Over Inheritance principle
document: the similarity is coincidental (both happen to need a `Descriptor`, a
`State`, and a `FailureReason` today), not a genuine, stable "is-a" relationship;
forcing a shared base class would couple two types that are deliberately
independent (see ADR-0001 and ADR-0002) for the sake of avoiding a small, honest
amount of duplication.

## 7. Why This Solution Was Chosen

Each type's shape follows directly from asking "what does the *next* stage need
to depend on, safely, without depending on how the previous stage works
internally?" — the answer, each time, was: a small, fixed, immutable snapshot.

## 8. Architectural Principles

Immutability, Separation of Concerns (each type belongs to, and is only
constructible by, one pipeline stage).

## 9. Benefits

Data can be freely passed, logged, compared, and cached across the whole
pipeline without any consumer needing to worry about it changing underneath
them, or about accidentally corrupting a previous stage's records.

## 10. Trade-offs

Some structural duplication between `RuntimeModule` and `ModuleLifecycleStatus`
was accepted deliberately, as the direct cost of keeping registration and
lifecycle genuinely independent — see ADR-0001's Consequences section.

## 11. Common Mistakes

Assuming `RuntimeModule.State` reflects a module's *current* lifecycle state,
because the property is literally named `State`, is the single most likely
misunderstanding a new reader will have — it reflects registration-time state
only, permanently. `ModuleLifecycleStatus.State` is the one that changes.

## 12. Future Evolution

Any future pipeline stage (Health, Diagnostics) that needs to expose its own
"what do we know about this module, right now" data should follow this same
pattern — its own small, immutable snapshot type, constructible only by that
stage — rather than reaching for `RuntimeModule` or `ModuleLifecycleStatus` and
adding fields to either.

## 13. Key Takeaway

Two types can share nearly identical data and still deserve to be genuinely
separate — the question is not "how similar do they look" but "do they change
for the same reason, owned by the same component."
