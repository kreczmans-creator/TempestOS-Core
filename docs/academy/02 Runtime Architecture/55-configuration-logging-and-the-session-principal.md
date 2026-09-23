# Configuration, Logging and the Session Principal

**Release:** `v0.17.0` · **Work Package(s):** `WP 17.2A` part 2, `WP 17.9.1` ·
**Decision:** `ADR-0146` (Decision B), `ADR-0043` (amended), `ADR-0044` ·
**Code:** `Tempest.Core.Configuration.MicrosoftExtensionsConfigurationSource`,
`Tempest.Core.Logging.RollingFileLogSink`/`TempestLoggerProvider`,
`Tempest.Core.Identity.ISessionPrincipal`/`SessionPrincipalSource`/
`IPrincipalDirectory`

**In plain terms.** Configuration is the set of switches an operator can flip
without touching the program's own code — where the data folder lives, how
chatty the diary should be, who the software should think is using it. Logging
is that diary: a plain record of what the application did, so a problem can be
explained after the fact instead of guessed at. A "principal" is simply
whoever the software believes is at the keyboard right now, because every note
it writes — who ran this calculation, who released this material — has to name
somebody. This chapter is about trading two hand-built platform services for
the industry's own, and shrinking the third down to exactly what a one-person
desktop needs.

## Two services that had to exist before anything else could

`WP 2.5` and `WP 2.6` built Configuration and Logging first because nothing
else in TempestOS can start without them: a module cannot be discovered until
the container exists, and the container cannot be built until configuration
has told it what to build (`02-the-startup-sequence.md`). `WP 5.2` later gave
logging `CompositeLogSink`, so one entry could reach more than one destination
(`12-diagnostics-and-composite-logging.md`). Both were entirely in-house —
TempestOS's own immutable `IConfigurationProvider` (Case Study 05 explains why
immutable) and its own `ILogger`/`ILogSink` pipeline — and for years that was
correct: `Tempest.Core` took no third-party dependency, and a platform's own
parser is a defensible cost when nobody outside it has to use it.

**Why that changed now.** `ADR-0146` is one decision on two clocks. Its first
half — the freeze, implemented in the same commit — moved the plugin trust
platform, the inbound REST API and Licensing to `src/Frozen/`
(`56-frozen-layers.md`). That freeze is what makes this chapter's half
affordable: identity carried a second, component-scoped axis so a loaded
plugin could be told apart from a person, and collapsing it to one principal
is small and mechanical against a platform with no second axis to reconcile,
large and review-heavy against one that still has it. This chapter is Decision
B — decided in the same ADR, implemented separately (`WP 17.2A` part 2)
against the tree the freeze left behind.

`v1.0.0`'s own re-scope named what TempestOS actually is: a single-user,
locally-trusted desktop for a small engineering consultancy. Nobody ships it a
plugin or calls its API outside a demonstration harness — but that desktop
still needs `appsettings.json`, an environment variable and a command-line
override, the way every Windows operator already expects, and reinventing that
buys nothing `Microsoft.Extensions.Configuration`/`.Logging` do not already
give for free. `Tempest.Core` took its first NuGet packages here, pinned to
`10.0.11` — the pin `Microsoft.Data.Sqlite` already carried
(`53-sqlite-persistence.md`).

## Configuration: three sources, one precedence

`MicrosoftExtensionsConfigurationSource` (`src/Tempest.Core/Configuration/`)
builds a tree from, in increasing precedence: an `appsettings.json` next to
the executable, one in the working directory, `TEMPEST_`-prefixed environment
variables (`__` as the separator, so
`TEMPEST_Runtime__Logging__MinimumLevel=Debug` becomes
`Runtime:Logging:MinimumLevel`), and the command line — flattened to the
`Section:Key` strings the platform's own provider already used.
`TempestHostBuilder.Build()` places it first, always, so an explicit in-memory
override — the Desktop's own persistence-root override, every isolated test
root — still wins; `Tempest.Desktop`'s `App.cs` wires Avalonia's own `Args`
through the same `AddCommandLineArgs` seam.

`src/Tempest.Desktop/appsettings.sample.json` documents every key the platform
reads — `Persistence:RootPath`/`Backend`, `Runtime:Logging:MinimumLevel`,
`Runtime:Plugins:*`, `Identity:DisplayName`/`Role` — and ships next to the
executable for an operator to copy and edit. All are reachable this way for
the first time; before, they were source code an operator could not touch
without rebuilding.

## Logging: a diary a desktop application can actually keep

A desktop application has no console. `Tempest.Desktop` is a Windows `WinExe`
with no terminal attached — nowhere for "printed to the screen" to go. Before
this Work Package TempestOS wrote Information-level lines to the console
alone, so the Desktop's own diary went into nothing; an operator who hit a
problem had no record to hand to support.

`RollingFileLogSink` (`src/Tempest.Core/Logging/`) writes `<persistence
root>/logs/tempest-yyyyMMdd.log`, one file per UTC day, deletes the oldest
once more than 14 accumulate, and flushes every entry immediately — a buffered
write an operator cannot find after a crash defeats the point of a durability
sink — never throwing into its caller: an I/O failure is caught, counted, and
reported on disposal. A console sink is added only when one is genuinely
attached, which is never true for `Tempest.Desktop` (its
`Console.IsOutputRedirected` reports `true` because the handle is invalid,
exactly as for a genuinely redirected stream); `Tempest.Harness`'s console
runs get both, composed through `CompositeLogSink`.

**Quieter by default.** The same commit demoted per-call chatter — every DI
resolution, event publish, command dispatch, registration — from Information
to Debug, keeping one-time lifecycle lines (a module actually registering, a
Host phase completing) at Information: a rolling file an operator might read
is not worth filling with a line for every routine resolution.

**The bridge, and what stayed.** `TempestLoggerProvider` implements both
`Microsoft.Extensions.Logging.ILoggerProvider` and the platform's own
`ILoggerFactory` over the platform's own pipeline, registered under the
Microsoft type any future library code asks for by convention — closing
`TD-01` ("two logging mechanisms coexist"), since such a caller now forwards
into the one pipeline instead of a second, disconnected one. `ADR-0146` keeps
the custom container — `TempestServiceProvider`/`ServiceCollection` —
unreplaced: Configuration and Logging adopted Microsoft's abstractions because
they suit a multi-source tree and a sink-based pipeline, not because taking
one package implied taking them all.

## One session principal, not two axes

Before this Work Package, identity carried a second, component-scoped axis
(`ICurrentComponentAccessor`) that existed only to tell a loaded plugin's
principal apart from a person's. The freeze removed the plugin platform that
axis served, so the axis is deleted, not merely disconnected — nothing left
needs to distinguish "which component" from "which signed-in person."

What remains is `ISessionPrincipal`
(`src/Tempest.Core/Identity/SessionPrincipal.cs`) — `IdentityId`,
`DisplayName`, `Role` — extending the existing `IPrincipal` so every
consumer of `ICurrentPrincipalAccessor` kept working unchanged.
`SessionPrincipalSource` (replacing the `ADR-0116`-era
`LocalSessionPrincipalSource` —
`40-production-rehydration-and-the-principal-boundary.md` tells that
boundary's origin) reads `IdentityId` from the operating system at every
launch: the Windows account's own security identifier on Windows,
`Environment.UserName` elsewhere. Configuration overrides exactly two fields —
`Identity:DisplayName` and `Identity:Role` (`Engineer`, the default, or
`Checker`) — never `IdentityId`.

**Why the identity id must not be configurable.** Every audit row, authorship
field and check record stores the identity id, not the display name. If
configuration could change it, an operator could make the software believe a
different person did the work than the operating system actually authenticated
— turning the audit trail into a record of what somebody typed into a settings
file. The role only governs which commands a session is offered; keeping the
two separate narrows the non-negotiable-identity principle `ADR-0043` set in
`WP 6.1` from "local, extensible" to "one session, OS-derived."

Deleted, not merely unregistered — `IIdentityService`/`IdentityService`,
`IRoleProvider`/`RoleProvider`, `IRole`/`Role`, `RoleNotFoundException`, and
the `Identity:Roles:*`/`Identity:Principals:*` configuration shape: nothing
outside `Tempest.Samples` ever called `IIdentityService` directly, and its
only real dependency was the role provider deleted alongside it.
`ICurrentPrincipalAccessor` and `IPermissionEvaluator` are untouched in shape
— `ADR-0044` stands exactly as `WP 6.1` decided it, ambient accessor and all,
because this product has one session, not one principal per concurrent
request.

**Real audit rows, at last.** `IAuditRecorder` (`ADR-0045`) already worked; it
lacked callers. This Work Package gave it two, both attributed to the session
principal's own `IdentityId`: `CalculationEngine.ExecuteAsync` records
`calculation.executed`, and
`ReferenceReviewService.VerifyAsync`/`ReleaseAsync` record
`reference.verified`/`reference.released`, each an optional, nullable
parameter so a hand-assembled test context is unchanged. `ADR-0146` says
plainly that the calc-sheet, check and other audit surfaces it also names were
not built here — those objects did not exist yet in `v0.17.0`.

## Names, not SIDs: `IPrincipalDirectory`

Storing the Windows SID as the identity id is right for an audit trail and
wrong to put in front of a person — the Product Owner's first Windows session
saw a Properties panel's *Last Revised By* read `S-1-5-21-…`. `WP 17.9.1`
closed the gap with one interface, `IPrincipalDirectory.Describe(identityId)`
(`src/Tempest.Core/Identity/PrincipalDirectory.cs`), answered in order: the
session principal's own display name if the id is the session's; failing that,
the Windows account name via SID translation, cached once resolved; failing
that, the id verbatim — never an invented name. `TempestHost` registers one
instance over the same `CurrentPrincipalAccessor` every identity consumer
shares, and `PropertyFacetKind.Principal` marks the eight "…By" facets shown
through it; the stored value never changes, only what a person is shown.

A directory that cannot resolve a name simply falls back to the id, which is
never a security problem, because nothing downstream compares display names —
a readable screen and a correct security decision are different problems, and
this one never touched the other.
`64-independent-check-and-the-issue-sheet.md`'s independence rule compares
identity ids for exactly that reason: a name is a courtesy for a reader, an
identity id is what the platform reasons about.

## What was deliberately not built

No login screen, no password, no credential store — the fail-closed,
local-only assumptions `ADR-0043` set in `WP 6.1` are narrowed here, not
lifted. No administered, runtime-mutable role store: changing a role still
means editing configuration and relaunching. No request-scoped principal: the
ambient accessor `ADR-0044` chose was built for one concurrent user and stays
that way, because nothing in this product's shape has asked for more.

## What to take away

- **A "we build everything" discipline is a means, not an identity.**
  TempestOS took its first dependency not because its principles
  softened, but because a hand-rolled `appsettings.json` parser bought
  nothing an operator's own expectations did not already supply for free.
- **What must never be configurable is exactly what an audit trail
  depends on.** The identity id comes from the operating system alone;
  everything softer — a display name, a role — is fair game to configure.
- **A correct security decision and a readable screen are different
  problems.** `IPrincipalDirectory` fixed what people saw without moving
  what the platform stores or trusts.
