# Desktop Productisation and Brand Recovery

**Release:** `v0.15.0` (`WP 15.0A`–`D`, 2026-09-03) · plus the `v0.16.0` Linux
launch spike (`WP 16.5B`) · **Debt:** `TD-116`, `TD-121`, `TD-122` · **Code:**
`Tempest.Desktop.Theming`, `Tempest.Desktop.Branding`,
`Tempest.Desktop.Views.RibbonView`

**In plain terms.** Software can work — every button does what it says — and
still not feel like a real product: the colours look borrowed, a click on an
obvious control does nothing, a list forgets which row you had selected, a
scrollbar that should appear simply doesn't. None of that is a crash, but it
is what a customer notices in the first five minutes. This chapter covers
recovering the desktop application's visual identity, fixing a string of "it
doesn't quite work" defects found by actually using the app, and then getting
that same application to start at all on a different operating system.

## Twenty commits, no numbers

Between the `v0.14.0` tag and `v0.15.0`, twenty commits landed directly on
`main`: a brand recovery, a real Windows crash fix, two phases of
productisation, and a Ribbon fix — all real, already-merged work, none of it
with a Work Package number, a `WorkPackages.md` row, a Technical Debt entry,
or a retrospective. `VERSION` still read `0.14.0` while the tree had moved
well past what that tag described. `v0.15.0` shipped no new capability; its
purpose was to make that work honestly represented in governance, with a
second, independent review re-verifying it before recommending release. **A
commit that fixes a real defect is not finished the moment it merges — it is
finished when the register knows it exists.**

## WP 15.0A: recovering a brand, not inventing one

`Tempest.Desktop`'s theme, icon set and chrome had never been aligned with the
Tempest Engineering Design System the Product Owner supplied. The only place
that alignment already existed, tested and shipped, was the sibling
`Tempest.Companion` product. Commit `cd68077` recovered that work rather than
re-deriving it: `BrandPalette` (colour tokens and theme-reactive keys),
`TempestTheme` (Fluent theme resources for both dark and light variants),
`ChromeStyles` (three reusable button treatments — flat, subtle, `Primary`,
its accent fill reserved for "the one call-to-action a surface offers", per
its own doc comment at HEAD), realigned `DesignTokens`, mark geometry
transcribed verbatim into `Branding/`, and a monochrome vector icon set
replacing every emoji in the shell. Naming treatments, rather than
hand-painting buttons, means a button's colour now says what kind of action it
is and still responds to hover/press feedback — the pre-brand buttons set
colour as a local value, silently overriding that feedback. Recovery beat
invention because the Companion had already solved this once; re-deriving it
risked a second, divergent reading of the same mark.

**A citation that does not resolve, disclosed rather than fixed.** `cd68077`'s
message and the design system reference both cite `FCR-0092` as the finding
this closes. The Future Capability Register runs `FCR-0001`–`FCR-0088`;
`FCR-0092` is not in it — likely a Companion-scoped number cited without
translation. Not a release blocker; worth writing down anyway.

**The Object Editor's own branding.** The Object Editor — the most-used
surface, since every object edit goes through it — still used plain
`FluentTheme` buttons and colour emoji. Commit `257bac7` rebranded it with
`ChromeStyles`, the vector icon set, and the brand's title face. It merged
alongside the productisation work below (as `820d052`), but completes the
brand-recovery thread `cd68077` started.

## WP 15.0B: found by using the app, not reading it

The method matters more than any single fix: **these defects were found by
driving the real, running application under Xvfb** (a virtual display that
lets a desktop app run and be screenshotted without a monitor) **— real
interaction, not source review.** A dead button and a working one look
identical in a diff. What that audit found, closed by `ee15986` and `0151f35`:

- **A dead-end chip.** `ShellHeaderView`'s project chip looked clickable and
  did nothing; `IShellNavigator.ReturnToProjectAsync` already existed with no
  caller. Now wired, disabled with no project open.
- **Selection lost on every reload.** `ProjectExplorerView` rebuilt its tree
  wholesale and Avalonia's `TreeView` matches selection by reference, so any
  rename silently lost the user's place. Fixed by restoring the selected Id
  after rebuild; each node now also shows its real child count from data
  already in memory.
- **A KPI card computed but never shown.** `EngineeringCockpit.KpiCards`,
  the cross-discipline aggregate, was fully computed and simply never
  reached the Cockpit view — static data beside numbers that moved.
- **Typed gestures, and dead chrome elsewhere.** Menu shortcut text was
  hand-appended to labels, separate from the real keydown handler; replaced
  with `MenuItem.InputGesture`. The Digital Thread graph view (how engineering
  objects relate) got the same chrome pass — emoji gone, real vector icons,
  themed titles.
- **Scroll-to-new-item, built around a real gap.** Proper support needs
  `CommandResult` to carry a structured Id — it exposes only success and a
  message, a Core contract gap disclosed, not silently worked around. The
  Desktop-layer fix diffs the tree's node Ids before and after reload: a
  genuine create is "exactly one Id appeared and nothing disappeared."

`WP 15.0B` was worked sequentially: its files — chrome styles, the menu
factory, the Explorer — were judged too coupled to parallelise safely. **A
figure this chapter does not repeat as fact:** these commits' own messages
cite pass counts from a local workaround (deleting `global.json`) later found
to manufacture its own failures; the confirmed CI baseline is 0 failures.

## WP 15.0C: three independent defects, worked in parallel

`WP 15.0C` checked first whether its three problem areas shared any files —
they did not — then dispatched three background agents in isolated worktrees,
each merged sequentially by review, never by the agent itself:

- **`64b0c16`** — Ribbon Create-group buttons used the same flat weight as
  ordinary actions; moved to `ChromeStyles.Primary`. The Property Inspector's
  Name field silently fell back to read-only text for any non-renamable Kind;
  a tooltip now discloses why.
- **`cbde521`** — `Escape` did nothing in the confirmation/message dialogs
  used for genuine, irreversible deletes; keyboard handling and safe default
  focus were added. The Command Palette's search, wired to `TextChanged`, did
  not fire reliably on programmatic text assignment; rewired to
  `PropertyChanged`. A validation error rendered as bare colour with no glyph;
  it now reuses the Object Editor's severity row via `InternalsVisibleTo`.
- **`0ba55cb`** — `DeclaredCapabilityView`'s hero icon rendered a raw Unicode
  fallback glyph instead of the rail's own vector icon. `DocumentAreaView`
  lost tab context on close, since `TabControl` resets to the first tab
  regardless of which closed; fixed by explicitly selecting the tab that
  replaces it.

## WP 15.0D: the scrollbar with nowhere to grow

The Ribbon's horizontal scrollbar never appeared at compact widths, even
though its command groups genuinely overflowed. The first hypothesis — a width
value failing to propagate down to the Ribbon — was tested with real headless
diagnostics and disproven: the `ScrollViewer` already received a correctly
bounded width.

The real cause was height, not width: **a `ScrollViewer`'s `Auto` horizontal
scrollbar only occupies room its own final size already has slack for — it
never grows its own container to make room for itself.** The Ribbon's
`ScrollViewer` sits in a vertical `StackPanel` sized to its own content, so
nothing gave it more height than its button rows needed; overflowing groups
were clipped in total silence. Neither `AllowAutoHide` nor swapping the outer
panel for a `DockPanel` touched the actual starvation. The fix, in
`RibbonView.cs` at HEAD:

```csharp
void ReserveScrollbarHeightOnce(object? _, EventArgs __)
{
    scroller.LayoutUpdated -= ReserveScrollbarHeightOnce;
    scroller.MinHeight = groupsRow.DesiredSize.Height + DesignTokens.SpaceXl;
}
scroller.LayoutUpdated += ReserveScrollbarHeightOnce;
```

One scrollbar's worth of height, reserved once from the button row's own
measured height (width-independent) plus an existing spacing token — free when
nothing overflows. Two regression tests find the real `ScrollBar` control and
assert its rendered visibility, a check `Extent`/`Viewport` values alone would
not catch.

Both this defect (`TD-122`) and the Windows startup crash from `WP 15.0A`'s
own commit (`TD-121` — an `async void` continuation resuming off the UI thread
and throwing `Dispatcher.VerifyAccess` unhandled) are named here only in
passing: the crash is a UI-thread affinity problem, covered fully in
`46-the-ui-thread-and-blocking-calls.md`.

## WP 16.5B: the desktop that would not start on Linux

`TD-116` was worse than cosmetic: the desktop application could not launch
under Linux/X11 at all. The cause was a trade already accepted —
`Tmds.DBus.Protocol` pinned to `0.94.2` to remediate a security advisory — but
that version no longer presented the type layout `Avalonia.FreeDesktop 11.2.3`
bound against by compiled member layout, not semantic version. Windows and
macOS never initialise that code path; the test suite passed regardless, since
headless tests never touch X11 either — which is why nothing had noticed.

`WP 16.5B` was framed as a **one-day, timeboxed spike with a pre-declared
fallback**: state the platform matrix honestly if no fix was found in time.
Research found two versions both marked "fixed" for the same advisory on
incompatible API lines: `0.94.2` on a rewritten line with a different layout,
`0.21.3` backporting the identical fix onto the older line
`Avalonia.FreeDesktop` actually binds against. Reverting the pin was rejected
outright — that reopens the advisory. Avalonia was upgraded `11.2.3` →
`11.3.20`, the newest release whose own `Avalonia.FreeDesktop` requires
`Tmds.DBus.Protocol >= 0.21.3`, and the `.csproj` at HEAD confirms `Avalonia
11.3.20`/`Tmds.DBus.Protocol 0.21.3` directly. **The README is stale on this
point** — its architecture diagram still describes `Tempest.Desktop` as
"Avalonia 11.2.3"; the code and release notes are correct, and the README was
simply not updated.

The `v0.16.0` release notes state the resulting platform matrix precisely, and
this chapter states it exactly the same way:

- **Windows** — CI-verified on `windows-2022`: build and full test suite, both
  configurations, every push.
- **Linux** — the desktop application launches, verified under `xvfb-run`; a
  `linux-launch-smoke` CI job asserts it reaches a running state. That job is
  **advisory** — not a required check, no long track record.
- **macOS** — expected to work, untested. There is no macOS CI.

A single local `Xvfb` launch is real evidence the crash is fixed; it is not
the same evidentiary weight as a CI-verified Windows leg, and the release
notes say so rather than letting a green result read as more. `TD-116` moved
to Resolved; nobody moved Linux to "supported."

## What was deliberately not built

`ProjectExplorerView`'s drag-and-drop still uses Avalonia's older,
deprecated-but-functional API, narrowly suppressed rather than migrated — the
replacement needs a custom identifier serialiser, a real redesign outside a
one-day security spike. No structured Id payload was added to `CommandResult`;
scroll-to-new-item remains a workaround for whoever next touches command
dispatch.

## What to take away

- **A working feature and a finished product are different claims, and only
  driving the real, running application — not reading its source — tells you
  which one you have.**
- **A dependency crash caused by a security pin is not always fixed by
  reverting it; two versions can both be "fixed" for the same advisory on
  incompatible binary layouts, and the real fix is the one that is both.**
- **Work that ships without a Work Package number still needs one eventually —
  governance that lags reality long enough stops being a record of what was
  built at all.**
