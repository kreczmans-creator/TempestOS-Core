# ADR-0116: Companion Platform Heads Live Outside the Gating Solution and Build via a Dedicated, Non-Gating Workflow

## Status

Accepted — `WP 14.2A` (Companion Android & iOS Platform Heads),
2026-08-28.

Realises the platform-head shape `ADR-0113` anticipated and `TD-57`
recorded as debt, and settles two conventions with lasting consequences:
where mobile head projects live relative to the CI gate, and what shape
the shared Companion application takes.

## Context

`WP 14.0A` shipped the Companion as one executable project that was both
the application and its own phone-frame desktop host, with Android/iOS
heads recorded as `TD-57`. Adding real heads surfaced two hard
constraints:

1. **Toolchain reach.** Building `net10.0-android` requires the .NET
   Android workload *plus* an Android SDK and JDK; building
   `net10.0-ios` requires the iOS workload *plus* a macOS/Xcode
   toolchain — and the iOS workload does not exist for Linux hosts at
   all. The gating CI (`ci.yml`, pinned `windows-2022`) installs no
   workloads, and `dotnet restore` of the gating solution fails with
   `NETSDK1147` for any member project whose workload is absent — so
   putting the heads into `src/TempestOS.slnx` would break every
   existing CI run and every contributor build on day one.
2. **Project shape.** A self-contained mobile executable cannot
   reference another executable (`NETSDK1150`, hit directly during this
   Work Package): the `WP 14.0A` single-project shape structurally
   cannot grow heads.

## Decision

**`Tempest.Companion` becomes the shared single-view application
library — the official Avalonia cross-platform shape — with four thin
heads over it: `Tempest.Companion.Desktop` (the phone-frame window,
inside the gating solution), and `Tempest.Companion.Android` /
`Tempest.Companion.iOS`, which live in `src/Tempest.Companion.Mobile.slnx`
— deliberately outside `src/TempestOS.slnx` — and build via
`.github/workflows/mobile-heads.yml`, a manually dispatched, non-gating
workflow (Android on `windows-2022`, whose runner image ships an Android
SDK; iOS on `macos-15`, targeting the simulator RID so no signing is
required).**

### The heads are bootstrap, never behaviour

Every screen, service, theme, and the `App` class live in the shared
library; a head contributes only platform entry (`MainActivity` /
`AppDelegate` / `Program`), launcher identity (the brand pack's app
icons; `Tempest OS Companion` label), and platform manifest/theme
plumbing. A head that grows logic is a defect.

### The gate's meaning is unchanged

`ci.yml` and the `CI Gate` check are untouched: they still build and
test everything a workload-free environment can — now including the
shared library, the desktop head, and the whole Companion test suite,
which exercises the identical shared shell the mobile heads host. The
mobile workflow is additive evidence, not a new gate; it is dispatch-only
so it can never turn an unrelated push red.

### Validation is disclosed, not overstated

The authoring environment could not run the mobile toolchains (Android
SDK hosts unreachable; no Linux iOS workload). The Android head was
validated by full NuGet restore plus a Roslyn compile of its source
against the workload's own reference assemblies — which caught a real
launch-crash defect (`AvaloniaMainActivity` is an `AppCompatActivity`;
a non-AppCompat Activity theme throws at startup) — and stops at the
Android-SDK check (`XA5300`). The iOS head mirrors the official
Avalonia template and has had no compile of any kind. `TD-57` therefore
moves to **Partially resolved**, closing only when `mobile-heads.yml`
has been dispatched green and the apps observed on device/simulator.

## Consequences

**Positive:**

- The gate stays green and workload-free; contributors without mobile
  toolchains build and test everything else exactly as before.
- The head/library split is the ecosystem-standard shape — future heads
  (the `FCR-0091` display client included) plug into the same library.
- One shared `App` means the mobile heads inherit every tested screen,
  state machine, and brand rule with zero duplication.

**Negative:**

- Mobile builds are not continuous: a change that breaks only a mobile
  head is caught at the next manual dispatch, not the next push — the
  accepted cost of never gating on toolchains the environment cannot
  hold. Revisit if mobile becomes release-critical.
- The iOS head is unvalidated until first dispatch (disclosed in
  `TD-57`).
- Two solution files must be kept in mind, though each lists its own
  scope completely.

## Alternatives Considered

**Heads inside `src/TempestOS.slnx` plus `dotnet workload install` in
`ci.yml`.** Rejected: iOS cannot build on `windows-2022` regardless
(macOS required), so the gate would need a new macOS leg for a
non-shipping artefact; every contributor restore would demand workloads;
and a red mobile toolchain would block platform merges — gating on
exactly the environments this repository cannot reproduce locally.

**Conditional solution membership (`Condition` on workload presence).**
Rejected: solution files don't evaluate conditions; per-project
`Condition` hacks inside csproj make restore behaviour environment-
dependent and silently skip what looks included.

**An auto-triggered mobile workflow (on push/PR paths).** Rejected for
now: the workflow's first-ever run could not be executed from the
authoring environment, and an unvalidated auto-run that lands red on
every Companion push is noise masquerading as signal. Dispatch-only
until proven; promotion to path-triggered is a one-line change once
green.

**Keeping the single-project shape and referencing it with
`ReferenceOutputAssembly=false` tricks.** Rejected: fights `NETSDK1150`
instead of adopting the shape the toolchain and the whole Avalonia
ecosystem define for exactly this case.

## Related Documents

`ADR-0113` (the Companion boundary and head anticipation), `ADR-0094`
(Avalonia), `docs/architecture/TempestOS Companion Architecture.md`
(§2 layout, §10 boundaries),
`docs/governance/Quality/Technical Debt Register.md` (`TD-57`),
`.github/workflows/mobile-heads.yml`, `src/Tempest.Companion.Mobile.slnx`,
`docs/academy/03 Work Packages/WP14.2A-companion-android-and-ios-platform-heads.md`.
