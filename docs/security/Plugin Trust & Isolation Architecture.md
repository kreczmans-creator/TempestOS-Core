# Plugin Trust & Isolation Architecture

## Status

**Design — `WP 13.0A`, not yet implemented.** This document is the direct
answer to `Security Roadmap.md` items 1, 2, and 10, commissioned by the
Product Owner's confirmed commitment to third-party plugins
(`docs/releases/v0.13.0/WorkPackages.md`). It designs the retrofit
`ADR-0044` explicitly deferred and named as the future work that would
close `TD-09`, `TD-10`, and `TD-11` — it does not implement that retrofit.
Implementation is `WP 13.0B`'s scope, not this document's.

This document is written alongside a parallel, sibling architecture
effort (`WP 13.0A`'s Plugin Architecture workstream) covering manifest
v2's dependency model, discovery/registration mechanics, and the DI
service-registration mechanism itself. This document owns the trust and
security semantics layered onto that mechanism; it does not redesign
discovery, registration, or lifecycle machinery that document owns.

## Overview

Today, a loaded plugin is indistinguishable from a first-party module the
moment its assembly finishes loading (`Threat Model.md`, Scenario 1;
`Technical Debt Register.md`, `TD-09`). Every registry a module can reach
— the DI container, `NavigationService`, the Command Framework — treats a
plugin's registration exactly like a first-party one, with no ownership
check (`TD-10`) and no defence against a plugin's own Id sorting ahead of
its intended first-party owner (`TD-11`, `CMD-1`). `ADR-0044` built the
single enforcement point (`IPermissionEvaluator.RequirePermission`) this
retrofit needs, but deliberately did not call it anywhere new.

This document designs:

1. A **four-tier trust model**, established once per plugin at Plugin
   Discovery/Loading time, that never changes for the life of the process
   run (`ADR-0015`'s "single-use Host" precedent applied to a single-use
   plugin trust assignment).
2. A **capability model** extending `Permission`/`IPermissionEvaluator`
   with a namespaced key set (`plugin.*`), a new ambient **component
   principal** axis distinct from `ICurrentPrincipalAccessor`'s existing
   *user* principal, and a **trust-ordered registration rule** replacing
   "first registration wins" wherever a well-known Id is shared across
   trust tiers.
3. A **signing strategy** — a detached, hash-based signature over the
   manifest and its declared assembly, verified entirely at Plugin
   Discovery, with no new runtime dependency.
4. An explicit, defended **isolation-boundary decision**: capability
   scoping and construction-time reflection checks, entirely in-process —
   **not** a separate `AssemblyLoadContext`, **not** process separation.
   **This does not enable per-plugin unload.** State this early, plainly,
   for the sibling Plugin Architecture document: if its lifecycle design
   wants real plugin unload, that is a separate, later decision this
   document does not make and does not unlock.
5. A **failure containment model** extending `ADR-0025`'s classification
   table with four new categories, and a **logging convention** reusing
   `ILogger`'s existing severity scale — no new telemetry pipeline.

## Trust Tiers

| Tier | How assigned | What it permits |
|---|---|---|
| **First-Party** | (a) Every module that reaches the process via the ordinary project-referenced/compiled-in path — it never touches Plugin Discovery/Loading at all, exactly as today. (b) A plugin whose manifest carries a `Signature` verifying against TempestOS's own publisher certificate (shipped in the trust store as a fixed, always-present entry). | Unrestricted — identical to today's behaviour. **Not subject to any capability check.** This tier's entire purpose is to guarantee this design changes nothing observable for any actor that exists today. |
| **Verified-Signed** | A plugin whose manifest carries a `Signature` that verifies against a certificate in the local trust store, other than TempestOS's own. | May be granted exactly the capabilities its manifest's `RequestedCapabilities` declares, subject to the Discovery-time eligibility and construction-time conformance checks below. No fixed ceiling beyond "what it declared and what its constructor demonstrably needs." |
| **Unsigned-Local** | A plugin manifest with no `Signature` field at all, loaded only if `Plugins:AllowUnsignedLoad` is explicitly `true` (default `false` — fail closed, mirroring `ADR-0043`'s identical fail-closed precedent for an unrecognised identity). | Capability grants are clamped to a **fixed ceiling regardless of what the manifest requests**: `plugin.navigation.register` and `plugin.commands.register` only. Never `plugin.di.register`, never any `plugin.events.publish:*`, never any `plugin.services.resolve:*` beyond the fixed always-allowed baseline (`ILogger`, `IConfigurationProvider`, `IDiagnosticsProvider`). An unsigned plugin can appear in the UI and register commands — both fully attributable and inspectable by an operator at runtime — but cannot speak on the event bus, cannot shadow a platform service, and cannot pull in anything beyond what every module has always been able to read. |
| **Untrusted** | A manifest carrying a `Signature` that **fails** to verify — thumbprint not in the trust store, signature does not verify against the recomputed payload, or the publisher certificate is outside its validity window. | Never loads. Not a runtime tier a process ever observes running — it is the terminal rejection outcome, listed for completeness of the model. A present-but-broken signature is always worse than no signature at all and is never downgraded to Unsigned-Local. |

Tier is assigned exactly once, at Plugin Loading (Phase 3.2), and is
immutable for the remainder of that process run — consistent with
`ADR-0015`'s "single-use, construct once" convention and with
`ADR-0043`'s "configuration/grants are immutable once built" precedent.
There is no runtime tier promotion or demotion; a plugin fix requires a
new process run, exactly as a plugin code fix already does today
(`Plugin Manifest Architecture.md`, Risks).

## Capability Model

### Reusing, not replacing, `Permission`/`IPermissionEvaluator`

No new permission type is introduced. A plugin capability **is** a
`Permission` (`Tempest.Core.Identity.Permission`), namespaced under a
`plugin.` prefix, checked through the exact same
`IPermissionEvaluator.HasPermission`/`RequirePermission` this platform
already has. This is a direct, minimal extension of `ADR-0044`, not a
parallel mechanism.

**Reserved capability keys** (v1, closed set — extending this set later
is purely additive):

| Key | Grants |
|---|---|
| `plugin.navigation.register` | May call `NavigationService.Register`. |
| `plugin.commands.register` | May call the Command Framework's registration path (`CommandHandlerTable.Register`/`CommandRegistry.RegisterDescriptor`). |
| `plugin.di.register` | May contribute a DI service registration, if/when the sibling Plugin Architecture's manifest v2 introduces that mechanism. This document decides *whether trust gates it*, not *how registration itself works* — the gate is this one capability key. |
| `plugin.events.publish:<FullTypeName>` | May call `IEventBus.PublishAsync<TEvent>`/`Publish` for the named event type. One key per event type — no wildcard in v1 (see Non-Goals). |
| `plugin.services.resolve:<FullTypeName>` | Declares the plugin's module constructor is permitted to depend on the named service type, beyond the fixed always-allowed baseline (`ILogger`, `IConfigurationProvider`, `IDiagnosticsProvider` — every module has always been able to receive these; excluding them from the declared set keeps a typical, well-behaved plugin's manifest short). |

Every key is a plain string, reusing `Permission`'s existing, unvalidated
free-string contract exactly — no new parsing, no new comparer, no
wildcard/glob matching.

### The manifest field shape — reconciled with the sibling document

The sibling Plugin Architecture document's manifest v2 DTO
(`Plugin Platform Architecture.md`) already reserves three optional
top-level fields for this design, fixing their CLR *shape* while
deferring their *semantics* here exactly as intended:
`RequestedCapabilities` (`IReadOnlyList<string>`, never null),
`Publisher` (`string?`, free text, unverified), and `Signature`
(`string?`, an opaque, self-describing string). This document adopts
that shape as-is rather than proposing a competing, object-valued
`Signature` field — `Signature`'s string value is itself a small JSON
envelope this design defines and parses:

```json
{
  "RequestedCapabilities": [
    "plugin.navigation.register",
    "plugin.commands.register",
    "plugin.events.publish:Tempest.Core.Events.RequirementChangedEvent",
    "plugin.services.resolve:Tempest.Core.Events.IEventBus"
  ],
  "Publisher": "Acme Engineering Ltd.",
  "Signature": "{\"Algorithm\":\"RSA-SHA256\",\"PublisherCertificateThumbprint\":\"5F2B9C...\",\"Value\":\"base64...\"}"
}
```

- `Signature` absent → Unsigned-Local (subject to `AllowUnsignedLoad`).
  Present → must verify (parse the envelope, then verify the signature —
  a parse failure is treated as a verification failure, not a softer
  category), or the plugin is Untrusted (never Unsigned-Local as a
  fallback).
- `Publisher` is **informational only** — free text, unverified, never
  itself a trust anchor. Trust comes entirely from the
  `PublisherCertificateThumbprint` carried *inside* `Signature`'s own
  envelope, matched against the local trust store.
- `RequestedCapabilities` absent or empty → the plugin requests nothing
  beyond the fixed baseline; a maximally inert plugin.
- All three fields are purely additive to the existing, implemented
  `PluginManifest` shape (`Id`, `Name`, `Version`,
  `MinimumPlatformVersion`, `AssemblyFileName`) — no existing field
  changes shape or meaning. See `ADR-0112` for the full envelope and
  payload/verification definition.

### Two enforcement mechanisms: static (declaration-time) and dynamic (call-site)

**Static — Discovery/Loading-time, entirely Host-owned, before Module
Discovery ever runs:**

1. **Capability eligibility check** (Plugin Loading, Phase 3.2): every
   key in `RequestedCapabilities` is checked against the plugin's own
   assigned tier's ceiling (Unsigned-Local's fixed two-key ceiling above;
   Verified-Signed has no ceiling beyond the closed key set itself).
   A requested key outside the tier's ceiling isolates the whole plugin —
   new failure category 17, below.
2. **Constructor conformance check** (Plugin Loading, Phase 3.2, before
   handoff to Module Discovery): the plugin's own loaded assembly is
   reflected over — using the same kind of `IModule`-type scan Module
   Discovery would perform, run here independently and *before* Module
   Discovery so nothing downstream changes — and every constructor
   parameter type on a discovered `IModule` implementer is checked
   against the fixed baseline plus the plugin's own granted
   `plugin.services.resolve:*` declarations. A parameter type outside
   both isolates the whole plugin — same failure category 17.

   **This is the concrete mechanism that closes the "resolve a given
   service" half of `TD-09`.** It requires no change to `TempestServiceProvider`'s
   resolution logic, no interception hook, and no change to Module
   Discovery, Registration, or Lifecycle — `Plugin Manifest Architecture.md`'s
   load-bearing "Unchanged" claim for all three survives this design
   intact. The check runs once, entirely within the existing Plugin
   Loading phase boundary, against the plugin's own assembly only.

**Dynamic — call-site, for actions that happen throughout a plugin's
running life, not once at construction:**

`NavigationService.Register`/`Unregister`, the Command Framework's
registration path, and `IEventBus.PublishAsync` each gain one
`IPermissionEvaluator.RequirePermission(componentPrincipal, permission)`
call, exactly the shape `ADR-0044` anticipated. The check is skipped
entirely — not merely satisfied — when the ambient component principal
is `null` or First-Party, so every actor that exists today (every
first-party module, every test, every Host-owned call) observes zero
behavioural change. It applies only to the one new actor this document
exists to govern: a Verified-Signed or Unsigned-Local plugin.

### The component principal: a second identity axis

`ICurrentPrincipalAccessor` (`ADR-0044`) answers "which **user** is
acting" — a single, ambient, process-wide value, deliberately *not*
call-chain-scoped, because a human user is established once and expected
to remain visible to unrelated later callers. That is the wrong shape
for "which **loaded component's own code** is currently executing" —
this must revert the instant control returns from a plugin's code back
to its caller, and must nest correctly when one component's code calls
into another's (a plugin's event handler dispatching a command whose
handler is itself first-party code must not appear to be "acting as"
the plugin).

This document introduces `ICurrentComponentAccessor`
(`IPrincipal? Current { get; }`, mirroring `ICurrentPrincipalAccessor`'s
exact shape), backed by an `AsyncLocal<T>`-flowed stack, with a
`IDisposable BeginScope(IPrincipal componentPrincipal)` token the Host
pushes around every point it re-enters plugin-owned code: a module's own
`InitialiseAsync`/`StartAsync`/`StopAsync`/`DisposeAsync` calls, an event
subscriber invocation, and a command handler invocation. This is
precisely the case `ADR-0044` itself named as the correct future use of
`AsyncLocal<T>` ("a genuinely concurrent, per-request scenario") — this
is not a revision of `CurrentPrincipalAccessor`, it is the additional,
layered accessor `ADR-0044` already anticipated ("more likely… the REST
API introducing its own request-scoped accessor layered on top of this
one").

A plugin's component principal is constructed once, at Plugin Loading,
from its manifest `Id` (as `IIdentity.Id`) and its final, tier-clamped,
eligibility-checked capability grant set (as `IPrincipal.Permissions`) —
reusing `IPrincipal`/`IIdentity` exactly as they already exist, with zero
new type beyond the accessor itself.

### The trust-ordered registration rule (`TD-10`, `TD-11`, `CMD-1`)

Applied identically to `NavigationService.Register`/`Unregister` and the
Command Framework's registration path — "any future shared registry with
the same shape," per `Security Roadmap.md` item 2's own language.

**Ownership (`TD-10`).** At `Register(item)`, the registry captures
`ICurrentComponentAccessor.Current` (or a sentinel "Host/first-party"
value if `null`) alongside the item, out-of-band — no change to
`NavigationItem`'s or `CommandDescriptor`'s own public shape.
`Unregister(id)` compares the caller's own current component principal's
`Identity.Id` against the stored owner; a mismatch is rejected
(`RequirePermission` called with a reserved override permission,
`navigation.unregister.any` / `commands.unregister.any`, held only by
First-Party by construction) rather than silently succeeding.

**Priority (`TD-11`/`CMD-1`).** Replaces "first registration wins"
unconditionally with: **first registration wins among registrants of the
same trust tier** (unchanged for every registrant that exists today, all
of which are First-Party); **a higher-trust-tier registration always
wins over a lower one, regardless of order** — a First-Party registrant
claiming an Id a Verified-Signed or Unsigned-Local plugin registered
first **evicts and replaces** it, logging a loud, disclosed "Id ownership
override" event (never silent), rather than being rejected as a
duplicate. This directly answers the roadmap's own stated problem
("a plugin whose own Id sorts earlier… can legitimately claim a
well-known command or navigation Id before its real owner ever
registers") without inventing or retrofitting an Id-namespace-prefix
convention across every existing first-party Id — no such convention
exists in the codebase today, and manufacturing one would be a far more
invasive, riskier change than comparing two already-necessary tier
values.

## Signing Strategy

**Decision: a detached signature over a canonical hash of the manifest
(minus its own `Signature` field) concatenated with a hash of the
declared assembly file's bytes, verified using .NET's own
`System.Security.Cryptography` primitives (`RSA`/`X509Certificate2`) —
zero new NuGet dependency, consistent with `ADR-0005`'s reuse-first
mandate.**

**Payload construction:** `hash1 = SHA-256(canonical UTF-8 manifest JSON,
`Signature` field omitted, `System.Text.Json` default member-declaration
ordering, no indentation)`; `hash2 = SHA-256(raw bytes of the file named
by AssemblyFileName)`; `payload = hash1 ++ hash2` (64 raw bytes). The
publisher signs `payload` with `RSA.SignData` (RSA-PSS, SHA-256) using
their own private key; `Value` is that signature, Base64-encoded.

**Verification (entirely at Plugin Discovery, Phase 3.1 — before Plugin
Loading, before any `Assembly.LoadFrom` call):** resolve
`PublisherCertificateThumbprint` against the local trust store; recompute
`payload` from the manifest and assembly bytes sitting on disk;
`RSA.VerifyData` using the matched certificate's public key; confirm the
certificate's `NotBefore`/`NotAfter` window covers "now." **No network
call, no CRL/OCSP revocation check** — this platform has no network-facing
surface today (`Security Roadmap.md` item 7 is a separate, unfired
trigger), and a local-only verification model directly mirrors
`ADR-0043`'s own local-only identity precedent.

**Why Discovery, not Loading.** The signature covers only file bytes
(manifest text, assembly bytes) — verifying it requires no CLR assembly
load at all, only `File.ReadAllBytes`. Doing this at Discovery means a
signature failure is caught, and the untrusted binary is never even
`Assembly.LoadFrom`'d into the process — strictly safer than verifying
after Loading, and it fits `ADR-0026`'s own phase boundary exactly:
Discovery is side-effect-free validation, Loading is the harder-to-reverse
step. No new phase is introduced.

**Trust store.** A TempestOS-owned, flat-file store — a
`TrustedPublishers/` folder, fixed convention relative to
`AppContext.BaseDirectory`, containing one `.cer` (public-only X.509)
file per trusted publisher, read once during Plugin Discovery. This
mirrors the exact "fixed convention, not configurable yet, purely
additive to make configurable later" precedent `Plugin Manifest
Architecture.md` already established for the plugins root and manifest
file name — no OS-specific certificate-store API (Windows Cert Store,
etc.), keeping verification portable and dependency-free.
TempestOS's own first-party publisher certificate ships in this same
folder as a fixed, always-present entry — the one whose match assigns
the First-Party tier to a plugin-packaged first-party component.

**Establishing a "trusted publisher."** Purely operational for this
release: an operator (or this project's own release engineering, for a
future first-party-plugin scenario) places a `.cer` file into
`TrustedPublishers/`. No online CA integration, no automated
trust-chain-to-a-root validation beyond the one certificate matched by
thumbprint — a deliberately minimal, local trust model matching the
actual, disclosed threat this WP defends against (see Isolation
Boundary Decision, below) rather than a full PKI.

## Isolation Boundary Decision

**Decision: the isolation boundary is capability-scoped, in-process
enforcement — plus the Discovery/Loading-time signature and constructor
conformance checks above. No separate `AssemblyLoadContext` per plugin.
No process separation.**

**Stated plainly, for the sibling Plugin Architecture document: this
does not enable real per-plugin unload.** A loaded plugin remains loaded
for the life of the process run, exactly as `Plugin Manifest
Architecture.md`'s existing, disclosed Risk already states ("No assembly
unloading support… a loaded plugin — bad or good — stays loaded for the
process's entire life") and exactly as `ADR-0015` already establishes for
the Host itself. If the sibling's lifecycle design wants real unload,
that requires a separate, later decision to adopt a collectible
`AssemblyLoadContext` specifically for that purpose — this document does
not make that decision and does not unlock it as a side effect of trust
enforcement.

**Direct answer to `Plugin Platform Architecture.md`'s own reserved
`Loaded → Unloading → Unloaded` lifecycle seam (`ADR-0108`):** that
document's own text already anticipates this outcome ("If the sibling
document's isolation boundary does not, in fact, use a per-plugin
`AssemblyLoadContext` or equivalent collectible mechanism, `ADR-0108`'s
own reserved `Unloading`/`Unloaded` seam simply stays reserved, unused").
This decision is exactly that case — the seam stays reserved and unused
under this design, not removed, not built against, until a future,
separate decision adopts a collectible mechanism.

### Alternatives Considered

**A separate `AssemblyLoadContext` (ALC) per plugin.** Seriously
considered — it is the Security Roadmap's own first-named option, and it
is a real, available .NET mechanism. Rejected as the *trust* boundary
specifically, for one concrete, decisive reason: **`AssemblyLoadContext`
is not a security boundary in modern .NET.** Code Access Security and
AppDomain-based sandboxing were removed entirely from .NET Core; an ALC
governs assembly *identity and unload*, not *privilege*. A plugin loaded
into its own ALC still runs with the full process's own OS privileges,
can still call any public API of any type loaded anywhere in the process
(`AppDomain.CurrentDomain.GetAssemblies()` already returns assemblies
across every ALC in a single-AppDomain .NET Core process — the exact fact
`ADR-0026` already depends on), can still hold and use any DI-resolved
service reference it was ever given, and can still reach the filesystem,
network, or process APIs directly. Adopting an ALC here would add real
implementation complexity — collectible-context lifetime management, the
type-identity-across-context hazards that come with it — while closing
none of `TD-09`'s actual complaint. Its one genuine, real benefit —
enabling unload — is explicitly a Non-Goal today, for both this document
and the existing, implemented Plugin Manifest Architecture. Building it
now, for a benefit this release does not use, is exactly the "security
theatre ahead of a real need" `Security Principles.md` Principle 7
warns against.

**Separate OS process per plugin, with IPC to the host.** The only
mechanism available in modern .NET that provides a *genuine* privilege
boundary — a plugin running as a different OS-level principal, unable to
touch the host process's memory at all. Seriously considered, and
rejected for this WP: it is disproportionate to the actual, disclosed
threat. The trigger that opened this Work Package is a Product Owner
commitment to *vetted, signed, commercial third-party plugins* — not a
plugin marketplace accepting anonymous, unvetted, actively adversarial
publishers. Defending against a resourced adversary willing to reflect
past a permission check requires OS-process isolation; defending against
careless, unverified, or merely unaccountable third-party code — the
actual, named threat — does not. Process isolation would also
require redesigning DI resolution, event dispatch, and every module
constructor-injection point across an IPC boundary — a change an order
of magnitude larger than this WP's own brief, and one `Security
Roadmap.md` itself does not name as a current trigger.
**Revisit trigger:** TempestOS is ever asked to run genuinely
adversarial, unvetted third-party code (an open marketplace with no
publisher accountability) rather than signed, accountable commercial
plugins.

**Code-signing alone, with no capability scoping.** Seriously considered
— it is the cheapest option, and it does solve a real problem
(authenticity and tamper-detection: knowing *who* published a plugin).
Rejected as a *complete* answer: it says nothing about *what* a
legitimately-signed plugin may then do once loaded — a properly signed,
fully accountable plugin would still receive `TD-09`'s exact, unrestricted
DI-container trust. Signing and capability scoping solve different
problems (*who* vs. *what*) and are complementary, not substitutable —
this document adopts both, deliberately, rather than either alone.

**A manifest-declared capability/permission scope alone, with no
signing.** Seriously considered as the minimal option. Rejected: without
a signature, "the manifest declares X capabilities" is an unverifiable,
self-asserted claim with no accountability behind it — nothing prevents
a malicious manifest from simply declaring every capability it wants.
Signing is what makes the capability declaration mean something: a
Verified-Signed plugin's capability grant is backed by an identifiable,
accountable publisher; an Unsigned-Local plugin's request is clamped to
a fixed, low ceiling precisely because nothing backs its self-assertion.

**Combination — signing decides trust tier; capability scope, gated by
tier, decides runtime permission — CHOSEN.** Proportionate to the actual,
disclosed threat; reuses `ADR-0044`'s existing enforcement point almost
entirely unchanged; requires no new runtime dependency; and is honest
about its own residual limit (see Risks, below) rather than overclaiming
a hard sandbox it does not build.

## Failure Containment

A new, companion ADR (`ADR-0112`) extends `ADR-0025`'s eleven-category
failure classification table with four new categories — `ADR-0025`
itself is Accepted/historical and is not edited. **Numbered 15–18, not
12–14**: the sibling Plugin Architecture workstream's own `ADR-0107`
independently extends the same table for dependency-resolution failures
and already occupies categories 12–14 — this design's own categories
continue the single, project-wide sequence rather than colliding with an
already-claimed range. Full table and severity reasoning live in
`ADR-0112`; summarised here:

| # | Category | Classification | Severity |
|---|---|---|---|
| 15 | Signature present but fails to verify | Isolated | Error |
| 16 | No signature, and `Plugins:AllowUnsignedLoad` is not enabled | Isolated | Warning |
| 17 | Requested capability outside the assigned tier's ceiling, or a plugin module's constructor requires an undeclared/ineligible service type | Isolated | Warning |
| 18 | A running plugin attempts a capability-gated operation it was not granted | Isolated | Warning |

Category 18 deliberately mirrors `PermissionEvaluator`'s own existing
Warning-level convention for an ordinary denied permission check exactly
— a plugin capability denial is the identical mechanism applied to a new
principal kind, not a new severity policy. No category here is Host-fatal;
every one isolates to the one plugin (15–17) or the one blocked call (18),
consistent with `ADR-0025`'s own "isolate failure, not trust" discipline
(`Security Principles.md` Principle 2) extended, for the first time, to a
genuine trust violation rather than only a crash. Categories 15–17 are
recorded in the sibling document's own `IPluginRegistry` as
`PluginRegistryState.TrustDenied` — the sixth registry state
`Plugin Platform Architecture.md`'s own "The Boundary With Trust &
Isolation" section explicitly reserved for this decision. Category 18
has no `PluginRegistryState` at all: it occurs after the plugin is
already `Loaded` and running, long past Discovery/Loading.

**No automatic quarantine.** A plugin that repeatedly triggers category
18 is not automatically disabled — each attempt is independently blocked
and logged. A "three strikes" or similar policy is a Non-Goal here (see
below); it can be added later against real evidence of a plugin doing
this, not speculatively.

## Logging & Telemetry

No new telemetry pipeline is introduced. `Diagnostics Architecture.md`
already establishes the pattern this design reuses exactly:
`IDiagnosticsProvider` is a pure, read-only reporter over data two
Host-owned managers already maintain, with "no imperative registration
surface" and "no write access to the underlying managers." This
document's own security-relevant logging follows the identical shape,
through `ILogger`'s existing severity scale — no new sink, no new
pipeline:

- Every new failure category this document introduces (15–18) is logged,
  unconditionally, exactly as every one of `ADR-0025`'s existing eleven
  already is, and exactly as the sibling `ADR-0107`'s own categories
  12–14 already are — the plugin's
  manifest-declared `Id` (or candidate path if unreadable), the category,
  and the underlying reason. Never a credential or certificate private
  key material — only the certificate thumbprint (already a public,
  non-sensitive value), consistent with `PermissionEvaluator`'s own
  existing "principal Id and permission key only, never a credential"
  logging convention.
- A tier-priority-override eviction (the `TD-11` fix) is always logged,
  at Warning, regardless of how routine it becomes — mirroring `ADR-0025`'s
  own "even a benign, expected category is always logged" rule
  (category 4's Information-level-but-always-logged precedent).

**Recommended, additive extension — not designed here, not this
document's to build:** `IDiagnosticsProvider` (or a small, sibling
read-only type following its exact snapshot-type shape —
`ModuleLifecycleStatus`/`HostedServiceStatus`'s own precedent) should
gain a `Plugins` collection reporting each candidate plugin's `Id`,
assigned trust tier, signature outcome, and granted capability set —
purely for operator/diagnostic visibility, never write access, never a
new registration surface. This is a direct, natural extension of the
existing, established Diagnostics pattern, not a new mechanism — named
here as a recommendation for `WP 13.0B`/a future Diagnostics update to
build, not designed in full by this document.

## Non-Goals

Explicitly not designed here, each with its own named revisit trigger:

- **A DI resolution interceptor checking every individual `GetService`
  call against a plugin's declared capabilities.** The construction-time
  reflection check (above) closes the realistic case — a plugin's own
  module constructor — without one. A plugin's own code calling
  `IServiceProvider.GetService` directly (bypassing constructor
  injection, if it ever obtains an `IServiceProvider` reference at all)
  is not intercepted by this design. **Revisit trigger:** real evidence,
  from `WP 13.0B`'s own implementation or a real plugin, that
  constructor-time gating alone is insufficient.
- **Wildcard/glob capability keys** (for example, `plugin.events.publish:*`).
  Every `plugin.events.publish:<Type>`/`plugin.services.resolve:<Type>`
  key names one exact type. **Revisit trigger:** a real plugin needing
  broad, dynamic event-type coverage makes an exact-key manifest
  genuinely unworkable, not merely verbose.
- **Automated quarantine/disable-on-repeated-violation policy.**
  **Revisit trigger:** real evidence of a plugin repeatedly attempting
  denied operations in a way that itself becomes a problem (log noise,
  a denial-of-service pattern against logging itself).
- **`AssemblyLoadContext`-based unload/hot-reload.** Entirely separate
  from this document's trust decision — see Isolation Boundary Decision.
  **Revisit trigger:** a real, demonstrated need for in-process plugin
  unload or update-without-restart (mirrors `ADR-0015`'s own identical
  "layer a supervisor above `TempestHost`, don't retrofit reset
  semantics" reasoning, applied one level down).
- **Online CA / CRL / OCSP revocation checking.** **Revisit trigger:**
  `Security Roadmap.md` item 7 (API and networking exposure) fires —
  revocation checking is inherently a network-facing capability this
  platform does not have today.
- **Runtime-mutable trust store or capability administration UI.**
  Mirrors `ADR-0043`'s identical "no administration UI, edit
  configuration/files directly" precedent for identity/role grants.
  **Revisit trigger:** the same trigger `ADR-0043` names for its own
  eventual reconsideration — a genuine multi-user/administered
  deployment scenario.

## Risks

- **This is not a hard sandbox — disclosed plainly, not overclaimed.**
  A sufficiently determined plugin, in-process, could in principle use
  reflection to reach past a `RequirePermission` call site (for example,
  invoking a private member directly, or holding a service reference
  obtained before a capability was revoked — though grants never change
  mid-run, so the latter cannot occur). This design closes the
  *ordinary, cooperative API surface* — exactly the surface `TD-09`,
  `TD-10`, `TD-11`, and `CMD-1` describe — matching `Security
  Principles.md` Principle 2's own honest distinction ("isolated from
  crashing the Host" and "isolated from the rest of the process" are
  different properties). It does not, and does not claim to, defend
  against an actively adversarial assembly willing to use reflection to
  bypass its own declared trust — that requires OS-process isolation
  (see Alternatives Considered), explicitly out of scope, with its own
  named revisit trigger.
- **The construction-time conformance check depends on Plugin Loading
  reflecting over the plugin's own `IModule` types independently of
  Module Discovery's later, official scan.** A divergence between the two
  scans (for example, a future Module Discovery change not mirrored here)
  would be a real, if narrow, maintenance hazard. `WP 13.0B` should share
  the underlying type-scanning logic with `IFrameworkDiscoveryService`
  where practical, rather than duplicating it independently.
- **`Plugins:AllowUnsignedLoad` is a single, global switch**, not
  per-plugin. An operator who enables it for one legitimately-unsigned
  internal tool also permits every other unsigned candidate in the
  Plugins folder to load under the same clamped ceiling. Accepted for
  v1 — a per-plugin allow-list is purely additive if real need for finer
  granularity emerges.

## ADRs Required

Three ADRs, numbered `ADR-0110`–`ADR-0112`, each independently meeting
Engineering Governance §5's criteria (a genuine, seriously-considered
alternative was rejected for each; each establishes a convention future
plugin-related work depends on; each would be expensive to reverse once
plugins exist that depend on it):

1. **`ADR-0110`** — the isolation-boundary mechanism decision (capability
   scoping, not ALC or process separation), stating the no-unload
   consequence explicitly.
2. **`ADR-0111`** — the capability/permission model extending `ADR-0044`
   (the component-principal axis, the trust-ordered registration rule).
3. **`ADR-0112`** — the signing mechanism, and the `ADR-0025`
   failure-classification extension (categories 15–18) that follows
   directly from it.

## Recommendation

Adopt all three decisions as a single, coherent package — `WP 13.0B`'s
implementation brief. Do not implement any one in isolation: the
capability model (`ADR-0111`) is unenforceable without a trust tier to
gate it (`ADR-0112`'s signing decision), and the signing decision alone
closes none of `TD-09`/`TD-10`/`TD-11` without the enforcement calls
`ADR-0111` designs. Sequence within `WP 13.0B` is an implementation
decision, not an architectural one — this document does not mandate an
order.

## Related Documents

`Security Roadmap.md` items 1, 2, 3, 10; `Threat Model.md` (Scenario 1,
2, 3); `Security Principles.md` Principles 2, 3, 6, 7;
`docs/governance/Quality/Technical Debt Register.md` `TD-09`, `TD-10`,
`TD-11`; `docs/governance/Future Capability Register.md` `FCR-0001`,
`FCR-0020`; `ADR-0043`, `ADR-0044`, `ADR-0025`, `ADR-0026`, `ADR-0015`;
`ADR-0032`, `ADR-0037` (the already-Accepted Navigation/Command
registration behaviour `ADR-0111`'s own trust-ordered registration rule
additively revises — see that ADR's own Decision section for the full
acknowledgement);
`docs/architecture/Plugin Manifest Architecture.md`;
`docs/architecture/Command Framework Architecture.md` (Finding `CMD-1`);
`docs/architecture/Diagnostics Architecture.md`; `ADR-0110`, `ADR-0111`,
`ADR-0112` (this document's own companion decisions);
`docs/architecture/Plugin Platform Architecture.md` and its own
`ADR-0107`/`ADR-0108`/`ADR-0109` (the sibling Plugin Architecture
workstream's own manifest v2, dependency, lifecycle, and DI-registration
design this document composes with — see "The manifest field shape"
above for the reconciled `RequestedCapabilities`/`Publisher`/`Signature`
shape, and the Isolation Boundary Decision for this document's own
answer to that document's reserved `Unloading`/`Unloaded` lifecycle
seam).
