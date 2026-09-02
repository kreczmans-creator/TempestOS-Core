using Tempest.App.Composition;
using Tempest.App.Workspace;
using Tempest.App.Workspace.Calculations;
using Tempest.App.Workspace.Documents;
using Tempest.App.Workspace.Manufacturing;
using Tempest.App.Workspace.Mechanical;
using Tempest.App.Workspace.Requirements;
using Tempest.App.Workspace.Verification;
using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.Macros;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Commands;

/// <summary>
/// TD-77 Stage 5 — macro eligibility and macro execution, against the real
/// registry.
/// </summary>
/// <remarks>
/// A macro is unattended by definition (<c>ADR-0098</c>: an ordered list of
/// Ids, no branching, no looping, no scripting, no parameters). Before
/// Stage 5 the Macro Manager decided what could be a step by asking whether
/// a parameterless factory existed, which no production discipline command
/// has ever had — so no real engineering command could be a macro step at
/// all. It now asks the binding, which already knows: a command declaring
/// values or a confirmation needs a person, and a person is exactly what an
/// unattended run does not have.
/// </remarks>
[Collection("Console output capture")]
public sealed class MacroBindingEligibilityTests : IAsyncLifetime
{
    private static readonly IReadOnlyList<string> Disciplines =
        ["Calculations", "Documents", "Manufacturing", "Mechanical", "Requirements", "Verification"];

    /// <summary>The Stage 3 audited macro-safe set, written out.</summary>
    private static readonly IReadOnlyList<string> MacroSafe =
    [
        "calculations.approve", "calculations.archive", "calculations.lock",
        "calculations.request-review", "calculations.unlock",
        "documents.approve", "documents.release", "documents.request-review",
        "manufacturing.archive", "manufacturing.release",
        "mechanical.validate-configuration",
        "verification.approve", "verification.archive", "verification.request-review",
    ];

    private TempDirectory _temp = null!;
    private ITempestHost _host = null!;
    private WorkspaceManager _manager = null!;
    private ICommandRegistry _registry = null!;

    public async Task InitializeAsync()
    {
        _temp = new TempDirectory();
        _host = new TempestHostBuilder(
        [
            typeof(MechanicalWorkspaceExplorerModule),
            typeof(RequirementsWorkspaceExplorerModule),
            typeof(CalculationsWorkspaceExplorerModule),
            typeof(DocumentsWorkspaceExplorerModule),
            typeof(VerificationWorkspaceExplorerModule),
            typeof(ManufacturingWorkspaceExplorerModule),
        ])
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, _temp.Path),
            ]))
            .Build();
        _manager = new WorkspaceManager(_host);

        var originalOut = Console.Out;
        try
        {
            Console.SetOut(new StringWriter());
            await _manager.StartAsync();
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        EngineeringWorkspaceComposer.RegisterEngineeringDisciplines(_manager, _host);
        _registry = (ICommandRegistry)_host.Services!.GetService(typeof(ICommandRegistry));
    }

    public async Task DisposeAsync()
    {
        await _manager.ShutdownAsync();
        await _host.DisposeAsync();
        _temp.Dispose();
    }

    private IReadOnlyList<CommandDescriptor> Production =>
        _registry.Items.Where(d => Disciplines.Contains(d.Category, StringComparer.Ordinal)).ToList();

    // The Macro Manager's own rule, applied to the real registry. Kept
    // identical to MacroManagerDialog.IsMacroEligible, which is internal to
    // Tempest.Desktop; a Desktop test asserts the dialog uses this rule.
    private static bool IsMacroEligible(CommandDescriptor descriptor) =>
        descriptor.Binding is { } binding
            ? binding is { IsInvocable: true, RequiresPrompt: false }
            : descriptor.CreateDefault is not null;

    [Fact]
    public void RealDisciplineCommands_AreNowOfferableAsMacroSteps()
    {
        var eligible = Production.Where(IsMacroEligible).Select(d => d.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();

        // Previously this list was empty: not one of the seventy-four had a
        // CreateDefault, so the Macro Manager offered none of them.
        Assert.NotEmpty(eligible);
        Assert.Equal(MacroSafe.OrderBy(id => id, StringComparer.Ordinal).ToList(), eligible);
    }

    [Fact]
    public void NoParameterisedCommand_IsOfferedAsAMacroStep()
    {
        var offered = Production.Where(IsMacroEligible).Select(d => d.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var descriptor in Production.Where(d => d.Binding is { IsInvocable: true, Parameters.Count: > 0 }))
            Assert.DoesNotContain(descriptor.Id, offered);

        // Every Create, every rename/edit, every Set-something, both
        // record-result commands and set-bom-line: all excluded, because
        // each declares values a person has to supply.
        Assert.DoesNotContain("requirements.create", offered);
        Assert.DoesNotContain("mechanical.create", offered);
        Assert.DoesNotContain("mechanical.set-bom-line", offered);
        Assert.DoesNotContain("verification.record-result", offered);
    }

    [Fact]
    public void NoDestructiveOrConfirmationGatedCommand_IsOfferedAsAMacroStep()
    {
        var offered = Production.Where(IsMacroEligible).Select(d => d.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var descriptor in Production.Where(d => d.Binding?.ConfirmationMessage is not null))
            Assert.DoesNotContain(descriptor.Id, offered);

        foreach (var id in new[]
                 {
                     "calculations.delete", "documents.delete", "manufacturing.delete", "mechanical.delete",
                     "verification.delete", "requirements.delete", "requirements.delete-group",
                     "requirements.delete-collection",
                     "calculations.duplicate", "documents.duplicate", "manufacturing.duplicate",
                     "mechanical.duplicate", "verification.duplicate", "requirements.duplicate",
                 })
        {
            Assert.DoesNotContain(id, offered);
        }
    }

    [Fact]
    public void NoExplicitlyUnavailableCommand_IsOfferedAsAMacroStep()
    {
        var offered = Production.Where(IsMacroEligible).Select(d => d.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var descriptor in Production.Where(d => d.Binding is { IsInvocable: false }))
            Assert.DoesNotContain(descriptor.Id, offered);
    }

    // ==================================================================
    // Running a macro
    // ==================================================================

    private async Task<(IMacroManager Macros, ICommandDispatcher Dispatcher)> MacroSetupAsync()
    {
        var macros = (IMacroManager)_host.Services!.GetService(typeof(IMacroManager));
        var dispatcher = (ICommandDispatcher)_host.Services!.GetService(typeof(ICommandDispatcher));

        await Task.CompletedTask;
        return (macros, dispatcher);
    }

    [Fact]
    public async Task ATwoStepParameterlessMacro_RunsItsStepsInOrder_AgainstTheCapturedContext()
    {
        var (macros, dispatcher) = await MacroSetupAsync();
        var macro = await macros.CreateAsync("Stage 5 order", ["calculations.request-review", "calculations.approve"]);

        // A context captured at macro start, replayed for both steps.
        var context = CommandContext.For(Guid.NewGuid(), "Calculation");
        var result = await dispatcher.DispatchAsync(new RunMacroCommand(macro.Id, context), CancellationToken.None);

        // Neither step succeeds against an object that does not exist, and
        // that is the point: the run stops at step 1 of 2, naming it — so
        // the order is observable and the first failure is what stopped it.
        Assert.False(result.Succeeded);
        Assert.Contains("step 1/2", result.Message!, StringComparison.Ordinal);
        Assert.Contains("calculations.request-review", result.Message!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AMacroStep_ThatNeedsAPerson_FailsHonestly_AndNeverPrompts()
    {
        var (macros, dispatcher) = await MacroSetupAsync();

        // A legacy/hand-made macro naming a parameterised command. The
        // registry accepts the Id — it is real and registered — so the
        // honest failure has to happen at run time.
        var macro = await macros.CreateAsync("Stage 5 legacy", ["requirements.create"]);

        var result = await dispatcher.DispatchAsync(
            new RunMacroCommand(macro.Id, CommandContext.Empty), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("step 1/1", result.Message!, StringComparison.Ordinal);

        // The reason is the framework's own "nothing could ask you" - no
        // prompt was supplied, and none was invented.
        Assert.Contains("no input surface was supplied", result.Message!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AMacroStep_ThatIsConfirmationGated_NeverRunsUnattended()
    {
        var (macros, dispatcher) = await MacroSetupAsync();
        var macro = await macros.CreateAsync("Stage 5 destructive", ["mechanical.delete"]);

        var result = await dispatcher.DispatchAsync(
            new RunMacroCommand(macro.Id, CommandContext.For(Guid.NewGuid(), "Part")), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("no input surface was supplied", result.Message!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AMacroDescriptor_CarriesABinding_SoASurfaceCanHandItTheSelection()
    {
        var (macros, _) = await MacroSetupAsync();
        var macro = await macros.CreateAsync("Stage 5 binding", ["calculations.approve"]);

        var descriptor = _registry.Items.Single(d => d.Id == IMacroManager.CommandIdPrefix + macro.Id);

        // CreateDefault is kept exactly as it was, so every caller that
        // already invoked a macro by bare Id still does.
        Assert.NotNull(descriptor.CreateDefault);
        Assert.IsType<RunMacroCommand>(descriptor.CreateDefault!());

        // And the binding is what lets a surface capture the selection.
        Assert.NotNull(descriptor.Binding);
        Assert.True(descriptor.Binding!.IsInvocable);
        Assert.False(descriptor.Binding.RequiresPrompt);

        var selected = Guid.NewGuid();
        var built = (RunMacroCommand)descriptor.Binding.Build(
            CommandContext.For(selected, "Calculation"), new Dictionary<string, string>());

        Assert.Equal(macro.Id, built.MacroId);
        Assert.Equal(selected, built.Context!.Primary!.ObjectId);
    }

    [Fact]
    public async Task AMacroRunWithNoCapturedContext_StillReportsPerStep_RatherThanThrowing()
    {
        var (macros, dispatcher) = await MacroSetupAsync();
        var macro = await macros.CreateAsync("Stage 5 no context", ["calculations.approve"]);

        // The parameterless CreateDefault path: no context captured.
        var result = await dispatcher.DispatchAsync(new RunMacroCommand(macro.Id), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("needs a selected object", result.Message!, StringComparison.Ordinal);
    }
}
