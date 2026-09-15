# Deleting Dead Architecture and Consolidating Duplicates

**Release:** `v0.14.0` remediation programme, 2026-08-31 – 2026-09-01 ·
**Work Package(s):** `WP-C`, `WP-D1`, `WP-D2`, `WP-G` · **Debt:** `TD-01`
(closed by this programme — see below), `TD-111` (closed), `TD-112`
(closed), `TD-109` (partially resolved — still open) · **Decision:**
`ADR-0103` · **Code:** `src/Tempest.Core/Settings/SettingsDocument.cs`,
`src/Tempest.Desktop/Composition/ActionOutcomeReporter.cs`,
`src/Tempest.Desktop/Composition/ProjectDeliveryCoordinator.cs`,
`src/Tempest.Desktop/Composition/ProjectGovernanceCoordinator.cs`

**In plain terms.** Four pieces of work in this release added no
feature at all. One deleted code nobody used any more. Two took several
near-identical copies of the same behaviour, scattered across screens
you click through every day, and replaced them with one shared piece of
logic. One moved a large chunk of code to a tidier home without changing
what it does. None of this is visible on screen; all of it is what stops
small inconsistencies — a saved setting that silently vanishes on one
screen but not another, a message that appears on four screens but not a
fifth — from creeping in as the product grows.

## Four different ways to make the codebase smaller

Every other Work Package in this remediation programme added a
capability or a safety check. These four made the platform smaller and
more uniform instead, which is not lesser work — it is riskier work.
"Delete this" and "these seven things are one thing" are claims that are
provably wrong in a way "add this new feature" is not: get a feature
wrong and it doesn't work; get a deletion wrong and something that used
to work quietly stops, somewhere nobody is looking. So each Work Package
below spends most of its effort on the proof that the change is safe,
not the change itself — which is exactly the discipline worth reading it
for.

## `WP-C`: proving an abandoned fork was dead

`Tempest.Core` had carried a second, earlier architecture since before
the runtime host it was replaced by existed — `BootstrapService`,
`HostingService`, `ConfigurationService`, `LoggingService`,
`ProjectService`, `ProjectNumberGenerator`, `IProjectRepository` and
`JsonProjectRepository`. It still compiled. It was also, on inspection,
called by nothing: `BootstrapService` was the only thing that used the
first three, and nothing called `BootstrapService`; `ProjectService`
built its own repository with `new` in its constructor, bypassed
dependency injection entirely, and had been superseded by
`Tempest.App.Projects` (now `Tempest.Workspace`) releases earlier.

Dead code that still compiles is not harmless: it is the first thing a
new reader finds when they go looking for "how does this platform
start", and it gives them the wrong answer. This debt row, first opened
at `WP 2.6`, had sat on exactly that question for several releases:
revive the old architecture, or delete it. `WP 5.2` had investigated
once and declined to migrate it, but never ruled on deleting it. An
audit on 2026-08-31 re-confirmed the same finding — zero production or
test callers, no XML documentation (every other type in `Tempest.Core`
carries some), and the only remaining mentions anywhere were two
comments calling the code "retired". That three-way proof, not a single
grep, is what made deletion safe to *establish* rather than assume.

Commit `dfa6ee1` removed the eight named types. Deleting them orphaned a
ninth: `ApplicationConfiguration`, the settings record only those types
had produced and consumed. It was not in the approved scope, so it was
reported rather than swept away in the same commit — deletions that grow
themselves mid-flight are how "scope" stops meaning anything — and
removed the next morning, ruled on, in commit `7e28f74`. Two governance
registers that still described the deleted namespaces as current were
corrected in the same commits that falsified them, not in a later
tidy-up.

**The row that closed twice.** Both the commit message and this
programme's own retrospective call this the closure of the debt row.
The current live register, `BACKLOG.md`, disagrees in a way worth
recording rather than smoothing over: it attributes the row's actual
closure to a later Work Package, `WP 17.2A`, because the row's real
complaint — two logging mechanisms coexisting — had a second, less
visible life after `LoggingService` the class was gone. Code built
directly against `Microsoft.Extensions.Logging` kept running unconnected
to the platform's own `ILogger`/`ILogSink` pipeline until `WP 17.2A`
routed it through the same one. Deleting the named, unreferenced classes
was real and necessary work; it turned out not to be the whole of what
the row was naming. A register that says "closed" the day the visible
copy disappears can still be wrong about whether the underlying problem
is gone.

## `WP-D1`: the tail seven screens each wrote by hand

Seven places in the Desktop wrote the same four steps after almost every
user action: set the status-bar text, raise a pop-up confirmation (a
"toast") whose colour depends on success or failure, record an entry in
the Command History panel, and refresh whichever other panels might now
be showing stale data — the last step skipped when the action failed and
changed nothing. `ActionOutcome`, a small type carrying exactly the two
facts this sequence needs, already existed; what had never existed was
one implementation of the sequence itself.

Seven hand-written copies of a four-step sequence is not seven times the
work of one — it is seven independent chances for one copy to skip a
step, run it in the wrong order, or check the wrong condition, with
nothing able to tell the difference at runtime, because seven correct
copies and one shared implementation look identical from outside. That
is a plain-language reason duplication is dangerous: fix a mistake and
you may fix it in six places and miss the seventh, and nothing will fail
a test to tell you so.

Commit `0ced2c5` gave the sequence one implementation,
`ActionOutcomeReporter` (`src/Tempest.Desktop/Composition/ActionOutcomeReporter.cs`),
built once by `MainWindow` and used at six call sites covering seven
tails: the Ribbon, Explorer, Inspector and Object Editor's own
"action completed" handling, drag-and-drop move, and Undo/Redo. Two
things a careless merge would have broken were kept deliberately. First,
the set of panels each caller refreshes is genuinely different — the
Explorer does not reload itself after its own action, having just done
so — so the refresh is passed in as a delegate; the reporter decides only
*whether* to run it, never *what* it does. Second, the gate is on
whether anything changed, not on whether the action succeeded:

```csharp
if (outcome.WorkspaceChanged && refresh is not null)
    await refresh().ConfigureAwait(true);
```

The Object Editor's combined Owner/Priority save is the concrete case
that makes `Succeeded` the wrong test: the Owner half can commit before
the Priority half is refused, so the action reports failure while the
workspace has, in fact, changed. Gating on success there would leave the
Explorer, Inspector and Cockpit showing values that are already wrong.

Four call sites were deliberately left unmigrated, each named with a
reason rather than folded in regardless: the Command Palette reports
through a different call and raises no toast; the Digital Thread graph
sets only the status bar; a third path reports a severity the others
don't use; and the large family of project/task/risk CRUD methods inside
`MainWindow` is a different shape entirely — that is `WP-G`'s subject,
below.

## `WP-D2`: safe and silent are not the same thing

Nine settings stores, spread across `Tempest.Core`, `Tempest.App` (now
`Tempest.Workspace`) and `Tempest.Desktop` — the user's window layout,
recent-object list, favourites, panel arrangement, workspace layout,
current project, shell location, and saved command macros — had each
hand-written the same two blocks: register the setting idempotently, and
load its stored JSON, discarding it back to documented defaults if it
failed to parse. Six of the nine had no logger at all. A torn write —
disk full, a crash mid-save — silently discarded a user's saved state
with no record anywhere that it had happened. That behaviour was correct
about *what* to do (fall back to defaults, never crash the application
over a corrupted setting — a contract already named `TD-60`) and wrong
about doing it *invisibly*. Degrading safely and degrading silently had
been quietly conflated nine times over.

Commit `171dc68` gave both blocks one implementation,
`SettingsDocument<TDocument>`
(`src/Tempest.Core/Settings/SettingsDocument.cs`), used today by
`UserSettings`, `WindowUiState`, `RecentObjectsState`,
`FavouriteObjectsState`, `DesktopPanelUiState`, `WorkspaceState`,
`ProjectContext`, `ShellNavigator` and `MacroManager`. The recovery
contract did not change — a missing or corrupted value still returns
`null`, never throws — but a corrupted one now leaves a trace:

```csharp
catch (JsonException ex)
{
    _logger?.Warning(
        $"Stored setting '{Key}' could not be read and was discarded; " +
        $"falling back to defaults. {ex.Message}");
    return null;
}
```

Registering a logger in the container was not enough on its own: six of
the nine stores are constructed by hand rather than resolved by it, so
each had to be threaded a logger explicitly, through
`DesktopCompositionRoot`, `MainWindow`, `WorkspaceHost` and
`WorkspaceManager` — the cost of making the logging actually reach code
the container never sees. The audit behind this Work Package had counted
eight affected stores; the real count, re-derived from the repository,
was nine — `WorkspaceState` had been missed — corrected rather than
carried forward wrong.

## `WP-G`: a move that is verifiable because it improves nothing

`MainWindow` was, at the same time, the composition root that wires the
application together *and* the implementation of nineteen CRUD methods
for a project's tasks, milestones, deliverables, risks, issues and
decisions — over five hundred lines of dialogs, prompts and history
entries sitting inside the file whose actual job is deciding what the
application is made of. `TD-109` named exactly that mixing of
responsibilities.

Commit `0a9e49b` moved all nineteen methods out, into two new
collaborators split along the domain service each method already used
rather than by size: `ProjectDeliveryCoordinator` (tasks, milestones,
deliverables) and `ProjectGovernanceCoordinator` (risks, issues,
decisions). Not one of the nineteen methods touched both services, which
is what made the seam a real one rather than an arbitrary line drawn
through the file.

The important word is **verbatim**. Every method body was diffed against
its old copy in `MainWindow` and found identical except for two
mechanical edits, everywhere: a field read became a constructor-injected
dependency, and the shared `RecordHistory(...)` call became a delegate
passed in at construction (ten call sites — four in delivery, six in
governance). Prompts, error messages, identifier schemes and operation
order were left exactly as they were. That restraint is the whole reason
the move is trustworthy: a move that also improves something on the way
stops being checkable by comparison, because a difference in the diff
could be either the move or the improvement. Refuse to improve anything,
and every difference in the diff is a bug.

Compiling is not proof the wiring still reaches the moved code, so the
Work Package tested the wiring itself, not just the methods: deliberately
disconnecting one event handler failed four real end-to-end tests that
drive the actual buttons and dialogs; disconnecting another failed two
more. A coordinator nothing calls is worse than the untidy file it
replaced, and only a test that exercises the real wiring can catch that.

**The row that stayed open on purpose.** `MainWindow` went from 1,577 to
1,042 lines — a one-third cut — and `TD-109` was left **open**, marked
partially resolved, rather than closed. What remained was the
composition root proper and a set of genuinely shell-level services
(rendering the current view, the status bar, layout save and restore),
which is a different question from the one this row names. Overstating
what one extraction achieved would have hidden how much of the file is
still doing the wrong kind of work. At the current release, `BACKLOG.md`
still lists `TD-109` as open, owned by a Work Package that has not yet
run (`WP 19.2A`), and `MainWindow` (now
`src/Tempest.Desktop/MainWindow.cs`) is 1,475 lines — grown again since
`WP-G`'s cut, the same pattern the row's own figure has shown before:
each release adds a little, and only a further deliberate extraction
brings the number back down.

The same Work Package's own audit found that `TD-112` — `WP-D2`'s
row — had shipped fully closed a day earlier but had never actually been
marked resolved. It corrected that stale entry as part of this one,
rather than leaving it to a separate commit: work completed in an
earlier gate can outrun the register that is supposed to track it.

## The seam this reopened, and closed for good

`SettingsDocument<TDocument>` was hardened once more after this
programme, in a later release. `WP 16.4B-R1` (commit `6cd31eb`) found
that its optional schema-migration chain — added for documents that
need to evolve their stored shape over time — could walk partway through
a chain and hand back a document stuck at whatever version the walk
happened to stop at, rather than falling back to defaults the way a
document it could not migrate at all should. Its sibling seam,
`EngineeringObjectStateStore`, already had exactly that check, added
after a review rejected an earlier version of it for the same omission.
The fix was to make `SettingsDocument` match: a document the supplied
chain cannot carry all the way to its own highest reachable version is
now discarded and logged, never returned half-migrated. No production
migration used the chain yet when this landed, which is precisely why it
was worth fixing immediately rather than waiting for a defect report —
see `48-durable-state-schema-versioning.md` for the full mechanism and
the golden-corpus tests that guard it.

## What to take away

- **Duplication is dangerous because it is invisible.** Seven correct
  copies of a sequence and one shared implementation behave identically
  from outside; the difference only shows up the day someone fixes a bug
  in six of the seven copies and has no way of knowing a seventh exists.
- **A "verbatim" move is a promise you can check, and that is the whole
  point of making it.** Refuse to improve anything while relocating code
  and every line of the diff becomes evidence; improve it on the way and
  the evidence is gone.
- **Deleting code needs proof that nothing calls it, not confidence that
  nothing should.** No callers in production or tests, no documentation,
  and a named live replacement — three independent supports, because any
  one of them alone can be wrong.
- **Closing the visible copy of a problem is not the same as closing the
  problem.** `TD-01`'s own history is the clearest example here: the
  named dead classes were deleted on schedule, and the duplication they
  stood for still had one more life to live in a different form.
