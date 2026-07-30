# TempestOS v0.7.0 — Engineering Foundation Contracts

## Status

**Contract Review — documentation only. No implementation.** This
document is the pre-implementation review of the five proposed
Engineering Foundation frameworks (`FCR-0029`–`FCR-0033`, identified by
`WP 7.0B`), mirroring `docs/releases/v0.6.0/Platform Service
Contracts.md`'s own role for that release's own new surfaces. Every
interface below is a **proposed** design artifact for review — matching
this codebase's own XML-doc and naming conventions exactly — without
being compiled, tested, or committed as source. No interface below is
final; each remains subject to its own owning Work Package's dedicated
architecture phase (`WP7.0B Candidate Work Package Catalogue.md`,
Candidates `D`–`H`), where the open questions `WP7.0C Required ADR
Catalogue.md` names are actually decided.

## How to Read This Document

Each framework section answers the same twelve questions, in the same
order, this Work Package's own controlling instruction names: Purpose,
Responsibilities, Public Interfaces, Primary Entities, Dependency Rules,
Platform Services Consumed, Expected Future Consumers, Lifetime,
Thread-Safety Expectations, Error Handling, Extension Points, Testing
Strategy Summary (full detail in `WP7.0C Testing Strategy.md`), Academy
Requirements Summary (full detail in `WP7.0C Academy Plan.md`). Where a
question does not apply, the section says so explicitly rather than
being silently omitted.

## Design Principles Applied Uniformly

Every signature below follows a rule already established somewhere in
this platform, mirroring `Public Interface Catalogue.md`'s own "How
These Signatures Were Derived" discipline:

- **A `Try`-style or nullable-return lookup paired with a throwing
  primary method where "not found" is exceptional** — mirrors
  `ICommandRegistry`'s own `InvokeAsync`/`RegisterDescriptor` pair.
- **An abstract base exception per namespace, with concrete leaf
  exceptions beneath it** — mirrors `PersistenceException`,
  `LicensingException`, `ExportImportException`.
- **Optional `ILogger?` constructor parameter** where a type is a
  DI-registered service, matching every existing platform service's
  convention (`ADR-0010`); **no** `ILogger?` on a pure value type
  (`Quantity<TDimension>`, `Unit<TDimension>`), matching how
  `CommandResult`/`LicenseValidationResult` carry no logger either.
- **Immutable records for anything representing a past fact** (a
  revision, a calculation record, a verification record) — mirrors
  `AuditRecordResult`/`LicenseValidationResult`.

---

## 1. Engineering Data Model & Document Management Foundation *(`Tempest.Core.EngineeringData`, proposed — `FCR-0029`)*

**Purpose.** A shared store for engineering-domain entities — a
requirement, a project record, a material specification, a verification
subject — as revisioned documents with typed relationships between them,
so no future Engineering Module invents its own incompatible storage
shape.

**Responsibilities.** Create a document of a caller-declared `Kind`;
record an immutable revision each time its content changes; enumerate a
document's own revision history; link two documents with a typed
relationship. Does **not** interpret `Kind` or `Content` — both are
opaque to this framework, exactly as `IPersistenceStore` treats its own
stored values as opaque strings. Does not provide querying beyond direct
Id lookup and per-document revision/reference enumeration (`FCR-0007`'s
own gap applies here identically, disclosed, not solved).

**Public Interfaces.**

```csharp
namespace Tempest.Core.EngineeringData;

/// <summary>
/// A shared store for engineering-domain documents — revisioned,
/// typed, and linkable, but opaque in content. Not a general-purpose
/// document database; scoped to what Engineering Foundation and future
/// Engineering Module consumers need.
/// </summary>
public interface IEngineeringDocumentStore
{
    /// <summary>Creates a new document of the given <paramref name="kind"/> with an initial revision.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="kind"/> or <paramref name="initialContent"/> is <see langword="null"/>.</exception>
    Task<IEngineeringDocument> CreateAsync(string kind, string initialContent, CancellationToken cancellationToken = default);

    /// <summary>Returns the document, or <see langword="null"/> if none exists.</summary>
    Task<IEngineeringDocument?> FindAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>Records a new revision, incrementing <see cref="IEngineeringDocument.CurrentRevisionNumber"/>.</summary>
    /// <exception cref="EngineeringDocumentNotFoundException"><paramref name="documentId"/> does not exist.</exception>
    Task<IDocumentRevision> ReviseAsync(Guid documentId, string newContent, string? changeSummary, CancellationToken cancellationToken = default);

    /// <summary>Every revision of the document, oldest first. Never <see langword="null"/>.</summary>
    /// <exception cref="EngineeringDocumentNotFoundException"><paramref name="documentId"/> does not exist.</exception>
    Task<IReadOnlyList<IDocumentRevision>> GetRevisionHistoryAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>Records a typed, directed relationship between two existing documents.</summary>
    /// <exception cref="EngineeringDocumentNotFoundException"><paramref name="sourceDocumentId"/> or <paramref name="targetDocumentId"/> does not exist.</exception>
    Task LinkAsync(Guid sourceDocumentId, Guid targetDocumentId, string relationshipKind, CancellationToken cancellationToken = default);

    /// <summary>Every reference where <paramref name="documentId"/> is the source. Never <see langword="null"/>.</summary>
    Task<IReadOnlyList<DocumentReference>> GetReferencesAsync(Guid documentId, CancellationToken cancellationToken = default);
}

public interface IEngineeringDocument
{
    Guid Id { get; }
    string Kind { get; }
    int CurrentRevisionNumber { get; }
    DateTimeOffset CreatedAt { get; }
}

public interface IDocumentRevision
{
    Guid DocumentId { get; }
    int RevisionNumber { get; }
    string Content { get; }
    string? ChangeSummary { get; }
    string AuthorPrincipalId { get; }
    DateTimeOffset CreatedAt { get; }
}

/// <summary>A directed, typed relationship between two documents (e.g., "verifies", "derivedFrom").</summary>
public sealed record DocumentReference(Guid SourceDocumentId, Guid TargetDocumentId, string RelationshipKind);

public abstract class EngineeringDataException : Exception { /* ... */ }
public sealed class EngineeringDocumentNotFoundException : EngineeringDataException { /* ... */ }
```

**Primary Entities.** `IEngineeringDocument` (identity + current
revision pointer), `IDocumentRevision` (immutable, append-only content
snapshot), `DocumentReference` (a typed edge between two documents).

**Dependency Rules.** None upstream within the Engineering Foundation
set. A plausible, not guaranteed, storage substrate is `IPersistenceStore`
(`WP 6.4`) — this is the first open question `WP7.0C Required ADR
Catalogue.md` names, since `IPersistenceStore`'s own key-value shape has
no native concept of a revision or a typed reference.

**Platform Services Consumed.** Identity & Permissions (attributing
`AuthorPrincipalId`); Persistence (plausible storage substrate, not
guaranteed); Audit (a plausible, not mandatory, consumer relationship —
see `WP7.0C Platform Integration Matrix.md`).

**Expected Future Consumers.** `FCR-0031` (Materials Framework),
`FCR-0033` (Verification & Validation Framework), and — outside this
Work Package's own five-framework scope, but named for completeness —
`FCR-0027` (Requirements Engine) and `FCR-0028` (Project Engine), each
of which would represent its own domain entities as a `Kind` of
document rather than inventing a parallel storage mechanism.

**Lifetime.** DI-public, container-constructed singleton, mirroring
`IPersistenceStore`'s own registration shape — proposed for the same
Phase 6 registration slot, pending the owning Work Package's own
confirmation.

**Thread-Safety Expectations.** Every method must be safe for concurrent
invocation — multiple future Engineering Module consumers may read and
revise documents concurrently, mirroring `IPersistenceStore`'s own
requirement. `ReviseAsync`'s own revision-number increment must be
atomic per document — two concurrent `ReviseAsync` calls against the
same document must never produce two revisions claiming the same
`RevisionNumber`.

**Error Handling.** `EngineeringDocumentNotFoundException` for any
operation against a non-existent Id — never a silent no-op, mirroring
`ReportDefinitionNotFoundException`'s own pattern. No swallowed
exception from the underlying storage substrate — it propagates
unmodified, mirroring `IPersistenceStore`'s own `PersistenceStoreUnavailableException`
philosophy.

**Extension Points.** A native query/filter capability beyond direct Id
lookup (shared with `FCR-0007`'s own named gap); a typed (not
string-only) `Content` contract, if a future consumer needs structured
data rather than caller-serialized strings — deliberately not designed
here, mirroring `IPersistenceStore`'s own identical, disclosed
limitation.

**Testing Strategy Summary.** Round-trip create/revise/read
correctness; revision-number atomicity under concurrent revision;
reference integrity (a link to a non-existent document fails, never
silently succeeds). Full detail: `WP7.0C Testing Strategy.md`.

**Academy Requirements Summary.** A new concept guide teaching the
document/revision/reference pattern as this platform's first
data-modelling abstraction beyond flat key-value storage. Full detail:
`WP7.0C Academy Plan.md`.

---

## 2. Units & Quantities Framework *(`Tempest.Core.UnitsAndQuantities`, proposed — `FCR-0030`)*

**Purpose.** A shared representation for dimensioned physical
quantities and conversion between compatible units, preventing the
unit-conversion defect class industry-wide engineering software has
repeatedly demonstrated.

**Responsibilities.** Represent a numeric value paired with a unit of a
specific dimension; convert a quantity to another unit of the *same*
dimension; reject conversion between incompatible dimensions at compile
time where possible, at runtime otherwise. Does **not** provide a
calculation engine (`FCR-0032`'s own concern), does not provide a fixed
catalogue of every real-world unit (a starting set only, extensible).

**Public Interfaces.**

```csharp
namespace Tempest.Core.UnitsAndQuantities;

/// <summary>
/// Marker for a physical dimension (length, mass, force, temperature,
/// and so on). A distinct type per dimension prevents a length-typed
/// quantity from being mistaken for a mass-typed one at compile time.
/// </summary>
public interface IDimension { }

/// <summary>A named unit of measurement for dimension <typeparamref name="TDimension"/>.</summary>
public readonly record struct Unit<TDimension> where TDimension : IDimension
{
    /// <summary>The unit's display symbol (e.g., "m", "kg", "N").</summary>
    public string Symbol { get; }

    /// <summary>The multiplicative factor converting one of this unit into this dimension's base unit.</summary>
    public double ToBaseUnitFactor { get; }
}

/// <summary>An immutable numeric value paired with a unit of dimension <typeparamref name="TDimension"/>.</summary>
public readonly record struct Quantity<TDimension> where TDimension : IDimension
{
    public double Value { get; }
    public Unit<TDimension> Unit { get; }

    /// <summary>Returns an equivalent quantity expressed in <paramref name="targetUnit"/>.</summary>
    /// <exception cref="IncompatibleUnitsException"><paramref name="targetUnit"/> is not a valid unit for <typeparamref name="TDimension"/> (defensive; the generic constraint prevents this at compile time for correctly-implemented units).</exception>
    public Quantity<TDimension> ConvertTo(Unit<TDimension> targetUnit);
}

/// <summary>Converts quantities between units, for callers that do not hold a strongly-typed <see cref="Quantity{TDimension}"/> directly (e.g., a value read from configuration or a REST request).</summary>
public interface IUnitConverter
{
    Quantity<TDimension> Convert<TDimension>(Quantity<TDimension> source, Unit<TDimension> targetUnit) where TDimension : IDimension;
}

public sealed class IncompatibleUnitsException : Exception { /* ... */ }
```

**Primary Entities.** `Quantity<TDimension>` (value + unit), `Unit<TDimension>`
(a named unit within a dimension), `IDimension` (a compile-time marker
type, not a runtime entity — `Length`, `Mass`, `Force`, and so on would
each be a distinct type implementing it).

**Dependency Rules.** None upstream, and — distinctly from every other
Engineering Foundation framework — **no dependency on the DI container
at all**. `Quantity<TDimension>`/`Unit<TDimension>` are pure, immutable
value types; `IUnitConverter` is a plausible thin convenience wrapper,
not a stateful service. This mirrors `CommandResult`'s own "not every
public type is a DI-registered service" precedent, applied here for the
first time to an entire framework rather than a single result type.

**Platform Services Consumed.** None. This is the only Engineering
Foundation framework with zero Platform Service dependency, a genuine
architectural finding worth its own disclosure (see `WP7.0C
Cross-Framework Dependency Report.md`).

**Expected Future Consumers.** `FCR-0031` (Materials Framework, for
dimensioned material properties), `FCR-0032` (Engineering Calculation
Framework, for dimensioned calculation inputs/outputs), and every
not-yet-identified Mechanical/Structural/Electrical/Building
Services-HVAC/Manufacturing capability once one exists.

**Lifetime.** Not applicable in the DI sense — `Quantity<TDimension>`
and `Unit<TDimension>` are value types, constructed directly by their
own consumers, never resolved from the container. `IUnitConverter`, if
built at all, would be a stateless, trivially-constructible singleton —
the weakest case for DI registration in this entire document, and this
Work Package does not assume it needs one.

**Thread-Safety Expectations.** Trivially satisfied — both value types
are immutable by construction (`readonly record struct`); no shared
mutable state exists to synchronize.

**Error Handling.** `IncompatibleUnitsException` is a defensive
safeguard, not the primary safety mechanism — the generic constraint
(`TDimension` shared between source and target) is expected to prevent
the vast majority of incompatible-conversion attempts at compile time.
The exception exists for the residual runtime case (a malformed
`Unit<TDimension>` constructed with a mismatched conversion factor by a
careless caller), not as the primary defence.

**Extension Points.** Additional `IDimension` implementations and
`Unit<TDimension>` values are purely additive — a future Structural
Engineering module adding a `Pressure` dimension requires no change to
this framework itself, only a new type implementing `IDimension`.

**Testing Strategy Summary.** Conversion round-trip correctness
(`quantity.ConvertTo(u).ConvertTo(originalUnit)` recovers the original
value within floating-point tolerance); compile-time rejection of
cross-dimension conversion (a test asserting the code does not compile,
mirroring how this platform already tests certain generic-constraint
guarantees). Full detail: `WP7.0C Testing Strategy.md`.

**Academy Requirements Summary.** A new concept guide teaching the
generic-dimension-marker pattern as a compile-time safety mechanism —
this platform's first use of a phantom-type-style pattern. Full detail:
`WP7.0C Academy Plan.md`.

---

## 3. Materials Framework *(`Tempest.Core.Materials`, proposed — `FCR-0031`)*

**Purpose.** Shared material specification and traceability capability
— the first real content for the `Materials` category.

**Responsibilities.** Register and retrieve a named material's own
specification, including dimensioned properties (density, yield
strength, and so on, each a `Quantity<TDimension>`). Does **not**
itself provide a materials science calculation (that is
`FCR-0032`'s own concern, consuming a material's properties as
calculation input) and does not duplicate document revisioning — a
material specification is itself represented as an `IEngineeringDocument`
(`Kind = "MaterialSpecification"`), not a second, parallel storage
mechanism.

**Public Interfaces.**

```csharp
namespace Tempest.Core.Materials;

/// <summary>
/// A catalogue of material specifications. Each specification is
/// itself an <see cref="Tempest.Core.EngineeringData.IEngineeringDocument"/>
/// of <c>Kind = "MaterialSpecification"</c> — this catalogue is an
/// indexed, typed view over that shared store, not a second storage
/// mechanism.
/// </summary>
public interface IMaterialCatalog
{
    /// <summary>Registers a new material specification.</summary>
    /// <exception cref="DuplicateMaterialException"><paramref name="materialId"/> is already registered.</exception>
    Task<IMaterialSpecification> RegisterAsync(string materialId, string name, IReadOnlyDictionary<string, object> properties, CancellationToken cancellationToken = default);

    /// <summary>Returns the specification, or <see langword="null"/> if none is registered under <paramref name="materialId"/>.</summary>
    Task<IMaterialSpecification?> FindAsync(string materialId, CancellationToken cancellationToken = default);

    /// <summary>Every registered material specification. Never <see langword="null"/>.</summary>
    Task<IReadOnlyList<IMaterialSpecification>> ListAsync(CancellationToken cancellationToken = default);
}

public interface IMaterialSpecification
{
    string MaterialId { get; }
    string Name { get; }

    /// <summary>Dimensioned properties (e.g., "Density", "YieldStrength"), each a boxed <c>Quantity&lt;TDimension&gt;</c> for a dimension this framework does not itself constrain.</summary>
    IReadOnlyDictionary<string, object> Properties { get; }

    Guid UnderlyingDocumentId { get; }
}

public abstract class MaterialsException : Exception { /* ... */ }
public sealed class DuplicateMaterialException : MaterialsException { /* ... */ }
public sealed class MaterialNotFoundException : MaterialsException { /* ... */ }
```

**Primary Entities.** `IMaterialSpecification` (a named material and its
dimensioned properties), backed by `IEngineeringDocument`.

**Dependency Rules.** Depends on `Tempest.Core.EngineeringData`
(`FCR-0029`, for underlying storage and revisioning) and
`Tempest.Core.UnitsAndQuantities` (`FCR-0030`, for dimensioned property
values). No circular dependency — neither `FCR-0029` nor `FCR-0030`
depends back on Materials.

**Platform Services Consumed.** Identity & Permissions (registration
authorization); indirectly, Persistence and Audit, through
`IEngineeringDocumentStore`.

**Expected Future Consumers.** Any Manufacturing capability, once
identified (material selection during process planning); any
Materials-discipline capability, once identified; potentially
`FCR-0032` (Engineering Calculation Framework), where a calculation
consumes a material's own properties as input.

**Lifetime.** DI-public, container-constructed singleton, mirroring the
Data Model's own registration shape.

**Thread-Safety Expectations.** Safe for concurrent invocation, mirroring
every other Engineering Foundation DI-registered service — concurrent
registration of two different materials must not corrupt the catalogue.

**Error Handling.** `DuplicateMaterialException` for a re-used
`materialId`; `MaterialNotFoundException` where the Public Interfaces
above show a throwing variant would be needed (this proposal favours
nullable-return `FindAsync` over a throwing lookup, mirroring
`IPersistenceStore.ReadAsync`'s own precedent, since "material not
found" is an ordinary, expected outcome for a catalogue lookup, not an
exceptional one).

**Extension Points.** No fixed set of property names is enforced —
`IReadOnlyDictionary<string, object>` is deliberately open, so a future
Materials-discipline capability can add a new property name without a
contract change. Whether `object` should become a stronger, generic
`Quantity<TDimension>`-typed dictionary is an open question (see
`WP7.0C Required ADR Catalogue.md`) — deferred because C#'s own type
system makes a heterogeneous dictionary of differently-dimensioned
quantities awkward to express strongly, a genuine, disclosed limitation
rather than a solved problem.

**Testing Strategy Summary.** Registration/lookup round-trip; duplicate
rejection; confirmation that a registered material's own underlying
document is genuinely revisionable through `IEngineeringDocumentStore`
directly (proving the "no second storage mechanism" claim, not merely
asserting it). Full detail: `WP7.0C Testing Strategy.md`.

**Academy Requirements Summary.** No new concept guide required beyond
what the Data Model's own guide already teaches — Materials is
presented as a worked example of building *on* the Data Model, not a
new pattern in its own right. Full detail: `WP7.0C Academy Plan.md`.

---

## 4. Engineering Calculation Framework *(`Tempest.Core.Calculations`, proposed — `FCR-0032`)*

**Purpose.** A shared calculation/formula execution model, mirroring
the Command Framework's own "one dispatch mechanism, not reinvented per
consumer" precedent (`ADR-0037`/`ADR-0038`).

**Responsibilities.** Register a calculation definition by Id; dispatch
a calculation request by Id with a typed input, producing a typed
result and a durable record of what was calculated, by whom, and when.
Does **not** itself provide any concrete calculation (a structural
load formula, an HVAC sizing formula) — those are supplied entirely by
each registering consumer, mirroring how `ICommandRegistry` supplies no
command logic of its own.

**Public Interfaces.**

```csharp
namespace Tempest.Core.Calculations;

/// <summary>A single, registrable calculation, taking <typeparamref name="TInput"/> and producing <typeparamref name="TResult"/>.</summary>
public interface ICalculationDefinition<TInput, TResult>
{
    string CalculationId { get; }

    /// <summary>Performs the calculation. Must be a pure function of <paramref name="input"/> — no I/O, no shared mutable state.</summary>
    /// <exception cref="CalculationInputInvalidException"><paramref name="input"/> fails this calculation's own validation.</exception>
    TResult Calculate(TInput input);
}

/// <summary>Registers and dispatches calculations by Id, recording each execution.</summary>
public interface ICalculationEngine
{
    /// <summary>Registers a calculation definition. Expected to be called only during module initialisation, mirroring <c>ICommandRegistry.RegisterDescriptor</c>.</summary>
    /// <exception cref="DuplicateCalculationException"><paramref name="definition"/>'s own <c>CalculationId</c> is already registered.</exception>
    void RegisterDefinition<TInput, TResult>(ICalculationDefinition<TInput, TResult> definition);

    /// <summary>Executes the named calculation and records the result.</summary>
    /// <exception cref="CalculationDefinitionNotFoundException"><paramref name="calculationId"/> is not registered.</exception>
    /// <exception cref="CalculationInputInvalidException">The registered definition rejected <paramref name="input"/>.</exception>
    Task<CalculationRecord<TResult>> ExecuteAsync<TInput, TResult>(string calculationId, TInput input, CancellationToken cancellationToken = default);
}

/// <summary>An immutable record of one calculation's execution.</summary>
public sealed record CalculationRecord<TResult>(string CalculationId, TResult Result, DateTimeOffset ExecutedAt, string ExecutedByPrincipalId);

public abstract class CalculationException : Exception { /* ... */ }
public sealed class DuplicateCalculationException : CalculationException { /* ... */ }
public sealed class CalculationDefinitionNotFoundException : CalculationException { /* ... */ }
public sealed class CalculationInputInvalidException : CalculationException { /* ... */ }
```

**Primary Entities.** `ICalculationDefinition<TInput, TResult>` (the
registered formula), `CalculationRecord<TResult>` (an immutable
execution record).

**Dependency Rules.** Depends on `Tempest.Core.UnitsAndQuantities`
(`FCR-0030`) only in the sense that `TInput`/`TResult` are *expected*,
by convention, to be `Quantity<TDimension>`-based where the calculation
is dimensioned — not a hard type constraint, since a purely
dimensionless calculation is legitimate too. No dependency on the Data
Model (`FCR-0029`) is required for the core dispatch mechanism itself;
a future integration recording `CalculationRecord<TResult>` as an
`IEngineeringDocument` revision is a plausible, not mandatory,
extension (see `WP7.0C Platform Integration Matrix.md`).

**Platform Services Consumed.** Identity & Permissions
(`ExecutedByPrincipalId` attribution); Audit (a plausible consumer
relationship — recording that a calculation occurred, distinct from the
calculation record itself, mirroring how the REST API's own dispatch is
independently audited without duplicating Command Framework's own
state).

**Expected Future Consumers.** Any Mechanical/Structural/Electrical/
Building Services-HVAC capability, once identified — each would
register its own domain-specific calculations against this one shared
dispatch mechanism rather than inventing its own.

**Lifetime.** DI-public, container-constructed singleton, mirroring
`ICommandRegistry`'s own registration shape.

**Thread-Safety Expectations.** `RegisterDefinition` expected to be
called only during module initialisation (single-threaded by
construction, mirroring `ICommandRegistry`); `ExecuteAsync` must be safe
for concurrent invocation once registration is complete, and — since
`ICalculationDefinition.Calculate` is required to be a pure function —
concurrent execution of the *same* calculation Id with different inputs
is inherently safe without additional synchronization, a genuine
architectural benefit of the purity requirement worth stating
explicitly.

**Error Handling.** `CalculationDefinitionNotFoundException` for an
unregistered Id, mirroring `CommandNotFoundException`/
`ReportDefinitionNotFoundException`'s own precedent.
`CalculationInputInvalidException` propagates from the registered
definition's own validation, never swallowed — mirroring Reporting's
own "a renderer's failure propagates unmodified" philosophy (`ADR-0038`
lineage).

**Extension Points.** No constraint on `TInput`/`TResult` beyond the
purity expectation on `Calculate` itself — a future consumer may use
primitive types, `Quantity<TDimension>`, or its own richer types.
Whether purity should be enforced at compile time (impossible in C#
without a dedicated analyzer) or only documented as a convention is an
open question (see `WP7.0C Required ADR Catalogue.md`).

**Testing Strategy Summary.** Registration/dispatch round-trip;
duplicate-registration rejection; concurrent-execution safety for a
single registered, genuinely pure calculation; failure-path propagation
for a calculation that throws `CalculationInputInvalidException`. Full
detail: `WP7.0C Testing Strategy.md`.

**Academy Requirements Summary.** A new concept guide teaching the
calculation-dispatch pattern as a Command-Framework-adjacent but
distinct abstraction (a calculation is a pure function producing a
value; a command is an imperative action with a success/failure
result) — the distinction is worth its own worked comparison, mirroring
how this project has repeatedly distinguished structurally similar
pairs (Command Framework vs. Event Bus; Notifications vs. Event Bus).
Full detail: `WP7.0C Academy Plan.md`.

---

## 5. Verification & Validation Framework *(`Tempest.Core.Verification`, proposed — `FCR-0033`)*

**Purpose.** A cross-cutting mechanism for recording a pass/fail/
conditional verification outcome against a requirement or specification
— distinct from Audit (who did what) and from a calculation record
(what was computed).

**Responsibilities.** Record a verification outcome against a document
Id (expected, by convention, to be a document of `Kind = "Requirement"`
or an equivalent discipline-specific kind, **not** a hard dependency on
`FCR-0027`'s own not-yet-designed Requirements Engine service — see
Dependency Rules); enumerate the verification history for a given
document. Does **not** itself define what "a requirement" is, does not
provide requirements management or traceability beyond the single
document-Id reference it records against (that remains `FCR-0027`'s own
future scope).

**Public Interfaces.**

```csharp
namespace Tempest.Core.Verification;

public enum VerificationOutcome
{
    Pass,
    Fail,
    Conditional
}

/// <summary>Records and retrieves verification outcomes against a document (typically, but not necessarily, a requirement).</summary>
public interface IVerificationService
{
    /// <summary>Records a verification outcome against <paramref name="subjectDocumentId"/>.</summary>
    /// <exception cref="EngineeringData.EngineeringDocumentNotFoundException"><paramref name="subjectDocumentId"/> does not exist.</exception>
    Task<IVerificationRecord> RecordAsync(Guid subjectDocumentId, VerificationOutcome outcome, string method, string? evidence, CancellationToken cancellationToken = default);

    /// <summary>Every verification recorded against <paramref name="subjectDocumentId"/>, oldest first. Never <see langword="null"/>.</summary>
    Task<IReadOnlyList<IVerificationRecord>> GetVerificationHistoryAsync(Guid subjectDocumentId, CancellationToken cancellationToken = default);
}

public interface IVerificationRecord
{
    Guid SubjectDocumentId { get; }
    VerificationOutcome Outcome { get; }
    string Method { get; }
    string? Evidence { get; }
    string VerifiedByPrincipalId { get; }
    DateTimeOffset VerifiedAt { get; }
}
```

**Primary Entities.** `IVerificationRecord` (an immutable outcome record),
`VerificationOutcome` (Pass/Fail/Conditional).

**Dependency Rules.** Depends on `Tempest.Core.EngineeringData`
(`FCR-0029`) directly — `subjectDocumentId` must reference a real
document. **Does not depend on `FCR-0027` (Requirements Engine) as a
service** — this is a deliberate clarification this Work Package makes
that `WP 7.0B`'s own dependency graph left ambiguous: Verification
depends on the Data Model's generic document concept, not on a specific,
not-yet-designed Requirements Engine. This removes a plausible-looking
circular dependency (Requirements needs Verification; Verification
needs Requirements) that would otherwise exist if Verification were
built directly against a future `IRequirementsService` instead of the
shared, generic document store.

**Platform Services Consumed.** Identity & Permissions
(`VerifiedByPrincipalId`, permission-gated read access, mirroring
`IAuditQuery`'s own gating pattern); Audit (a plausible, not mandatory,
cross-service integration — a verification action could itself be
audited via `IAuditRecorder` at the calling layer, mirroring how every
`v0.6.0` sample module already demonstrates permission-check-then-audit-
record as a calling-layer pattern, never inside the service itself).

**Expected Future Consumers.** `FCR-0027` (Requirements Engine, once
designed); any Quality-discipline capability, once identified
(inspection/non-conformance workflows would naturally consume
verification history).

**Lifetime.** DI-public, container-constructed singleton.

**Thread-Safety Expectations.** Safe for concurrent invocation,
mirroring `IAuditRecorder`'s own requirement — multiple verifications
against different (or the same) subject document may occur
concurrently.

**Error Handling.** `EngineeringDocumentNotFoundException` (reused from
`Tempest.Core.EngineeringData`, not a duplicate exception type) for a
non-existent `subjectDocumentId` — deliberately reusing the Data
Model's own exception rather than inventing a parallel
`VerificationSubjectNotFoundException`, since the failure's true cause
is the Data Model layer, not Verification itself.

**Extension Points.** No fixed vocabulary for `method` (the verification
method — inspection, test, analysis, demonstration, in the sense
Systems Engineering practice commonly uses these four terms) is
enforced; `string` is deliberately open rather than a closed enum, so a
future consumer is not blocked by a vocabulary this framework does not
yet have grounds to fix (see `WP7.0C Engineering Standards Mapping.md`).

**Testing Strategy Summary.** Record/retrieve round-trip; permission
gating on `GetVerificationHistoryAsync`, mirroring `IAuditQuery`'s own
test shape; failure when `subjectDocumentId` does not exist. Full
detail: `WP7.0C Testing Strategy.md`.

**Academy Requirements Summary.** A new concept guide distinguishing
Verification from Audit and from a Calculation Record — three
structurally similar "record what happened" types with genuinely
different semantics, mirroring this project's own repeated practice of
explicitly distinguishing structurally similar pairs. Full detail:
`WP7.0C Academy Plan.md`.

## Related Documents

`docs/governance/Future Capability Register.md` (`FCR-0029`–`FCR-0033`);
`WP7.0B Engineering Foundation Architecture.md`; `WP7.0C Cross-Framework
Dependency Report.md`; `WP7.0C Required ADR Catalogue.md`; `WP7.0C
Testing Strategy.md`; `WP7.0C Academy Plan.md`.
