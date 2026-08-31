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
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Commands;

/// <summary>
/// TD-77 Stage 4 — the Core invocation contract, exercised against the real
/// Stage 3 binding data.
/// </summary>
/// <remarks>
/// <para>
/// <c>CommandBindingTests</c> already proves every rule of this contract
/// against purpose-built fixture commands, and
/// <c>CommandDescriptorBindingTests</c> proves what the production
/// descriptors <i>declare</i>. Neither answers the question this file
/// exists for: does <see cref="ICommandRegistry"/> actually carry all
/// seventy-four real bindings from an Id and a context through to a
/// registered handler — every one of them, not the handful a
/// hand-picked example covers.
/// </para>
/// <para>
/// So nothing here is hand-picked. Every assertion below enumerates the
/// production registry and holds for all of it, which is the only way the
/// "a throw out of Build is a defect" invariant can be claimed rather than
/// hoped for.
/// </para>
/// </remarks>
[Collection("Console output capture")]
public sealed class CommandInvocationContractTests : IAsyncLifetime
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

    private IEnumerable<CommandDescriptor> Invocable =>
        Production.Where(d => d.Binding is { IsInvocable: true });

    // A context that satisfies whatever the binding declared it needs, and
    // nothing more - the minimum Evaluate will accept.
    private static CommandContext ContextFor(CommandBinding binding)
    {
        if (!binding.Requires.HasFlag(CommandContextRequirement.SelectedObject))
            return CommandContext.Empty;

        return CommandContext.For(Guid.NewGuid(), binding.AppliesToKinds?[0] ?? "Part");
    }

    // The first value that satisfies the parameter's own Check - preferring
    // what the binding itself offers. That a value exists at all is the
    // assertion: a parameter nothing can satisfy is unusable.
    private static string SampleFor(CommandParameter parameter)
    {
        foreach (var candidate in Candidates(parameter))
        {
            if (parameter.Check(candidate) is null)
                return candidate;
        }

        throw new InvalidOperationException($"No value satisfies parameter '{parameter.Name}'.");
    }

    private static IEnumerable<string> Candidates(CommandParameter parameter)
    {
        if (parameter.DefaultValue is { } declaredDefault)
            yield return declaredDefault;

        if (parameter.AllowedValues is { Count: > 0 } allowed)
        {
            foreach (var value in allowed)
                yield return value;
        }

        yield return "1";
        yield return "Stage 4";
    }

    private static IReadOnlyDictionary<string, string> SatisfyingValues(CommandBinding binding) =>
        binding.Parameters.ToDictionary(p => p.Name, SampleFor, StringComparer.Ordinal);

    private static CommandParameterPrompt Supplying(CommandBinding binding)
    {
        var values = SatisfyingValues(binding);
        return (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(values);
    }

    private static readonly CommandParameterPrompt Declining =
        (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(null);

    // ==================================================================
    // Building: every binding, not a chosen few
    // ==================================================================

    [Fact]
    public void EveryDeclaredParameter_HasAtLeastOneValueThatSatisfiesIt()
    {
        var unsatisfiable = new List<string>();

        foreach (var descriptor in Invocable)
        {
            foreach (var parameter in descriptor.Binding!.Parameters)
            {
                if (!Candidates(parameter).Any(candidate => parameter.Check(candidate) is null))
                    unsatisfiable.Add($"{descriptor.Id}.{parameter.Name}");
            }
        }

        // A parameter no value can satisfy is a command nobody can run.
        Assert.Empty(unsatisfiable);
    }

    [Fact]
    public void EveryInvocableBinding_BuildsACommand_WithoutThrowing()
    {
        var failures = new List<string>();
        var built = 0;

        foreach (var descriptor in Invocable)
        {
            var binding = descriptor.Binding!;

            try
            {
                // Handed a context its own declared requirements said was
                // sufficient, and values its own declared parameters
                // accepted. A throw here is a defect in the binding, which
                // is exactly the invariant this asserts across all of them
                // rather than for a hand-picked example.
                Assert.NotNull(binding.Build(ContextFor(binding), SatisfyingValues(binding)));
                built++;
            }
            catch (Exception ex)
            {
                failures.Add($"{descriptor.Id}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        Assert.Empty(failures);
        Assert.Equal(56, built);
    }

    [Fact]
    public void EveryBuiltCommand_IsAnICommand_AndNothingElseIsReturned()
    {
        foreach (var descriptor in Invocable)
        {
            var binding = descriptor.Binding!;
            var command = binding.Build(ContextFor(binding), SatisfyingValues(binding));

            Assert.IsAssignableFrom<ICommand>(command);

            // Two builds from the same inputs are two commands, never one
            // cached instance a second caller could mutate underneath.
            Assert.NotSame(command, binding.Build(ContextFor(binding), SatisfyingValues(binding)));
        }
    }

    // ==================================================================
    // Dispatching: every binding reaches a registered handler
    // ==================================================================

    [Fact]
    public async Task EveryInvocableBinding_ReachesARegisteredHandler_ThroughTheRegistrysOwnPath()
    {
        var failures = new List<string>();
        var executed = 0;

        foreach (var descriptor in Invocable)
        {
            var binding = descriptor.Binding!;

            try
            {
                var invocation = await _registry.InvokeAsync(
                    descriptor.Id, ContextFor(binding), Supplying(binding));

                if (invocation.Outcome != CommandOutcome.Executed)
                    failures.Add($"{descriptor.Id}: {invocation.Outcome} ({invocation.Reason})");
                else
                    executed++;
            }
            catch (CommandHandlerNotRegisteredException ex)
            {
                // The one failure mode this test exists to catch: a binding
                // constructing a command type nothing is registered to handle.
                failures.Add($"{descriptor.Id}: no handler for the constructed type - {ex.Message}");
            }
            catch (Exception ex)
            {
                failures.Add($"{descriptor.Id}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        Assert.Empty(failures);
        Assert.Equal(56, executed);
    }

    [Fact]
    public async Task EveryExecutedCommand_ReturnsItsHandlersOwnResult_NeverANullOne()
    {
        foreach (var descriptor in Invocable)
        {
            var binding = descriptor.Binding!;
            var invocation = await _registry.InvokeAsync(
                descriptor.Id, ContextFor(binding), Supplying(binding));

            // Executed means a handler ran and answered. Whether it
            // succeeded against a target that does not exist is the
            // handler's business, not this contract's.
            Assert.Equal(CommandOutcome.Executed, invocation.Outcome);
            Assert.NotNull(invocation.Result);
            Assert.Null(invocation.Reason);
        }
    }

    // ==================================================================
    // The eighteen that declare they cannot be invoked
    // ==================================================================

    private IEnumerable<CommandDescriptor> Unavailable =>
        Production.Where(d => d.Binding is { IsInvocable: false });

    [Fact]
    public async Task EveryUnavailableBinding_ReportsItsOwnReasonVerbatim_FromBothEvaluateAndInvoke()
    {
        var checkedCount = 0;

        foreach (var descriptor in Unavailable)
        {
            var reason = descriptor.Binding!.UnavailableReason!;
            var context = CommandContext.For(Guid.NewGuid(), "Requirement");

            var availability = _registry.Evaluate(descriptor.Id, context);
            var invocation = await _registry.InvokeAsync(descriptor.Id, context, Supplying(descriptor.Binding));

            Assert.False(availability.IsAvailable);
            Assert.Equal(reason, availability.Reason);
            Assert.Equal(CommandOutcome.Unavailable, invocation.Outcome);
            Assert.Equal(reason, invocation.Reason);
            Assert.Null(invocation.Result);
            checkedCount++;
        }

        Assert.Equal(18, checkedCount);
    }

    [Fact]
    public void DeclaredUnavailability_IsIndependentOfTheContext()
    {
        foreach (var descriptor in Unavailable)
        {
            var reason = descriptor.Binding!.UnavailableReason!;

            // A command that cannot be built has no useful answer to "is
            // the selection right", so the selection never changes it.
            foreach (var context in ContextMatrix(descriptor.Binding))
                Assert.Equal(reason, _registry.Evaluate(descriptor.Id, context).Reason);
        }
    }

    [Fact]
    public void AnUnavailableBinding_HasNothingToBuild()
    {
        foreach (var descriptor in Unavailable)
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => descriptor.Binding!.Build(CommandContext.Empty, new Dictionary<string, string>()));

            Assert.Contains(descriptor.Binding!.UnavailableReason!, exception.Message, StringComparison.Ordinal);
        }
    }

    // ==================================================================
    // Evaluate and the context-aware InvokeAsync agree, for all of it
    // ==================================================================

    private static IEnumerable<CommandContext> ContextMatrix(CommandBinding binding)
    {
        var applicable = binding.AppliesToKinds?[0] ?? "Part";

        yield return CommandContext.Empty;
        yield return CommandContext.For(Guid.NewGuid(), applicable);
        yield return CommandContext.For(Guid.NewGuid(), "AKindNoDisciplineDeclares");
        yield return new CommandContext(
        [
            new CommandContextObject(Guid.NewGuid(), applicable),
            new CommandContextObject(Guid.NewGuid(), applicable),
        ]);
    }

    [Fact]
    public async Task EvaluateAndInvoke_AgreeForEveryProductionCommand_AcrossAContextMatrix()
    {
        var disagreements = new List<string>();
        var compared = 0;

        foreach (var descriptor in Production)
        {
            var binding = descriptor.Binding!;

            foreach (var context in ContextMatrix(binding))
            {
                var availability = _registry.Evaluate(descriptor.Id, context);
                var invocation = await _registry.InvokeAsync(descriptor.Id, context, Supplying(binding));
                compared++;

                if (!availability.IsAvailable)
                {
                    // Blocked means blocked, for the identical reason -
                    // never a different message from the two paths.
                    if (invocation.Outcome != CommandOutcome.Unavailable || invocation.Reason != availability.Reason)
                        disagreements.Add($"{descriptor.Id}: Evaluate blocked ('{availability.Reason}') but invoke gave {invocation.Outcome} ('{invocation.Reason}')");
                }
                else if (invocation.Outcome != CommandOutcome.Executed)
                {
                    disagreements.Add($"{descriptor.Id}: Evaluate available but invoke gave {invocation.Outcome} ('{invocation.Reason}')");
                }
            }
        }

        Assert.Empty(disagreements);
        Assert.Equal(74 * 4, compared);
    }

    // ==================================================================
    // Value-level refusal is an outcome, never an exception
    // ==================================================================

    // A value this parameter rejects, or null when it genuinely accepts
    // anything (free text with no rule of its own).
    private static string? RejectedValueFor(CommandParameter parameter)
    {
        foreach (var candidate in new[] { "", "   ", "not a member of any declared set", new string('x', 201) })
        {
            if (parameter.Check(candidate) is not null)
                return candidate;
        }

        return null;
    }

    [Fact]
    public async Task EveryValidatedParameter_RefusesABadValue_AsAnOutcome_NeverAnException()
    {
        var refused = 0;

        foreach (var descriptor in Invocable)
        {
            var binding = descriptor.Binding!;

            foreach (var parameter in binding.Parameters)
            {
                if (RejectedValueFor(parameter) is not { } rejected)
                    continue;

                var values = SatisfyingValues(binding).ToDictionary(v => v.Key, v => v.Value, StringComparer.Ordinal);
                values[parameter.Name] = rejected;

                var invocation = await _registry.InvokeAsync(
                    descriptor.Id, ContextFor(binding),
                    (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(values));

                Assert.Equal(CommandOutcome.Unavailable, invocation.Outcome);
                Assert.Contains(descriptor.DisplayName, invocation.Reason!, StringComparison.Ordinal);
                Assert.Null(invocation.Result);
                refused++;
            }
        }

        // Pinned exactly, not as a threshold: thirty of the forty-one
        // declared parameters carry a rule of their own (a Kind or enum
        // set, a non-blank requirement, a length limit, a decimal), and
        // every one of them refuses a bad value as an outcome. The other
        // eleven are genuinely free text - the five content fields, two
        // owner fields, and set-bom-line's four optional strings - and
        // have nothing to refuse.
        Assert.Equal(30, refused);
    }

    [Fact]
    public void TheParametersWithNoRuleOfTheirOwn_AreExactlyTheFreeTextOnes()
    {
        var freeText = Invocable
            .SelectMany(d => d.Binding!.Parameters.Select(p => (Id: d.Id, Parameter: p)))
            .Where(entry => RejectedValueFor(entry.Parameter) is null)
            .Select(entry => $"{entry.Id}.{entry.Parameter.Name}")
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            [
                "calculations.edit.newContent",
                "documents.edit.newContent",
                "manufacturing.edit.newContent",
                "mechanical.edit.newContent",
                "mechanical.set-bom-line.findNumber",
                "mechanical.set-bom-line.itemNumber",
                "mechanical.set-bom-line.referenceDesignator",
                "mechanical.set-bom-line.unitOfMeasure",
                "requirements.bulk-set-owner.owner",
                "requirements.set-owner.owner",
                "verification.edit.newContent",
            ],
            freeText);

        Assert.Equal(41, Invocable.Sum(d => d.Binding!.Parameters.Count));
    }

    [Fact]
    public async Task AMissingValue_IsRefusedByName_ForEveryParameterisedBinding()
    {
        foreach (var descriptor in Invocable.Where(d => d.Binding!.Parameters.Count > 0))
        {
            var binding = descriptor.Binding!;
            var omitted = binding.Parameters[0];
            var values = SatisfyingValues(binding)
                .Where(v => v.Key != omitted.Name)
                .ToDictionary(v => v.Key, v => v.Value, StringComparer.Ordinal);

            var invocation = await _registry.InvokeAsync(
                descriptor.Id, ContextFor(binding),
                (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(values));

            Assert.Equal(CommandOutcome.Unavailable, invocation.Outcome);
            Assert.Contains(omitted.Label, invocation.Reason!, StringComparison.Ordinal);
        }
    }

    // ==================================================================
    // Prompt semantics, across the real data
    // ==================================================================

    [Fact]
    public async Task EveryPromptRequiringBinding_RefusesToRunWithoutAPrompt()
    {
        var refused = 0;

        foreach (var descriptor in Invocable.Where(d => d.Binding!.RequiresPrompt))
        {
            var binding = descriptor.Binding!;
            var invocation = await _registry.InvokeAsync(descriptor.Id, ContextFor(binding));

            // Never a silent no-op, and never a silent run.
            Assert.Equal(CommandOutcome.Unavailable, invocation.Outcome);
            Assert.Contains("no input surface was supplied", invocation.Reason!, StringComparison.Ordinal);
            refused++;
        }

        Assert.Equal(42, refused);
    }

    [Fact]
    public async Task EveryPromptRequiringBinding_TreatsADeclinedPromptAsCancelled_NotAFailure()
    {
        foreach (var descriptor in Invocable.Where(d => d.Binding!.RequiresPrompt))
        {
            var invocation = await _registry.InvokeAsync(
                descriptor.Id, ContextFor(descriptor.Binding!), Declining);

            // Declining is not failing: nothing ran, and nothing is reported.
            Assert.Equal(CommandOutcome.Cancelled, invocation.Outcome);
            Assert.Null(invocation.Result);
            Assert.Null(invocation.Reason);
        }
    }

    [Fact]
    public async Task TheFourteenUnattendedCommands_RunWithNoPromptAtAll()
    {
        var ran = 0;

        foreach (var descriptor in Invocable.Where(d => !d.Binding!.RequiresPrompt))
        {
            var invocation = await _registry.InvokeAsync(descriptor.Id, ContextFor(descriptor.Binding!));

            Assert.Equal(CommandOutcome.Executed, invocation.Outcome);
            ran++;
        }

        Assert.Equal(14, ran);
    }

    [Fact]
    public async Task EveryConfirmationMessage_ReachesThePromptVerbatim()
    {
        var confirmed = 0;

        foreach (var descriptor in Invocable.Where(d => d.Binding!.ConfirmationMessage is not null))
        {
            var binding = descriptor.Binding!;
            string? seen = null;

            await _registry.InvokeAsync(
                descriptor.Id, ContextFor(binding),
                (_, _, message, _) =>
                {
                    seen = message;
                    return Task.FromResult<IReadOnlyDictionary<string, string>?>(null);
                });

            Assert.Equal(binding.ConfirmationMessage, seen);
            confirmed++;
        }

        // Eight deletes and six duplicates.
        Assert.Equal(14, confirmed);
    }

    [Fact]
    public async Task EveryBindingReceivesItsOwnParameterList_AtThePrompt()
    {
        foreach (var descriptor in Invocable.Where(d => d.Binding!.Parameters.Count > 0))
        {
            var binding = descriptor.Binding!;
            CommandDescriptor? seenDescriptor = null;
            IReadOnlyList<CommandParameter>? seenParameters = null;

            await _registry.InvokeAsync(
                descriptor.Id, ContextFor(binding),
                (d, parameters, _, _) =>
                {
                    seenDescriptor = d;
                    seenParameters = parameters;
                    return Task.FromResult<IReadOnlyDictionary<string, string>?>(null);
                });

            Assert.Same(descriptor, seenDescriptor);
            Assert.Equal(binding.Parameters, seenParameters);
        }
    }

    // ==================================================================
    // What Stage 4 must not have changed
    // ==================================================================

    [Fact]
    public async Task TheIdOnlyInvokeAsync_StillRefusesEveryProductionCommand_Unchanged()
    {
        foreach (var descriptor in Production)
        {
            // No production descriptor has ever had a CreateDefault, so the
            // pre-binding overload's behaviour is exactly what it was: a
            // CommandException, not a binding-assisted invocation. A binding
            // is reachable only through the context-aware overload.
            var exception = await Assert.ThrowsAsync<CommandException>(
                () => _registry.InvokeAsync(descriptor.Id));

            Assert.Contains("no default-instance factory", exception.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void NoProductionDescriptor_HasACreateDefault_SoNoSurfaceBehaviourMoved()
    {
        Assert.All(Production, d => Assert.Null(d.CreateDefault));
        Assert.Equal(74, Production.Count);
    }

    [Fact]
    public void Items_StillReturnsEveryProductionDescriptor_RegardlessOfWhetherItCanBeInvoked()
    {
        // Filtering by availability is the caller's decision, never the
        // registry's - the eighteen unavailable commands are still listed.
        Assert.Equal(56, Invocable.Count());
        Assert.Equal(18, Unavailable.Count());
        Assert.Equal(74, Production.Count);
    }

    [Fact]
    public async Task TheDispatcherPath_StillReachesTheSameHandler_ABindingAlsoReaches()
    {
        var dispatcher = (ICommandDispatcher)_host.Services!.GetService(typeof(ICommandDispatcher));
        var targetId = Guid.NewGuid();
        var context = CommandContext.For(targetId, "Requirement");

        // The same command type, reached two ways: directly by a caller
        // that already holds the data, and through a binding that builds
        // it. Both land in the one shared CommandHandlerTable.
        var direct = await dispatcher.DispatchAsync(
            new SetRequirementOwnerCommand(targetId, "A. Engineer"), CancellationToken.None);

        var viaBinding = await _registry.InvokeAsync(
            "requirements.set-owner", context,
            (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(
                new Dictionary<string, string>(StringComparer.Ordinal) { ["owner"] = "A. Engineer" }));

        Assert.NotNull(direct);
        Assert.Equal(CommandOutcome.Executed, viaBinding.Outcome);
        Assert.Equal(direct.Succeeded, viaBinding.Result!.Succeeded);
    }

    // ==================================================================
    // Decision points: the approved semantics, pinned as they stand
    // ==================================================================

    /// <summary>
    /// <b>Carried forward, deliberately unchanged.</b> A multi-selection is
    /// Kind-gated on <see cref="CommandContext.Primary"/> alone, so a
    /// mixed-Kind selection whose first entry is a Requirement passes the
    /// gate and the whole selection reaches the command. This pins the
    /// approved Stage 2 semantics rather than correcting them; whether the
    /// gate should apply to every selected object is a Stage 5 decision.
    /// </summary>
    [Fact]
    public void MixedKindMultiSelection_IsGatedOnThePrimaryOnly()
    {
        var requirement = Guid.NewGuid();
        var document = Guid.NewGuid();
        var mixed = new CommandContext(
        [
            new CommandContextObject(requirement, "Requirement"),
            new CommandContextObject(document, "Document"),
        ]);

        Assert.True(_registry.Evaluate("requirements.bulk-set-status", mixed).IsAvailable);

        var command = (BulkSetRequirementStatusCommand)_registry.Items
            .Single(d => d.Id == "requirements.bulk-set-status").Binding!
            .Build(mixed, new Dictionary<string, string>(StringComparer.Ordinal) { ["status"] = "Approved" });

        // Both Ids reach the command, the Document included.
        Assert.Equal([requirement, document], command.RequirementIds);

        // Reversed, the primary is the Document and the same command is
        // refused - which is what "gated on the primary alone" means.
        var reversed = new CommandContext(
        [
            new CommandContextObject(document, "Document"),
            new CommandContextObject(requirement, "Requirement"),
        ]);

        Assert.False(_registry.Evaluate("requirements.bulk-set-status", reversed).IsAvailable);
    }

    /// <summary>
    /// <b>Carried forward, deliberately unchanged.</b> The "one object at a
    /// time" gate is applied to every binding without
    /// <see cref="CommandContextRequirement.MultipleAllowed"/>, including
    /// the eight creation commands that declare
    /// <see cref="CommandContextRequirement.None"/> and read no selection
    /// at all. A creation command is therefore unavailable purely because
    /// the user happens to have two unrelated objects selected. This pins
    /// the approved Stage 2 semantics; whether the gate should apply only
    /// to bindings that actually read a selection is a Stage 5 decision.
    /// </summary>
    [Fact]
    public void ACommandNeedingNoSelection_IsStillGatedByAnUnrelatedMultiSelection()
    {
        var two = new CommandContext(
        [
            new CommandContextObject(Guid.NewGuid(), "Part"),
            new CommandContextObject(Guid.NewGuid(), "Part"),
        ]);

        var affected = Production
            .Where(d => d.Binding is { IsInvocable: true } b
                        && b.Requires == CommandContextRequirement.None
                        && !_registry.Evaluate(d.Id, two).IsAvailable)
            .Select(d => d.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            [
                "calculations.create", "documents.create", "manufacturing.create", "mechanical.create",
                "requirements.create", "requirements.create-collection", "requirements.create-group",
            ],
            affected);

        Assert.Contains("one object at a time", _registry.Evaluate("requirements.create", two).Reason!, StringComparison.Ordinal);

        // With nothing, or one thing, selected they are available as normal.
        Assert.True(_registry.Evaluate("requirements.create", CommandContext.Empty).IsAvailable);
        Assert.True(_registry.Evaluate("requirements.create", CommandContext.For(Guid.NewGuid(), "Part")).IsAvailable);
    }
}
