# Third-Party Notices

TempestOS is built on the .NET SDK and NuGet packages restored from the
public nuget.org feed (`PHYSICAL_REVIEW.md` §1, §5: network is needed once,
for restore; nothing else phones home). This file lists every third-party
package whose own licence terms require or warrant a notice here, beyond
what each package's own NuGet listing already states — created when the
first such package (below) was added to a shipped project, rather than
retroactively for every dependency this solution has ever restored.

| Package | Version | Licence | Used by | Purpose |
|---|---|---|---|---|
| [Velopack](https://github.com/velopack/velopack) | 1.2.0 | MIT | `Tempest.Desktop` only | The Windows installer, in-place update check, and installed-vs-development-run detection (`WP 21.5A`, `WP RC.0A` scope item 1). Never referenced by `Tempest.Core`, `Tempest.Workspace`, `Tempest.Harness`, or any other project — `Tempest.Harness` (the Internal Engineering Harness, `ADR-0101`) is not a Velopack-packaged application and carries no installer. |

Every package above is used exactly as published, unmodified, through its
own public NuGet package; none of its source is vendored into this
repository.
