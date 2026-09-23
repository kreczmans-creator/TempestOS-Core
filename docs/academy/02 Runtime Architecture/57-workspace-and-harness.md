# Workspace and Harness: Splitting Tempest.App

**Release:** `v0.17.0` · **Work Package(s):** `WP 17.2B` · **Decision:**
`ADR-0101` (amended) · **Code:** `src/Tempest.Workspace/`,
`src/Tempest.Harness/`,
`tests/Tempest.Core.Tests/Architecture/DependencyDirectionTests.cs`

**In plain terms.** TempestOS is shipped to users as one application,
`Tempest.Desktop` — the graphical program with the Ribbon, the Project
Explorer, the object editors. Alongside it the project has always kept a
second, text-only tool for its own engineers to poke at the same data
from a terminal. Until this release that tool and the shipped product
were tangled into one compiled unit, and — the part worth a user caring
about — the *shipped product depended on the internal tool*, the wrong
way round. `WP 17.2B` cut them apart into two separate, honestly named
pieces, so a change made for an internal diagnostic tool's convenience
can never accidentally break the product people actually run.

## What a "project" is

In a .NET solution, a "project" is a `.csproj` file plus the source files
it lists — one unit the build compiles into exactly one thing: a library
(a `.dll`, code other projects can call but that does nothing alone) or
an executable (a `.exe`, a program you can run). `src/TempestOS.slnx`
lists every project in the build. A project declares which others it
needs with a `<ProjectReference>` line in its own `.csproj` — the only
thing letting its code call into the other project at all.
`src/Tempest.Workspace/Tempest.Workspace.csproj` is six lines that matter:

```xml
<ItemGroup>
  <ProjectReference Include="..\Tempest.Core\Tempest.Core.csproj" />
</ItemGroup>
<PropertyGroup>
  <OutputType>Library</OutputType>
</PropertyGroup>
```

That is the entire, honest description of what the project is (a
library) and what it is allowed to see (`Tempest.Core`, nothing else).

## Which way the arrow should point

A **dependency direction** is just "who is allowed to need whom". If
project A references project B, A cannot run without B, but B can be
built and tested with no idea A exists. TempestOS's rule is that
dependencies flow one way, downward: the shipped product may depend on
shared platform code, never the reverse. A diagnostic tool or harness
sits *beside* the product in that graph, or below it — it exists to
poke at the product's foundations, and must never be something the
product needs in order to run. "The product depends on the demo
harness" is backwards for the same reason a factory depending on its own
inspection rig would be: the rig checks the factory, and if the factory
stopped working without it, the rig would no longer be a rig.

## A console that outgrew its own name

`Tempest.App` started, at `WP 5.0D`, as *the* application —
`TempestShell`, TempestOS's first composition root, putting the platform
in front of a person (`10-shell-and-application-composition.md`). At
`WP 8.1A` its default launch target became the Engineering Workspace,
whose console front end took the name `WorkspaceShell`
(`17-engineering-workspace.md`). By `v0.10.0` a second, graphical layer
existed — `Tempest.Desktop` — over the same shared Workspace code
(`20-desktop-application-framework.md`), and a `WP11.0A` platform review
asked the obvious question: with two presentation layers over one
project, which one is TempestOS?

`ADR-0101` (`v0.11.0`, `WP 11.3B`) answered it. `TempestShell`, unreachable
from any entry point for three releases, was deleted outright.
`WorkspaceShell` was kept, but reclassified: **`Tempest.Desktop` is
TempestOS's sole shipped product; `Tempest.App`, through
`WorkspaceShell`, is TempestOS's Internal Engineering Harness — a
diagnostic tool, not a second product.** The ADR changed classification,
not code, and declined the one change that would have made it watertight:

> Splitting them into separate projects would be a genuine architectural
> change and is explicitly not proposed here.

So `Tempest.App` kept two identities in one assembly — the shared
Workspace domain layer (`WorkspaceManager`, `IWorkspace`, the six
Engineering Disciplines' commands and node providers) `Tempest.Desktop`
depended on for real work, and the harness console meant to stay a side
tool. Worse, `Tempest.Desktop` reached a few of that layer's non-public
members through an **`InternalsVisibleTo`** grant — a C# attribute one
assembly puts on itself, letting a second, named assembly see its
`internal` members, the ones not part of its public contract. That is
legitimate for a project's *own* test assembly; used between two
different production projects it is a smell, because the real contract
becomes whatever the grant exposes, not the interface — either side can
quietly reach the other's private parts, and a change to something never
meant to be seen from outside can break a consumer nobody declared as
depending on it. The shipped product depended on the harness's own
assembly compiling correctly, the exact backward arrow above, reached
through a side door rather than a declared contract.

## The split: two projects, four commits

`WP 17.2B` did what `ADR-0101` had declined, in four commits on
2026-09-08. `b1cf10f` moved first, with no renaming: `git mv
src/Tempest.App src/Tempest.Workspace`, `OutputType` changed to
`Library`; `WorkspaceShell.cs` and `Program.cs` moved into a new
`src/Tempest.Harness` console project referencing it. `28a4b72` renamed
every `Tempest.App.*` namespace to `Tempest.Workspace.*`/`Tempest.Harness`
across `src/` and `tests/`, and rewrote `DependencyDirectionTests` for
the resulting four-project graph (`44-invariants-that-fail-the-build.md`
covers that rewrite in full): the test now states `Workspace → Core`,
`Harness → Workspace` and `Desktop → Workspace`, each reaching `Core`
only transitively:

```csharp
[Fact]
public void Harness_DependsOnWorkspace_AndReachesCoreThroughIt()
{
    Assert.Equal([Workspace], ReferencedProjects(Harness));
}
```

`04a447b` removed the `InternalsVisibleTo("Tempest.Desktop")` grant and
made the three things it reached real public contract instead:
`IWorkspace` gained a public `Cockpit` property; `WorkspaceManager.StatusBar`
and `EngineeringCockpit` (and the small types it returns, such as
`CockpitActionItem`) became `public`. Desktop now reads
`workspace.Cockpit` through the interface, not a cast to a concrete
class. `Tempest.Harness` keeps its own, disclosed `InternalsVisibleTo`
grant, reaching `ProjectExplorer.ProjectExplorerConcrete` and the
concrete `Workspace` class — never part of the twelve `WP8.0B` Workspace
Contracts, so a diagnostic tool reaching its own project's internals is
the attribute's legitimate use, not the smell. `cc31de3` updated CI,
`README.md` and `PHYSICAL_REVIEW.md`, and added one line under
`ADR-0101`'s Status section recording the amendment without rewriting
its body.

## What to run, and why keep the console at all

`README.md`'s two run commands now say plainly what each project is for:

```
dotnet run --project src/Tempest.Desktop/Tempest.Desktop.csproj   # the product
dotnet run --project src/Tempest.Harness/Tempest.Harness.csproj   # the harness
```

Keeping a console harness beside a full graphical application is not
redundancy. `ADR-0101`'s finding `F6` still holds: `WorkspaceShell` loads
no Avalonia (the graphical framework), so it starts in a fraction of a
windowed application's time; it is directly scriptable and redirectable,
which a GUI is not; and it exercises the same shared Workspace logic
`Tempest.Desktop` runs, from a terminal or a CI job, with nothing to
click — a fast, text-only way into the same engine, worth the small cost
of building it twice.

## The warning made honest

The `v0.17.0` release notes flagged, honestly, that "documentation
comments still say `Tempest.App` in a handful of Core, Samples,
Validation, governance and security files that never referenced the
project". Re-running the check today, `grep -rn "Tempest.App" src/
docs/` finds **214** matches, not a handful. Of those, 17 sit in current
`src/` files — doc comments in `Tempest.Core`, `Tempest.Samples`,
`Tempest.Validation` and one template `README.md`, none of which ever
held a project reference to `Tempest.App` and none of which affect how
anything compiles. The
remaining ~197 are in `docs/`: mostly historical ADRs and per-release
`Release Notes.md` files that correctly describe what the project was
called when they were written, plus eighteen Academy chapters — the
three this chapter cross-references included — not yet revisited.
`cc31de3`'s own commit message names this as deliberate scope:
`docs/adr/*.md` files other than `ADR-0101`, `docs/governance/`,
`docs/security/` and those `src/` doc comments were "left as
historical/prose references, outside this WP's named doc scope." The
honest state is a large, mostly historical residue, left alone
deliberately — rewriting a historical ADR to describe a name that did
not exist yet would make the record less true, not more.

## What this deliberately did not change

No behaviour changed. `WorkspaceShell` still starts the same way, prints
the same Cockpit, and every one of `Tempest.Core.Tests`'s several
thousand tests passed after each commit. `ADR-0101`'s own body —
Decision, Consequences and Alternatives — was not rewritten; only its
Status line gained one sentence recording the split. `Tempest.Harness`'s
`InternalsVisibleTo` grant stayed, because it reaches members never part
of the platform's declared contract — the fix ended the *cross-product*
grant, not every grant.

## What to take away

- **A dependency direction is a promise, and it runs one way.** A
  shipped product may depend on shared platform code; it must never
  depend on a tool that exists only to test that platform.
- **`InternalsVisibleTo` between two production projects means the real
  contract is whatever the grant exposes, not the interface.** The fix
  is not removing the access; it is making what was reached through the
  side door part of the real, declared contract.
- **A decision an ADR declines is not closed forever.** `ADR-0101`
  recorded exactly why it was not splitting the project; amending the
  same ADR four weeks later, rather than writing a new one, kept the
  decline, the reasoning and the reversal in one place a future
  contributor can actually find.
