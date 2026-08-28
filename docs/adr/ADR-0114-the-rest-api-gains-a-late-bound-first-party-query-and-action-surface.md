# ADR-0114: The REST API Gains a Late-Bound, First-Party Query-and-Action Surface

## Status

Accepted — `WP 14.0A` (TempestOS Companion — Mobile Companion
Application), 2026-08-28.

Extends `ADR-0048` (REST endpoints dispatch through the Command
Framework) exactly along the evolution that ADR's own scope note
anticipated ("a REST caller's inbound request body or query string is not
threaded into the invoked command *in this release*"), and applies
`ADR-0063`'s standing read/mutation rule at the API boundary. `ADR-0047`
(hosted service), `ADR-0049` (Kestrel), and `ADR-0052` (identity
resolution) are unchanged.

## Context

The REST API shipped by `WP 6.3` is a route-to-command trigger surface:
`IApiEndpointRegistry.MapCommand` maps a method + path to a zero-argument
command invoked via `ICommandRegistry.InvokeAsync`, and the response is a
status code plus `CommandResult.Message` as plain text. Two structural
gaps blocked any remote client that must *display* platform state:

1. **No reads.** `CommandResult` carries no payload; no route can return
   a project, a Cockpit summary, or any JSON at all. `ADR-0063` already
   resolves this tension inside the process — Workspace views read
   directly through read models, and only mutations dispatch through the
   Command Framework — but the API had no expression of that rule.
2. **A start-time route snapshot.** `RestApiHostedService` maps
   registered routes into Kestrel when it starts (Host Phase 8.1).
   The Engineering Workspace's read models — the very data a Companion
   client needs — first exist only *after* the Host is running
   (`EngineeringWorkspaceComposer.RegisterEngineeringDisciplines`'s own
   documented precondition), so nothing composed at the Workspace layer
   could ever register a route in time.

## Decision

**`Tempest.Core.Api` gains `IApiQueryRegistry` — a second, late-bound
route registry with two verbs: `MapQuery` (a `GET` route serving a
complete JSON body produced by a registered delegate) and `MapAction` (a
`POST` route whose delegate binds the request body to a typed
`ICommand` and dispatches it through the existing, unmodified Command
Framework). `RestApiHostedService` serves the whole surface through one
catch-all fallback route resolved against the registry per request, at
request time.**

### Reads read; mutations still dispatch

A query is `ADR-0063` applied at the API boundary: a read-only
projection, produced by the registering layer (which owns both the data
access and the JSON shape), never a command. An action is `ADR-0048`'s
anticipated completion: the registering layer's delegate deserializes the
body, constructs the *existing* typed command, and dispatches through
`ICommandDispatcher` — the identical handler every in-process caller
runs. The API layer itself interprets neither; it stores and serves
delegates.

### One pipeline discipline, one new handler

`ApiQueryRequestHandler` mirrors `ApiRequestHandler` step for step —
request-time route lookup, `X-Identity-Id` resolution via the pure
`IIdentityService.GetPrincipal` (never the ambient principal,
`ADR-0052`), per-route `Permission` enforcement, an `api.request` audit
record with the caller in `CallerIdentityId`, and the same
failure-mapping vocabulary — extended by exactly one case: a binding
fault (`ApiRequestBindingException`, or `JsonException` from the binder)
maps to `400` with the binder's own message, because a malformed body is
a caller-correctable input fault, not a `500`-class platform fault.
Internal exception detail is logged and never leaked, unchanged.

### Late binding via one catch-all fallback

Kestrel maps statically registered command routes exactly as before, plus
one `MapFallback` — the lowest routing precedence by definition, so
existing routes always win. The fallback consults `IApiQueryRegistry` at
request time, which makes post-start registration (the Workspace
composition root's only option) fully supported rather than silently
ignored. An unknown path behaves as before: `404`.

### First-registration-wins, DI-public, OpenAPI-visible

`ApiQueryRegistry` is a Phase 6 singleton beside `IApiEndpointRegistry`,
keyed `"METHOD path"` ordinal-case-insensitively with duplicate
registration rejected via the existing `DuplicateApiRouteException` —
except that its lock is load-bearing (registration is legal from any
phase, concurrently with requests). `OpenApiDocumentGenerator` describes
query/action routes alongside command routes.

## Consequences

**Positive:**

- Remote clients can finally *read* platform state — through projections
  the owning layer defines, not through a generic data-exposure endpoint
  that would bypass domain boundaries.
- Mutations cannot bypass the Command Framework: the only thing an action
  can do with its body is build a command and dispatch it.
- The Workspace layer registers routes at the only time it exists —
  after Host start — with no change to Host phases, hosted-service
  ordering, or `IApiEndpointRegistry`'s existing contract and consumers.
- The pipeline discipline (identity, permission, audit, error mapping) is
  identical across both surfaces, so a security reviewer audits one
  model, not two.

**Negative:**

- Two registries now describe the API surface; a reader must consult both
  (the OpenAPI document merges them, mitigating this).
- A query delegate runs arbitrary read code per request with no caching
  or rate limiting — acceptable at the API's current loopback-only
  exposure (`TD-14`), and a named revisit item for any future off-box
  exposure (`TD-58`).
- `RestApiHostedService`'s constructor gained a required
  `IApiQueryRegistry` parameter — its direct test constructions were
  updated in the same change.

## Alternatives Considered

**Extending `CommandResult` with a payload field.** Rejected: every
command everywhere would carry a presentation concern; reads would
masquerade as mutations, inverting `ADR-0063`; and the frozen
`CommandResult` shape (`Succeeded`/`Message`) is consumed platform-wide.

**Registering the Workspace's routes into `IApiEndpointRegistry` before
Host start.** Rejected: the Workspace read models do not exist before
Host start — registration would need lazy indirection to services that
cannot be resolved yet, moving the late binding somewhere less honest
rather than removing it.

**A discovered module that maps Companion routes during Module
Initialisation.** Rejected: module discovery scans loaded assemblies, so
a module in `Tempest.App` would activate in every test host that loads
the assembly, perturbing discovery-count expectations platform-wide; and
it still could not reach the Workspace read models, which outlive module
initialisation.

**Path-templated REST resources (`/projects/{id}`) with a generic
serializer.** Rejected: the existing API deliberately has no path
templates or model binding; introducing a generic resource framework for
one client is exactly the "silently invent APIs" scope creep the
commissioning brief prohibits. The delegate registry keeps each route's
shape owned by the layer that owns the data.

## Related Documents

`ADR-0047`, `ADR-0048`, `ADR-0049`, `ADR-0052` (the existing REST API),
`ADR-0063` (reads vs mutations), `ADR-0044` (permission enforcement),
`ADR-0113` (the Companion client this surface exists for), `ADR-0115`
(the client-side freshness model over this surface),
`docs/architecture/TempestOS Companion Architecture.md`,
`docs/architecture/Platform Service Map.md` (REST API section),
`tests/Tempest.Core.Tests/Api/ApiQueryRegistryTests.cs`,
`ApiQueryRequestHandlerTests.cs`, and `ApiQueryHttpTests.cs` (the
surface's own proof).
