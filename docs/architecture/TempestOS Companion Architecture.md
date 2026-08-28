# TempestOS Companion Architecture

**Established:** `WP 14.0A` (TempestOS Companion — Mobile Companion
Application), 2026-08-28. **Governing ADRs:** `ADR-0113` (client
boundary and technology), `ADR-0114` (REST query-and-action surface),
`ADR-0115` (offline model). This is the standing reference for how the
Companion fits the platform; the decision reasoning lives in the ADRs.

## 1. What the Companion Is

TempestOS Companion is the platform's mobile operational interface —
awareness, review, triage, and controlled quick actions — beside the two
existing presentation surfaces:

| Surface | Role | Composition |
|---|---|---|
| `Tempest.Desktop` | The shipped engineering authoring environment (`ADR-0092`/`ADR-0094`) | In-process: composes the Workspace directly |
| `Tempest.App` (`WorkspaceShell`) | The internal engineering harness (`ADR-0101`) | In-process: same composer |
| `Tempest.Companion` | The mobile operational window (`ADR-0113`) | **Separate process:** REST API only |

The Companion answers *"what is happening in TempestOS right now, what
needs my attention, and what can I safely deal with from my phone?"* —
cockpit-first (`ADR-0069`'s landing-screen rule, re-expressed for
mobile), never a phone-sized copy of the desktop authoring surface.

## 2. Solution Layout

```
src/
├── Tempest.Companion.Contracts/   # Wire contract: route constants, permission
│                                  # keys, DTO records, shared JsonSerializerOptions.
│                                  # Dependency-free — referenced by BOTH sides.
├── Tempest.Companion/             # The client application (Avalonia 11.2.3,
│   ├── Branding/                  # C#-constructed views, no .axaml — the
│   ├── Theming/                   # Tempest.Desktop authoring style)
│   ├── Client/                    # CompanionApiClient + settings store
│   ├── Offline/                   # SnapshotCache, freshness model (ADR-0115)
│   ├── Services/                  # CompanionDataService (fetch-with-fallback)
│   └── Views/                     # Shell, five sections, palette, state views
└── Tempest.App/Composition/
    ├── CompanionApiRegistration.cs   # Server side: maps every route (ADR-0114)
    ├── CompanionQueryService.cs      # Projects Cockpit/Domain reads → DTOs
    └── CompanionNotificationBuffer.cs# Bounded IPlatformNotification window

tests/Tempest.Companion.Tests/     # Unit + headless UI + full-stack integration
```

## 3. The Data Path

```
Phone UI (Views)
   ↓ render SnapshotResult<T>
CompanionDataService            — fetch-with-fallback state machine (ADR-0115)
   ↓ ICompanionApiClient        — typed CompanionApiException failures
CompanionApiClient              — HttpClient, X-Identity-Id header (ADR-0052)
   ═══ HTTP (loopback by default, TD-14) ═══
RestApiHostedService            — Kestrel; catch-all fallback route (ADR-0114)
   ↓ ApiQueryRequestHandler     — identity → permission → audit → execute
IApiQueryRegistry               — late-bound route lookup, per request
   ↓ registered delegates (Tempest.App)
CompanionQueryService           — projections over EngineeringCockpit +
   │                              EngineeringDomainContext (reads, ADR-0063)
   └─ actions → ICommandDispatcher → existing command handlers (mutations)
```

Three representations are kept deliberately distinct (`ADR-0113`):
**Domain objects** (`Tempest.Core.EngineeringDomain`) never cross the
wire; the **API representation** is the `Tempest.Companion.Contracts`
DTO set, serialized with one shared `JsonSerializerOptions`; the
**mobile presentation** is whatever the views render. The one constant
duplicated across the process boundary — the identity header name — is
drift-guarded by a test asserting equality with
`ApiRequestHandler.IdentityHeaderName`.

## 4. Routes

All under `/api/v1/companion` (`CompanionApiRoutes`), registered by
`CompanionApiRegistration` from
`EngineeringWorkspaceComposer.RegisterEngineeringDisciplines` — so the
console shell and the desktop serve the identical surface (`ADR-0071`'s
composition-root rule):

| Route | Verb | Permission | Serves |
|---|---|---|---|
| `/cockpit` | GET | `companion.read` | `CockpitSummaryDto` — the full Cockpit projection |
| `/projects` | GET | `companion.read` | `ProjectListDto` — live Projects, newest first |
| `/attention` | GET | `companion.read` | `AttentionDto` — triage regions + actionable pending reviews |
| `/activity` | GET | `companion.read` | `ActivityDto` — recent Workspace navigation |
| `/notifications` | GET | `companion.read` | `NotificationListDto` — bounded event window (ADR-0046) |
| `/actions/set-document-status` | POST | `companion.act` | Binds to the existing `SetDocumentStatusCommand`, dispatched through the Command Framework |

Every value in every query is read from the same `EngineeringCockpit`
read models and Domain repositories the desktop renders — one
authoritative read model, multiple presentation surfaces, no duplicated
computation. Placeholders stay disclosed on the wire
(`KpiCardDto.IsPlaceholder`; "Overdue Actions" remains honestly absent —
no due-date field exists in the Domain to compute it from).

## 5. Mobile UX Architecture

- **Shell** (`CompanionShellView`): a sunken instrument app bar (the
  supplied TEMPEST OS lockup, the COMPANION surface tag, the
  `● LIVE`/`● OFFLINE` readout, and `CMD`/`SYNC` label actions), the
  active page, and a five-tab thumb-reach bottom bar — Cockpit,
  Projects, Attention, Activity, More. The Cockpit is the landing tab
  (`ADR-0069`); the Command Palette is a global entry point on every page
  (`ADR-0070`), substring-filtered like the desktop's, listing
  navigation targets, cached projects, and refresh.
- **Pages** derive from `CompanionPage`, which enforces the state machine
  every screen must have: loading → freshness banner + content / honest
  empty state / error state with Retry. No page can ship without offline
  and error presentation.
- **Quick action**: the Attention page's "Reviews Awaiting Decision"
  rows carry Approve / Return-to-Draft, each behind an inline
  confirmation step, with the command's own outcome rendered in place —
  observe → understand → decide → act, nothing more.
- **Wiring** is direct delegates between shell and pages — `ADR-0104`'s
  desktop rule, applied unchanged: no mobile-local mediator, dispatcher,
  or event bus.
- **Touch floors**: 44dp minimum targets (`CompanionTokens`), the same
  4px spacing rhythm as `DesignTokens`, portrait-first with reflow tested
  at 320dp width.

## 6. Visual Identity

`WP 14.1A` aligns the Companion to the authoritative **Tempest
Engineering Design System** (supplied by the Product Owner; condensed
reference: `docs/design/Tempest Engineering Design System Reference.md`),
superseding `WP 14.0A`'s provisional values:

- **Ground**: instrument-dark first — navy `#0b0e1e` page, `#111527`
  cards, `#070915` sunken chrome; the paper theme (`#f5f6fa`) is the
  light variant. Dark is the default.
- **Colour**: the brand triad, read off the mark — indigo `#1c2d97`,
  cyan `#40a2ce` (THE interactive/live accent on dark; indigo takes that
  role on paper), violet `#6c29d9` (strictly secondary — category rules,
  the Command surface). Green/amber/red (`#12b981`/`#f5a524`/`#e5484d`)
  are reserved for machine state.
- **Type**: Chakra Petch for structure (headings, UPPERCASE tracked
  labels, readouts), Inter for prose, Space Mono for machine data — IDs,
  units, log levels (`INFO WARN ERR OK`), and UTC timestamps with a
  trailing `Z`.
- **Shape**: squared corners (2px badges, 3px controls, 5px cards); a
  2px status rule on a card's top edge; a 2px accent rule marks the
  selected navigation item; the 64px blueprint grid at 5.5% cyan
  textures the page ground behind opaque cards.
- **Logo**: the supplied artwork only — `TempestMarkGeometry` carries
  the pack's 18-stroke/hexagonal-core mark and TEMPEST OS logotype
  coordinates verbatim (a transcription, never a redraw), rendered by
  `TempestLogoControl`/`TempestLockupControl`; the pack's PNG lockups
  render the About surface, and its app icons ship for the platform
  heads. No emoji or hand-drawn glyphs anywhere; Unicode is limited to
  `●` `→` `·`.
- Conformance is test-guarded (`BrandConformanceTests`: token hexes,
  stroke counts, transcription spot-checks, the corner system).

## 7. Offline Model (summary — `ADR-0115`)

One snapshot per endpoint, stamped at fetch; four disclosed states —
`Live`, `Cached`, `Stale` (>15 min), `Unavailable`; 401/403 never fall
back to cache; **no offline write queue** (`AT-24`); "Clear Local Data"
removes every snapshot. The phone never claims authority — only "fetched
from the authoritative platform at this moment."

## 8. Security Posture (summary)

Identity: the existing configured-identity model, asserted via
`X-Identity-Id` (`ADR-0043`/`ADR-0052`) — no parallel identity system,
no invented tokens, nothing secret stored on the device because nothing
secret exists in the model. Authorization: per-route `Permission` keys
(`companion.read` / `companion.act`) through the existing
`IPermissionEvaluator`; unconfigured deployments serve 403 everywhere.
Audit: every authorized request records `api.request` with the caller
id. Transport: the platform's deliberate loopback-only, no-TLS posture
is inherited unchanged (`TD-13`/`TD-14`) and bounds deployment (`TD-58`)
until `FCR-0003`/`FCR-0004`. Full findings:
`docs/security/Companion Security Review WP14.0A.md`.

## 9. Testing Strategy

- `Tempest.Core.Tests/Api` — the query surface itself: registry
  semantics, the full pipeline status map, late binding over real
  Kestrel, OpenAPI inclusion.
- `Tempest.Companion.Tests` — unit (cache, freshness state machine,
  client construction, server-side binder), headless UI
  (`Avalonia.Headless.XUnit`: shell, every page state, palette, brand
  mark, 320dp small-phone layout), and full-stack integration: a real
  `TempestHost` + Kestrel on an OS-assigned port, the real composition
  step, and the production `CompanionApiClient` over real HTTP —
  including the quick action end-to-end against the Engineering Domain
  and the server-shutdown → cached-fallback path. Fakes exist only
  inside unit/view tests; the integration path has none.

## 10. Known Boundaries

`TD-57` (no Android/iOS heads yet — phone-frame desktop head is the
runnable form), `TD-58` (off-box reach gated on real auth/TLS), `AT-24`
(no offline writes), `FCR-0089` (no business-management surface — the
domain does not exist), `FCR-0090` (notifications are poll-based),
`FCR-0091` (the dedicated display client does not exist yet).

## Related Documents

`ADR-0113`, `ADR-0114`, `ADR-0115`; `Platform Service Map.md` (REST API
section); `docs/security/Companion Security Review WP14.0A.md`;
`docs/academy/03 Work Packages/WP14.0A-tempestos-companion-mobile-companion-application.md`;
`docs/releases/v0.14.0/WP14.0A Completion Report.md`.
