# ADR-0079: Object and Relationship Factories Are Realised as Few Generic Types, Instantiated Once per Kind, Not Dozens of Hand-Written Per-Kind Types

## Status

Accepted — `v0.8.0` "Engineering Workspace", `WP 8.2C` (Engineering Domain Implementation), 2026-08-04.

## Context

`WP8.2B Dependency Rules.md` §7 states: "`IEngineeringObjectFactory`/`IEngineeringRelationshipFactory` ... are each responsible for exactly one `Kind`/`RelationshipKind` ... a factory never constructs an object or relationship of a Kind other than its own declared one." Read as a instruction about *types*, this could mean one hand-written factory class per canonical object Kind (39 for the Kinds this Work Package gives concrete classes to) and one per named relationship kind (~18-20) — around 60 small, near-identical classes differing only in which concrete constructor they close over.

## Decision

**`IEngineeringObjectFactory` is realised by one generic type, `EngineeringObjectFactory<T> where T : EngineeringObjectBase`, parameterised by a `Kind` string and a `Func<IEngineeringDocument, IDocumentRevision, T>` constructor delegate supplied at construction. `IEngineeringRelationshipFactory` is realised by one concrete type, `EngineeringRelationshipFactory`, parameterised by a `RelationshipKind` string and `RelationshipCategory` — its own shape needs no per-Kind specialisation at all, since every relationship is the same, generic `IEngineeringRelationship`.** Every Kind still gets its own factory *instance*, constructed directly by the composition root (the sample module, or a future discipline module), never resolved from a registry — `WP8.2B Dependency Rules.md` §8 already states no such registry contract is proposed. Dependency Rules §7's own rule — "a factory never constructs an object of a Kind other than its own declared one" — is satisfied at the instance level exactly as written: each `EngineeringObjectFactory<T>` instance is permanently bound to the one `Kind` string passed to its own constructor, for its own lifetime.

## Consequences

**Positive:**

- Adding a fortieth (or four-hundredth) canonical Kind never requires a new factory *class* — only a new concrete object class and one line constructing a new `EngineeringObjectFactory<T>` instance for it, mirroring how the object classes themselves stayed small precisely because `EngineeringObjectBase` already carries every facet's own plumbing.
- The generic factory's own `CreateAsync` implementation (call `IEngineeringDocumentStore.CreateAsync`, fetch the resulting current revision, construct `T`, attach its self-factory delegate, register it in `IEngineeringObjectRepository`) is written and tested exactly once, rather than copy-pasted with the attendant risk of one copy silently drifting from the others.
- `EngineeringRelationshipFactory` needing no generic parameter at all confirms `ADR-0076`'s own central finding (one relationship shape, never per-category types) holds just as cleanly at the factory layer as it does at the contract layer.

**Negative:**

- A consumer inspecting the compiled assembly for "the Portfolio factory" finds a generic instantiation, `EngineeringObjectFactory<Portfolio>`, not a named `PortfolioFactory` type — a minor discoverability cost, mitigated by every construction site naming its own `Kind` string explicitly and adjacently (see `EngineeringDomainSampleModule.InitialiseAsync`, where every factory is constructed immediately next to the object it exists to create).
- Nothing in the type system stops two different call sites from constructing two independent `EngineeringObjectFactory<Portfolio>` instances with two different `Kind` strings, which would technically violate the "one Kind per instance, consistently" expectation this ADR otherwise relies on convention, not the compiler, to uphold — the same trade-off `ADR-0073` already accepted for `RelationshipKind` itself.

## Alternatives Considered

**One hand-written factory class per Kind (~39 + ~20 types)** — considered and rejected; see Context, above. Pure boilerplate, proportional to the number of Kinds a product brief happened to enumerate rather than to genuine structural variation, echoing the exact reasoning `ADR-0076` already applied to relationship types one Work Package earlier.

**A single non-generic factory dispatching internally on a `Kind` parameter** — considered and rejected; `WP8.2B Dependency Rules.md` §7 explicitly forbids this shape ("never a caller needing to create objects of several Kinds holds ... one polymorphic factory dispatching internally on a Kind parameter").

## Related Documents

`ADR-0076`; `WP8.2B Dependency Rules.md` §7/§8; `src/Tempest.Core/EngineeringDomain/Implementation/EngineeringObjectFactory.cs`; `src/Samples/Tempest.Samples/EngineeringDomainSampleModule.cs`.
