# ADR-0077: Engineering Domain Shared Services Reuse the Existing `IEngineeringDocumentStore` in Production; a New In-Memory Repository Layer Is the "In-Memory Repositories" Deliverable, Not a Second Document Store

## Status

Accepted — `v0.8.0` "Engineering Workspace", `WP 8.2C` (Engineering Domain Implementation), 2026-08-04. Resolves a direct tension between this Work Package's own controlling instruction and `ADR-0072`'s own already-locked decision.

## Context

`WP 8.2C`'s own controlling instruction names "In-memory repositories" as an explicit deliverable and states, twice, "No persistence. No database. No file storage." Read literally, this could mean every canonical Engineering Object this Work Package implements should be backed by a storage mechanism that never touches disk.

`ADR-0072` (`WP 8.2A`) already decided, as binding platform-wide architecture, that every canonical Engineering Object is realised as an `IEngineeringDocumentStore`-backed `Kind` — explicitly **never a new storage hierarchy**. Exactly one production implementation of that interface exists and is already registered in `TempestHost.cs` (`EngineeringDocumentStore`, built on `IPersistenceStore` per `ADR-0053`) — every one of the four Engineering Core frameworks (`Requirements`/`Verification`/`Materials`/`Calculations`) already shares that single instance. Introducing a second, competing `IEngineeringDocumentStore` registration into the same Host container would either silently shadow the real one (breaking those four frameworks) or require a second, parallel storage substrate — both directly contradicting `ADR-0072`.

## Decision

**The tension is resolved by distinguishing what is genuinely new here from what is reused.** `EngineeringDomainContext` (`src/Tempest.Core/EngineeringDomain/Implementation/EngineeringDomainContext.cs`) depends on `IEngineeringDocumentStore` as an interface, exactly as `WP8.2B Dependency Rules.md` §2 already allows. When resolved through the real `TempestHost`, it receives the same, already-registered, persistence-backed `EngineeringDocumentStore` every other framework shares — **zero new persistence is introduced**, since this is pure reuse of infrastructure that already exists and already persists, the identical relationship `IRequirementsService` already has to it.

**The genuinely new "in-memory repositories" deliverable is a different layer entirely**: `IEngineeringObjectRepository`/`InMemoryEngineeringObjectRepository` and `IEngineeringRelationshipRepository`/`InMemoryEngineeringRelationshipRepository` — a `ConcurrentDictionary`-backed, Kind-queryable index over constructed object instances, and a side index recording the richer `IEngineeringRelationship` shape (`Category`, `CreatedByPrincipalId`, `CreatedAt`) that `DocumentReference` does not carry. Neither wraps or competes with `IEngineeringDocumentStore` — they answer a question it cannot ("list every object of Kind X"), and both are genuinely, unconditionally in-memory: they hold no reference to `IPersistenceStore`, write nothing to disk, and are lost on process restart.

**`InMemoryEngineeringDocumentStore`** (a second, fully in-memory `IEngineeringDocumentStore` implementation) is also provided, but disclosed as a reference/test artifact, not production wiring — it is never registered in `TempestHost.cs`. It exists so this Work Package's own framework unit tests do not need to compose `InMemoryPersistenceStore` + `EngineeringDocumentStore` by hand for every test, and so a future, genuinely offline deployment has a working, already-proven alternative available without inventing one.

## Consequences

**Positive:**

- Fully honours the controlling instruction's own request (a real, in-memory repository layer exists and is the mechanism every canonical object's own by-Kind lookup goes through) without reopening `ADR-0072`'s own settled decision — the two requirements turn out to be compatible once "in-memory repositories" is understood as "a new indexing layer," not "replace the document store."
- Every canonical object created through the real Host is genuinely durable exactly as every other Engineering Core object already is — a future discipline module built on these contracts inherits that durability for free, without doing anything differently from `RequirementsService`.
- `InMemoryEngineeringDocumentStore` gives the framework's own test suite (and any future all-in-memory use case) a working, non-hacky alternative, proven by the same interface every production consumer already trusts.

**Negative:**

- Two `IEngineeringDocumentStore` implementations now exist in the repository, and a reader unfamiliar with this ADR could reasonably ask why. Disclosed here, in code comments on both types, and in the Academy documentation — not hidden.
- The repository layer's own state (which objects exist, by Kind) is not itself durable — restarting the Host loses it, even though the underlying documents themselves survive in `IPersistenceStore`. A future Work Package wanting the repository to rebuild itself from the store on startup is a genuine, disclosed gap, not attempted here (WP8.2B's own Dependency Rules §8 explicitly left registration/indexing decisions to a future implementation Work Package).

## Alternatives Considered

**Registering `InMemoryEngineeringDocumentStore` as the Host's own `IEngineeringDocumentStore`** — considered and rejected; would either silently break every existing Engineering Core framework's own persistence, or require a competing dual-registration scheme this platform has no precedent for and `ADR-0072` does not anticipate.

**Treating "no persistence" as forbidding any use of the real store at all, even via DI** — considered and rejected. Would mean the Engineering Domain framework could never be proven against the real Host, only against private, hand-assembled pipelines — a materially weaker form of "reusable platform component" than every prior Engineering Core framework has already demonstrated.

## Related Documents

`ADR-0072`; `ADR-0053`; `WP8.2B Dependency Rules.md` §2/§8; `src/Tempest.Core/EngineeringDomain/Implementation/EngineeringDomainContext.cs`; `src/Tempest.Core/EngineeringDomain/Implementation/InMemoryEngineeringDocumentStore.cs`; `src/Tempest.Core/EngineeringDomain/Implementation/InMemoryEngineeringObjectRepository.cs`.
