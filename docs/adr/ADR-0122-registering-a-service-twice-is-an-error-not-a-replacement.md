# ADR-0122: Registering a Service Twice Is an Error, Not a Replacement

## Status

Accepted — `WP 16.4B-R1` (Architecture), 2026-09-05. Records a decision
taken during `WP 16.4B` and written here after the `v0.16.0` review board
found it undocumented; Engineering Governance §5 criteria 1 and 3 both
apply, since a real alternative (the prior behaviour) was considered and
rejected, and the outcome sets a convention every future registration call
site must follow. Closes the architectural half of `TD-69`. Serves the
`v1.0.0` scope `D-021` (**Proposed**, `WP 16.0A`) governs.

## Context

`ServiceCollection.Add` and `AddInstance` wrote straight into a dictionary
keyed by service type:

```csharp
_descriptorsByType[serviceType] = new ServiceDescriptor(…);
```

Last registration wins. No exception, no log, no diagnostic of any kind.

`TD-69` named the consequence: a mistaken re-registration of a platform
contract — `IEventBus` was the example — silently swaps the platform's own
implementation, and the first symptom is behaviour nobody can trace back
to a registration line. On a platform that loads third-party plugins
(`ADR-0107`–`ADR-0112`), "silently replaces a platform service" is not a
neutral default.

The audit `WP 16.4B` ran before changing anything found roughly 330
registration call sites, and exactly two categories among them:

1. **One test asserting the defect as intended behaviour** —
   `ServiceCollectionTests.Add_SameServiceTypeTwice_LastRegistrationWins`.
2. **Eight `Samples` integration fixtures each registering
   `IPermissionEvaluator` twice** — an `AddInstance` immediately shadowed
   by a `Singleton<>`. Under last-wins the first line was dead code that
   nobody could see was dead. This was a real latent bug the old behaviour
   was hiding, not a legitimate override.

**No call site in the entire tree was found that legitimately relied on
replacing an existing registration.** That fact is what made the decision
available.

## Decision

**A second registration for a service type throws
`DuplicateServiceRegistrationException`. First registration wins.
Replacing one deliberately requires passing `allowReplace: true`.**

- `ServiceRegistrationException` is the base type, deliberately a separate
  root from `ServiceResolutionException`: that one covers failures while a
  built `ITempestServiceProvider` resolves a service; this one covers
  failures while an `IServiceCollection` is still being assembled and no
  provider exists. This mirrors the registration/discovery split
  `ModuleRegistrationException`/`ModuleDiscoveryException` already draws.
- `allowReplace` is an opt-in parameter defaulting to `false`, so no
  existing call site changed meaning silently — every one either kept
  working unchanged or was found by the audit and dealt with explicitly.
- First-wins rather than last-wins is the direction chosen because the
  platform registers its own services first, in `TempestHost`'s Phase 6,
  before any module or plugin gets a chance to register anything.

This also matches an idiom the codebase already had, in two places:
`DuplicateApiRouteException` and `DuplicateReportDefinitionException` both
already refused a colliding second registration rather than accepting it.
The container was the outlier, not the precedent.

## Consequences

- A mistaken duplicate registration now fails loudly, at composition time,
  naming the service type — instead of producing behaviour to debug later.
- The eight `Samples` fixtures lost a line of dead code each, and the one
  test that asserted the defect now asserts the fix.
- **A plugin can no longer silently shadow a platform service** by
  registering the same contract after the platform did. It is not a
  complete containment story on its own — capability enforcement per
  `ADR-0107`–`ADR-0112` is what actually bounds a plugin — but the
  previously-silent path is now an exception.
- `allowReplace: true` is a real escape hatch and could be misused. It is
  visible at the call site and greppable, which last-wins was not.
- `AddDiscoveredModules`/`AddDiscoveredHostedServices` will now throw if a
  caller ever supplies two descriptors sharing a concrete type. No such
  caller exists today; recorded here rather than pre-emptively worked
  around.

## Alternatives Considered

**Keep last-wins.** The status quo, and a real option: it is what most
mainstream containers do, and it never breaks a caller. Rejected because
`TD-69` is precisely a report of that behaviour causing harm, and because
the audit established that nothing in the tree actually wanted it.

**Warn instead of throwing.** Rejected. A warning in a startup log that
already runs to hundreds of lines is not a control; this release contains
a second, independent instance of the same lesson, where
`new-release.ps1`'s yellow warning about un-green CI was found to be
functionally equivalent to no check at all (`WP 16.1A-R1`).

**Throw with no escape hatch.** Considered seriously, and it is the right
answer for a surface with no legitimate replacers. Rejected here only
because a container is exactly the kind of surface where a legitimate
replacer eventually appears — a test host substituting a fake for a
platform service is the obvious future case — and discovering that with
no opt-in available would force either a hurried API change or a workaround.

**Last-wins but ordered, e.g. platform registrations locked after Phase 6.**
Rejected as more machinery for the same guarantee: it needs a lifecycle
concept the collection does not have, and it would still be silent for
collisions inside a single phase.

## Future Considerations

- If `allowReplace: true` ever appears in production (non-test) code, that
  is a signal to revisit whether a first-class "test host" composition
  path is the better answer.
- The same bug class — a keyed collection silently overwriting on a second
  write — was found in a second place during this same review board, in
  `StateMigrationRegistry.Register` (`WP 16.4B-R1`). Worth grepping for
  the pattern `dictionary[key] = value` on any registration surface before
  the next release.

## Related Documents

`docs/governance/Quality/Technical Debt Register.md` (`TD-69`);
`docs/releases/v0.16.0/WP16.4B-2 Platform Hygiene.md` (the full call-site audit);
`docs/releases/v0.16.0/WP16.4B-R1 Migration Collision Guard and Platform Service Registration.md`;
`docs/adr/ADR-0009` (composition root); `docs/adr/ADR-0017`;
`docs/adr/ADR-0107`–`ADR-0112` (plugin trust and capability enforcement);
`src/Tempest.Core/DependencyInjection/ServiceCollection.cs`;
`src/Tempest.Core/DependencyInjection/DuplicateServiceRegistrationException.cs`;
`src/Tempest.Core/Api/DuplicateApiRouteException.cs`;
`docs/academy/06 Engineering Standards/Engineering Governance.md` §5, §9.
