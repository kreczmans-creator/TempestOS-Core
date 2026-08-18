# Plugin Trust & Isolation Architecture

## Status

**Status: Implemented — `v0.13.0`.** Designed `WP 13.0A`; this document is
the direct answer to `Security Roadmap.md` items 1, 2, and 10,
commissioned by the Product Owner's confirmed commitment to third-party
plugins (`docs/releases/v0.13.0/WorkPackages.md`). It designs the
retrofit `ADR-0044` explicitly deferred and named as the future work that
would close `TD-09`, `TD-10`, and `TD-11`. **Not `WP 13.0B`** (which this
document's own original Recommendation section, below, named as the
anticipated implementer — `WP 13.0B` was in fact commissioned as an
independent architecture review of this document instead, a disclosed
divergence recorded in `docs/releases/v0.13.0/WorkPackages.md`'s own
`WP 13.0B` row); the real retrofit was implemented by `WP 13.2A`
(`ADR-0110`–`ADR-0112`: trust tier assignment, detached-signature
verification, capability enforcement, the component-principal model),
independently reviewed by `WP 13.2B`, and independently re-verified end
to end by `WP 13.3A`/`WP 13.3B` — closing `TD-09`, `TD-10`, and `TD-11`
in full. `WP 13.9.0`'s own Security/Trust review subsequently found a
genuine, empirically-demonstrated multi-assembly trust-boundary bypass in
the implemented enforcement mechanism's own scope, not previously tracked
anywhere — see this document's own Risks section, below, for the full
account of that finding, `WP 13.9.1` Security Remediation's own partial
fix, and `WP 13.9.3`'s subsequent full closure after `WP 13.9.2`'s
re-verification found the `WP 13.9.1` fix's own scan still incomplete.
`WP 13.9.3`'s own Adversarial Review then found a second, separate, more
severe defect — trust denial never actually prevented a denied plugin's
module from being discovered, registered, and fully lifecycle-run —
closed by `WP 13.9.4`; `WP 13.9.4`'s own Adversarial Review, in turn,
found a third, sibling defect in that same closure (Hosted Service
Registration was a second, unfiltered pipeline), closed within the same
Work Package. `WP 13.9.5`'s own independent, final review then found a
fourth, distinct defect — Module Discovery itself constructed a denied
plugin's unattributed module before the `WP 13.9.4` execution boundary
was ever reached, in one variant crashing the Host entirely — closed by
`WP 13.9.6`, itself verified by a further fresh, independent adversarial
review finding no remaining gap. See the Risks section's own dedicated
entries for each. **`WP 13.10A`'s own architecture/hardening review**
(read-only, no implementation) subsequently found a further, distinct
gap in the same static enforcement mechanism — it was never extended to
`IHostedService` types at all, and a DI-public, un-denylisted interface
reached the ambient-identity write surface the denylist exists to keep
out of plugin hands (`TD-51`/`TD-52`) — closed by `WP 13.10B`, itself
finding and fixing a compounding Host-crash regression mid-implementation
before any commit, independently re-verified by four fresh, read-only
`WP 13.10C` reviewers before this closure was committed. See the Risks
section's own "Closed, `WP 13.10B`" entry, and `ADR-0111`'s own
"Corrected, `WP 13.10B`" note. **`WP 13.11A`'s own final hardening
architecture review** (read-only, six parallel disciplines) then
re-examined `WP 13.10C`'s own "safely isolated, no crash" characterisation
of `TD-51`'s disclosed residual gap and found it false for the `IModule`
axis — the identical class of Host-crash defect `WP 13.9.5`/`WP 13.9.6`
already closed once, for a different specific denial path, reopened
through a newer one (`WP 13.9.3`'s own reflection guard, later widened by
`WP 13.10B`) that never wires into `WP 13.9.6`'s own crash-prevention
filter. **Closed, `WP 13.11B`** — the denial path now records every type
its own fixed-point scan discovered before throwing, so `WP 13.9.6`'s own
Module Discovery filter genuinely excludes it, and `CreateDescriptor`
gained the same fail-closed reflection guard as a backstop. See the Risks
section's own "Reopened `WP 13.11A`, closed `WP 13.11B`" entry for the
full account and `Technical Debt Register.md`'s own updated `TD-51`
Status cell.
Corrected `WP 13.9.1` Governance & Documentation Remediation
(`WP13.9.0 Engineering Release Report.md`'s own Governance-readiness
Finding 3): only this Status header and the stale `WP 13.0B` implementer
citation in Recommendation, below, were out of date — this document's
own underlying technical content was independently confirmed accurate
throughout by `WP13.9.0` and required no correction of its own beyond
the Risks-section update Security Remediation made directly, in parallel,
for the newly-found trust-boundary finding.

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
here as a recommendation for a future Diagnostics update to build, not
designed in full by this document. **Status, `WP 13.9.1`: still open** —
confirmed directly, `PluginRegistryEntry` (built by `WP 13.1A`) carries
only `Id`/`Name`/`Version`/`State`/`Detail`; no trust tier, signature
outcome, or granted capability set field exists yet anywhere in
`IDiagnosticsProvider.Plugins`'s own projection.

## Non-Goals

Explicitly not designed here, each with its own named revisit trigger:

- **A DI resolution interceptor checking every individual `GetService`
  call against a plugin's declared capabilities.** The construction-time
  reflection check (above) closes the realistic case — a plugin's own
  module constructor — without one. A plugin's own code calling
  `IServiceProvider.GetService` directly (bypassing constructor
  injection, if it ever obtains an `IServiceProvider` reference at all)
  is not intercepted by this design. **Revisit trigger:** real evidence,
  from the retrofit's own implementation (`WP 13.2A`, not `WP 13.0B` —
  see Status, above) or a real plugin, that constructor-time gating alone
  is insufficient.
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
  would be a real, if narrow, maintenance hazard. The implementing Work
  Package (`WP 13.2A`, not `WP 13.0B` — see Status, above) should share
  the underlying type-scanning logic with `IFrameworkDiscoveryService`
  where practical, rather than duplicating it independently.
- **Closed, `WP 13.9.3` (corrected from an earlier, incomplete `WP
  13.9.1` closure claim): the multi-assembly trust-boundary bypass `WP
  13.9.0`'s Security/Trust review found.** Until the `WP 13.9.1` fix,
  `PluginAssemblyLoader.EnforceTrust`'s own construction-time conformance
  check (the bullet directly above) scanned only the one manifest-declared
  assembly's own types — but .NET only loads a referenced assembly
  lazily, the moment one of its types is resolved, and Module Discovery
  (deliberately plugin-unaware, `ADR-0110`) scans the entire process
  `AppDomain` regardless of which assembly a type came from. A plugin
  packaging a second, wholly undeclared assembly in its own candidate
  folder — with a type in its primary, manifest-declared assembly
  inheriting from a type in that second assembly — had that second
  assembly's own `IModule` implementers reach Module Discovery with zero
  trust checking of any kind: no capability check, no constructor-
  conformance check, no component principal recorded, so its ambient
  component principal was `null` and therefore treated as First-Party
  (`PluginTrustPermission.IsFirstParty(null) == true`). Empirically
  demonstrated by `WP 13.9.0`'s Security/Trust review against this
  project's own compiled binary — a genuine, previously-untracked
  Release Blocking finding, not a `TD-49`/`TD-50`-adjacent theoretical
  risk. `WP 13.9.1` partially closed it: `EnforceTrust` gained a
  fixed-point, breadth-first scan of every assembly that enters the
  `AppDomain` as a direct or transitive side effect of examining the
  plugin's own primary assembly (mirroring `PluginManifestDiscoveryService`'s
  own dependency-graph fixed-point idiom), applying the identical
  constructor-conformance check to every discovered `IModule` type across
  every scanned assembly and recording a component principal for each one
  — but this scan's own AppDomain diff was taken only around
  `Assembly.GetTypes()`/`IsAssignableFrom` (the touchpoint that resolves a
  discovered type's own base-type chain), not around the independent CLR
  lazy-load trigger a discovered module's own constructor parameter types
  represent (`ConstructorInfo.GetParameters()`/`ParameterInfo.ParameterType`).
  `WP 13.9.2`'s re-verification found this gap still empirically
  exploitable: a second, wholly undeclared assembly reachable only through
  a non-compliant constructor's own parameter type — never through any
  base type — again reached Module Discovery with zero trust checking,
  including a more severe variant where the same module also exposes an
  alternate, individually-compliant constructor and so is not even
  rejected on its own conformance check. `WP 13.9.3` closed it in full:
  each fixed-point scan step now also forces resolution of every
  discovered module type's every public constructor's every parameter's
  `ParameterType` before that step's own AppDomain diff is taken, so
  either lazy-load trigger is captured by the step that caused it, not
  invisible to it. Non-vacuous regression coverage added for both the
  direct case and the alternate-compliant-constructor case, plus a
  three-assembly transitive chain proving the fix generalises beyond a
  single extra hop, and a benign multi-assembly-with-granted-capability
  case proving no legitimate plugin regresses. See `ADR-0111`'s own
  "Corrected, `WP 13.9.3` Multi-Assembly Trust-Boundary Remediation" note.
  This does not widen the isolation boundary itself — no
  `AssemblyLoadContext`, no process separation is introduced; the fix
  only widens the existing capability-scoped enforcement mechanism's own
  coverage to the plugin's real, complete footprint, and the fixed-point
  traversal's own termination guarantee (a visited-assembly `HashSet`) is
  unchanged.
- **Closed, `WP 13.9.4`: trust denial did not actually gate downstream
  execution.** A genuinely separate, more severe defect than the
  multi-assembly scan gap above — found by `WP 13.9.3`'s own Adversarial
  Review while verifying that fix, independently reconfirmed by
  `WP 13.9.4`'s own Security workstream with a fresh, live
  proof-of-concept. `PluginTrustDeniedException` isolated a denied
  plugin only from `PluginAssemblyLoader.LoadPlugins`'s own returned
  list and `PluginRegistryState.Loaded` — nothing stopped the plugin's
  already-loaded assembly (`Assembly.LoadFrom` runs before this check;
  ADR-0015: that step cannot be undone) from being separately,
  redundantly rediscovered by Module Discovery (deliberately
  plugin-unaware, `ADR-0110`) and fully lifecycle-run
  (`InitialiseAsync`/`StartAsync`), indistinguishable from first-party
  code: a denied module's ambient component principal is always
  `null`, and `null` is treated as First-Party
  (`PluginTrustPermission.IsFirstParty`), so every dynamic capability
  check downstream (Command/Navigation/Event registration) was skipped
  too. True of every denial reason — both constructor non-compliance and
  capability-ceiling exceedance — not only the multi-assembly case; this
  has been true since `WP 13.2A` first introduced trust denial. Closed
  by a new, small, additive filter entirely within `TempestHost`'s own
  orchestration, between Module Discovery's output and Module
  Registration: every discovered `IModule` type belonging to a denied
  plugin is now recorded (`IPluginDeniedTypeRecorder`/
  `IPluginDeniedTypeRegistry`, mirroring
  `IPluginComponentPrincipalRecorder`/`IPluginComponentPrincipalRegistry`'s
  own established pattern exactly) and excluded before
  `RuntimeModuleManager.Register` ever sees it — no `Type` a denied
  plugin's own trust-evaluation scan discovered can reach Registration,
  and therefore never Lifecycle, and therefore never Command/
  Navigation/Event registration. Required reordering `EnforceTrust` so
  its own `DiscoverModuleTypes` scan runs unconditionally, before either
  static check, not only ahead of the constructor-conformance check —
  otherwise a capability-ceiling denial (which previously
  short-circuited before any module-type discovery ran at all) would
  still have had no data to record.
  **This fix's own first pass was itself incomplete** — found by `WP
  13.9.4`'s own Adversarial Review before this Work Package concluded, not
  by a later one: Module Discovery/Registration is only one of two wholly
  independent discovery/registration pipelines a denied plugin's
  already-loaded assembly can be found through. `BackgroundServices.HostedServiceDiscoveryService`/
  `BackgroundServices.IHostedServiceManager` is a second, equally
  plugin-unaware pipeline with no relationship to the first — a single
  `Type` implementing both `IModule` and `IHostedService`, correctly
  excluded from Module Registration by the first-pass fix, still reached
  `StartAsync` unfiltered through Hosted Service Registration, live-PoC
  confirmed. `DiscoverModuleTypes` was broadened, in the same scan pass,
  to also collect every discovered `IHostedService` implementer (recorded
  only on denial — never constructor-checked, never granted a component
  principal, matching this type's own existing, unrelated lack of a
  component-scope hook for hosted services); `TempestHost` gained an
  identical filter at the Hosted Service Discovery → Registration
  boundary, reading the same registry (renamed `IPluginDeniedTypeRecorder`/
  `IPluginDeniedTypeRegistry`, from the first pass's narrower
  `IPluginDeniedModuleTypeRecorder`/`IPluginDeniedModuleTypeRegistry`, since
  it is no longer `IModule`-scoped) — one registry, keyed on `Type` alone,
  correctly excludes a dual-interface type from both pipelines regardless
  of which one would otherwise have found it first.
  `ReflectionFrameworkDiscoveryService`, `RuntimeModuleManager`,
  `Modules.ModuleLifecycleManager`, `HostedServiceDiscoveryService`, and
  `IHostedServiceManager` themselves gain no trust awareness and no other
  change — both discovery services remain deliberately plugin-unaware, per
  `ADR-0110`; only `TempestHost`'s own composition-root orchestration
  gained the two filters. No new isolation mechanism, no
  `AssemblyLoadContext`, no process separation. See `ADR-0111`'s own
  "Corrected, `WP 13.9.4` Trust-Denial Execution Boundary Remediation"
  note.
- **Closed, `WP 13.9.6`: Module Discovery itself constructed a denied
  plugin's unattributed module before the `WP 13.9.4` execution boundary
  was ever consulted.** A third, distinct defect in the same closure —
  found by `WP 13.9.5`'s own final adversarial review (three independent
  reviewers, each with a separate live proof-of-concept against the
  unmodified pipeline), not by `WP 13.9.4` itself.
  `ReflectionFrameworkDiscoveryService.CreateDescriptor`'s own
  metadata-reading convention (`ADR-0027`) calls
  `Activator.CreateInstance` for any `IModule` candidate lacking
  `[ModuleMetadataAttribute]`, purely to read `Id`/`Name`/`Version` — a
  pre-plugin-trust convention (`WP 5.3`) that was never a security concern
  while every module was first-party by construction. Module Discovery
  runs after Plugin Loading (which already knows, and records, every
  denied plugin's full type set) but *before* the `WP 13.9.4` Module
  Registration filter is ever reached — so a denied plugin's unattributed
  module constructor genuinely ran: real, verified code execution,
  live-PoC confirmed three separate times. A more severe variant was also
  found and confirmed: a denied, unattributed module with *no* public
  parameterless constructor hits `CreateDescriptor`'s own
  `ModuleDiscoveryException` guard, **uncaught** inside the Discovery
  loop — Host-fatal, crashing `RunAsync` entirely (a denial-of-service any
  plugin author could trigger deliberately). `Plugins:AllowUnsignedLoad`
  is irrelevant to either: capability-ceiling denial alone is a sufficient
  precondition, requiring no constructor trickery at all. Closed by a
  small, additive `Func<Type, bool>` predicate (`isTypeExcluded`) threaded
  into `ReflectionFrameworkDiscoveryService`'s own existing constructor,
  consulted inside its existing candidate loop immediately after the
  existing `IsValidModuleType` check and strictly before `CreateDescriptor`
  is ever called — `TempestHost` supplies `deniedTypeRegistry.IsDenied`,
  the same, unmodified `WP 13.9.4` registry. Defaults to `null`
  (never-excluded) for every existing caller/test; `Modules` gains no
  reference to `Plugins` (the predicate is generic), preserving
  `ADR-0110`'s "deliberately plugin-unaware" status at the type-reference
  level exactly as the `WP 13.9.4` Registration filter already did. The
  existing Registration-time filter remains in place, unchanged, as
  harmless defense-in-depth for the module pipeline and still fully
  load-bearing for Hosted Service Registration — confirmed, independently,
  to need no equivalent fix (`HostedServiceDiscoveryService` never
  instantiates a candidate at all). Verified non-vacuous by mutation
  testing (temporarily disabling the guard reproduced both the quiet
  execution and the `Faulted`-crash failure modes exactly) and by a fresh,
  independent adversarial review with its own separate, standalone
  proof-of-concept, finding no remaining or newly-introduced gap. See
  `ADR-0111`'s own "Corrected, `WP 13.9.6` Module Discovery Trust Boundary
  Remediation" note.
- **`Plugins:AllowUnsignedLoad` is a single, global switch**, not
  per-plugin. An operator who enables it for one legitimately-unsigned
  internal tool also permits every other unsigned candidate in the
  Plugins folder to load under the same clamped ceiling. Accepted for
  v1 — a per-plugin allow-list is purely additive if real need for finer
  granularity emerges.
- **Closed, `WP 13.10B`: the entire static
  constructor-conformance/denylist check applied only to `IModule`,
  never to `IHostedService`, and a DI-public, un-denylisted interface
  reached the exact ambient-identity write surface the denylist exists
  to keep out of plugin hands.** Found by `WP 13.10A`'s own Security/
  Trust and Lifecycle/Composition reviewers independently, each with a
  live proof-of-concept against the current, committed code (not a
  theoretical concern). Two distinct findings, compounding:
  - **`HasCompliantConstructor` — and, with it, the
    `NeverEligibleServiceResolveTypes` denylist — was invoked only
    against `moduleTypes`, never `hostedServiceTypes`**
    (`PluginAssemblyLoader.EnforceTrust`). A plugin assembly containing
    *only* an `IHostedService` implementer, zero `IModule` types, hit
    `moduleTypes.Count == 0` and returned immediately after the
    (trivially-satisfied, empty) capability-ceiling check — its hosted
    service's constructor was never inspected at all. Confirmed live: an
    `UnsignedLocal` manifest requesting zero capabilities, one
    `IHostedService` type whose sole constructor took
    `Identity.CurrentComponentAccessor` (the concrete, denylisted,
    identity-forging type), loaded via `PluginAssemblyLoader.LoadPlugins`
    — accepted, not denied. Compounded by `HostedServiceManager` having
    no `componentScopeProvider`-equivalent hook at all (confirmed
    directly in `TempestHost.cs`/`HostedServiceManager.cs`) — a
    *legitimately-passing* plugin's own hosted service ran with a
    `null` ambient component principal, identical in effect to a denied
    one, so every dynamic capability check downstream
    (`PluginTrustPermission.IsFirstParty(null) == true`) was silently
    skipped for it too.
  - **`IIdentityService.EstablishCurrentPrincipal` called
    `ICurrentPrincipalAccessor.SetCurrent` directly**, and `IIdentityService`
    is an ordinary, DI-public, un-denylisted interface every sample
    module already legitimately depends on. A plugin declaring
    `plugin.services.resolve:Tempest.Core.Identity.IIdentityService` — an
    innocuous-looking capability request — passed `EnforceTrust`
    cleanly (confirmed live), then could call `EstablishCurrentPrincipal`
    for *any* configured identity at runtime, with no ownership check;
    because `ICurrentPrincipalAccessor` is deliberately not
    call-chain-scoped (`ADR-0044`), the effect persisted ambiently for
    every later, unrelated caller in the process.

  Neither finding was a variant of anything the `WP 13.9.1`–`WP 13.9.6`
  remediation chain closed — all six of those rounds scoped "the plugin
  trust boundary" as coextensive with the `IModule` pipeline; none asked
  whether the same static enforcement applies uniformly to every way a
  plugin's own code can be discovered, constructed, or granted ambient
  identity. Closed without any new isolation mechanism and without a new
  ADR — the fix reuses existing mechanisms exactly as both reviewing
  disciplines' own independent `WP 13.10A` assessment anticipated:
  `HasCompliantConstructor`/the denylist now run identically against
  `hostedServiceTypes`, not only `moduleTypes`; `HostedServiceManager`
  gained an optional `componentScopeProvider` constructor hook
  (`Func<Type, IDisposable?>`), mirroring `ModuleLifecycleManager`'s own
  established `ADR-0111` hook, held for the duration of each
  `StartAsync`/`StopAsync` call, `TempestHost` supplying a non-null
  provider closing over the same `ICurrentComponentAccessor`/component-
  principal registry; `EstablishCurrentPrincipal` gained a new dynamic
  `IPermissionEvaluator.RequirePermission` gate against a new capability
  key, `plugin.identity.establish` (`PluginCapability.IdentityEstablish`),
  mirroring `NavigationService.Register`'s own existing gate exactly —
  skipped, not merely satisfied, for a `null`/First-Party ambient
  component principal, so every existing first-party caller observes
  zero behavioural change.

  **`WP 13.10B`'s own independent Adversarial Security review found and
  fixed a compounding regression, mid-implementation, before any
  commit**, disclosed here in full rather than only in the fix's own
  commit message: `DiscoverModuleTypes`'s own `WP 13.9.3` pre-resolution
  loop (forcing every discovered type's every constructor parameter to
  resolve inside that scan step's own AppDomain-diff window) originally
  iterated `moduleTypes` only. Once `HasCompliantConstructor` was
  extended, above, to also check `hostedServiceTypes`, an
  `IHostedService`-only plugin with a genuinely unresolvable
  constructor-parameter type reached `HasCompliantConstructor`'s own
  `GetParameters()` call as the *first* resolution attempt for that
  type — with no exception handling anywhere in reach — throwing
  uncaught out of `LoadPlugins` and `TempestHost.RunAsync` entirely: a
  Host-wide crash, strictly worse than the gap being closed, not merely
  one denied plugin. Found independently twice: the `IModule`-only
  version of this exact pre-resolution fix had already landed when a
  second reviewer reproduced the identical gap for hosted services; a
  first attempted fix also placed its own `try`/`catch` one level too
  deep (around the per-parameter body, not `constructor.GetParameters()`
  itself, which eagerly resolves every parameter's own signature the
  moment it is called) and never actually caught anything for its own
  documented scenario — corrected before landing. Closed by iterating
  both `moduleTypes` and `hostedServiceTypes` uniformly in the
  pre-resolution loop, converting the resulting
  `TypeLoadException`/`FileNotFoundException`/`FileLoadException`/
  `BadImageFormatException` into a `PluginTrustDeniedException`,
  isolating the one plugin exactly like any other trust-check failure,
  never the whole Host. `WP 13.10C`'s own Verification/RAM-concurrency
  reviewer found this specific scenario had only ever been proven fixed
  via throwaway proof-of-concept code across two independent reviewers,
  never a permanent regression test — closed directly by `WP 13.10C`
  itself (`LoadPlugins_FirstHostedServiceOnlyPluginHasUnresolvableConstructorParameterType_IsolatesFailure_SecondLegitimatePluginStillLoads`,
  non-vacuousness independently confirmed: reverted, observed to fail,
  restored).

  **Two further items disclosed by `WP 13.10C`'s own Security/Adversarial
  reviewer, deliberately not fixed here to avoid scope creep beyond this
  closure's own remit:**
  - A small, low-severity, pre-existing (not `WP 13.10B`-introduced) gap:
    the "unresolvable constructor parameter type" denial path throws
    `PluginTrustDeniedException` directly from inside
    `DiscoverModuleTypes`, before `EnforceTrust` ever reaches a
    `RecordDenied` call site — so that one specific denied type is never
    added to `deniedTypeRegistry` (`WP 13.9.4`'s own execution-boundary
    registry), and a second, independent construction attempt is made
    during Module/Hosted Service Registration, safely failing for the
    identical reason (no code execution, a `Failed` state, not a crash).
    Confirmed pre-existing since `WP 13.9.3` for the `IModule` axis —
    `WP 13.10B` merely widened this already-existing gap's reach to the
    `IHostedService` axis too, it did not create a new category of
    defect. See `docs/governance/Quality/Technical Debt Register.md`
    `TD-51`'s own updated Status cell for the recommended future
    follow-up.
  - A governance-only note, not a code defect: `plugin.identity.establish`
    is grantable to any trust tier above `UnsignedLocal` (including
    `VerifiedSigned`, not only `FirstParty`), and `EstablishCurrentPrincipal`
    accepts an arbitrary `identityId` with no ownership check by design
    (`ADR-0043`, no authentication) — granting this one capability to any
    signed third-party publisher is effectively full ambient-identity
    impersonation power, a materially broader blast radius than every
    other v1 capability. Inherent to the capability's own necessary
    semantics, not a code defect; warrants explicit governance sign-off
    before any third-party publisher is actually granted it. See
    `TD-52`'s own updated Status cell.

  See `ADR-0111`'s own "Corrected, `WP 13.10B`" note for full detail.
- **Reopened `WP 13.11A`, closed `WP 13.11B`: the "unresolvable constructor parameter type"
  residual gap disclosed immediately above (the redundant, second
  construction attempt) is not, in fact, always safe.** `WP 13.10C`
  characterised it uniformly as "no code execution, a `Failed` state, not
  a crash" — true for the `IHostedService` axis (`HostedServiceManager.
  StartServiceAsync`'s own generic `try`/`catch` genuinely isolates it),
  but false for the `IModule` axis. Traced end to end by `WP 13.11A`'s own
  Security/Adversarial reviewer, independently re-confirmed against
  current source before this document was updated:
  - The denial path throws `PluginTrustDeniedException` directly from
    inside `PluginAssemblyLoader.DiscoverModuleTypes`, before
    `EnforceTrust` ever reaches a `RecordDenied` call site — exactly as
    already disclosed above — so the offending type is never added to
    `deniedTypeRegistry`.
  - `TempestHost.cs`'s own `WP 13.9.6` Module Discovery filter
    (`isTypeExcluded: deniedTypeRegistry.IsDenied`) — built specifically
    to stop an unattributed module belonging to a denied plugin from
    ever being constructed during discovery — therefore never excludes
    this type. Module Discovery is deliberately plugin-unaware
    (`ADR-0110`) and independently rescans the whole `AppDomain`,
    rediscovering it.
  - `ReflectionFrameworkDiscoveryService.CreateDescriptor`'s own
    `type.GetConstructor(Type.EmptyTypes)` call (reached for any module
    lacking `[ModuleMetadataAttribute]`) is not wrapped in any exception
    handling. Empirically confirmed: this call throws an uncaught
    `TypeLoadException`/`FileNotFoundException` when *any* of the type's
    own constructors — not only the parameterless one being matched —
    names a parameter type that cannot be resolved, mirroring the
    identical eager-signature-resolution CLR behaviour `WP 13.10B`'s own
    `GetParameters()` investigation already established for a different
    call site.
  - Nothing catches this exception anywhere between `TempestHost.
    ExecuteStartupPhasesAsync`'s call to `discovery.DiscoverModules()`
    and `RunAsync`'s own outer `catch (Exception ex) { EnterFaulted(ex);
    throw; }` — it propagates out of `RunAsync` entirely, a genuine Host
    crash, not a denied plugin.

  Reachable by a single, otherwise-inert `IModule` type — any trust
  tier, zero requested capabilities, no `[ModuleMetadataAttribute]`, any
  constructor naming an unresolvable parameter type — directly against
  `ADR-0025`'s own founding guarantee that a plugin-scoped failure must
  never become a platform-wide outage, the exact guarantee this whole
  `WP 13` plugin trust chain exists to provide. Not release-blocking for
  `v0.13.0` as currently shipped (`src/Plugins/` is empty today — no real
  plugin assembly exists to trigger this path), but must be closed before
  third-party plugin support is actually enabled or advertised, matching
  `TD-51`'s own original `WP 13.10A` framing exactly. **Closed,
  `WP 13.11B`** — both recommended options taken, and no wider. The root
  cause first: `DiscoverModuleTypes`'s own unresolvable-parameter failure
  no longer throws from inside the scan, but is surfaced to `EnforceTrust`
  through an `out PluginTrustDeniedException?`, which calls `RecordDenied`
  for every discovered `IModule` and `IHostedService` type before throwing
  it — so `WP 13.9.6`'s own filter correctly excludes the type and
  `CreateDescriptor` is never reached for it. Note this is deliberately
  *not* the "whatever partial type list had already been scanned" shape
  recommended above: `WP 13.11B`'s own Security/Adversarial analysis found
  that shape actively unsafe, because aborting the fixed-point traversal
  leaves an assembly already resident in the `AppDomain` — enqueued but
  not yet dequeued, or pulled in earlier in the same step — unscanned and
  unrecorded, while Module Discovery's own plugin-unaware `AppDomain` scan
  still sees it. A well-formed module among those (attributed,
  parameterless constructor) would then have been registered and
  lifecycle-run with a `null`, and therefore First-Party-treated
  (`PluginTrustPermission.IsFirstParty`), ambient component principal:
  a silent trust bypass in place of a crash, strictly worse and not
  fail-closed. The scan is therefore allowed to complete — the failing
  type's remaining constructors are skipped, the enclosing loops continue,
  and the per-step before/after `AppDomain` diff still runs. Then the
  backstop: `ReflectionFrameworkDiscoveryService.DiscoverModules` now
  wraps its own `CreateDescriptor` call in the same four-exception guard
  already used at other call sites in this codebase, excluding and logging
  the candidate instead of faulting the Host. That class gains no plugin
  awareness (ADR-0110) — it is a reflection guard, not a trust decision,
  and the guard is deliberately narrow: `ModuleDiscoveryException` derives
  from none of the four, so the `WP 5.3` "no parameterless constructor and
  no `[ModuleMetadataAttribute]`" guidance still propagates unchanged. See
  `Technical Debt Register.md`'s own updated `TD-51` Status cell for full
  detail, and `TD-53`/`TD-54` for two further, low-severity, non-blocking
  items the same review found (a hosted-service construction-throw
  criticality-misclassification gap; an incidental, not explicit,
  `ITempestServiceProvider` DI-escape closure) — neither plugin-trust-
  specific, neither release- or WP14-blocking.

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

Adopt all three decisions as a single, coherent package. **Corrected,
`WP 13.9.1`: this was, in fact, `WP 13.2A`'s own implementation brief, not
`WP 13.0B`'s** — `WP 13.0B` was commissioned as an independent
architecture review of this document instead (see Status, above, and
`docs/releases/v0.13.0/WorkPackages.md`'s own `WP 13.0B` row). Do not
implement any one in isolation: the capability model (`ADR-0111`) is
unenforceable without a trust tier to gate it (`ADR-0112`'s signing
decision), and the signing decision alone closes none of
`TD-09`/`TD-10`/`TD-11` without the enforcement calls `ADR-0111` designs.
`WP 13.2A` did in fact adopt all three together, in one Work Package, not
sequenced — the sequencing question this paragraph originally left open
was resolved by not needing to choose.

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
