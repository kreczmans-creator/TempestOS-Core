# The Registry Pattern

## 1. Introduction

`RuntimeModuleManager` (WP 2.2) is TempestOS's clearest example of the Registry
pattern: a single, authoritative, in-memory catalogue of known items, keyed for
fast lookup, protected against duplicates, exposed only through read-only views.
This document describes the pattern generally and then in the specific terms
TempestOS applies it.

## 2. Purpose

To provide one place a system can ask "what do we know about X?" — reliably,
quickly, and without the asker needing to know how that knowledge is stored or
where it originally came from.

## 3. Background

Registries are one of the oldest recurring shapes in software architecture: a
DNS server is a registry of names to addresses; a service registry in a
microservice architecture is a registry of service names to network locations;
`RuntimeModuleManager` is a registry of module IDs to `RuntimeModule` records.
The shape recurs because the underlying problem — "many parts of a system need
to look up the same shared information, consistently" — recurs constantly.

## 4. The Problem

Without a registry, "what modules does this system know about" would have no
single answer — every consumer would need its own copy of, or its own way of
re-deriving, the same information, with no guarantee any two copies agree.

## 5. The Design

A Registry, in TempestOS's implementation, consists of: a mutable, internal
store (a `Dictionary<string, RuntimeModule>` for O(1) lookup, paired with a
`List<RuntimeModule>` preserving insertion order — the dictionary alone cannot
answer "in what order were these added"); a single entry point for adding items,
which validates and rejects duplicates (`Register`, throwing
`DuplicateModuleRegistrationException`); and read-only query operations
(`Get`, `TryGet`, `IsRegistered`, `GetAll`) that never expose the internal,
mutable store directly — every collection handed to a caller is a defensive
copy wrapped in `ReadOnlyCollection<T>`.

## 6. Alternatives Considered

**A bare `Dictionary<string, RuntimeModule>` exposed directly**, with callers
expected not to mutate it. Rejected immediately — "expected not to" is not a
guarantee, and the brief's explicit "consumers must never receive mutable
collections" requirement ruled this out regardless.

**No dedicated duplicate-detection**, relying on `Dictionary<TKey,TValue>`'s own
indexer semantics (silently overwriting on a duplicate key). Rejected in favour
of an explicit, Fail-Fast check and a dedicated exception — silent overwrite
would hide a real configuration error (two modules claiming the same ID) as if
it were normal behaviour.

## 7. Why This Solution Was Chosen

A registry's entire value proposition depends on callers being able to trust
what it hands them — trust that a lookup is accurate, trust that a returned
collection can't secretly be a live view into internal state, trust that
duplicate registration is impossible rather than silently tolerated. Every
design choice here traces back to preserving that trust.

## 8. Architectural Principles

Immutability (of both the returned records and the returned collections), Fail
Fast (duplicate detection), and Single Responsibility (a registry registers; it
does not discover, orchestrate, or construct) — see the corresponding
Engineering Principle documents.

## 9. Benefits

A registry decouples "who added this information" from "who needs to look it
up" — neither has to know anything about the other. It also gives the system
exactly one place to look when auditing "what does the runtime currently know
about," rather than several independent, potentially-disagreeing sources.

## 10. Trade-offs

A registry is a single point of coordination — in TempestOS's case, guarded by
one lock — which is fine for the current workload but would need reconsidering
if registration or lookup throughput ever became a genuine bottleneck.

## 11. Common Mistakes

Exposing the registry's internal collection directly, or a shallow copy that
still shares references to mutable internal state, defeats the entire pattern —
see the Immutability principle document and ADR-0001 for why TempestOS insists
on both immutable *records* and immutable *collections*, not just one or the
other.

## 12. Future Evolution

If deregistration is ever needed (see WP 2.2's Future Evolution notes), the
registry pattern itself doesn't need to change — it needs one more validated,
Fail-Fast entry point (`Deregister`), following exactly the same discipline
`Register` already established.

## 13. Key Takeaway

A registry is only as trustworthy as the immutability guarantees around what it
hands out — the pattern's value collapses the moment a caller can mutate what
they were given and have that mutation silently affect the registry's own
internal state.
