using Tempest.Workspace.Composition;
using Tempest.Workspace;
using Tempest.Workspace.Calculations;
using Tempest.Workspace.Documents;
using Tempest.Workspace.Manufacturing;
using Tempest.Workspace.Mechanical;
using Tempest.Workspace.Requirements;
using Tempest.Workspace.Verification;
using Tempest.Core.Audit;
using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Macros;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Commands;

/// <summary>
/// TD-77 Stage 5 — macro eligibility and macro execution, against the real
/// registry, widened by `WP 20.2C` (`ADR-0099`'s own addendum).
/// </summary>
/// <remarks>
/// A macro still never runs unattended past a confirmation (<c>ADR-0098</c>):
/// a step declaring one needs a person's "yes", and no recording can stand
/// in for that. Before Stage 5 the Macro Manager decided what could be a
/// step by asking whether a parameterless factory existed, which no
/// production discipline command has ever had — so no real engineering
/// command could be a macro step at all. Stage 5 asked the binding instead,
/// but still excluded anything declaring a <i>value</i>, on the same
/// unattended-by-definition reasoning — correct until a step could record
/// what a person actually supplied. `WP 20.2C` adds exactly that
/// (<see cref="Macros.MacroStep.RecordedValues"/>), so a parameterised
/// binding is admitted here too; only a declared confirmation, or a
/// binding this platform genuinely cannot invoke at all
/// (<see cref="CommandBinding.Unavailable"/>), still excludes a command.
/// </remarks>
public sealed class MacroBindingEligibilityTests : IAsyncLifetime
{
    private static readonly IReadOnlyList<string> Disciplines =
        ["Calculations", "Documents", "Manufacturing", "Mechanical", "Requirements", "Verification"];

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
                new KeyValuePair<string, string>(SqlitePersistenceStore.RootPathConfigurationKey, _temp.Path),
            ]))
            .Build();
        _manager = new WorkspaceManager(_host);

        await _manager.StartAsync();

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
    // `WP 20.2C`: a parameterised binding is admitted now that recording
    // (Macros.MacroStep.RecordedValues) can answer for it — only a
    // declared confirmation, or a binding this platform cannot invoke at
    // all, still excludes a command.
    private static bool IsMacroEligible(CommandDescriptor descriptor) =>
        descriptor.Binding is { } binding
            ? binding.IsInvocable && binding.ConfirmationMessage is null
            : descriptor.CreateDefault is not null;

    [Fact]
    public void RealDisciplineCommands_AreNowOfferableAsMacroSteps()
    {
        var eligible = Production.Where(IsMacroEligible).Select(d => d.Id).ToList();

        // Previously this list was empty: not one of the seventy-four had a
        // CreateDefault, so the Macro Manager offered none of them.
        Assert.NotEmpty(eligible);
    }

    [Fact]
    public void ParameterisedCommands_AreNowOfferableAsMacroSteps()
    {
        // `WP 20.2C`'s own point: these were excluded by TD-77 Stage 5
        // (RequiresPrompt: false) and are admitted now that a step can
        // record what a person supplies for it. One representative per
        // discipline, spanning both a required-selection and a
        // no-selection-needed binding.
        var offered = Production.Where(IsMacroEligible).Select(d => d.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var id in new[]
                 {
                     "mechanical.create", "mechanical.rename", "mechanical.set-bom-line",
                     "requirements.create", "verification.record-result",
                     "calculations.rename", "documents.rename", "manufacturing.rename",
                 })
        {
            Assert.Contains(id, offered);
        }

        // Every declared-parameter, non-confirmation-gated binding across
        // the six disciplines is offered — the eligibility rule admits
        // every one of them, not merely the ones named above.
        foreach (var descriptor in Production.Where(
                     d => d.Binding is { IsInvocable: true, ConfirmationMessage: null, Parameters.Count: > 0 }))
        {
            Assert.Contains(descriptor.Id, offered);
        }
    }

    [Fact]
    public void NoConfirmationGatedCommand_IsOfferedAsAMacroStep()
    {
        // Unchanged by `WP 20.2C`: a confirmation is the one thing a
        // recording can never answer for, so every delete and every
        // duplicate — the platform's own confirmation-gated commands —
        // stays excluded regardless of how many, or how few, values it
        // also declares.
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

        // The structured-input set, named directly - the three descriptors
        // still declared Unavailable once `WP 20.2A` (FCR-0073) made the
        // object-picker set invocable: a file's bytes and a calculation's
        // own typed inputs are not strings a prompt can collect.
        foreach (var id in new[] { "calculations.execute", "calculations.recalculate", "documents.attach" })
            Assert.DoesNotContain(id, offered);

        // And the object-picker set is offered now (`WP 20.2A`): a
        // destination is collected through the picker when the step is
        // added, exactly as any other declared value.
        foreach (var id in new[] { "mechanical.move", "mechanical.copy", "mechanical.compare-baselines" })
            Assert.Contains(id, offered);
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

    // ==================================================================
    // Recording two real, parameterised commands and replaying them —
    // the acceptance scenario `WP 20.2C`'s own brief names directly.
    // ==================================================================

    [Fact]
    public async Task ARecordedMacroOfTwoRealCommands_ReplaysBothEffects_WithTwoAuditRows()
    {
        var (macros, dispatcher) = await MacroSetupAsync();
        var domainContext = (EngineeringDomainContext)_host.Services!.GetService(typeof(EngineeringDomainContext));
        var queryableStore = (IQueryablePersistenceStore)_host.Services!.GetService(typeof(IQueryablePersistenceStore));
        var factory = new MechanicalObjectFactoryRegistry(domainContext);

        // A fresh project, and one object already inside it — both created
        // directly (never through the macro under test), so this test's own
        // count below measures only what the macro's two steps write.
        var project = await factory.CreateAsync(
            MechanicalObjectFactoryRegistry.Project, identifier: null, displayName: "Fresh Project",
            initialContent: "Fresh project.", parentId: null);
        var existingPart = await factory.CreateAsync(
            MechanicalObjectFactoryRegistry.Part, identifier: null, displayName: "Existing Part",
            initialContent: "Existing part.", parentId: project.Id);
        var existingPartAuditRowsBefore = await queryableStore.ListKeysAsync(AuditRecorder.AuditCollectionName, existingPart.Id.ToString("N"));

        // Record: mechanical.create (a new Part, named) and mechanical.rename
        // (the object selected when the macro runs) as one macro's own two
        // steps, each carrying the values a person would have supplied when
        // adding it (`WP 20.2C`).
        var macro = await macros.CreateAsync(
            "Two real commands",
            [
                new MacroStep(MechanicalCommandIds.Create, new Dictionary<string, string>
                {
                    ["kind"] = MechanicalObjectFactoryRegistry.Part,
                    ["displayName"] = "Recorded Part",
                }),
                new MacroStep(MechanicalCommandIds.Rename, new Dictionary<string, string>
                {
                    ["newDisplayName"] = "Renamed Existing Part",
                }),
            ]);

        // Replay on the fresh project: the pre-existing Part selected,
        // captured once at the start (ADR-0098) — not a container Kind, so
        // the new Part the first step creates gets no parent of its own,
        // keeping the audit-row count below to exactly what the two
        // commands themselves wrote.
        var context = CommandContext.For(existingPart.Id, MechanicalObjectFactoryRegistry.Part);
        var result = await dispatcher.DispatchAsync(new RunMacroCommand(macro.Id, context), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Contains("completed all 2 step(s)", result.Message, StringComparison.Ordinal);

        // Effect one: the new Part exists, with the recorded name — never
        // prompted for, only replayed from what was recorded.
        var allParts = await domainContext.Repository.ListByKindAsync(MechanicalObjectFactoryRegistry.Part, CancellationToken.None);
        var createdPart = Assert.Single(allParts, p => p.DisplayName == "Recorded Part");

        // Effect two: the pre-existing Part carries the recorded new name.
        var renamed = await domainContext.Repository.FindAsync(existingPart.Id, CancellationToken.None);
        Assert.Equal("Renamed Existing Part", ((IHasBusinessIdentifier)renamed!).DisplayName);

        // Two audit rows: the new Part's own creation (its only one — no
        // follow-up Move, since nothing container-shaped was selected for
        // it to be placed under), and the pre-existing Part's own row
        // count grown by exactly one (the rename, alongside the row
        // already there for its own creation above).
        var createdPartAuditRows = await queryableStore.ListKeysAsync(AuditRecorder.AuditCollectionName, createdPart.Id.ToString("N"));
        Assert.Single(createdPartAuditRows);

        var existingPartAuditRowsAfter = await queryableStore.ListKeysAsync(AuditRecorder.AuditCollectionName, existingPart.Id.ToString("N"));
        Assert.Equal(existingPartAuditRowsBefore.Count + 1, existingPartAuditRowsAfter.Count);
    }

    [Fact]
    public async Task ARecordedMacroStep_WithAMissingRecordedValue_AndNoFallback_FailsHonestly()
    {
        // The production shape: no fallback prompt is wired into the
        // handler this host resolves, so a step recording nothing for a
        // declared value fails exactly as it always has — the identical
        // assertion `AMacroStep_ThatNeedsAPerson_FailsHonestly_AndNeverPrompts`
        // already makes for the legacy string-Id overload, repeated here
        // for the MacroStep-with-empty-values shape directly.
        var (macros, dispatcher) = await MacroSetupAsync();
        var macro = await macros.CreateAsync(
            "Missing value, no fallback",
            [new MacroStep(MechanicalCommandIds.Rename, new Dictionary<string, string>())]);

        var result = await dispatcher.DispatchAsync(
            new RunMacroCommand(macro.Id, CommandContext.For(Guid.NewGuid(), "Part")), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("no input surface was supplied", result.Message!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARecordedMacroStep_ForAnUnavailableCommand_IsRefusedAtRecordTime()
    {
        var (macros, _) = await MacroSetupAsync();

        // documents.attach is the structured-input set (`WP 20.2A` made the
        // object-picker set invocable, so mechanical.move no longer serves
        // here): its own binding is declared Unavailable, so recording it
        // as a step is refused here, naming the reason, rather than
        // accepted and left to fail unexplained the first time this macro
        // ran.
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => macros.CreateAsync("Bad step", [new MacroStep(DocumentsCommandIds.Attach)]));

        Assert.Contains(DocumentsCommandIds.Attach, exception.Message, StringComparison.Ordinal);
        Assert.Contains("file picker", exception.Message, StringComparison.Ordinal);
    }
}
