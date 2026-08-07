# ADR-0086: `CalculationTemplateRegistry` Is a Workspace-Layer, JSON-Marshalled Type-Erasure Adapter Over `ICalculationEngine` — Never a Domain-Layer Registry

## Status

Accepted — `v0.9.0` "Mechanical Foundation", `WP 9.2A` (Engineering Calculations Workspace), 2026-08-05.

## Context

`WP 9.2A`'s own controlling instruction requires a complete Engineering Calculations experience — Execute/Recalculate over Calculation Templates, full Workspace integration (Explorer tree, Property Inspector, Command Palette), and five representative engineering calculations — over the already-real `ICalculationEngine`/`ICalculationDefinition<TInput,TResult>` framework (`WP 7.1D`). Its own explicit constraints match every prior real-discipline Work Package's own: "No architectural redesign. No contract redesign. No duplicate framework."

`ICalculationEngine.ExecuteAsync<TInput,TResult>` is generic over each Template's own input/output shape — by design (`ADR-0056`), dispatched internally via a type-erased `ConcurrentDictionary<string, object>` keyed by `CalculationId`. A Workspace command's own shape, by contrast, is fixed at compile time; it cannot itself carry an open-ended `TInput`/`TResult` type parameter for an arbitrary, future-registered Template. `WP 9.2A`'s own five representative Templates already have five different `TInput`/`TResult` pairs, and nothing in the Calculation Framework's own design closes the set — a future module may register a sixth, with its own, different shape, without any change to the Calculation Framework itself.

`ICalculationEngine` also has no "list every registered definition" method — by original `WP 7.1D` design, mirroring the Command Framework's own registration-only-at-module-init-time model. The Explorer/Property Inspector still need a Template catalogue to browse (this Work Package's own "Calculation Templates" scope item), and `WP8.2B Dependency Rules.md` §8 proposes no Domain-layer registry contract for this class of need — the same constraint `ADR-0079` (Engineering Domain object factories) and `MechanicalObjectFactoryRegistry` (`WP 9.0A`) already navigated for a structurally identical problem.

## Decision

**A new, `Tempest.App`-only type, `CalculationTemplateRegistry`, is the single point where the Workspace layer knows how to dispatch any registered Calculation Template generically:**

- Each Template is wrapped by one `CalculationTemplateAdapter<TInput,TResult>` (a `private sealed` nested type — the only place `TInput`/`TResult` are ever statically known), registered into a `Dictionary<string, ICalculationTemplateAdapter>` keyed by `CalculationId`, plus a second index by a registry-local, synthetic `Guid` (Templates have no Domain identity of their own to be addressed by).
- `ICalculationTemplateAdapter.ExecuteAsync` deserializes a caller-supplied JSON string into `TInput` (`System.Text.Json.JsonSerializer.Deserialize<TInput>`), calls the real, unmodified `ICalculationEngine.ExecuteAsync<TInput,TResult>`, and serializes the result back to JSON for display — the identical type-erasure principle `CalculationEngine` itself already uses internally, applied one layer higher, entirely inside `Tempest.App`.
- `CalculationTemplateRegistry.ExecuteAsync` additionally links the caller-supplied target Domain object to the resulting record via the existing `"calculatedBy"` relationship kind (already mapped to `RelationshipCategory.Calculation` — `WP 8.2A`/`WP 8.2B`, unchanged) — the one, real integration point connecting the Calculation Framework's own evidentiary output back to a real `Calculation` Domain object, built entirely additively, in neither framework's own code.
- `ExecuteCalculationCommand`/`RecalculateCalculationCommand` are consequently one non-generic command pair, dispatching to any registered Template by `CalculationId` string — never one hand-written command per Template.
- Each of `WP 9.2A`'s own five representative Templates is registered into `CalculationTemplateRegistry` by `CalculationsWorkspaceRegistration.Register`, reading the already-registered definition's own `Metadata` directly (a throwaway instance construction, since every definition is a small, stateless class) — never a Domain-layer "list every registered definition" call, which does not exist and is not added.

## Consequences

**Positive:**

- `ICalculationEngine`/`ICalculationDefinition<TInput,TResult>`/`CalculationRecord<TResult>` are completely unchanged — zero Domain-layer risk.
- Any future module registering a new Calculation Template needs only to also register it with `CalculationTemplateRegistry` (one line, in its own Workspace registration) to become Execute/Recalculate/Explorer/Property-Inspector-visible — no change to `Tempest.App.Workspace.Calculations` itself is required per new Template.
- The JSON boundary is the same one `IEngineeringDocumentStore.IDocumentRevision.Content` already uses platform-wide — no new marshalling technique introduced.

**Negative:**

- A malformed or mismatched-shape JSON input surfaces only at execution time, as a `CalculationInputInvalidException` → `CommandResult.Failure` — never validated ahead of time against a schema. Judged acceptable: the underlying `ICalculationDefinition.Calculate` already validates its own input at that same point; this adds one deserialisation step in front of an already-present validation boundary, not a second, independent risk.
- `CalculationTemplateRegistry`'s own registry-local `Guid` per Template exists only in memory, rebuilt fresh on every process start — a Template's own Explorer/Property-Inspector node Id is not stable across restarts. Judged acceptable: Templates are registered fresh at every module-initialisation, exactly like `ICommandRegistry`'s own descriptors, which carry the identical characteristic.

## Alternatives Considered

**One hand-written Workspace command per Calculation Template** — considered and rejected; does not scale past this Work Package's own five Templates, and requires a `Tempest.App` change for every future Template a module wishes to register.

**A Domain-layer "list every registered definition" method on `ICalculationEngine`** — considered and rejected; `WP8.2B Dependency Rules.md` §8 proposes no such registry contract, and adding one to serve only the Workspace's own display/dispatch need would itself be the "contract redesign" this Work Package's own controlling instruction forbids. `MechanicalObjectFactoryRegistry`'s own precedent (`WP 9.0A`) already established that a Workspace-layer registry is the correct answer to this exact class of need.

**Reflection-based dynamic dispatch instead of an explicit adapter map** — considered and rejected; would obscure, rather than make explicit, exactly which Templates the Workspace can dispatch to, and would not meaningfully reduce the code already required to register each Template's own `TInput`/`TResult` pair.

## Related Documents

`ADR-0056`; `ADR-0079`; `WP8.2B Dependency Rules.md`; `WP7.1D-engineering-calculation-framework-implementation.md`; `WP9.2A Implementation Report.md`; `WP9.2A Architecture Conformance Review.md`; `src/Tempest.App/Workspace/Calculations/CalculationTemplateRegistry.cs`; `src/Tempest.Core/Calculations/ICalculationEngine.cs`.
