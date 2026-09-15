# Invariants That Fail the Build

**Release:** `v0.14.0` remediation programme, 2026-08-31 – 2026-09-01
(amended `v0.17.0`, `v0.18.0`) · **Work Package(s):** `WP-B1`, `WP-B2`,
`WP-H`; amended by `WP 17.2B`, `WP 18.1A`/`WP 18.1A-R1` · **Debt:**
`TD-107` (closed), `AT-10`, `AT-23`, `AT-24`, `TD-115` · **Decision:**
`ADR-0118` · **Code:**
`tests/Tempest.Core.Tests/Architecture/DependencyDirectionTests.cs`,
`tests/Tempest.Desktop.Tests/KindEligibilityInvariantTests.cs`,
`tests/Tempest.Desktop.Tests/DormantKeyboardBindingTests.cs`,
`tests/Tempest.Core.Tests/Workspace/DuplicateCopyDelegationTests.cs`,
`tests/Tempest.Core.Tests/Workspace/FutureCapabilityCommandTests.cs`

**In plain terms.** Every engineering object in TempestOS — a Part, a
Drawing, a Requirement — has a "Kind": a label for what sort of thing it
is. Not every action applies to every Kind: you can rename a Part, but
there is no such thing as renaming a Requirement — you revise its
wording instead. "Eligibility" is the answer to "which actions make sense
for this Kind". The platform worked that answer out in two separate
places in the code, and those places could in principle disagree — a
button that looks clickable but fails when clicked, or a menu offering
something the ribbon shows disabled. This chapter closes that gap not by
writing a rule down for people to remember, but by writing a check that
stops the software being built at all if the two places disagree — and
does the same, later, for four other rules the project had only stated
in prose.

## A fact stated twice, and nothing watching for drift

`CommandBinding.AppliesToKinds` drives `ICommandRegistry.Evaluate`, and so
the Ribbon and the Command Palette. Separately, `IWorkspaceManager`'s
`Register{Rename,Delete,Revise}Factory` Kind maps drive `CanRename`,
`CanRevise` and `CanDelete`, and so the Project Explorer's context menu,
the Property Inspector's name field, and the Object Editor's save path.
`43-one-way-to-run-a-command.md` covers `TD-77` Stage 5 moving the Ribbon
onto the first mechanism — which is what split the two apart. They agreed
afterwards, but only by coincidence; nothing detected drift.

`WP-B1` (commit `25de7a3`, closing `TD-107`) added one test-only class,
`KindEligibilityInvariantTests` — 250 lines, no production change — to
hold the two encodings consistent where they overlap. The obvious test,
asserting the two sets are equal, is wrong: `ManufacturingWorkspaceRegistration`
registers *Documents'* rename command for its own `WorkInstruction` Kind
and *Verification's* for `Inspection` (disclosed reuse, `WP 9.5A`), so the
manager's map is deliberately **wider** than any one command's
`AppliesToKinds`. A symmetric assertion fails on correct code. Two
directions are asserted instead:

```csharp
foreach (var kind in kinds!)
    if (!Can(manager, verb, kind))
        gaps.Add($"'{commandId}' applies to Kind '{kind}', but " +
                  $"IWorkspaceManager.Can{verb}(\"{kind}\") is false.");
```

— a command claiming a Kind with no matching factory would enable a
button whose dispatch then fails — and its mirror: every Kind the manager
supports must be claimed by at least one command, or the Explorer offers
what the Ribbon shows disabled. A third test pins the Manufacturing
asymmetry itself, so a future reader meets it as a decision rather than
an inconsistency to "fix". Mutation-tested: a binding claiming a
factory-less Kind, and a factory for a Kind no command claims — both
killed.

## Two mechanisms, one invariant — the decision not to unify

`WP-B1` deliberately left one question open: should the two encodings be
merged? `WP-B2` (commit `464bdff`) answered it as `ADR-0118` — documentary
only, no code changed. A work package that closes a debt row by
*deciding* the question, not by editing anything.

The audit found "these look the same" did not survive contact with the
code. There are really **three** questions, and only two are represented
anywhere: *"is a command constructible for this Kind?"* (the factory
lookup), *"is this command available for this selection?"* (`Evaluate`,
of which `AppliesToKinds` is one of five terms), and *"does this Kind
support this operation at all?"* — represented nowhere; naming it would
add a mechanism, not remove one. The overlap is only 19 commands of 74;
the rest are creates, copies and bulk operations with no manager question
to ask. The Manufacturing asymmetry is **load-bearing**: deriving one
side from the other means either putting `documents.rename` on the wrong
Ribbon tab, or stopping the Explorer renaming a `WorkInstruction` at all.
Registration order independently blocks the tempting direction —
Documents' descriptors register before Manufacturing's factories exist.

**What was deliberately not built:** a unified Kind-eligibility source of
truth, in any of three shapes considered and rejected in `ADR-0118`.
`KindEligibilityInvariantTests` was declared the *permanent* control, not
a stopgap for a fix that was never coming. The cost is named, not hidden:
the same Kind list is written twice for nineteen commands. It is bounded
twice over — the invariant fails the build in either direction and names
the Kind, and a runtime disagreement degrades to an honest
`CommandResult.Failure`, never a crash.

## Five invariants nothing was holding

`WP-H` (commit `9c25223`) widened the lens to the platform's architectural
invariants generally, and started with an audit rather than a task list.
Eight decisions were already enforced — `KindEligibilityInvariantTests`
among them — and the audit's most useful output was the list of what
*not* to duplicate. Five were held by nothing, or by something that only
looked like enforcement:

**Dependency direction** was "enforced" only by the compiler, which
checks the graph the `.csproj` files *declare*, not the rule that the
graph must flow one way — adding an upward reference makes more code
compile, not less. `DependencyDirectionTests` asserts the declared graph
and re-checks it against `Assembly.GetReferencedAssemblies()`, so a
reference arriving transitively is caught too:

```csharp
[Fact]
public void Core_DependsOnNothingInThisRepository()
{
    Assert.Empty(ReferencedProjects(Core));
}
```

**Two allow-list premises had never been checked.** `AT-23` allow-listed
the keyboard router's Id-only call as DORMANT, on the premise that
nothing in production binds a gesture to a discipline command; `AT-10`
allow-listed the REST API's `MapCommand`, on the premise that no shipped
assembly maps a route onto one. Both were true, and both were assumptions
nobody had verified. `DormantKeyboardBindingTests` and
`IdOnlyInvocationGuardTests.NoShippedAssembly_MapsAnHttpRouteOntoACommand`
now scan production source for exactly those calls and fail if either
appears.

**The bounded `Copy`-delegation set** (`AT-24`): five `Duplicate*CommandHandler`
classes, one per discipline, call their `Copy*CommandHandler.HandleAsync`
directly rather than going through the shared dispatch table — a
disclosed shortcut, honest only while it stays five.
`DuplicateCopyDelegationTests` asserts set equality between the five
documented files and the files that actually do it, so a sixth appearing,
or one of the five disappearing, both fail.

**The dormant-command set** (`TD-115`): three commands are fully built,
registered and tested, but nothing in the product can construct one,
because each needs a second object the user must pick and the platform
has no object picker yet (`FCR-0073`). `FutureCapabilityCommandTests`
pins the three by name and asserts nothing constructs one — so the day a
picker lands and a real construction path appears, this test fails, which
is the invitation to retire the `TD-115` row, not a bug.

Mutation-tested, five for five: an Avalonia package added below the
shell, a sixth `Copy` delegation, a production construction of
`CompareBaselinesCommand`, a production keyboard binding, and an HTTP
route mapped onto a command — each killed exactly the test meant to catch
it. `PROJECT_STATUS.md`'s release gate still reads, unchanged since:
**"Architecture invariants (`DependencyDirectionTests`): 5/5 green."**
That is what "held" now means here — not a sentence in a document, a
number a build either produces or does not.

## The graph grew a project, and the test grew with it

`WP-H`'s dependency test asserted three projects: `Tempest.App` sat
between `Tempest.Core` and `Tempest.Desktop`. `WP 17.2B` split
`Tempest.App` — one assembly carrying both the shared domain layer and a
console harness — into `Tempest.Workspace` (a library) and
`Tempest.Harness` (a console executable), each its own project
(`57-workspace-and-harness.md` covers the split). Commit `28a4b72`
rewrote `DependencyDirectionTests` for the four-project graph, and
`cc31de3` updated the surrounding documentation — CI workflows,
`README.md`, `PHYSICAL_REVIEW.md`, `ADR-0101` — to match. The rewritten
test now states five facts instead of four: `Core` depends on nothing;
`Workspace` depends on `Core` alone; `Harness` and `Desktop` each depend
on `Workspace` alone, reaching `Core` only transitively; and no Avalonia
reference reaches anything below `Desktop`.

That is the honest cost of a rule expressed as a test rather than as
prose: when the shape it protects changes, the test needs rewriting, not
just its comment. A paragraph saying "dependencies flow downward" would
not have needed touching — and would also not have noticed for one second
if the split had gone wrong. Several other tests carried the old project
name as a load-bearing string rather than as commentary
(`IdOnlyInvocationGuardTests`, `CommandDescriptorBindingTests`,
`DuplicateCopyDelegationTests`, `SampleSeparationTests`), and all needed
the same correction.

## The same idea, applied again to a different rule

`v0.18.0`'s `WP 18.1A` took the identical pattern — a structural rule with
no runtime behaviour to observe, so asserted against the source text
rather than an execution — and applied it to a different invariant: no
blocking call (`GetAwaiter().GetResult()`, `.Result`, `.Wait()`) on a
`Task` anywhere in `Tempest.Desktop`, outside a small, disclosed
allow-list. `WP 18.1A-R1` found the original guard scanned only
`Tempest.Desktop` while `Tempest.Workspace`'s own Cockpit read models
still blocked the UI thread underneath it, and extended the same test to
`src/Tempest.Workspace` (commit `3da87ac` and its companion in the same
work package). The reasoning and the cost it accepts belong to
`61-the-screen-follows-the-store.md` — the same lesson, landing a second
time on a different rule.

## What to take away

- **An architectural rule that exists only in prose is a rule nobody is
  holding.** The compiler enforces the graph the project files declare,
  not the rule the team agreed to. Writing the rule as a test is what
  makes "we decided this" survive a careless change.
- **Audit before you add enforcement.** `WP-H`'s most valuable output was
  the list of eight invariants already covered — five focused tests
  beat thirteen redundant ones.
- **A test that states a real fact about the build has to be maintained
  like one.** When `Tempest.App` split in two, the dependency-direction
  test was rewritten for the new shape on the same day as the code it
  protects — that is what keeping an invariant actually costs.
