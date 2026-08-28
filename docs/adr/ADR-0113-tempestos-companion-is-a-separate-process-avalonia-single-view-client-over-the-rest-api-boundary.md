# ADR-0113: TempestOS Companion Is a Separate-Process Avalonia Single-View Client Over the REST API Boundary

## Status

Accepted — `WP 14.0A` (TempestOS Companion — Mobile Companion Application), 2026-08-28.

Establishes the product boundary and concrete technology for TempestOS's
first non-desktop client — the first concrete step toward `FCR-0023`
(Offline Synchronisation & Mobile Client Support), pulled forward from
`Product Roadmap.md`'s own Phase 8 by explicit direction of this Work
Package's commissioning brief. Complements, and does not modify,
`ADR-0092`/`ADR-0094` (the desktop application) and `ADR-0101` (the
console harness).

## Context

TempestOS has one shipped presentation surface — the Avalonia desktop
Engineering Workspace (`ADR-0092`, `ADR-0094`) — and one internal console
harness (`ADR-0101`), both composing the identical Engineering Workspace
domain layer in-process through `EngineeringWorkspaceComposer`. `WP 14.0A`
commissions a third experience: **TempestOS Companion**, a mobile
operational interface for awareness, triage, and controlled quick
actions — explicitly not a third authoring surface.

Two boundary questions had to be settled before any Companion code
existed:

1. **Process boundary.** A phone is a separate device; the Companion can
   never compose the Workspace in-process the way the desktop and console
   do. Something must define what crosses the wire, and in what shape.
2. **Technology.** `ADR-0094` chose Avalonia 11 for the desktop and
   explicitly rejected .NET MAUI ("primarily a mobile-first framework")
   *for the desktop role*. The Companion is exactly the mobile role that
   rejection carved out — the choice had to be made afresh, not inherited
   by inertia.

The commissioning brief also asserts concepts this repository does not
contain (an "offline dashboard" implementation, invoice/contract
business-management entities, established brand assets). Repository
reconnaissance verified none of these exist in code or in any register —
the resolution of each discrepancy is recorded in the Decision and in the
`WP 14.0A` retrospective, per `Future Work Package Guidelines.md`'s
evidence-over-speculation rule.

## Decision

**TempestOS Companion is a separate operating-system process — a
first-party Avalonia single-view application (`Tempest.Companion`) that
reaches the platform exclusively through the REST API boundary
(`ADR-0047`–`ADR-0049`, extended by `ADR-0114`), holds no reference to
`Tempest.Core` or `Tempest.App`, and shares wire shapes with the server
through one dependency-free contracts assembly
(`Tempest.Companion.Contracts`).**

### The companion is a client, never a second composition of the platform

The desktop and console compose the Workspace in-process; the Companion
deliberately cannot. It consumes read-only projections of the Engineering
Cockpit and Engineering Domain served over HTTP, and dispatches its one
mutation category through the same Command Framework every other caller
uses (`ADR-0114`). Domain object ≠ API representation ≠ mobile
presentation model: Domain objects stay in `Tempest.Core`; the API
representation is the `Tempest.Companion.Contracts` DTO set, serialized
by the server-side registration in `Tempest.App` and deserialized by the
client; the mobile presentation is whatever the Companion's own views
render from those DTOs. The contracts assembly is referenced from both
sides, so the wire shape has exactly one definition.

### Avalonia, in the desktop's own authoring style

The Companion uses Avalonia 11.2.3 — the identical framework, version,
and pure-C# view construction style `Tempest.Desktop` established
(`ADR-0094`; no `.axaml`), tested with the identical
`Avalonia.Headless.XUnit` strategy. The application itself is a
single-view shell (`ISingleViewApplicationLifetime`-ready — Avalonia's
mobile lifetime); the shipped head in this Work Package is a phone-frame
desktop window, because Android/iOS platform heads require mobile
workloads unavailable in the build environment (`TD-57` records this as
debt, not design).

### Composition-root-owned server-side registration

The server side of the Companion API is registered by
`EngineeringWorkspaceComposer.RegisterEngineeringDisciplines` (via
`CompanionApiRegistration`), so the console shell and the desktop both
serve the identical Companion surface with zero presentation-layer
changes — the same composition-root-owns-registration rule `ADR-0071`
established for Workspace extensibility.

### Security model: reuse, disclose, and fail closed

The Companion asserts identity through the existing `X-Identity-Id`
header model (`ADR-0043`/`ADR-0052`) and is authorized per route by the
existing `IPermissionEvaluator` against two new flat permission keys
(`companion.read`, `companion.act`). No parallel identity system, no
invented tokens. The header model's disclosed limitations (`TD-13` no
credential verification, `TD-14` no TLS, loopback bind) are inherited
unchanged and bound the Companion's deployable reach: off-box production
use remains gated on `FCR-0003`/`FCR-0004`, recorded as `TD-58`. An
unconfigured platform serves `403` for every Companion route — the
fail-closed default `ADR-0043` already guarantees.

### The brief's phantom concepts are resolved by governance, not invention

No offline dashboard exists to reuse — the Companion API's query surface
is designed as the shared information architecture a future dedicated
display client would consume (`FCR-0091`). No business-management domain
exists — the Companion ships no invoice/contract surface rather than
fabricating one (`FCR-0089`); milestones (`IMilestone.TargetDate`), the
Domain's one real deadline carrier, serve deadline awareness. No brand
assets existed anywhere in the repository — `WP 10.0A`'s Visual Design
System explicitly deferred concrete values to an implementation phase,
and the Companion is that phase: the brief's brand palette (Royal Blue
`#1E2F97`, Electric Blue `#00AEEF`, Purple `#6C2BD9`), typography
(Chakra Petch / Inter / Space Mono), and six-blade shutter/iris mark are
realised concretely for the first time, as code-drawn vector geometry and
OFL-licensed embedded fonts.

## Consequences

**Positive:**

- One UI framework, one authoring style, one headless test strategy
  across every TempestOS presentation surface — a contributor who can
  build a desktop view can build a Companion view.
- The process boundary makes the Companion honest by construction: it can
  only ever know what the API serves, so cached-vs-authoritative can
  never blur (`ADR-0115`).
- The contracts assembly gives the wire shape a single source of truth,
  and the client's one deliberately duplicated constant (the identity
  header name) is drift-guarded by a test asserting equality with
  `ApiRequestHandler.IdentityHeaderName`.
- Registering the server side in the shared composer means every future
  presentation layer serves the Companion API for free.

**Negative:**

- No Android/iOS heads ship in this Work Package — the mobile form factor
  is proven in a phone-frame window and headless tests, not on device
  (`TD-57`).
- Until `FCR-0003`/`FCR-0004` land, the Companion's production reach is
  the platform host's own machine (or an operator-managed tunnel) —
  a real phone on a real network cannot securely reach a real TempestOS
  today (`TD-58`).
- Two more projects (`Tempest.Companion`, `Tempest.Companion.Contracts`)
  and a third test assembly join the solution and CI build.

## Alternatives Considered

**Avalonia mobile heads (Android/iOS) in this Work Package.** Rejected:
the build environment cannot acquire the Android SDK or .NET mobile
workloads (network policy), so heads could not be compiled, let alone
tested — shipping unbuildable projects would violate the Build Gate.
Recorded as `TD-57` with the single-view shell already shaped for them.

**.NET MAUI.** Rejected: a second UI framework, a second theming and
test stack, and zero reuse of the desktop's established view idiom —
`ADR-0094` already rejected MAUI for the desktop, and adopting it for
mobile would split the platform's presentation knowledge in two.

**A browser/PWA client.** Rejected: `ADR-0092`/`ADR-0094` twice rejected
browser-based presentation for TempestOS surfaces; a PWA would introduce
a web asset pipeline and a third styling system for no capability the
Avalonia shell lacks.

**Embedding the Companion in Tempest.Desktop as a responsive mode.**
Rejected: a phone-sized desktop window is not a mobile product — it would
inherit desktop-only assumptions (docking, ribbon, pointer interaction)
and could never become a phone application.

**In-process composition (referencing Tempest.App from the Companion).**
Rejected: it fakes the device boundary the product exists for, duplicates
the Workspace on every phone, and bypasses identity/permission
enforcement at the API boundary.

## Related Documents

`ADR-0114` (the REST query-and-action surface), `ADR-0115` (the
Companion offline model), `ADR-0043`/`ADR-0044`/`ADR-0052` (identity,
authorization, REST identity resolution), `ADR-0047`–`ADR-0049` (REST
API), `ADR-0069` (Engineering Cockpit), `ADR-0070` (Command Palette),
`ADR-0071` (composition-root registration), `ADR-0092`/`ADR-0094`
(desktop application and framework), `ADR-0101` (console harness),
`ADR-0103`/`ADR-0104` (composition-root decomposition and direct-delegate
wiring, mirrored in the Companion shell),
`docs/architecture/TempestOS Companion Architecture.md`,
`docs/releases/v0.10.0/WP10.0A Visual Design System.md` (deferred visual
values), `docs/governance/Future Capability Register.md` (`FCR-0023`,
`FCR-0089`, `FCR-0090`, `FCR-0091`),
`docs/governance/Quality/Technical Debt Register.md` (`TD-13`, `TD-14`,
`TD-57`, `TD-58`, `AT-24`),
`docs/academy/03 Work Packages/WP14.0A-tempestos-companion-mobile-companion-application.md`.
