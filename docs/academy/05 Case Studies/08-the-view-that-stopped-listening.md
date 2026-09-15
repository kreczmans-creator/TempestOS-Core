# The View That Stopped Listening

**Release:** `v0.19.1` release candidate (`release/v0.19.1`, head
`94998b9` — unreleased) · **Work Package(s):** `WP 19.7C` · **Debt:** none
(no backlog row was raised; the defect was found and fixed inside the
release) · **Code:** `Tempest.Desktop.Views.WorkspaceChangesSubscription`, all
thirteen views named below

**In plain terms.** A screen in this program — a whole area like Tasks,
or one tab inside a project — is built once, the first time you visit
it, and after that it is only shown or hidden as you move around:
leaving Home for Projects does not destroy the Home screen, it tucks it
out of sight, and coming back reveals the very same one. Programmers
call showing it "attach" and hiding it "detach". A "subscription" is a
standing request a screen makes when it first appears — "tell me
whenever anything changes, so I can update myself" — renewed each time
it reappears, the way you would raise your hand again walking back into
a meeting you had stepped out of. This is the story of thirteen screens
that raised their hand once, were heard, and never raised it again: the
first visit worked perfectly, which is exactly what made the fault
invisible for a whole release.

## The shape of the defect

`61-the-screen-follows-the-store.md` describes `WP 18.1A`'s change feed:
every saved change publishes one `WorkspaceChange` naming what it
touched, and a screen that wants to stay current subscribes to
`IWorkspaceChanges.Changed` once and reacts to every commit from then on.
`WP 18.2A` (`63-the-evidence-workspace.md`'s release) built the Evidence
workspace on that pattern, and every screen since copied its shape: a
`WorkspaceChanges` property whose setter subscribed the feed, paired
with a line on the control's own `DetachedFromVisualTree` event that
cleared the property, so a screen leaving the window for good would not
hold a subscription forever. `MainWindowComposer`, the code that builds
the shell, assigns that property exactly once, in an object initializer,
immediately after constructing each view — never again. So the first
time an affected screen detached — another rail area, another project
tab — its handler cleared the property and unsubscribed it, and nothing
set it back on the next attach: the subscription died, permanently,
after the very first exit.

## Why "it worked the first time" is the symptom to distrust

Every one of the thirteen screens also re-reads its own data on entry —
the "load when you land here" discipline that chapter `61` describes
for the Explorer and Cockpit. That hid the defect: revisit Tasks after its
subscription had died and it still reads correctly, because arriving is
what triggers the read, not the subscription. Only a change made *while
the screen was already showing, on a second visit* — nobody navigating
away and back to force a fresh read — could expose that nothing was
listening. Right on the way in and silently dead once settled looks, to
anyone testing it, indistinguishable from working — also why no
existing test caught it: the Desktop journeys each visit their own
surface exactly once. `WP 19.7B`, building the four dashboards, was the
first work to leave a dashboard and return to it while writing its own
tests, and noticed it had gone quiet. The defect had shipped since
`WP 18.2A` (`v0.18.0`) — one full release — with leave-and-return never
exercised.

## The fix: tie the subscription to the control's own lifetime

`WorkspaceChangesSubscription` (`2e8bca8`) replaces every view's own
setter-and-detach-handler pair with one small class that subscribes on
every `AttachedToVisualTree` and unsubscribes on every
`DetachedFromVisualTree`, instead of once at construction. `Feed` can
still be assigned once, so every composer call site is untouched — the
subscription now follows however many times the control is shown,
hidden and shown again, not the moment it was built. All thirteen views
take it: the five rail areas (`TasksAreaView`, `ProjectsAreaView`,
`HomeDashboardView`, `EngineeringAreaView`, `BusinessAreaView`),
Evidence, Invoicing, Quotes, Reports and Timesheets
(`EvidenceWorkspaceView`, `InvoicingView`, `QuotesView`, `ReportsView`,
`TimesheetWeekView`), a project's Deliverables and Quote tabs
(`ProjectDeliverablesView`/`ProjectQuoteView`), and the object editor
(`ObjectEditorView`). A second commit (`8cb913c`) added a test-only
`RefreshCount` to each of the thirteen, incremented as the first line
of the view's existing `RefreshAsync()` — the cheapest signal that
worked identically across views sharing no other common observable. A
third commit (`771c849`) did nothing but move a doc-comment left over
the wrong member once `RefreshCount` was inserted above it — a small
tidy kept honestly as its own commit.

## The proof: reacts, stops, reacts again — thirteen times, then a journey

`WorkspaceChangesReattachTests` (`6a5fc5d`) puts each view through one
shared sequence: attach it to a bare window and confirm a raised change
refreshes it; detach it and confirm a raised change neither throws nor
refreshes it; reattach it and confirm a raised change refreshes it again
— the reattach itself being the fix under test. Before trusting the
suite, its author reverted `TasksAreaView.cs` to the pre-fix,
subscribe-once shape and reran it: the test failed, with "Expected a
refresh again after reattaching," proving it genuinely detects the
defect rather than passing regardless — exactly the rule `06 Engineering
Standards/07-test-determinism-and-suite-hygiene.md` names: "prove a test
can fail before trusting it." A suite never shown red is not evidence.

Two end-to-end journeys were then written, and they teach different
lessons about what a passing test is worth. The Tasks journey (`27fd4cd`)
enters Tasks, leaves for Home, returns — the exact reattach the fix
covers — then creates a task through the real New task button, and
asserts the row appears with no second visit to the area. Checked
against both the fixed and the pre-fix code, it passed in both:
`TasksAreaView.CreateAsync` already calls `RefreshAsync()` the moment a
create succeeds, independent of any subscription, so this action was
never actually broken — kept anyway, as an honest regression check of
the create flow, though it does not demonstrate the bug it was written
to chase. The Home journey (`8c8f8eb`) is the one that does: it creates
a task while Home is freshly shown; navigates Home → Projects → Home,
reattaching the very same `HomeDashboardView` instance the module host
reuses; then, with no further navigation or manual refresh, completes
the task while Home is on screen the second time and asserts the row
disappears — located by its own "Open …" automation name, never a text
search, so the still-present "Recently changed" mention of the same
task cannot fake a pass. The commit says outright it "fails on the
pre-`WP 19.7C` Desktop sources at exactly that last assertion … passes
with `WorkspaceChangesSubscription`." A test passing on both the broken
and the fixed code has proven a user action works, not that a defect is
closed; only a test that fails on the old code and passes on the new
one is evidence the fix changed anything real — writing both, and
saying which is which, beats keeping only the reassuring one.

## The limit that remained, and the fact pinned to justify disclosing it

`WorkspaceChangesSubscription`'s own first doc comment claimed
`ObjectEditorView` never shared the defect, reasoning that an editor
"closes rather than hides." `943bd98` found that wrong: the Document
Area hosting every open editor is a `TabControl`, and a `TabControl`
detaches the content of whichever tab it leaves and reattaches it on
reselection — the same cycle every area view goes through on
navigation. `TabControlDetachTests` pins this platform fact headlessly,
with no TempestOS code involved, just a bare `TabControl` counting
attaches and detaches as the selected tab changes.

So an editor on a background tab does share the defect, and the fix
covers it correctly — the subscription returns when its tab is
reselected. What the fix cannot do is make the editor re-read anything:
nothing re-reads an editor's content on reselection, only when the feed
fires while it is the visible tab, or on Save. The Release Notes
disclose this as a Warning: an editor on a background tab "still misses
a change made while it is hidden … so a change committed elsewhere
while it was hidden shows only at the next change or its own Save. The
area views are not affected: every one re-reads on entry." That last
sentence is the fix's actual boundary — the thirteen area screens are
safe because they combine the restored subscription with an existing
on-entry re-read; the editor restores the subscription but has no such
re-read to fall back on, so its narrower gap is disclosed rather than
left for a user to find. Grepping the codebase confirms the release
notes' own count: all thirteen named views, and only those thirteen,
reference `WorkspaceChangesSubscription`, matching the thirteen tests
in `WorkspaceChangesReattachTests` one for one.

## What to take away

- **A screen that gets everything right on arrival can still be broken
  once settled** — an on-entry re-read hides a dead subscription
  perfectly, which is why someone had to leave and come back first.
- **A test that passes on both the broken and the fixed code has proven
  a feature works, not that a defect is closed** — only a journey that
  fails on the pre-fix sources is evidence the fix changed anything.
- **Disclosing a fix's actual boundary is part of the fix** — restoring
  a hidden editor's subscription was right, but saying plainly that
  nothing re-reads its content on reselection stops the narrower gap
  being mistaken for the same defect, rediscovered, later.

## Related

`02 Runtime Architecture/61-the-screen-follows-the-store.md` (the change
feed), `63-the-evidence-workspace.md` (where the affected subscription
shape was first built) and `71-the-shell-as-sketched.md` (`WP 19.7A`'s
shell, whose `WP 19.7B` dashboards found this fault);
`06 Engineering Standards/07-test-determinism-and-suite-hygiene.md`
("prove a test can fail before trusting it," satisfied here);
`docs/releases/v0.19.1/Release Notes.md` (`WP 19.7C` row; Warnings) and
`Execution Plan.md` (the same row) in the same directory.
