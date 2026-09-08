# TempestOS — Physical Review Guide

Everything needed to take a clean checkout of this repository onto a
workstation, build it, launch it, exercise it, and reset it — with no
dependency on any machine state that is not written down here.

Written for `WP-REVIEW` (Physical Review / Clean-Machine Readiness) and
verified against a clean clone at the commit this file was added on. Where
something is not verified, or is verified only on one platform, this file
says so rather than implying more than was tested.

---

## 1. Minimum development environment

| Requirement | Detail |
|---|---|
| **.NET SDK** | The version pinned in [`global.json`](global.json) — **10.0.302**, `rollForward: latestFeature`. Any 10.0.3xx SDK satisfies it. This is the only mandatory install. |
| **Operating system** | **Windows** is the CI-verified platform for **build and test**: `ci.yml` restores, builds and runs the full suite on `windows-2022`, both configurations, on every push. Stated precisely, because the distinction matters and this document's own standard demands it: **no CI step on any platform launches the real windowed application on Windows** — the suite is Avalonia headless. That the app launches on Windows is the development team's direct experience, not a CI artefact; it is asserted here without a citation, unlike the Linux claim below, and that asymmetry was found by the `v0.16.0` independent review rather than volunteered. macOS is expected to work and is untested. **Linux launches the desktop application** as of `WP 16.5B` (Avalonia 11.3.20) — but on weaker evidence than Windows: one local `xvfb-run` launch plus an advisory `linux-launch-smoke` CI job that is not a required check. See §8, item 1. Building and running the full test suite works on all three. |
| **PowerShell** | Only for the governance health check (§2.5). CI uses PowerShell 7 (`pwsh`); the reduced script (`WP 17.0B`) was verified under Windows PowerShell 5.1 on 2026-09-08 (`powershell -NoProfile -File scripts/governance-healthcheck.ps1`, 5 passed). |
| **Network** | Needed **once**, for `dotnet restore`. Packages come from the default nuget.org feed; the repository declares no `NuGet.config` and no private feed. After restore, build/test/run are offline. |
| **Not required** | No .NET workloads (`dotnet workload install` is never needed). No Visual Studio. No Node, Python or Docker. No database. No SDK-external build tools. No code generation step. No environment variables. No secrets, licence file, API key, account or sign-in of any kind. |

An IDE is optional. Visual Studio 2022+, Rider or VS Code all open
`src/TempestOS.slnx`; nothing in the build depends on one.

---

## 2. Build and test

Run from the repository root. `src/TempestOS.slnx` is the whole solution —
eight projects, including both test projects (`Tempest.Core`, `Tempest.Workspace`, `Tempest.Harness`, `Tempest.Desktop`, `Tempest.Samples`, `Tempest.Validation` and the two test projects; `src/Frozen/` is deliberately outside it).

```
git clone <repository-url> TempestOS-Core
cd TempestOS-Core

dotnet restore src/TempestOS.slnx
```

**2.1 Build Debug**

```
dotnet build src/TempestOS.slnx --configuration Debug --no-restore -p:TreatWarningsAsErrors=true
```

**2.2 Build Release**

```
dotnet build src/TempestOS.slnx --configuration Release --no-restore -p:TreatWarningsAsErrors=true
```

`TreatWarningsAsErrors` is deliberately **not** set in
`Directory.Build.props` — it is applied on the command line, exactly as CI
applies it, so a local build behaves as it always has while the gate stays
the same gate.

**2.3 Core tests** — 4,922 tests, ~20 seconds (re-derived at `v0.17.0`, 2026-09-08; the `v0.16.0` tree had 5,153 before WP 17.0C removed scaffolding and WP 17.2A froze the plugin, REST and licensing suites)

```
dotnet test tests/Tempest.Core.Tests/Tempest.Core.Tests.csproj --configuration Debug --no-build
```

**2.4 Desktop tests** — 496 tests, ~2.5–3 minutes (re-derived at `v0.17.0`, 2026-09-08; set `TEMPEST_TEST_TIMEOUT_FACTOR=3` when the machine is busy)

```
dotnet test tests/Tempest.Desktop.Tests/Tempest.Desktop.Tests.csproj --configuration Debug --no-build
```

These drive real Avalonia windows through `Avalonia.Headless`. **No display
is required and no window appears.** Three minutes with no visible output is
normal; the suite starts a full `WorkspaceHost` many times over.

Both suites at once, matching CI:

```
dotnet test src/TempestOS.slnx --configuration Debug --no-build
```

**2.5 Governance health check**

```
pwsh -File scripts/governance-healthcheck.ps1
```

Read-only; it never writes inside the repository. Expect
**5 passed, 0 warned, 0 failed** (`WP 17.0B` reduced the check set to
the five that derive from source and git). On Windows without PowerShell 7,
`powershell -NoProfile -File scripts/governance-healthcheck.ps1` works.

> If you pass `-RepoRoot` explicitly, give it an **absolute** path. A
> relative one produces spurious `FAIL` results.

---

## 3. Launching the application

**The shipped application is `Tempest.Desktop`.**

```
dotnet run --project src/Tempest.Desktop/Tempest.Desktop.csproj
```

Or run the built executable directly:
`src/Tempest.Desktop/bin/Release/net10.0/Tempest.Desktop` (`.exe` on
Windows).

> **The working directory decides where your data goes.** See §4. Running
> via `dotnet run` from the repository root puts data in the repository
> root; double-clicking the built executable puts it beside the executable.
> Pick one and stay with it for the whole review, or the second launch will
> look like it lost your work when it has simply looked in a different
> place.

`Tempest.Harness` is **not** a second application — it is the Internal
Engineering Harness, a console verification tool
([`ADR-0101`](docs/adr/ADR-0101-tempest-app-workspaceshell-is-tempestos-internal-engineering-harness-not-a-shipped-product.md),
amended `WP 17.2B`). It is not part of the review:

```
dotnet run --project src/Tempest.Harness/Tempest.Harness.csproj
```

### What happens on first launch

- The Runtime Host starts and discovers six Engineering Discipline modules.
- **There is no REST listener, no licence and no plugin trust to think
  about.** The inbound REST API, Licensing and the plugin trust platform
  are frozen out of the `v1.0` build (`ADR-0146`, `WP 17.2A`). Plugin
  *manifest* discovery still runs and logs that `Plugins/` is empty,
  which is expected.
- The first run creates `persistence-data/tempest.db` and
  `persistence-data/logs/` (§4) and takes `persistence-data/tempest.lock`
  for as long as the application is open.
- **There is no demo or sample data.** The shipped application does not
  reference the sample harness. Home, Projects and the Engineering
  Workspace all start genuinely empty. First-run state is deterministic:
  an empty catalogue, no project open, `Home` selected.

---

## 4. Where runtime data lives

All persisted state is written under a single folder:

```
<working directory>/persistence-data/
├── tempest.db      # Everything: settings, session and UI state, window
│                   # geometry, recents, favourites, macros, projects,
│                   # engineering objects, document revisions, audit rows
│                   # and attachment bytes. One SQLite database.
├── tempest.db-wal  # SQLite write-ahead log — present while running,
├── tempest.db-shm  # and its shared-memory index. Both are part of the
│                   # database, not caches you may delete separately.
├── tempest.lock    # Held open exclusively while the application runs,
│                   # so a second instance on this folder is refused
│                   # rather than allowed to interleave writes.
└── logs/           # tempest-yyyyMMdd.log, one file per day, oldest
                    # deleted beyond 14 files (`WP 17.2A`, ADR-0146).
```

- The root is the value of `Persistence:RootPath`, and when that is not
  configured it is the **relative** path `persistence-data` — resolved
  against the **process working directory**, not the install location.
- The folder is created on first launch. It is listed in `.gitignore` and
  is never source.
- **Before `v0.17.0` this folder held a tree of directories and files, one
  file per record** (`ADR-0041`). It now holds one database (`ADR-0144`),
  because a file tree could not fsync a write, answer a query without
  scanning a directory, make two writes land together, or keep a second
  instance out. `Persistence:Backend=files` restores the old layout for
  `v0.17.0` only, and is deleted in `v0.18.0`.
- There is no registry use, no `%APPDATA%`/`~/.config` use, and no file
  written outside this folder and the build output.
- **Logs go to `logs/` under this same root, and to the console when one
  is genuinely attached** (`Tempest.Harness`'s own console harness; never
  `Tempest.Desktop`, which has none) — `WP 17.2A` (ADR-0146). Before this,
  logs went to the console only, and the application wrote no log file.

### Configuring TempestOS

`WP 17.2A` (ADR-0146) made every `Runtime:*`/`Identity:*`/
`Persistence:*` key an operator can reach without editing source.
`src/Tempest.Desktop/appsettings.sample.json`, shipped next to the
executable, documents every key the platform reads — copy it to
`appsettings.json` (next to the executable, or in the current directory)
and edit it. The same keys may instead be set as environment variables
prefixed `TEMPEST_`, with `__` (double underscore) as the section
separator — for example `TEMPEST_Runtime__Logging__MinimumLevel=Debug` —
or on the command line as `--Section:Key=value`. Precedence, lowest to
highest: `appsettings.json` next to the executable, `appsettings.json` in
the current directory, environment variables, the command line.

---

## 5. External dependencies

**None at runtime.** No server, no database *service*, no cloud service,
no authentication, no network access after the initial package restore.
Since `v0.17.0` the application's own data lives in an embedded SQLite
file inside the persistence folder (`ADR-0144`): it runs in-process, it
listens on nothing, it needs nothing installed, and there is no
connection string to configure — the only knob is which folder it lives
in. The identity used for authorship and audit is taken from the
operating-system account, with a safe fallback when none can be read.

The only listener is the loopback REST API described in §3, which the
application itself starts and stops.

---

## 6. Clean reset

Stop the application first, then:

```
# Windows PowerShell
Remove-Item -Recurse -Force persistence-data

# Linux/macOS
rm -rf persistence-data
```

That is the complete reset: it removes `tempest.db` and its `-wal`/`-shm`
companions and `tempest.lock`, and with them every project, every
engineering object, all session state and all UI preferences, returning
the application to exactly its first-run state. Nothing else needs to be
cleaned, and nothing outside the folder is touched.

**Delete the whole folder, not files inside it.** `tempest.db`,
`tempest.db-wal` and `tempest.db-shm` are one database in three files; a
`-wal` left beside a deleted `.db`, or the reverse, is a broken store
rather than a fresh one. Stopping the application first is not optional
advice here either — while it runs it holds `tempest.lock` open
exclusively, so the delete will be refused.

To also reset the build:

```
git clean -xdf
```

which removes `bin/`, `obj/` and `persistence-data/` together. A full
rebuild then takes about 15 seconds after restore.

---

## 7. Physical smoke test (10–15 minutes)

Every step below uses behaviour that exists today. Where something is
deliberately not implemented, the step says so rather than asking for it.

Launch with `dotnet run --project src/Tempest.Desktop/Tempest.Desktop.csproj`
from the repository root, so data lands in `<repo>/persistence-data`.

| # | Step | Expected result | Counts as a failure if |
|---|---|---|---|
| 1 | Launch | A window titled *TempestOS — Engineering Workspace* opens on **Home**, showing the cross-project Cockpit with honest empty states. The left rail lists **Home, Projects, Engineering, Engineering Calculations** as active and **Tasks, Commercial, Resources, Knowledge, Administration** dimmed with a "not implemented" badge. | The window does not appear; an error dialog appears; a dimmed module is clickable and opens something. |
| 2 | Rail → **Projects** | The project catalogue appears, empty, with **Open Project** and **New Project…** buttons. | The catalogue does not render, or claims projects that do not exist. |
| 3 | **New Project…** | A prompt appears pre-filled with the next free identifier (`P-0001` on a clean machine). Accept it and give a name, e.g. *Apollo Pump Redesign*. The project appears in the list. | No prompt; the project is not listed after creating it. |
| 4 | Open the project | The **Project Workspace** opens. Tabs: **Overview, Engineering, Documents, Requirements, Tasks, Risks, Timeline** are live; **Reports** and **Settings** are marked not implemented. The status bar names the open project. | The status bar does not name the project; a live tab renders nothing. |
| 5 | Rail → **Engineering** | The Engineering Workspace opens *inside the project*: Ribbon across the top with one tab per discipline (Calculations, Documents, Manufacturing, Mechanical, Requirements, Verification), Project Explorer, and a docking area. | The Ribbon or Explorer is missing; the project context is lost. |
| 6 | Ribbon → **Mechanical** → a **Create** action | A prompt collects the values the command declares (name, and a Kind where the command offers one). The new object appears in the Project Explorer. | Nothing is created; the prompt collects nothing; the Explorer does not refresh. |
| 7 | Select it → Ribbon **Rename** (or **Edit**) | The **Object Editor** opens as a tab with Name/Content fields — deliberately, rather than a one-line box over the ribbon ([`ADR-0096`/`ADR-0097`](docs/adr/)). Change the name and save. The Explorer and Property Inspector show the new name. | The editor does not open; the change does not appear in the Explorer. |
| 8 | Press **Ctrl+K** | The Command Palette opens over the workspace. Type part of a command name to filter. Commands unavailable for the current selection stay listed but **disabled, each showing its own reason** — that is the designed behaviour ([`ADR-0070`](docs/adr/)), not a fault. | The palette does not open; an unavailable command is silently missing, or is enabled and then fails. |
| 9 | Invoke a status/lifecycle command from the palette | It runs against the selected object, and the outcome appears in the status bar and in Command History in the Output panel. | Nothing happens and nothing is reported either way. |
| 10 | Project Workspace → **Requirements** | With no requirements yet, the area says so plainly. Create one from the Engineering Workspace's **Requirements** ribbon tab, then return: it is listed with its status. | The area claims to be unimplemented, or stays empty after a requirement exists. |
| 11 | Project Workspace → **Documents** | Same shape: an honest empty state, then the document you create from the **Documents** ribbon tab appears. Opening one opens a real viewer panel. | The document does not appear, or the viewer fails to open it. |
| 12 | Engineering Workspace → **Calculations** ribbon tab | Create a Calculation. **Note:** *executing* a calculation from this tab is deliberately unavailable — the command is registered and reports that this platform cannot yet collect structured input for it. Seeing that stated reason is the correct result. | The command is missing entirely, or claims to run and silently does nothing. |
| 12a | Rail → **Engineering Calculations** | The workspace renders as **two side-by-side columns** — Calculations and Reference Library on the left, Inputs / Results / Traceability / Verification on the right — with nothing drawn over anything else (`v0.16.0` drew both columns on top of each other). | Overlapping text or controls anywhere on this surface. |
| 12b | **Populate Material Library** | Six material records appear in the Reference Library list, each marked *Draft, rev 1*. Select one; the release panel asks what source you consulted and why it is being released. Enter both and press **Verify and Release Material**: the record shows *Released*. | Nothing appears; the release succeeds with either box empty. |
| 12c | **New Calculation** → **Calculate** with the released material and the default inputs (12 kN, 60 mm², 150 mm, 50 g) | Results show applied stress **200 MPa** against the material's allowable, a margin, a mass of **24.3 g**, and *Meets criteria* for 6082-T6; Traceability names the material record **and the revision it was released at**. Name the calculation; it appears in the Calculations list. | A different number; no pinned revision; the calculation cannot be named or does not appear. |
| 13 | Close the application | It closes cleanly, prompting only if there is genuinely unsaved work. | A crash, a hang, or an error on exit. |
| 14 | Relaunch — **from the same working directory** | The project, every object created, the released material, the named calculation, the last area and the window geometry all come back, read from `persistence-data/tempest.db`. A new day's file appears under `persistence-data/logs/` with the host lifecycle phases at Information level. | Anything created in steps 3–12c is missing. Before recording a failure, confirm the working directory is the same one (§4). |
| 14a | While the application is running, launch a **second** copy from the same working directory | The second copy refuses to start with a message naming the data folder and the fact that another TempestOS is using it. | A second window opens over the same data. |
| 15 | Delete a test object | Ribbon **Delete** asks for confirmation first, naming what will be deleted. Confirm: the object goes, and the selection clears rather than pointing at something deleted. | No confirmation; the object stays; the Property Inspector still shows it. |
| 16 | Reset | Close the application and delete the whole `persistence-data` folder — `tempest.db`, its `-wal`/`-shm` companions and `tempest.lock` together (§6). Relaunch: the application is back to a clean first run — no projects, no objects. | Anything survives the reset. The delete is refused while the application is still running (that is the instance lock doing its job — close it and retry). |

**What is out of scope for this smoke test**, because it does not exist
yet: choosing a destination object for Copy/Move (no object picker — the
commands say so), attaching a file to a document (no file picker),
executing or recalculating a calculation with real inputs, keyboard
shortcuts bound to discipline commands, and the Reports, Settings,
Commercial, Resources, Knowledge, Administration and cross-project Tasks modules.

---

## 8. Known limitations that affect a physical review

1. **The desktop application now launches on Linux/X11** (`TD-116`,
   resolved by `WP 16.5B`). It previously failed with
   `System.TypeLoadException: Could not load type
   'Tmds.DBus.Protocol.Connection'` during Avalonia's X11 platform
   initialisation, before any window is created, because the security pin
   then in place — `Tmds.DBus.Protocol 0.94.2`, remediating
   `GHSA-xrw6-gwf8-vvr9` — sat on an API line `Avalonia.FreeDesktop 11.2.3`
   could not bind against. `WP 16.5B`'s spike upgraded `Avalonia`,
   `Avalonia.Desktop`, `Avalonia.Themes.Fluent` and `Avalonia.Fonts.Inter`
   to `11.3.20` and repinned `Tmds.DBus.Protocol` to `0.21.3` — the
   advisory's own backported fix on the API line `Avalonia.FreeDesktop
   11.3.x` binds against — verified by launching the built application
   under `xvfb-run` on Linux with the full Desktop and Core suites green.
   See `docs/releases/v0.16.0/WP16.5B Linux Launch Spike Report.md` for the
   reproduction, the fix, and the launch evidence. Per `D-025`: **Windows
   is CI-verified; macOS is supported by design, not CI-verified; Linux
   now launches (see that report for exactly what this evidence does and
   does not establish) with an advisory `linux-launch-smoke` CI job, not
   yet a required gate. Review on Windows or Linux.**
2. **Data location follows the working directory** (§4). Not a defect, but
   the single most likely way to conclude wrongly that persistence is
   broken.
3. **Port 5080 is not bound by default** (`D-024`, ratified by the Product
   Owner on 2026-09-05). The REST API's listener starts only when
   `Runtime:RestApi:Enabled` is configured `true`; absent, empty, or
   unparseable all resolve to disabled. When enabled, it still binds
   loopback-only on port 5080 (overridable via `Api:Port`), and a
   conflict is isolated and logged exactly as before — the application
   still starts.

---

## 9. If something goes wrong

| Symptom | Check |
|---|---|
| `dotnet` not found, or an SDK error naming `global.json` | Install .NET SDK 10.0.302 or a later 10.0.3xx. |
| Restore fails | Network access to nuget.org. No other feed is used or needed. |
| Build fails with warnings-as-errors | Confirm it fails without `-p:TreatWarningsAsErrors=true` too; a clean tree builds with zero warnings in both configurations. |
| Desktop tests appear to hang | They take about three minutes with no output. Let them finish. |
| The application starts empty after a relaunch | Working directory (§4), before anything else. |
| The application will not start on Linux | §8, item 1 — this was `TD-116`, resolved by `WP 16.5B`; if you still see it, that is a new defect, not the known one. |
