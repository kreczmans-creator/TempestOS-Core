# WP 5.3 — Developer Experience Improvements

## 1. Introduction

WP 5.3 closes the Developer Experience release's own final named gap:
`docs/releases/v0.5.0/WorkPackages.md`'s own `WP 5.3` entry (formerly
`WP 4.9`) asks for "templates, scaffolding, and documentation that make
everything above approachable, not just possible." Unlike every
implementation Work Package before it this release, `WP 5.3` has no
preceding architecture phase — it is a scoped polish pass, not a new
platform service, and this retrospective is written accordingly.

## 2. Purpose

To ship a `dotnet new` module template a new contributor can scaffold a
working module from without hand-copying `ClockModule`; to close one
genuine, previously-only-documented Discovery pitfall with a clear error
message instead of a raw runtime exception; and to correct real,
pre-existing documentation/governance drift found along the way.

## 3. Background

This Work Package's own brief, unlike every other implementation Work
Package this release, names no architecture document and no ADR to
implement "exactly" — `WP 5.3`'s own entry in `WorkPackages.md` never had
a design phase, and none of the standing architecture documents (`docs/
architecture/`) mention templates or scaffolding at all. This was
investigated directly before writing any code (mirroring `D-009`/`D-019`'s
own premise-verification discipline): confirmed against
`docs/releases/v0.4.0/WorkPackages.md`'s original `WP 4.9` wording,
`Rejected Designs.md`, and every architecture document — no existing
design was found to implement, and none was expected, since this Work
Package's own nature (tooling and polish, not a platform capability) does
not meet `FOUNDATION.md` principle ① ("architecture precedes
implementation, for anything non-trivial") the way a new platform
service would. This is not a conflict requiring confirmation before
proceeding — it is the expected shape of a Work Package like this one,
consistent with `WP 4.1` (Module SDK) needing no ADR of its own.

## 4. The Problem

Three small, independent problems, named directly by `WorkPackages.md`'s
own `WP 5.3` entry:

1. **No scaffold exists.** A new contributor writing their first module
   either hand-copies `ClockModule`/`CommandSampleModule`/
   `DiagnosticsSampleModule` (copying whichever one happens to be
   nearby, dragging along whatever platform-service dependency that
   module happens to demonstrate) or writes one from scratch, re-deriving
   `Building a Module.md`'s own shape by hand each time.
2. **A real, previously-only-documented Discovery pitfall.**
   `Building a Module.md` has warned, since `WP 4.1`, that a module
   without `[ModuleMetadata]` must keep a public parameterless
   constructor — but the code itself never enforced this with a clear
   message. A module with a constructor requiring a dependency and no
   attribute crashes Discovery with a raw `MissingMethodException`, with
   no indication of the actual fix.
3. **A documentation pass** across this release's own new Academy/SDK
   material, correcting any genuine drift found.

## 5. The Design

**`dotnet new tempest-module`** (`src/Templates/Tempest.Templates.Module/`)
generates a single `.cs`/`.csproj` pair shaped exactly as `Building a
Module.md` describes: `[ModuleMetadata]`, `ModuleLifecycleBase`, a
parameterless constructor free to gain a DI-public service dependency by
simply adding a parameter (the attribute is already present, so the
pitfall in Problem 2 cannot recur for anything scaffolded from this
template). Three parameters (`ModuleId`, `ModuleDisplayName`,
`ModuleVersion`) are exposed via the templating engine's own symbol
mechanism, each with an honest, clearly-a-placeholder default rather than
a cleverly-guessed one — `ModuleId` in particular is never auto-derived
from the class name, since a real module Id (`tempest.samples.clock`) is
a deliberately-chosen, dotted, hierarchical string, not a mechanical
transform of a C# identifier a "casing" generator could produce
correctly. Installed locally (`dotnet new install <path>`), not packaged
to NuGet — see Alternatives Considered.

**Discovery's own fix**: `ReflectionFrameworkDiscoveryService.CreateDescriptor`
now checks `type.GetConstructor(Type.EmptyTypes)` before calling
`Activator.CreateInstance`, for any module type without
`[ModuleMetadata]`. A type with no public parameterless constructor now
throws a `ModuleDiscoveryException` naming the type and the two actual
fixes (add the attribute, or add a parameterless constructor), instead of
a raw `MissingMethodException` with no actionable content. Purely
additive — no existing, correctly-shaped module's behaviour changes.

## 6. Alternatives Considered

**NuGet-packaged template distribution.** Considered, since this is the
conventional way to share a `dotnet new` template beyond a single
repository clone. Rejected: no NuGet publishing pipeline exists in this
repository today, and building one solely for this template would be
disproportionate to an **S–M** Work Package — the same proportionality
reasoning `WP 4.3`'s own Alternatives Considered applied to
plugin-packaging the sample module (`RD-0015`). Recorded permanently as
`RD-0045`.

**Auto-deriving `ModuleId` from the class name via a templating-engine
casing transform.** Considered, to spare the author one parameter.
Rejected: a real module Id is a deliberately-chosen, dotted string
(`tempest.samples.clock`), not a mechanical lowercase-transform of a
PascalCase class name (`TelemetryModule` → `telemetrymodule`, not
`tempest.samples.telemetry`) — a "clever" but subtly wrong default was
judged worse than an honest placeholder plus one required parameter,
consistent with `Building a Module.md`'s own "no hidden behaviour
anywhere" description of the Module SDK.

**Automatic solution/test-project wiring via template post-actions.**
Considered — the .NET templating engine supports post-actions that can
run `dotnet sln add` automatically. Rejected as disproportionate for this
Work Package's own scope: the generated `README.md`'s two documented
manual steps (`dotnet sln add`, then either a project reference or a
plugin manifest) are already standard, already-documented `dotnet`
workflow, not something requiring hand-copying a module — the Acceptance
Criteria's own bar.

## 7. Why This Solution Was Chosen

A local-folder template gives every in-repository contributor the
identical scaffolding result a NuGet-packaged one would, at a fraction of
the setup cost, with cheap, purely-additive reversibility if a real
publishing pipeline ever exists. Checking for a parameterless constructor
before calling `Activator.CreateInstance`, rather than catching
`MissingMethodException` afterward, follows this codebase's own Fail-Fast/
Defensive-Programming convention: validate the precondition explicitly,
don't rely on exception-driven control flow to discover it.

## 8. Architectural Principles

- **Fail Fast** — Discovery's new check surfaces a malformed module
  immediately, with an actionable message, rather than letting an
  unrelated runtime exception propagate.
- **Reuse Before Invention** — the template reuses `ModuleLifecycleBase`,
  `[ModuleMetadataAttribute]`, and `Building a Module.md`'s own
  documented shape exactly; no new SDK concept was introduced.
- **Proportionality** — both rejected alternatives (NuGet packaging,
  post-action solution wiring) were declined specifically for costing
  more than this Work Package's own **S–M** scope justifies, not because
  either is a bad idea in the abstract.

## 9. Benefits

- A new contributor scaffolds a correctly-shaped, compiling module
  without reading or copying any existing module's source file.
- The single most-documented-but-unenforced Discovery pitfall in this
  codebase (`Building a Module.md`, "One Constraint You Still Need to
  Know About," since `WP 4.1`) now fails with a message naming its own
  fix, closing a four-Work-Package-old gap between documentation and
  enforcement.
- Zero risk to any existing module: the Discovery check only changes
  behaviour for a type that would have crashed uninformatively anyway.

## 10. Trade-offs

- `dotnet new`'s own auto-generated short parameter aliases (`-M`,
  `-Mo`, `-p:M`) are not particularly memorable — documented in
  `src/Templates/README.md` using the long-form (`--ModuleId`, and so
  on) throughout, rather than relying on the generated abbreviations.
- The template does not wire the generated project into `TempestOS.slnx`
  or any consumer automatically — a deliberate, disclosed scope
  boundary (see Alternatives Considered), not an oversight.

## 11. Common Mistakes

- **Assuming the generated `ProjectReference` path works from anywhere.**
  It assumes the conventional `src/Samples/<Name>/` layout, generated
  from the repository root — documented explicitly in both the
  template's own `.csproj` comment and its `README.md`.
- **Assuming Discovery's new check changes behaviour for existing
  modules.** It doesn't — every module already in this codebase either
  carries `[ModuleMetadata]` or already has a public parameterless
  constructor; the new check only ever fires for a type that would have
  crashed uninformatively before this Work Package.

## 12. Future Evolution

A NuGet-packaged version of this template (once a real publishing
pipeline exists — `RD-0045`); additional templates for other recurring
shapes (a hosted service, a plugin manifest) if a real, demonstrated need
arises; automatic solution/test-project wiring via template post-actions,
if scaffolding friction is ever reported as a real problem rather than a
theoretical one.

## 13. Key Takeaways

1. Not every implementation Work Package has a preceding architecture
   phase to "implement exactly" — a scoped tooling/polish Work Package is
   a different, legitimate shape, and treating its absence of a design
   document as a blocking conflict would have been a misapplication of
   this project's own "architecture precedes implementation" principle,
   which explicitly qualifies itself with "for anything non-trivial."
2. A four-Work-Package-old documentation warning
   (`Building a Module.md`'s own Discovery pitfall) can sit unenforced in
   code for a long time without anyone noticing, until a Work Package
   whose own purpose is developer experience specifically goes looking
   for exactly this kind of gap.
3. Proportionality is itself a real design decision worth recording
   (`RD-0045`) — not every choice needs an ADR, but "we could have built
   more machinery here and chose not to, for now" is exactly as citable
   as a decision to build something.

## Architectural Debt Assessment

No new debt introduced. No existing debt resolved (this Work Package's
scope does not touch `TD-01`–`TD-11`).

## Observations

Three genuine, pre-existing governance/documentation drifts were found
during this Work Package's own repository review, none caused by this
Work Package's own changes:

1. **`Rejected Designs Register.md`** had added `RD-0042`–`RD-0044` (as
   part of `WP 5.2`) without the corresponding full entries ever being
   written into `docs/architecture/Rejected Designs.md` itself — the
   register's own declared Source of Truth. Backfilled here, unchanged in
   content from what the register already described.
2. **`Engineering Governance.md` §11** was never updated when `WP 5.2`
   added the `Tempest.Core.Diagnostics` namespace, leaving its own
   project-list example one namespace behind. Corrected here alongside
   this Work Package's own, genuinely new `src/Templates/` addition.
3. **`Governance Register.md`'s own Compliance Matrix** had not been
   updated since `WP 5.0D` — four completed Work Packages (`WP 5.0S`,
   `WP 5.1A`, `WP 5.1B`, `WP 5.2`) were entirely missing, and `WP 5.0D`'s
   own row still carried a `*(this commit)*` placeholder never backfilled
   with its real hash. All five rows backfilled here. This is the third
   Work Package in a row to find a real, previously-unnoticed governance
   drift during its own repository review (`WP 5.1B` found stale
   Engineering/Delivery registers; `WP 5.2` found a stale Command
   Framework marker and a stale `WP 5.0D` status note) — worth naming as
   its own small pattern: a repository review's own scope should be
   "everything encountered," not only "everything the current brief
   names," exactly as `WP 5.2`'s own retrospective already concluded.

## Related Documents

`src/Templates/README.md`; `docs/academy/02 Runtime Architecture/
03-building-a-module.md`; `docs/architecture/Sample Module
Architecture.md`; `docs/architecture/Rejected Designs.md` (`RD-0045`,
and the `RD-0042`–`RD-0044` backfill); `docs/academy/06 Engineering
Standards/Engineering Governance.md` (§11); `docs/releases/v0.5.0/
WorkPackages.md` (`WP 5.3`'s own entry).
