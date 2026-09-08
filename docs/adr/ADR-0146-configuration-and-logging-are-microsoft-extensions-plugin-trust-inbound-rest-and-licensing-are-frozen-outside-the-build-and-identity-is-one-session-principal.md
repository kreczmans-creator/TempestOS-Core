# ADR-0146: Configuration and Logging Are Microsoft.Extensions; Plugin Trust, Inbound REST, and Licensing Are Frozen Outside the Build; Identity Is One Session Principal

## Status

Accepted — `WP 17.2A` (Platform trim), 2026-09-08.

This decision has two parts, on two different clocks. The freeze —
Section "Decision A" below — is implemented in this commit, `WP 17.2A`
part 1. The Configuration/Logging/Identity/Audit adoption — Section
"Decision B" — is decided here, in full, but **implemented by `WP 17.2A`
part 2**, a separate, later Work Package against the tree this freeze
leaves behind. Both parts are one decision because the freeze is what
makes part 2 affordable: collapsing Identity to one session principal is
a small, mechanical change against a platform with no second,
component-scoped identity axis to reconcile it with, and a large,
review-heavy one against a platform that still has one.

Marks Frozen: `ADR-0047`, `ADR-0048`, `ADR-0049`, `ADR-0050`,
`ADR-0107`–`ADR-0112`.

## Context

The `v1.0.0` re-scope (`docs/releases/v1.0.0/WorkPackages.md`, superseding
the 2026-09-04 proposal) named the shape the product actually is: **a
single-user, locally-trusted desktop system of record for a small
engineering consultancy.** Nobody ships a third-party plugin into it.
Nobody calls its REST API from outside the sample harness that
demonstrates the API exists. Nobody has ever been handed a licence file
by this platform's own tooling, because no distribution channel for one
exists. Three substrates were built for a distribution model — a plugin
marketplace, an externally-callable service, a licensed product with
tiers — that `v1.0.0` does not have and is not building toward.

**The plugin trust platform.** `ADR-0110` through `ADR-0112` (`WP 13.0A`
architecture, `WP 13.2A` implementation onward) built a capability-scoped
trust model: a component-principal identity axis distinct from the
user-scoped one, trust tiers (`FirstParty`/`VerifiedSigned`/
`UnsignedLocal`), a detached manifest-and-assembly signature scheme
verified at Plugin Discovery, a local flat-file trust store, a
denied-type registry closing the gap between an assembly that has
already loaded (`ADR-0015`: a load cannot be undone) and the module or
hosted service pipelines that would otherwise still run it, and
trust-ordered registration rules in `EventBus`, `CommandRegistry`,
`CommandHandlerTable` and `NavigationService` letting a higher-trust-tier
registrant evict a lower one's descriptor. Measured directly against this
tree at the commit this freeze was taken: `src/Tempest.Core/Plugins`'
trust-specific production code (signing, the trust store, trust tiers,
capability enforcement, assembly loading, the component-principal and
denied-type registries — everything except manifest parsing/validation/
dependency-graph resolution, which is not trust) was 2,045 lines across
20 files; its dedicated tests, plus the trust-ordered-registration and
component-scope tests it left in `EventBus`, `CommandRegistry`,
`CommandHandlerTable`, `NavigationService` and `IdentityService`, plus the
full plugin-platform end-to-end and fault-injection suites in
`Tempest.Core.Tests/Runtime`, totalled roughly 10.7K further lines across
325 test methods. All of it exists to adjudicate trust for plugins that
do not exist. None of it has ever been exercised by anything other than
its own test suite and the sample harness `WP 13.x` built to prove it
against — `ApiSampleModule`, `LicensingSampleModule` and the dynamically
emitted plugin assemblies `DynamicPluginAssemblyBuilder` builds at test
time to have something to load.

**The seams it left everywhere else.** Trust enforcement could not live
only in `Tempest.Core.Plugins`, because a plugin's registrations have to
be checked at the exact point a first-party module's are: `EventBus`,
`CommandRegistry`, `CommandHandlerTable` and `NavigationService` each
carry a second, lock-guarded ownership dictionary and an optional,
trailing `ICurrentComponentAccessor`/`IPermissionEvaluator` constructor
pair; `IdentityService.EstablishCurrentPrincipal` carries a capability
gate on the same accessor; `ModuleLifecycleManager` and
`HostedServiceManager` each carry an optional `componentScopeProvider`
delegate `TempestHost` closes over to push a plugin's principal around
one lifecycle call; `ReflectionFrameworkDiscoveryService` carries an
optional `isTypeExcluded` predicate wired to the denied-type registry so
an already-loaded, already-denied plugin's module type is never
constructed. Every one of these parameters defaults to `null` and, when
`null`, reproduces the pre-trust behaviour exactly — which is the whole
problem: **a constructor with an optional trailing collaborator that
silently no-ops when omitted is a footgun in a hand-rolled DI container
that resolves constructor parameters by type, not by explicit wiring.** A
future caller that constructs any of these seven types directly — in a
test, in a tool, in a future host — gets the unguarded, pre-trust
behaviour by default and has no compiler error telling it so. The
seam was reviewed, tested and correctly implemented at every one of the
several Work Packages that touched it (`WP 13.2A`, `WP 13.9.1`
through `WP 13.11C`, `WP 13.9.4`, `WP 13.9.6`, `WP 13.10B`) — this is not
a defect in that work. It is a structural cost the platform has been
paying, in cognitive load on every one of those seven types' own
constructors, for a capability nothing uses.

**The inbound REST API.** `ADR-0047` through `ADR-0049` (`WP 6.3`) built
`RestApiHostedService` over Kestrel, dispatching through
`IApiEndpointRegistry`/`ApiRequestHandler` into the existing Command
Framework. `ADR-0048`'s own design accepted a real, disclosed limitation
at the time: **the REST transport has no request-parameter binding at
all (`AT-10`)** — an inbound request's body and query string are never
threaded into the invocation, so every mapped route dispatches its
command's parameterless `CreateDefault` instance. That is a
parameterless-RPC surface, not a REST API in any sense a caller outside
this repository could build against, and `AT-10` was never closed. Its
only caller is `ApiSampleModule`, deleted by this Work Package alongside
it (`src/Samples/Tempest.Samples/ApiSampleModule.cs`,
`CheckSampleCapabilityCommand[Handler].cs`). No production module maps a
route.

**Licensing.** `ADR-0050` (`WP 6.6`) made licence validation a
Host-startup, Host-fatal gate: `TempestHost.ExecuteStartupPhasesAsync`
constructed `LicenseValidator` and called `.Validate()` before even the
logger existed, aborting the whole Host on a licence file that failed to
parse or verify. `ADR-0050`'s own resolution of `Risk Register.md` item
R5 made a missing licence file resolve to a valid, unrestricted-but-
uncapable default specifically so that a licence-free install would still
run — which is the shape every actual `v1.0.0` install has, and always
will: **a licence file this platform trusts at face value, with no
issuing authority, no revocation, and no distribution mechanism, gates
nothing a locally-installed, single-user desktop product needs gated.**
`LicensingSampleModule` was the only caller and is deleted alongside it.

**What this Work Package's row also decided, stated here in full and
implemented by part 2.** `Configuration` is a custom
`IConfigurationProvider` with no environment-variable or command-line
source and no file format beyond what `ADR-0034`'s own sources
implement; an operator cannot reach `Runtime:*`/`Identity:*`/
`Persistence:RootPath` without editing source. `Logging` writes
Information-level chatter on every DI resolution, event publish and
command dispatch, to the console only, with no durable record an operator
can hand to support. `Identity` (`ADR-0043`, `ADR-0044`) already models
exactly the shape `v1.0.0` needs — a stable identity id, a display name,
a role — but carries the second, component-scoped `ICurrentComponentAccessor`
axis this ADR's freeze removes the only caller of, and the plugin-specific
`plugin.identity.establish` capability gate that axis existed to enforce.
Auditing (`ADR-0045`) is real but under-populated: nothing in the current
tree writes an audit row for a calc-sheet run, a check, an issue, a
material release, an invoice request or a project status change.

## Decision A: Freeze the Plugin Trust Platform, Inbound REST, and Licensing (implemented by this commit)

**Everything in `Tempest.Core/Plugins`, `Tempest.Core/Api` and
`Tempest.Core/Licensing` moves to `src/Frozen/`, a folder outside
`src/TempestOS.slnx`, except Plugin Manifest Discovery, which stays live,
in place.** `src/Frozen/Tempest.Core.Api` and
`src/Frozen/Tempest.Core.Licensing` take everything from their respective
source folders, unconditionally. `src/Frozen/Tempest.Core.Plugins` takes
signing (`PluginSignatureEnvelope`, `PluginSignatureVerifier`), the trust
store (`IPluginTrustStore`, `PluginTrustStore`), trust tiers
(`PluginTrustTier`, `PluginTrustPermission`), capability enforcement
(`PluginCapability`), the component-principal and denied-type registries
(`IPluginComponentPrincipalRegistry`/`PluginComponentPrincipalRegistry`,
`IPluginDeniedTypeRegistry`/`PluginDeniedTypeRegistry`, and their
recorder-side interfaces), assembly loading
(`IPluginAssemblyLoader`/`PluginAssemblyLoader` and its exceptions), and
`Tempest.Core.Identity`'s own `ICurrentComponentAccessor`/
`CurrentComponentAccessor` — the second, component-scoped identity axis
that exists only to carry a plugin's principal. **Plugin manifest
discovery does not move:** `PluginManifest`, `PluginManifestDto`,
`PluginDependency`, `PluginDependencyDto`,
`IPluginManifestDiscoveryService`/`PluginManifestDiscoveryService`,
`InvalidPluginManifestException`, `PluginException` and the minimum of
`IPluginRegistry`/`PluginRegistry`/`PluginRegistryEntry`/
`PluginRegistryState` the Host's manifest-discovery phase needs stay
live, in `src/Tempest.Core/Plugins`. The Host still scans the plugin drop
folder, parses and validates each manifest, resolves the dependency
graph, and records what it found — now as `PluginRegistryState.Discovered`,
replacing the `Loaded` state Plugin Loading used to record once assembly
loading actually ran. Nothing loads, signs, verifies, scopes or enforces
any of it.

**Every seam the trust platform left in a core service is removed, not
merely disconnected.** `EventBus`, `CommandRegistry`,
`CommandHandlerTable` and `NavigationService` lose their second ownership
dictionary and their optional `ICurrentComponentAccessor`/
`IPermissionEvaluator` constructor parameters; registration reverts to
the unconditional duplicate-rejection `ADR-0032`/`ADR-0037` originally
specified. `IdentityService.EstablishCurrentPrincipal` loses its
capability gate. `ModuleLifecycleManager` and `HostedServiceManager` lose
their `componentScopeProvider` delegate parameter and the scope-push
around each lifecycle call. `ReflectionFrameworkDiscoveryService` loses
its `isTypeExcluded` predicate parameter. `ICurrentPrincipalAccessor` and
`IPermissionEvaluator` — the user-scoped identity axis and the
capability-check contract, as opposed to the component-scoped axis and
its plugin-specific uses — are untouched; nothing about `v1.0.0`'s own
identity or permission model changes here.

**`TempestHost` no longer reads a licence file, at all.** The
licence-validation phase that used to run before even the logger existed
is deleted outright, not merely made to no-op on a missing file — there
is no `ILicenseValidator`, `LicenseValidator` or `LicenseValidationException`
call anywhere in the live startup path, and `TempestHostBuilder` drops
the `licenseFilePathOverride` constructor parameter that existed only to
test it. `TempestHost` no longer constructs the plugin trust store, the
component-principal registry, the denied-type registry, or
`CurrentComponentAccessor`; no longer loads plugin assemblies; no longer
filters Module Registration or Hosted Service Registration against a
denied-type registry; and no longer registers
`IApiEndpointRegistry`/`ILicenseProvider` into the DI container.

**The frozen layers' sample modules are deleted, not frozen alongside
their subject.** `ApiSampleModule`, `LicensingSampleModule`,
`CheckSampleCapabilityCommand`/`CheckSampleCapabilityCommandHandler` and
their tests are gone: a sample module demonstrates a capability the
shipped product has; once the capability is frozen out of the build,
demonstrating it is not "kept for later", it is dead code with nothing
live to call.

**Every test whose subject is a frozen type moves to `tests/Frozen/`,
mirroring the source layout.** The Api, Licensing and Plugins trust test
directories; the trust-ordered-registration and component-accessor tests
in `Commands`/`Events`/`Navigation`/`Identity`; the full plugin-platform
end-to-end and fault-injection suites, and the Host plugin-lifecycle/
trust/configuration suites, in `Runtime`; the Api/Licensing sample
integration tests; and the plugin-assembly-dependent half of one further
test (`ModuleDiscoveryUnresolvableConstructorTests`' unresolvable-
constructor-parameter case, which depends on `DynamicPluginAssemblyBuilder`
to build a real plugin assembly on disk — its self-contained,
plugin-independent case stays live). Manifest-discovery tests
(`PluginManifestDiscoveryServiceTests`,
`PluginDependencyGraphResolutionTests`, `PluginDisabledConfigurationTests`,
`PluginManifestV2FieldsTests`, `PluginRegistryTests`) stay live, updated
for the narrower live surface.

**`src/Plugins/` (the runtime drop folder) and `Templates/` are
unaffected.** They are not `Tempest.Core.Plugins`; the freeze concerns
code, not the folder a future plugin's files would eventually be dropped
into.

## Decision B: Configuration, Logging, Identity, and Audit (decided here, implemented by WP 17.2A part 2)

**Configuration adopts `Microsoft.Extensions.Configuration`.** Sources,
in increasing precedence: an `appsettings.json` file in the persistence
root, environment variables, and the command line — so `Runtime:*`,
`Identity:*` and `Persistence:RootPath` become reachable by an operator
without editing source, for the first time.

**Logging adopts `Microsoft.Extensions.Logging`, with a rolling file
sink in the persistence root.** The Information-level chatter DI
resolution, event publish and command dispatch currently write is reduced
to Debug; a durable, rotated log file gives an operator something to hand
to support that the console-only sink today does not.

**Identity collapses to `ISessionPrincipal`.** A single principal per
session carries a **stable identity id** — the OS security identifier on
Windows, the user id elsewhere, optionally mapped to a configured person
id — a display name, and a **role** (Engineer or Checker) kept separate
from the identity. The identity id is what every audit row, authorship
field and check record stores; the role governs which commands are
offered. A configuration override may change the display name or role;
it cannot change the identity id, which is read from the OS at each
launch and never from configuration — the same non-negotiable-identity
principle `ADR-0043` already established, narrowed from "local, extensible"
to "one session, OS-derived". `IPermissionEvaluator` is kept as the
one-line seam it already is; the second, component-scoped identity axis
this ADR's Decision A removes has no successor, because nothing left in
the platform needs to distinguish "which loaded component" from "which
signed-in person".

**`IAuditRecorder` becomes real.** Every calc-sheet run, check, issue,
material release, invoice request and project status change writes an
audit row, queryable by object and by date through a new index table.

**The custom DI container is kept and frozen.** `TempestServiceProvider`/
`ServiceCollection` are not replaced by `Microsoft.Extensions.DependencyInjection`;
Configuration and Logging adopt Microsoft's abstractions where those
abstractions are genuinely better-shaped for the job (a hierarchical,
multi-source configuration tree; a levelled, sink-based logging pipeline)
without pulling the container itself along, which nothing here has
found a reason to replace.

## Consequences

**Frozen, where, and what "frozen" means.** `src/Frozen/Tempest.Core.Api`,
`src/Frozen/Tempest.Core.Licensing` and `src/Frozen/Tempest.Core.Plugins`
hold the code as it stood at the commit it was frozen at. **No project
file lives under `src/Frozen/`, no folder there is referenced by
`src/TempestOS.slnx`, and nothing there compiles, is tested, or is
shipped**, starting from this commit. It compiles only against the
`Tempest.Core` of the commit it was frozen at; every later change to
`Tempest.Core` — a signature change to a type it references, a removed
collaborator — makes it drift further, and that drift is expected, not a
defect to fix. The matching tests sit alongside it at `tests/Frozen/`,
in the same non-building state. `src/Frozen/README.md` carries this in
full for a future reader who finds the folder without this ADR in hand.

**How it comes back.** Not by deleting `src/Frozen/` and starting over —
the design is sound, reviewed, and correctly implemented; deleting it
would throw away work a paying client's requirement may exactly need.
When a client asks for third-party plugins, for an inbound API, or for
licence enforcement, the relevant folder is brought back as a real
project: re-added to `src/TempestOS.slnx`, re-pointed at whatever
`Tempest.Core` has become by then, and re-reviewed against it — the seams
this ADR removed from `EventBus`/`CommandRegistry`/`CommandHandlerTable`/
`IdentityService`/`NavigationService`/`ModuleLifecycleManager`/
`HostedServiceManager`/`ReflectionFrameworkDiscoveryService` would need to
be re-added at that time, deliberately, against whatever those seven
types have become by then, rather than resurrected as optional
parameters nobody remembers the purpose of.

**What is not frozen.** `IPluginManifestDiscoveryService`/
`PluginManifestDiscoveryService` and the manifest value types
(`PluginManifest`, `PluginDependency` and their DTOs), because reading
what is in the plugin drop folder — parsing, validating, version-checking,
dependency-resolving and recording it — has no attack surface of its own
(nothing is loaded, signed, verified, scoped or enforced) and is what a
future plugin capability would be rebuilt on rather than rediscovered.
`ICurrentPrincipalAccessor` and `IPermissionEvaluator`, unchanged.

**Measured, at this commit.** `src/Frozen/` holds 41 production `.cs`
files, 3,120 lines. `tests/Frozen/` holds 37 test `.cs` files, roughly
12.6K lines and 325 test methods. `Tempest.Core`'s own live line count
and the live/frozen test totals are recorded in this Work Package's own
final report rather than restated here, where they would drift the
moment either count next changes.

**What this ADR does not do.** It does not touch Configuration, Logging,
Identity or Audit — Decision B is decided, not implemented, by this
commit; a reader of the live tree immediately after this commit will find
`Tempest.Core`'s own hand-rolled `IConfigurationProvider`, the
console-only logging pipeline, `ADR-0043`/`ADR-0044`'s identity model
including `ICurrentPrincipalAccessor`, and `ADR-0045`'s under-populated
`IAuditRecorder`, all exactly as they stood before this Work Package. It
does not touch the Validation project (`ADR-0102`) or its default-excluded
fault-injection discovery marker, which is a Modules concept, not a
Plugins one, and is untouched by this freeze.

## Related Documents

- `docs/adr/ADR-0047-rest-api-is-a-background-hosted-service.md`,
  `ADR-0048-rest-endpoints-dispatch-through-the-command-framework.md`,
  `ADR-0049-adopting-aspnetcore-kestrel-for-the-rest-api.md` — Frozen by
  this ADR.
- `docs/adr/ADR-0050-license-validation-is-a-host-startup-host-fatal-gate.md`
  — Frozen by this ADR.
- `docs/adr/ADR-0107-plugin-dependency-graph-resolution-and-extended-failure-classification.md`
  through
  `docs/adr/ADR-0112-plugin-signing-is-a-detached-manifest-and-assembly-hash-signature-verified-at-discovery.md`
  — Frozen by this ADR.
- `docs/adr/ADR-0025-plugin-failure-classification.md`,
  `docs/adr/ADR-0026-plugin-discovery-lifecycle-placement.md` — the
  Discovery/Loading phase boundary and failure classification Plugin
  Manifest Discovery still implements; not frozen.
- `docs/adr/ADR-0043-identity-model-scope-is-local-only-extensible.md`,
  `docs/adr/ADR-0044-authorization-enforcement-point.md` — the identity
  and permission model Decision B narrows and this freeze leaves standing.
- `docs/adr/ADR-0045-audit-durable-queryable-append-only.md` — the audit
  model Decision B makes real.
- `docs/adr/ADR-0102-fault-injection-modules-are-isolated-by-project-and-a-default-excluded-discovery-marker.md`
  — untouched; its discovery marker is a Modules concept.
- `src/Frozen/README.md` — the one-paragraph notice a reader who finds
  the folder without this ADR in hand needs.
- `docs/architecture/Plugin Platform Architecture.md`,
  `docs/architecture/Plugin Manifest Architecture.md` — carry a "Frozen
  by ADR-0146" banner; Manifest Architecture describes what stays live.
- `docs/architecture/Host Lifecycle.md`, `Startup Sequence.md`,
  `Shutdown Sequence.md` — phase tables updated to remove the frozen
  phases.
- `docs/releases/v1.0.0/WorkPackages.md`, `WP 17.2A`.
- `BACKLOG.md` — `TD-06`, `TD-13`, `TD-14`, `TD-49`–`TD-56`, `TD-61`,
  `TD-62`, `TD-64`, `TD-94`, `TD-129` and any other row whose subject is a
  frozen type, moved to "Archived with the layer".
