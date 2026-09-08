# WP 7.1D — Engineering Calculation Framework — Security Review Report

## Purpose

This Work Package's own controlling instruction required a proportionate
security review, distinct from the Engineering Review — this is the
first Engineering Foundation Work Package to require one explicitly.
Every category the controlling instruction named is reviewed below
against the real, committed implementation, classified as **Not
Applicable**, **Accepted Risk**, **Technical Debt**, or **Release
Blocking**, per that instruction's own required vocabulary.

## Review

| Category | Finding | Classification |
|---|---|---|
| **Input validation** | Every framework-owned parameter is validated (`calculationId`, `definition`, `CalculationContext`'s own method arguments all null/whitespace-checked). `TInput` itself is deliberately **not** validated by the framework — that is each registered definition's own responsibility, mirroring how the Command Framework validates no command's own payload either. | Not Applicable for framework-owned parameters (validated); Accepted Risk for `TInput` (validation is the registering consumer's own responsibility, by design — `ICalculationDefinition` "does not itself provide any concrete calculation") |
| **Exception disclosure** | `CalculationInputInvalidException`'s own message is authored by the registering definition, which may choose to echo the rejected input value (this Work Package's own `ThrowingBelowZeroCalculation` test fixture does exactly this). No sensitive data is echoed by any exception this framework itself constructs. | Accepted Risk — mirrors `EngineeringDocumentNotFoundException`'s own existing convention of embedding the failing identifier in its message; a definition author choosing to embed a genuinely sensitive input value in an exception message is the same class of risk any first-party code embedding data in a log or exception message already carries platform-wide |
| **Serialization safety** | `CalculationRecordDto<TResult>` is only ever **serialized**, never deserialized from untrusted input — `TResult` and `CalculationIntermediateResult.Value` are compile-time-known, first-party types at every call site. No polymorphic or type-name-based deserialization occurs anywhere in this framework. | Not Applicable — no deserialization of untrusted data occurs |
| **Thread safety** | Definition registration uses `ConcurrentDictionary.TryAdd` (atomic); `ExecuteAsync` constructs a fresh, non-shared `CalculationContext` per call. Confirmed by direct inspection and by `ExecuteAsync_ConcurrentDifferentInputs_SamePureCalculation_AllProduceCorrectResults` (30 concurrent executions, zero cross-contamination). | Not Applicable — reviewed, no gap found |
| **Concurrency correctness** | Same evidence as Thread Safety, above — purity of `Calculate` is what makes concurrent execution of the same Id, different inputs, safe without additional synchronization, exactly as `WP7.0C Engineering Foundation Contracts.md` itself predicted. | Not Applicable — reviewed, no gap found |
| **Resource exhaustion** | No cancellation reaches into `Calculate` once it has started — `Calculate(TInput, CalculationContext)` carries no `CancellationToken` (matching the approved contract's own signature, which had none either). A definition that loops indefinitely or blocks cannot be cancelled by the caller. `CalculationContext` also imposes no upper bound on how many intermediate results, constraint checks, or material references a single execution may record. | **Technical Debt** (`TD-21`, `TD-22`) — disclosed, not blocking, since calculation definitions are trusted, first-party, in-process code (the same trust boundary the Command Framework and every other registry-pattern service in this platform already operates under), not externally-supplied untrusted code |
| **Denial-of-service opportunities** | Registration itself has no upper bound on the number of calculations that may be registered — mirrors `ICommandRegistry`'s own identical, already-accepted registration-time trust model. No network-facing surface exists for this framework in this Work Package's own scope (no REST endpoint). | Accepted Risk — matches this platform's own existing, already-reviewed registration-time trust assumption; revisit if a future Work Package exposes calculation execution over a network boundary (see Future Capability Recommendations) |
| **Data integrity** | Every `CalculationRecord<TResult>` is stored as an immutable, append-only `IEngineeringDocument` revision — the same integrity guarantee `WP 7.1A`'s own Engineering Review already established for the Data Model generally. This Work Package introduces no new write path that could corrupt or partially write a record. | Not Applicable — inherited, already-reviewed guarantee |
| **Tamper resistance** | No cryptographic signing of stored calculation records exists, mirroring the platform's own existing, disclosed trust model (`TD-16`'s own identical disclosure for license files) — a local, trusted-file-system assumption already accepted platform-wide, not a new gap this Work Package introduces. | Not Applicable — inherits an already-disclosed, platform-wide trust boundary |
| **Trust boundaries** | Calculation definitions are registered only by trusted, in-process, first-party (or first-party-vetted-plugin) code during module initialisation — the identical trust boundary `ICommandRegistry.RegisterDescriptor` already operates under. No external or network caller can register or dispatch a calculation in this Work Package's own scope. | Not Applicable — matches an already-established, platform-wide trust boundary |
| **Unsafe assumptions** | The type-erased registration dictionary (`ConcurrentDictionary<string, object>`) is read back via a safe C# type pattern (`boxed is ICalculationDefinition<TInput, TResult> definition`), never an unsafe or forced cast — a signature mismatch fails gracefully into `CalculationDefinitionNotFoundException`, proven by `ExecuteAsync_MismatchedSignature_ThrowsCalculationDefinitionNotFoundException`, never an `InvalidCastException` leaking an internal implementation detail. | Not Applicable — reviewed, no gap found |
| **Dependency risk** | No new third-party dependency was introduced — only `System.Text.Json` and `System.Collections.Concurrent`, both already used extensively elsewhere in `Tempest.Core`. | Not Applicable |
| **Supply-chain considerations** | No new dependency, therefore no new supply-chain surface. | Not Applicable |
| **Secure defaults** | `ExecutedByPrincipalId` defaults to an honest `"unknown"` sentinel when no principal is established (never silently omitted, never spoofed) — mirroring `EngineeringDocumentStore`'s and `MaterialCatalog`'s own identical, already-reviewed pattern. `CalculationValidationOutcome` defaults toward `Conditional`, never silently upgrading to `Valid`, the moment any recorded constraint check fails. | Not Applicable — reviewed, secure by construction |
| **Backwards compatibility risks** | `Tempest.Core.Calculations` is a brand-new namespace with zero existing consumers — the `Calculate` signature change relative to `WP7.0C`'s own illustrative proposal (never compiled or shipped code) carries no backward-compatibility impact. | Not Applicable |

## New Debt Disclosed by This Review

### TD-21 — No Cancellation Reaches Into `Calculate` Once Execution Has Started

**What.** `ICalculationDefinition<TInput, TResult>.Calculate` carries no
`CancellationToken` — a long-running or blocking definition cannot be
cancelled by `ExecuteAsync`'s own caller once dispatch has begun.

**Revisit trigger.** A real, demonstrated need for cancelling an
in-flight calculation (a genuinely long-running definition, or exposure
to a caller that needs cooperative cancellation).

### TD-22 — `CalculationContext` Imposes No Bound on Recorded Data Volume or Type Fidelity

**What.** A definition may record an unbounded number of intermediate
results, constraint checks, or material references in a single
execution, with no framework-enforced limit; separately,
`CalculationIntermediateResult.Value` (a boxed value, like
`Materials.MaterialProperty.Value`) is not guaranteed to deserialize
back to its exact original CLR type if a future consumer reads a stored
calculation record's own JSON content directly — it is fully
inspectable from the in-memory record `ExecuteAsync` returns
immediately, this Work Package's own primary use case, but not
guaranteed durable-round-trip type fidelity for every possible CLR
type a future definition might choose.

**Revisit trigger.** A real, demonstrated need to bound recorded data
volume (a definition found to record unbounded data), or a real need to
deserialize intermediate results back into their original CLR type from
durable storage (no current consumer does this).

## New Accepted Trade-off Disclosed by This Review

### AT-16 — No Dependency on Materials for Material-Reference Validation

**What.** `CalculationContext.ReferenceMaterial` accepts any string —
this framework does not verify the referenced `materialId` actually
exists in `Tempest.Core.Materials`, and has no dependency on it at all.

**Why this is a trade-off, not debt.** The approved contract does not
require a hard Materials dependency for Calculation (`ADR-0056` Decision
6); an open, unvalidated reference mirrors
`EngineeringData.DocumentReference.RelationshipKind`'s own established,
already-reviewed precedent.

**Revisit trigger.** A real, demonstrated need for the Calculation
Framework itself to validate material references, rather than trusting
the calling definition.

## Future Capability Register Entry Raised

`FCR-0035` (Calculation Execution Timeout & Cancellation Support) —
raised directly from `TD-21`, above; see `WP7.1D Future Capability
Recommendations.md`.

## Verdict

**No Release Blocking finding.** Two new, disclosed Technical Debt items
(`TD-21`, `TD-22`) and one new, disclosed Accepted Trade-off (`AT-16`),
both proportionate to a first-implementation framework whose own
registering consumers remain first-party, trusted, in-process code — no
externally-facing attack surface exists for `Tempest.Core.Calculations`
in this Work Package's own scope. No speculative security feature was
implemented — every finding above is either already mitigated by an
existing, reviewed platform convention, or disclosed as debt/trade-off
with a concrete, evidence-based revisit trigger, never built ahead of a
real demonstrated need.

## Related Documents

`WP7.1D Implementation Report.md`; `ADR-0056`; `docs/governance/Quality/
Technical Debt Register.md` (`TD-21`, `TD-22`, `AT-16`);
`docs/governance/Future Capability Register.md` (`FCR-0035`).
