using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.App.Workspace;
using Tempest.App.Workspace.Mechanical;
using Tempest.App.Workspace.Requirements;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Requirements;
using Tempest.Desktop.History;
using Tempest.Desktop.Views;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// TD-77 Stage 5 — the three surfaces, consuming the binding contract.
/// </summary>
/// <remarks>
/// The Ribbon, the Command Palette and the Macro Manager all used to
/// decide for themselves what a command was and what it needed: the Ribbon
/// by reading the text after the last dot in an Id, the Palette and the
/// Macro Manager by asking whether a parameterless factory existed. All
/// three now ask the command framework. These tests prove the behaviours
/// that changed, and the shipped ones that must not have.
/// </remarks>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class SurfaceCommandIntegrationTests
{
    // ==================================================================
    // Command Palette
    // ==================================================================

    [AvaloniaFact]
    public async Task Palette_InvokesARealDisciplineCommand_WhichNoLongerJustClosesSilently()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var domainContext = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));

            var target = await SelectFirstAsync(workspace, MechanicalWorkspaceExplorerModule.NavigationItemId, "Part");

            var palette = Palette(registry, workspace);
            CommandResult? result = null;
            palette.CommandInvoked += (_, r) => result = r;

            await InvokeAsync(palette, "Request Review", "calculations.request-review", registry, workspace);

            // Wrong Kind for that command — the palette reports the
            // command's own reason rather than running it.
            Assert.Null(result);

            await InvokeAsync(palette, "Approve Mechanical", "mechanical.rename", registry, workspace,
                prompt: Answering(("newDisplayName", "Renamed By Palette")));

            Assert.NotNull(result);
            Assert.True(result!.Succeeded, result.Message);

            var reread = await domainContext.Repository.FindAsync(target.Id);
            Assert.Equal("Renamed By Palette", ((IHasBusinessIdentifier)reread!).DisplayName);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task Palette_KeepsAnUnavailableCommandListed_DisabledAndCarryingItsOwnReason()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var palette = Palette(registry, host.Workspace!);

            palette.Open();
            Query(palette).Text = "mechanical.move";

            // `TD-119`: `ApplyFilter` itself is synchronous, but `TextBox`
            // raises `TextChanged` on a later dispatcher pass rather than inside
            // the assignment — so the filtered rows are genuinely not ready when
            // this returns. Bounded poll on the real row count; the assertions
            // below are unchanged.
            var filterDeadline = DateTime.UtcNow.AddSeconds(2);
            while (((System.Collections.IEnumerable)Results(palette).ItemsSource!).Cast<ListBoxItem>().Count() != 1
                   && DateTime.UtcNow < filterDeadline)
                await Task.Delay(10);


            var rows = ((System.Collections.IEnumerable)Results(palette).ItemsSource!).Cast<ListBoxItem>().ToList();
            var row = Assert.Single(rows);

            // ADR-0070: listed, findable, visibly disabled, and saying why.
            Assert.False(row.IsEnabled);
            Assert.Contains("object picker", (string)row.Content!, StringComparison.OrdinalIgnoreCase);

            string? reason = null;
            palette.CommandUnavailable += (_, r) => reason = r;
            Results(palette).SelectedIndex = 0;
            Query(palette).RaiseEvent(new Avalonia.Input.KeyEventArgs
            {
                RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent,
                Key = Avalonia.Input.Key.Enter,
            });

            // `TD-119`: bounded poll on the real reported reason; the assertions
            // below are unchanged.
            var reasonDeadline = DateTime.UtcNow.AddSeconds(2);
            while (reason is null && DateTime.UtcNow < reasonDeadline)
                await Task.Delay(10);

            Assert.NotNull(reason);
            Assert.Contains("destination parent", reason!, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task Palette_DecliningThePrompt_ReportsNothingAndChangesNothing()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var requirements = (IRequirementsService)host.Services!.GetService(typeof(IRequirementsService));
            var before = (await requirements.ListAsync()).Count;

            var palette = Palette(registry, host.Workspace!);
            var invoked = 0;
            var unavailable = 0;
            palette.CommandInvoked += (_, _) => invoked++;
            palette.CommandUnavailable += (_, _) => unavailable++;

            await InvokeAsync(palette, "Create Requirement", "requirements.create", registry, host.Workspace!,
                prompt: (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(null));

            // Cancelling is not an error and not a failure: nothing is
            // reported through either event, and nothing was created.
            Assert.Equal(0, invoked);
            Assert.Equal(0, unavailable);
            Assert.Equal(before, (await requirements.ListAsync()).Count);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task Palette_CollectedValues_ReachTheRealCommand_EndToEnd()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var requirements = (IRequirementsService)host.Services!.GetService(typeof(IRequirementsService));

            var palette = Palette(registry, host.Workspace!);
            CommandResult? result = null;
            palette.CommandInvoked += (_, r) => result = r;

            await InvokeAsync(palette, "Create Requirement", "requirements.create", registry, host.Workspace!,
                prompt: Answering(("identifier", "REQ-PALETTE-1"), ("statement", "Collected values reach the real command.")));

            Assert.NotNull(result);
            Assert.True(result!.Succeeded, result.Message);

            var created = await requirements.FindByIdentifierAsync("REQ-PALETTE-1");
            Assert.NotNull(created);
            Assert.Equal("Collected values reach the real command.", created!.Statement);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ==================================================================
    // Ribbon — the two commands the suffix parser made unreachable
    // ==================================================================

    [AvaloniaFact]
    public async Task Ribbon_RequirementsDeleteGroup_ActuallyDeletes_WhereTheSuffixParserLeftItUnreachable()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var requirements = (IRequirementsService)host.Services!.GetService(typeof(IRequirementsService));

            var group = await requirements.CreateGroupAsync("Stage 5 Doomed Group");
            await workspace.Selection.SelectAsync(group.Id, RequirementsService.RequirementGroupDocumentKind);

            var ribbon = new RibbonView(registry, host.Manager!, workspace, _ => { }, _ => { });
            var outcomes = new List<Tempest.Desktop.ActionOutcome>();
            ribbon.ActionCompleted += (_, outcome) => outcomes.Add(outcome);

            Click(ribbon, registry, "requirements.delete-group");

            // `TD-119`: `Click` raises the real Click and returns — the
            // handler is `async void`, so there is no task to await, and
            // `RibbonView.OnCommandButtonClickAsync` raises `ActionCompleted`
            // only after `InvokeAsync` completes. A fixed `Task.Delay` here
            // was a race, not a wait: this exact assertion failed on the
            // push-triggered CI run at `b09a620` with `Collection: []`, while
            // the concurrent `pull_request` run on the identical SHA passed.
            // Same bounded-poll remedy as `TD-46`/`WP 13.12.9`, re-reading the
            // live collection every iteration. The condition is deliberately
            // "an outcome exists", not "a successful outcome exists": a
            // genuinely failed command must fail this assertion at once, on
            // its own message, rather than be waited out to the deadline and
            // reported as a timeout. A cancelled command raises nothing at
            // all, so it still fails here — exactly as it does today.
            var outcomeDeadline = DateTime.UtcNow.AddSeconds(2);
            while (outcomes.Count == 0 && DateTime.UtcNow < outcomeDeadline)
                await Task.Delay(10);

            Assert.Contains(outcomes, o => o.Succeeded);

            // Really deleted, through IWorkspaceManager.DeleteObjectAsync -
            // which is also what clears the now-dead selection (TD-58).
            // A group delete is a soft delete, exactly as it was before.
            // A group delete is a soft delete, exactly as it was before —
            // ListGroupsAsync deliberately still returns deleted groups, so
            // the flag is what says the command actually ran.
            var reread = await requirements.FindGroupAsync(group.Id);
            Assert.NotNull(reread);
            Assert.True(reread!.IsDeleted, "The group should have been marked deleted.");
            Assert.Null(workspace.Selection.Current);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task Ribbon_RequirementsRevise_OpensTheObjectEditor_WhereTheSuffixParserLeftItUnreachable()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));

            await SelectFirstAsync(workspace, RequirementsWorkspaceExplorerModule.NavigationItemId, RequirementsService.RequirementDocumentKind);

            var opened = 0;
            var ribbon = new RibbonView(registry, host.Manager!, workspace, _ => { }, _ => opened++);
            var messages = new List<string>();
            ribbon.ActionCompleted += (message, _) => messages.Add(message);

            Click(ribbon, registry, "requirements.revise");

            // `TD-119`: same fire-and-forget dispatch as
            // `Ribbon_RequirementsDeleteGroup_...` above. Bounded poll on the
            // real reported message; the assertions below are unchanged.
            var reviseDeadline = DateTime.UtcNow.AddSeconds(2);
            while (messages.Count == 0 && DateTime.UtcNow < reviseDeadline)
                await Task.Delay(10);

            // ADR-0097's product decision, kept: the editor is the real
            // text-collection surface. Previously this Id reached the
            // generic fallback, because "revise" was not "rename" or "edit".
            Assert.Equal(1, opened);
            Assert.Contains(messages, m => m.Contains("Opened for editing", StringComparison.Ordinal));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task Ribbon_Enablement_ComesFromEvaluate_NotFromTheIdsTrailingWord()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var requirements = (IRequirementsService)host.Services!.GetService(typeof(IRequirementsService));

            var ribbon = new RibbonView(registry, host.Manager!, workspace, _ => { }, _ => { });
            var revise = FindButton(ribbon, registry, "requirements.revise");
            var deleteGroup = FindButton(ribbon, registry, "requirements.delete-group");
            var move = FindButton(ribbon, registry, "requirements.move");

            // A Requirement: revise applies, delete-group does not.
            await SelectFirstAsync(workspace, RequirementsWorkspaceExplorerModule.NavigationItemId, RequirementsService.RequirementDocumentKind);
            ribbon.RefreshEnablement();

            Assert.True(revise.IsEnabled);
            Assert.False(deleteGroup.IsEnabled);

            // A RequirementGroup: exactly the other way round. A suffix
            // parser cannot tell these apart - it never saw the Kind.
            var group = await requirements.CreateGroupAsync("Stage 5 Enablement Group");
            await workspace.Selection.SelectAsync(group.Id, RequirementsService.RequirementGroupDocumentKind);
            ribbon.RefreshEnablement();

            Assert.False(revise.IsEnabled);
            Assert.True(deleteGroup.IsEnabled);

            // And a command that declares itself unavailable stays disabled
            // whatever is selected, carrying its own reason as its tooltip.
            Assert.False(move.IsEnabled);
            Assert.Contains("object picker", (string)ToolTip.GetTip(move)!, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ==================================================================
    // History — the behaviour change TD-77 Stage 5 makes deliberately
    // ==================================================================

    [AvaloniaFact]
    public async Task AStatusTransition_NowReachesCommandHistory_WhichTheOldClosuresNeverDid()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            await SelectFirstAsync(workspace, Tempest.App.Workspace.Calculations.CalculationsWorkspaceExplorerModule.NavigationItemId, "Calculation");

            var window = new MainWindow(host);
            var ribbon = GetPrivateField<RibbonView>(window, "_ribbon");
            var history = GetPrivateField<CommandHistoryLog>(window, "_commandHistory");
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));

            var before = history.Entries.Count;
            Click(ribbon, registry, "calculations.request-review");

            // `TD-119`: the history entry is written on the dispatch's own
            // continuation. Bounded poll on the real count; the assertions
            // below are unchanged.
            var historyDeadline = DateTime.UtcNow.AddSeconds(2);
            while (history.Entries.Count <= before && DateTime.UtcNow < historyDeadline)
                await Task.Delay(10);

            // The old RibbonObjectActionHandlers closures reported through
            // their own local ReportAsync - StatusBar and Toast only - and
            // never raised ActionCompleted, so none of the 31 wired
            // commands was ever written to history. Consolidating on the
            // one reporting path fixes that, and this is the assertion
            // that records the change.
            Assert.True(history.Entries.Count > before, "A status transition must now be recorded in command history.");
            Assert.Contains(history.Entries, e => e.Description.Contains("Request Review", StringComparison.Ordinal));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ==================================================================
    // Macro Manager
    // ==================================================================

    [AvaloniaFact]
    public async Task MacroManager_OffersRealDisciplineCommands_AndOnlyTheOnesThatNeedNobodyPresent()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));

            var offered = registry.Items.Where(MacroManagerDialog.IsMacroEligible).Select(d => d.Id).ToList();

            // Previously empty of real commands: no discipline descriptor
            // has ever set CreateDefault, which was the whole test.
            Assert.Contains("calculations.approve", offered);
            Assert.Contains("documents.release", offered);
            Assert.Contains("mechanical.validate-configuration", offered);

            // Parameterised, confirmation-gated and explicitly unavailable
            // commands stay out - by what their bindings declare, not by a
            // list maintained in the dialog.
            Assert.DoesNotContain("requirements.create", offered);
            Assert.DoesNotContain("mechanical.delete", offered);
            Assert.DoesNotContain("mechanical.duplicate", offered);
            Assert.DoesNotContain("mechanical.move", offered);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ==================================================================
    // Structural — the workaround is gone, not merely bypassed
    // ==================================================================

    [Fact]
    public void NoDispatchOrEnablementDecision_ReadsACommandIdsTrailingWord()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Tempest.Desktop", "Views", "RibbonView.cs"));

        // Comments are stripped: the surviving helper's own <remarks>
        // legitimately names the parser it replaced, and this rule is about
        // executable code.
        var ribbon = string.Join("\n", source
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => !line.StartsWith("//", StringComparison.Ordinal)));

        // The parser that decided what a command was is gone by name. What
        // remains is PresentationVerbSuffix, read only to pick a tab group
        // and a glyph.
        Assert.DoesNotContain("ClassifyVerbSuffix", ribbon, StringComparison.Ordinal);
        Assert.Contains("PresentationVerbSuffix", ribbon, StringComparison.Ordinal);

        var presentationUses = ribbon.Split("PresentationVerbSuffix").Length - 1;
        Assert.Equal(3, presentationUses); // its declaration, ClassifyGroup, GlyphFor

        // Dispatch and enablement both ask the framework instead.
        Assert.Contains("_commandRegistry.Evaluate(descriptor.Id, context)", ribbon, StringComparison.Ordinal);
        Assert.Contains(".InvokeAsync(descriptor.Id, context, ParameterPrompt)", ribbon, StringComparison.Ordinal);
    }

    [Fact]
    public void TheRibbonWorkaround_IsDeleted_NotMerelyUnused()
    {
        var desktop = Path.Combine(RepositoryRoot, "src", "Tempest.Desktop");

        Assert.False(
            File.Exists(Path.Combine(desktop, "Composition", "RibbonObjectActionHandlers.cs")),
            "RibbonObjectActionHandlers.cs must be deleted, not left unreferenced.");

        var offenders = Directory
            .EnumerateFiles(desktop, "*.cs", SearchOption.AllDirectories)
            .Where(file => File.ReadAllText(file).Contains("ObjectCreationHandlers", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(offenders);
    }

    // ==================================================================
    // Helpers
    // ==================================================================

    private static CommandPaletteOverlay Palette(ICommandRegistry registry, IWorkspace workspace) =>
        new(registry) { ContextSource = () => WorkspaceCommandContext.From(workspace.Selection) };

    private static TextBox Query(CommandPaletteOverlay palette) => (TextBox)((StackPanel)palette.Child!).Children[0];

    private static ListBox Results(CommandPaletteOverlay palette) => (ListBox)((StackPanel)palette.Child!).Children[1];

    private static CommandParameterPrompt Answering(params (string Name, string Value)[] answers) =>
        (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(
            answers.ToDictionary(a => a.Name, a => a.Value, StringComparer.Ordinal));

    /// <summary>Drives the palette exactly as a person does: filter, select, Enter.</summary>
    private static async Task InvokeAsync(
        CommandPaletteOverlay palette, string _, string commandId,
        ICommandRegistry registry, IWorkspace workspace, CommandParameterPrompt? prompt = null)
    {
        palette.ParameterPrompt = prompt;
        palette.ContextSource = () => WorkspaceCommandContext.From(workspace.Selection);
        palette.Open();

        Query(palette).Text = commandId;

        // `TD-119`: `TextChanged` reaches `ApplyFilter` on a later dispatcher
        // pass, so the palette's own rows must be observed rather than assumed.
        // Bounded poll until they match the same filter this helper applies.
        var expected = registry.Items
            .Where(d => d.DisplayName.Contains(commandId, StringComparison.OrdinalIgnoreCase) || d.Id.Contains(commandId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var paletteDeadline = DateTime.UtcNow.AddSeconds(2);
        while (((System.Collections.IEnumerable)Results(palette).ItemsSource!).Cast<ListBoxItem>().Count() != expected.Count
               && DateTime.UtcNow < paletteDeadline)
            await Task.Delay(10);

        var index = expected.FindIndex(d => d.Id == commandId);
        Assert.True(index >= 0, $"'{commandId}' was not among the filtered results.");
        Results(palette).SelectedIndex = index;

        // `TD-119`: Enter runs `CommandPaletteOverlay.InvokeSelectedAsync`, which
        // awaits the real `ICommandRegistry.InvokeAsync`. The palette raises
        // exactly one of `CommandInvoked`/`CommandUnavailable` when that dispatch
        // finishes, which is a real completion signal every caller can share —
        // so this helper joins on it rather than guessing a duration. A declined
        // prompt is `Cancelled` and deliberately raises neither; that caller
        // reaches the deadline and then asserts, correctly, that nothing
        // happened.
        var settled = false;
        void OnInvoked(CommandDescriptor _, CommandResult __) => settled = true;
        void OnUnavailable(CommandDescriptor _, string __) => settled = true;

        palette.CommandInvoked += OnInvoked;
        palette.CommandUnavailable += OnUnavailable;
        try
        {
            Query(palette).RaiseEvent(new Avalonia.Input.KeyEventArgs
            {
                RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent,
                Key = Avalonia.Input.Key.Enter,
            });

            var invokeDeadline = DateTime.UtcNow.AddSeconds(2);
            while (!settled && DateTime.UtcNow < invokeDeadline)
                await Task.Delay(10);
        }
        finally
        {
            palette.CommandInvoked -= OnInvoked;
            palette.CommandUnavailable -= OnUnavailable;
        }
    }
}
