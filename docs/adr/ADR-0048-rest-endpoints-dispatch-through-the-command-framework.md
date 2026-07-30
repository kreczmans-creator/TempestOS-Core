# ADR-0048: REST Endpoints Dispatch Through the Existing Command Framework

## Status

Accepted — `WP 6.3` (REST API), 2026-07-29.

## Context

`v0.6.0`'s own architecture package anticipated this decision but
deliberately left it unratified pending `WP 6.3`'s own implementation
phase. `Required ADRs.md` named this Work Package's own required ADR:
without an explicit decision, a REST endpoint implementation could
easily grow its own request-handling logic directly, duplicating what
the Command Framework already does.

## Decision

**Every REST route registered via `IApiEndpointRegistry.MapCommand`
dispatches through the existing, unmodified `ICommandRegistry.InvokeAsync`**
— realising the Command Framework's own original design intent
(`Command Framework Architecture.md`: "...or a future automation/AI
service") exactly as anticipated. `ApiRequestHandler` — the REST API's
own request-handling pipeline — contains route lookup, identity
resolution, and permission enforcement, but never a single line of
business logic; the command's own registered handler is the only place
business logic legitimately lives, per this Work Package's own Design
Principles ("no business logic shall exist inside controllers/
endpoints").

**`ApiSampleModule`, this Work Package's own reference module, proves
this with zero business logic of its own whatsoever** — it maps one
route (`POST /api/v1/sample-report`) directly to
`ReportingSampleModule.GenerateSampleReportCommandId`, a command already
registered by a different module (`WP 6.0`). The entire module is one
`MapCommand` call. This is a deliberate, disclosed departure from every
prior sample module's own "independently usable" convention — see this
Work Package's own Platform Integration Demonstration — because the
REST API's own domain purpose is to expose already-registered platform
capability over HTTP, never to define its own.

**`ICommandRegistry.InvokeAsync` dispatches only the command's own
default instance** (`CommandDescriptor.CreateDefault`) — `MapCommand`'s
own approved signature (`method, path, commandId, requiredPermission`)
carries no request-body-to-command-parameter binding mechanism, and
none was added. A REST caller's inbound request body or query string is
not threaded into the invoked command in this release. This is a
deliberate, minimal first pass, not an oversight — see this Work
Package's own Future Capability Recommendations.

## Consequences

**Positive:**

- No second, REST-specific invocation mechanism exists anywhere in this
  codebase — a menu click, a keyboard shortcut, and a REST request all
  converge on the identical `ICommandRegistry.InvokeAsync` call,
  exactly as `ADR-0036`/`ADR-0037` originally intended.
- `ApiRequestHandler` is trivially unit-testable in isolation from
  Kestrel/ASP.NET Core entirely, since it depends only on
  `IApiEndpointRegistry`, `ICommandRegistry`, `IIdentityService`,
  `IPermissionEvaluator`, and `IAuditRecorder` — all in-process
  interfaces, none of them HTTP-specific.

**Negative:**

- Without request-parameter binding, every REST-exposed command this
  release must be invocable via its own parameterless
  `CreateDefault` factory — a REST route cannot yet accept a caller-
  supplied value (a query parameter, a JSON body) and thread it into
  the command it invokes. This narrows what a REST endpoint can
  usefully expose until a future Work Package adds parameter binding.

## Alternatives Considered

**REST endpoints calling application/domain logic directly, bypassing
the Command Framework.** Rejected per `Required ADRs.md`'s own
anticipated decision — this would create two parallel, divergent
invocation paths (menu/toolbar/keyboard-shortcut-originated commands
vs. REST-originated calls) for what should be the same underlying
operation, undermining the very uniformity `ADR-0036`/`ADR-0037`
established.

**Adding request-body/query-parameter binding to `MapCommand` or
`ICommandRegistry.InvokeAsync` in this pass.** Rejected as scope beyond
what `Public Interface Catalogue.md`'s own approved
`IApiEndpointRegistry` signature defines; a genuine future need, named
explicitly in this Work Package's own Future Capability Recommendations,
not designed speculatively now.

**Having `ApiSampleModule` register its own, dedicated command instead
of depending on `ReportingSampleModule`'s.** Considered, to preserve
every prior sample module's own "independently usable" convention —
rejected because it would forfeit the single clearest possible proof
that the REST layer itself contains no business logic: exposing a
command already fully exercising Identity, Settings, Audit, and
Notifications (`WP 6.0`) demonstrates the design principle more
convincingly than a new, REST-specific command ever could.

## Related Documents

`docs/releases/v0.6.0/Required ADRs.md` (this decision's own anticipated
form); `Platform Service Contracts.md` (the REST API's own contract);
`ADR-0036`/`ADR-0037`/`ADR-0038` (the Command Framework's own design
this decision realises); `Command Framework Architecture.md`; `WP6.3
Platform Integration Demonstration.md`; `WP6.3 Future Capability
Recommendations.md`; `docs/academy/03 Work
Packages/WP6.3-rest-api-implementation.md`.
