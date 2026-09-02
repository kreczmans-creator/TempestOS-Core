using System.Reflection;
using Tempest.Core.Commands;

namespace Tempest.Core.Tests.Commands;

// TD-77 Stage 2 - the Core command context/binding contract.
//
// Proves the one thing the platform could not do before: construct a real,
// parameterised command from an Id plus the application's current context,
// and dispatch it through the same unmodified CommandHandlerTable every
// other path already uses. Everything the old framework did is asserted
// here too, unchanged - the additive claim is only worth as much as the
// evidence that nothing else moved.
public class CommandBindingTests
{
    private const string Kind = "Requirement";
    private const string OtherKind = "Document";

    private static (CommandRegistry Registry, RecordingCommandHandler<RecordedCommandA> Handler) Create(
        Func<RecordedCommandA, CancellationToken, Task<CommandResult>>? handle = null)
    {
        var table = new CommandHandlerTable();
        var handler = new RecordingCommandHandler<RecordedCommandA>(handle);
        new CommandDispatcher(table).RegisterHandler(handler);

        return (new CommandRegistry(table), handler);
    }

    private static CommandParameterPrompt Answering(params (string Name, string Value)[] answers) =>
        (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(
            answers.ToDictionary(a => a.Name, a => a.Value, StringComparer.Ordinal));

    private static CommandParameterPrompt Declining =>
        (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(null);

    private static CommandContext TwoSelected() =>
        new([new CommandContextObject(Guid.NewGuid(), Kind), new CommandContextObject(Guid.NewGuid(), Kind)]);

    // ==================================================================
    // 1-2. What a descriptor without a binding does
    // ==================================================================

    [Fact]
    public async Task NeitherBindingNorCreateDefault_IsUnavailable_NotAnException()
    {
        var (registry, handler) = Create();
        registry.RegisterDescriptor(new CommandDescriptor("sample.bare", "Bare Command"));

        var availability = registry.Evaluate("sample.bare", CommandContext.Empty);
        var invocation = await registry.InvokeAsync("sample.bare", CommandContext.Empty);

        Assert.False(availability.IsAvailable);
        Assert.Equal(CommandOutcome.Unavailable, invocation.Outcome);
        Assert.Contains("Bare Command", invocation.Reason);
        Assert.Contains("no binding", invocation.Reason);
        Assert.Empty(handler.Received);
    }

    [Fact]
    public async Task CreateDefaultWithNoBinding_StillInvokes_ThroughBothOverloads()
    {
        var (registry, handler) = Create();
        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.default", "Default Command", createDefault: () => new RecordedCommandA("from-create-default")));

        // The pre-existing Id-only path, untouched.
        var old = await registry.InvokeAsync("sample.default");

        // And the new context-aware path, which may use CreateDefault
        // precisely because a command needing no caller-supplied data needs
        // nothing from a context either - this is what keeps macros working.
        var recent = await registry.InvokeAsync("sample.default", CommandContext.Empty);

        Assert.True(old.Succeeded);
        Assert.Equal(CommandOutcome.Executed, recent.Outcome);
        Assert.True(recent.Result!.Succeeded);
        Assert.Equal(2, handler.Received.Count);
        Assert.All(handler.Received, c => Assert.Equal("from-create-default", c.Payload));
    }

    // ==================================================================
    // 3. The simplest binding
    // ==================================================================

    [Fact]
    public async Task BindingWithNoRequirementsAndNoParameters_Executes()
    {
        var (registry, handler) = Create();
        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.simple", "Simple Command")
        {
            Binding = new CommandBinding(CommandContextRequirement.None, (_, _) => new RecordedCommandA("built")),
        });

        var invocation = await registry.InvokeAsync("sample.simple", CommandContext.Empty);

        Assert.Equal(CommandOutcome.Executed, invocation.Outcome);
        Assert.Equal("built", Assert.Single(handler.Received).Payload);
    }

    [Fact]
    public async Task ABindingNeedingNothing_NeedsNoPrompt()
    {
        var (registry, _) = Create();
        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.simple", "Simple Command")
        {
            Binding = new CommandBinding(CommandContextRequirement.None, (_, _) => new RecordedCommandA()),
        });

        Assert.Equal(
            CommandOutcome.Executed,
            (await registry.InvokeAsync("sample.simple", CommandContext.Empty, prompt: null)).Outcome);
    }

    // ==================================================================
    // 4-5. The old overload's behaviour, including its exceptions
    // ==================================================================

    [Fact]
    public async Task OldInvokeAsync_StillThrowsCommandException_ForADescriptorWithNoCreateDefault()
    {
        var (registry, _) = Create();

        // Deliberately given a binding: a binding is not a CreateDefault,
        // and must not quietly make the old overload start working - that
        // overload's documented contract is about CreateDefault alone.
        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.bound", "Bound Command")
        {
            Binding = new CommandBinding(CommandContextRequirement.None, (_, _) => new RecordedCommandA()),
        });

        var exception = await Assert.ThrowsAsync<CommandException>(() => registry.InvokeAsync("sample.bound"));

        Assert.Contains("no default-instance factory", exception.Message);
    }

    [Fact]
    public async Task UnregisteredId_ThrowsCommandNotFound_OnBothInvokeOverloads()
    {
        var (registry, _) = Create();

        Assert.Equal("nope", (await Assert.ThrowsAsync<CommandNotFoundException>(
            () => registry.InvokeAsync("nope"))).Id);

        Assert.Equal("nope", (await Assert.ThrowsAsync<CommandNotFoundException>(
            () => registry.InvokeAsync("nope", CommandContext.Empty))).Id);
    }

    [Fact]
    public void UnregisteredId_IsReportedByEvaluate_RatherThanThrown()
    {
        // Evaluate answers "may I offer this?", which is a fair question to
        // ask about anything - only invoking something that does not exist
        // is a programming error.
        var availability = Create().Registry.Evaluate("nope", CommandContext.Empty);

        Assert.False(availability.IsAvailable);
        Assert.Contains("nope", availability.Reason);
    }

    // ==================================================================
    // 6-7. Exceptions propagate, never become outcomes
    // ==================================================================

    [Fact]
    public async Task BindingBuildThrows_PropagatesUncaught()
    {
        var (registry, handler) = Create();
        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.brokenbinding", "Broken Binding")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.None,
                (_, _) => throw new InvalidOperationException("this binding is wrong")),
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => registry.InvokeAsync("sample.brokenbinding", CommandContext.Empty));

        Assert.Equal("this binding is wrong", exception.Message);
        Assert.Empty(handler.Received);
    }

    [Fact]
    public async Task HandlerThrows_PropagatesUncaught_ThroughTheContextAwarePath()
    {
        var (registry, _) = Create((_, _) => throw new InvalidOperationException("handler blew up"));
        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.brokenhandler", "Broken Handler")
        {
            Binding = new CommandBinding(CommandContextRequirement.None, (_, _) => new RecordedCommandA()),
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => registry.InvokeAsync("sample.brokenhandler", CommandContext.Empty));

        Assert.Equal("handler blew up", exception.Message);
    }

    [Fact]
    public async Task NoHandlerForTheConstructedType_ThrowsCommandHandlerNotRegistered()
    {
        var (registry, _) = Create();
        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.unhandled", "Unhandled")
        {
            Binding = new CommandBinding(CommandContextRequirement.None, (_, _) => new RecordedCommandB()),
        });

        var exception = await Assert.ThrowsAsync<CommandHandlerNotRegisteredException>(
            () => registry.InvokeAsync("sample.unhandled", CommandContext.Empty));

        Assert.Equal(typeof(RecordedCommandB), exception.CommandType);
    }

    // ==================================================================
    // 8-9. The context reaches the command
    // ==================================================================

    [Fact]
    public async Task SelectedObjectBinding_TransfersObjectIdAndKind()
    {
        var (registry, handler) = Create();
        var objectId = Guid.NewGuid();

        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.selected", "Selected Command")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (context, _) => new RecordedCommandA($"{context.Primary!.ObjectId}|{context.Primary.Kind}")),
        });

        await registry.InvokeAsync("sample.selected", CommandContext.For(objectId, Kind));

        Assert.Equal($"{objectId}|{Kind}", Assert.Single(handler.Received).Payload);
    }

    [Fact]
    public async Task MultipleSelection_TransfersTheWholeSelection_InOrder()
    {
        var (registry, handler) = Create();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();

        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.bulk", "Bulk Command")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject | CommandContextRequirement.MultipleAllowed,
                (context, _) => new RecordedCommandA(string.Join(",", context.Selection.Select(s => s.ObjectId)))),
        });

        await registry.InvokeAsync(
            "sample.bulk",
            new CommandContext(
            [
                new CommandContextObject(first, Kind),
                new CommandContextObject(second, Kind),
                new CommandContextObject(third, Kind),
            ]));

        // Order is the contract: selection order is what a person built, and
        // a bulk command must not silently reorder their work.
        Assert.Equal($"{first},{second},{third}", Assert.Single(handler.Received).Payload);
    }

    [Fact]
    public void AContextCopiesItsSelection_RatherThanAliasingIt()
    {
        var live = new List<CommandContextObject> { new(Guid.NewGuid(), Kind) };
        var context = new CommandContext(live);

        live.Add(new CommandContextObject(Guid.NewGuid(), Kind));

        // A context describes the moment it was built. A caller mutating
        // its own list afterwards must not change what a binding sees.
        Assert.Single(context.Selection);
    }

    // ==================================================================
    // 10-12. Parameters
    // ==================================================================

    [Fact]
    public async Task DeclaredParameters_ReachTheCommand()
    {
        var (registry, handler) = Create();
        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.rename", "Rename")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (_, values) => new RecordedCommandA(values["newName"]),
                parameters: [new CommandParameter("newName", "New name")]),
        });

        await registry.InvokeAsync(
            "sample.rename", CommandContext.For(Guid.NewGuid(), Kind), Answering(("newName", "Bracket Assembly")));

        Assert.Equal("Bracket Assembly", Assert.Single(handler.Received).Payload);
    }

    [Fact]
    public async Task AllowedValues_AcceptsAMemberCaseInsensitively_AndRejectsANonMember()
    {
        var (registry, handler) = Create();
        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.status", "Set Status")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (_, values) => new RecordedCommandA(values["status"]),
                parameters:
                [
                    new CommandParameter("status", "Status", AllowedValues: ["Draft", "Approved", "Archived"]),
                ]),
        });

        var context = CommandContext.For(Guid.NewGuid(), Kind);

        // Case-insensitive, because the validation this replaces parsed
        // enums with ignoreCase: true.
        Assert.Equal(
            CommandOutcome.Executed,
            (await registry.InvokeAsync("sample.status", context, Answering(("status", "approved")))).Outcome);

        var rejected = await registry.InvokeAsync("sample.status", context, Answering(("status", "Deleted")));

        Assert.Equal(CommandOutcome.Unavailable, rejected.Outcome);
        Assert.Contains("must be one of: Draft, Approved, Archived", rejected.Reason);
        Assert.Single(handler.Received);
    }

    [Fact]
    public async Task ValidateCallback_RejectsWithItsOwnMessage_AndRunsAfterAllowedValues()
    {
        var (registry, handler) = Create();
        var validateCalls = 0;

        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.name", "Create")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.None,
                (_, values) => new RecordedCommandA(values["name"]),
                parameters:
                [
                    new CommandParameter(
                        "name", "Name",
                        AllowedValues: ["short", "toolong"],
                        Validate: value =>
                        {
                            validateCalls++;
                            return value.Length > 5 ? "Name is too long (5 characters max)." : null;
                        }),
                ]),
        });

        var accepted = await registry.InvokeAsync("sample.name", CommandContext.Empty, Answering(("name", "short")));
        var rejected = await registry.InvokeAsync("sample.name", CommandContext.Empty, Answering(("name", "toolong")));

        // A value outside AllowedValues never reaches Validate at all.
        var skipped = await registry.InvokeAsync("sample.name", CommandContext.Empty, Answering(("name", "other")));

        Assert.Equal(CommandOutcome.Executed, accepted.Outcome);
        Assert.Equal(CommandOutcome.Unavailable, rejected.Outcome);
        Assert.Contains("Name is too long (5 characters max).", rejected.Reason);
        Assert.Equal(CommandOutcome.Unavailable, skipped.Outcome);
        Assert.DoesNotContain("too long", skipped.Reason);
        Assert.Equal(2, validateCalls);
        Assert.Single(handler.Received);
    }

    [Fact]
    public async Task AnEmptyStringIsAValue_NotAMissingOne()
    {
        var (registry, handler) = Create();
        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.owner", "Set Owner")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (_, values) => new RecordedCommandA($"[{values["owner"]}]"),
                parameters: [new CommandParameter("owner", "Owner")]),
        });

        // A parameter that will not accept a blank says so through
        // Validate; the framework itself does not decide that for it.
        var invocation = await registry.InvokeAsync(
            "sample.owner", CommandContext.For(Guid.NewGuid(), Kind), Answering(("owner", "")));

        Assert.Equal(CommandOutcome.Executed, invocation.Outcome);
        Assert.Equal("[]", Assert.Single(handler.Received).Payload);
    }

    [Fact]
    public async Task AMissingValue_IsUnavailable_NamingTheParameter()
    {
        var (registry, handler) = Create();
        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.rename", "Rename")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.None,
                (_, values) => new RecordedCommandA(values["newName"]),
                parameters: [new CommandParameter("newName", "New name")]),
        });

        var invocation = await registry.InvokeAsync(
            "sample.rename", CommandContext.Empty, Answering(("somethingElse", "x")));

        Assert.Equal(CommandOutcome.Unavailable, invocation.Outcome);
        Assert.Contains("New name", invocation.Reason);
        Assert.Empty(handler.Received);
    }

    // ==================================================================
    // 13-14. Declining, and having nothing to ask with
    // ==================================================================

    [Fact]
    public async Task PromptDeclined_IsCancelled_AndNothingIsDispatched()
    {
        var (registry, handler) = Create();
        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.rename", "Rename")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.None,
                (_, values) => new RecordedCommandA(values["newName"]),
                parameters: [new CommandParameter("newName", "New name")]),
        });

        var invocation = await registry.InvokeAsync("sample.rename", CommandContext.Empty, Declining);

        // Declining is not failing: no result, no reason, nothing to report.
        Assert.Equal(CommandOutcome.Cancelled, invocation.Outcome);
        Assert.Null(invocation.Result);
        Assert.Null(invocation.Reason);
        Assert.Empty(handler.Received);
    }

    [Fact]
    public async Task ParametersDeclaredButNoPromptSupplied_IsUnavailable_NeverASilentNoOp()
    {
        var (registry, handler) = Create();
        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.rename", "Rename")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.None,
                (_, values) => new RecordedCommandA(values["newName"]),
                parameters: [new CommandParameter("newName", "New name")]),
        });

        var invocation = await registry.InvokeAsync("sample.rename", CommandContext.Empty, prompt: null);

        Assert.Equal(CommandOutcome.Unavailable, invocation.Outcome);
        Assert.Contains("no input surface was supplied", invocation.Reason);
        Assert.Empty(handler.Received);
    }

    // ==================================================================
    // 15-18. Everything Evaluate refuses, with a reason
    // ==================================================================

    [Fact]
    public async Task MissingSelection_IsUnavailable_WithAReason()
    {
        var (registry, handler) = Create();
        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.selected", "Delete Requirement")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject, (_, _) => new RecordedCommandA()),
        });

        var availability = registry.Evaluate("sample.selected", CommandContext.Empty);
        var invocation = await registry.InvokeAsync("sample.selected", CommandContext.Empty);

        Assert.False(availability.IsAvailable);
        Assert.Equal("'Delete Requirement' needs a selected object.", availability.Reason);
        Assert.Equal(CommandOutcome.Unavailable, invocation.Outcome);
        Assert.Empty(handler.Received);
    }

    [Fact]
    public async Task WrongKind_IsUnavailable_NamingBothTheActualAndTheRequiredKinds()
    {
        var (registry, handler) = Create();
        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.kinded", "Revise Requirement")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (_, _) => new RecordedCommandA(),
                appliesToKinds: [Kind, "RequirementGroup"]),
        });

        var invocation = await registry.InvokeAsync("sample.kinded", CommandContext.For(Guid.NewGuid(), OtherKind));

        Assert.Equal(CommandOutcome.Unavailable, invocation.Outcome);
        Assert.Contains(OtherKind, invocation.Reason);
        Assert.Contains("Requirement, RequirementGroup", invocation.Reason);
        Assert.Empty(handler.Received);
    }

    [Fact]
    public void KindMatchingIsCaseSensitive_BecauseAKindIsCanonicalVocabulary()
    {
        // Unlike a parameter's AllowedValues (which replaces an
        // ignoreCase enum parse), a Kind is a canonical vocabulary value
        // the platform assigns - not something a person types - so
        // "requirement" is a different Kind, not a spelling of this one.
        var (registry, _) = Create();
        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.kinded", "Revise Requirement")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (_, _) => new RecordedCommandA(),
                appliesToKinds: [Kind]),
        });

        Assert.False(registry.Evaluate("sample.kinded", CommandContext.For(Guid.NewGuid(), "requirement")).IsAvailable);
    }

    [Fact]
    public async Task TheRightKind_IsAvailable()
    {
        var (registry, _) = Create();
        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.kinded", "Revise Requirement")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (_, _) => new RecordedCommandA(),
                appliesToKinds: [Kind]),
        });

        Assert.True(registry.Evaluate("sample.kinded", CommandContext.For(Guid.NewGuid(), Kind)).IsAvailable);
        Assert.Equal(
            CommandOutcome.Executed,
            (await registry.InvokeAsync("sample.kinded", CommandContext.For(Guid.NewGuid(), Kind))).Outcome);
    }

    [Fact]
    public async Task SeveralSelected_WithoutMultipleAllowed_IsUnavailable_NotSilentlyAppliedToTheFirst()
    {
        var (registry, handler) = Create();
        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.single", "Rename Document")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject, (_, _) => new RecordedCommandA()),
        });

        var invocation = await registry.InvokeAsync("sample.single", TwoSelected());

        Assert.Equal(CommandOutcome.Unavailable, invocation.Outcome);
        Assert.Equal("'Rename Document' applies to one object at a time.", invocation.Reason);
        Assert.Empty(handler.Received);
    }

    [Fact]
    public void SeveralSelected_WithMultipleAllowed_IsAvailable()
    {
        var (registry, _) = Create();
        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.bulk", "Bulk Set Status")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject | CommandContextRequirement.MultipleAllowed,
                (_, _) => new RecordedCommandA()),
        });

        Assert.True(registry.Evaluate("sample.bulk", TwoSelected()).IsAvailable);
    }

    [Fact]
    public async Task CanExecuteFalse_IsUnavailable_AndIsTheLastGate()
    {
        var (registry, handler) = Create();
        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.gated", "Gated Command",
            canExecute: () => false)
        {
            Binding = new CommandBinding(CommandContextRequirement.None, (_, _) => new RecordedCommandA()),
        });

        var invocation = await registry.InvokeAsync("sample.gated", CommandContext.Empty);

        Assert.Equal(CommandOutcome.Unavailable, invocation.Outcome);
        Assert.Equal("'Gated Command' is not currently available.", invocation.Reason);
        Assert.Empty(handler.Received);
    }

    [Fact]
    public void AContextProblemIsReportedBeforeCanExecute_BecauseItIsTheMoreActionableOne()
    {
        var (registry, _) = Create();
        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.both", "Both Problems",
            canExecute: () => false)
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject, (_, _) => new RecordedCommandA()),
        });

        Assert.Equal(
            "'Both Problems' needs a selected object.",
            registry.Evaluate("sample.both", CommandContext.Empty).Reason);
    }

    // ==================================================================
    // Declared unavailability - the honest alternative to an absence
    // ==================================================================

    [Fact]
    public async Task AnExplicitlyUnavailableBinding_ReportsItsOwnReasonVerbatim()
    {
        const string Reason = "No destination picker exists in this platform yet (FCR-0073).";

        var (registry, handler) = Create();
        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.move", "Move Document")
        {
            Binding = CommandBinding.Unavailable(Reason),
        });

        var availability = registry.Evaluate("sample.move", CommandContext.For(Guid.NewGuid(), Kind));
        var invocation = await registry.InvokeAsync("sample.move", CommandContext.For(Guid.NewGuid(), Kind));

        Assert.Equal(Reason, availability.Reason);
        Assert.Equal(Reason, invocation.Reason);
        Assert.Empty(handler.Received);
    }

    [Fact]
    public void DeclaredUnavailability_OutranksAContextProblem()
    {
        // A command that cannot be built has no useful opinion about
        // whether the selection is right, so the real reason wins.
        var (registry, _) = Create();
        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.move", "Move")
        {
            Binding = CommandBinding.Unavailable("No destination picker exists yet."),
        });

        Assert.Equal(
            "No destination picker exists yet.",
            registry.Evaluate("sample.move", CommandContext.Empty).Reason);
    }

    [Fact]
    public void AnUnavailableBinding_CannotBeBuilt()
    {
        var binding = CommandBinding.Unavailable("Nothing to build.");

        Assert.False(binding.IsInvocable);
        Assert.Throws<InvalidOperationException>(
            () => binding.Build(CommandContext.Empty, new Dictionary<string, string>()));
    }

    // ==================================================================
    // 19. Evaluate and the context-aware InvokeAsync agree
    // ==================================================================

    [Fact]
    public async Task EvaluateAndInvokeAgree_AcrossARepresentativeMatrix()
    {
        var (registry, handler) = Create();
        var one = CommandContext.For(Guid.NewGuid(), Kind);
        var wrong = CommandContext.For(Guid.NewGuid(), OtherKind);
        var many = TwoSelected();

        registry.RegisterDescriptor(new CommandDescriptor(
            "m.none", "No Requirements")
        {
            Binding = new CommandBinding(CommandContextRequirement.None, (_, _) => new RecordedCommandA()),
        });
        registry.RegisterDescriptor(new CommandDescriptor(
            "m.selected", "Needs Selection")
        {
            Binding = new CommandBinding(CommandContextRequirement.SelectedObject, (_, _) => new RecordedCommandA()),
        });
        registry.RegisterDescriptor(new CommandDescriptor(
            "m.kinded", "Needs A Requirement")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject, (_, _) => new RecordedCommandA(), appliesToKinds: [Kind]),
        });
        registry.RegisterDescriptor(new CommandDescriptor(
            "m.bulk", "Accepts Many")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject | CommandContextRequirement.MultipleAllowed,
                (_, _) => new RecordedCommandA()),
        });
        registry.RegisterDescriptor(new CommandDescriptor(
            "m.gated", "Gated", canExecute: () => false)
        {
            Binding = new CommandBinding(CommandContextRequirement.None, (_, _) => new RecordedCommandA()),
        });
        registry.RegisterDescriptor(new CommandDescriptor(
            "m.declared", "Declared Unavailable")
        {
            Binding = CommandBinding.Unavailable("Not wired yet."),
        });
        registry.RegisterDescriptor(new CommandDescriptor("m.bare", "Bare"));
        registry.RegisterDescriptor(new CommandDescriptor(
            "m.default", "Default Only", createDefault: () => new RecordedCommandA()));

        string[] ids = ["m.none", "m.selected", "m.kinded", "m.bulk", "m.gated", "m.declared", "m.bare", "m.default"];
        var contexts = new[] { CommandContext.Empty, one, wrong, many };

        var agreed = 0;

        foreach (var id in ids)
        {
            foreach (var context in contexts)
            {
                var availability = registry.Evaluate(id, context);
                var invocation = await registry.InvokeAsync(id, context);

                if (availability.IsAvailable)
                {
                    Assert.Equal(CommandOutcome.Executed, invocation.Outcome);
                    Assert.Null(availability.Reason);
                }
                else
                {
                    Assert.Equal(CommandOutcome.Unavailable, invocation.Outcome);

                    // The same reason, not merely the same verdict - a
                    // second, differently-worded implementation would pass
                    // a verdict-only assertion.
                    Assert.Equal(availability.Reason, invocation.Reason);
                }

                agreed++;
            }
        }

        Assert.Equal(ids.Length * contexts.Length, agreed);
        Assert.NotEmpty(handler.Received);
    }

    // ==================================================================
    // 20. Confirmation, declared in Core, rendered nowhere near it
    // ==================================================================

    [Fact]
    public async Task ConfirmationMessage_ReachesThePromptVerbatim_WithNoParametersOfItsOwn()
    {
        var (registry, handler) = Create();
        const string Confirm = "Create a duplicate of the selected Part?";

        string? seenMessage = null;
        var seenParameterCount = -1;

        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.duplicate", "Duplicate")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (_, _) => new RecordedCommandA("duplicated"),
                confirmationMessage: Confirm),
        });

        var invocation = await registry.InvokeAsync(
            "sample.duplicate",
            CommandContext.For(Guid.NewGuid(), Kind),
            (_, parameters, message, _) =>
            {
                seenMessage = message;
                seenParameterCount = parameters.Count;
                return Task.FromResult<IReadOnlyDictionary<string, string>?>(
                    new Dictionary<string, string>(StringComparer.Ordinal));
            });

        Assert.Equal(Confirm, seenMessage);
        Assert.Equal(0, seenParameterCount);
        Assert.Equal(CommandOutcome.Executed, invocation.Outcome);
        Assert.Equal("duplicated", Assert.Single(handler.Received).Payload);
    }

    [Fact]
    public async Task ConfirmationDeclined_IsCancelled_AndNothingIsDispatched()
    {
        var (registry, handler) = Create();
        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.duplicate", "Duplicate")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (_, _) => new RecordedCommandA(),
                confirmationMessage: "Are you sure?"),
        });

        var invocation = await registry.InvokeAsync(
            "sample.duplicate", CommandContext.For(Guid.NewGuid(), Kind), Declining);

        Assert.Equal(CommandOutcome.Cancelled, invocation.Outcome);
        Assert.Empty(handler.Received);
    }

    [Fact]
    public async Task ConfirmationWithoutAPrompt_IsUnavailable_SoADestructiveCommandCannotRunUnattended()
    {
        // The mechanism that keeps a confirmation-gated command out of an
        // unattended macro: a step that needs a person cannot run without
        // one, and says so rather than proceeding.
        var (registry, handler) = Create();
        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.delete", "Delete")
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (_, _) => new RecordedCommandA(),
                confirmationMessage: "Delete the selected object? This cannot be undone."),
        });

        var invocation = await registry.InvokeAsync(
            "sample.delete", CommandContext.For(Guid.NewGuid(), Kind), prompt: null);

        Assert.Equal(CommandOutcome.Unavailable, invocation.Outcome);
        Assert.Empty(handler.Received);
    }

    [Fact]
    public void ABindingKnowsWhetherItNeedsAPerson()
    {
        var plain = new CommandBinding(CommandContextRequirement.None, (_, _) => new RecordedCommandA());
        var confirmed = new CommandBinding(
            CommandContextRequirement.None, (_, _) => new RecordedCommandA(), confirmationMessage: "Sure?");
        var parameterised = new CommandBinding(
            CommandContextRequirement.None, (_, _) => new RecordedCommandA(),
            parameters: [new CommandParameter("x", "X")]);

        Assert.False(plain.RequiresPrompt);
        Assert.True(confirmed.RequiresPrompt);
        Assert.True(parameterised.RequiresPrompt);
    }

    [Fact]
    public void TheNewContractIsCoreOnly_AndCarriesNoUiOrAmbientState()
    {
        // The confirmation is a string and the context is a selection: Core
        // states what is needed, and never how to ask for it. Asserted
        // structurally, because "no UI dependency" is exactly the kind of
        // claim that quietly stops being true.
        Type[] contract =
        [
            typeof(CommandContext), typeof(CommandContextObject), typeof(CommandContextRequirement),
            typeof(CommandParameter), typeof(CommandParameterPrompt), typeof(CommandBinding),
            typeof(CommandAvailability), typeof(CommandInvocation), typeof(CommandOutcome),
        ];

        var core = typeof(CommandDescriptor).Assembly;

        foreach (var type in contract)
        {
            foreach (var referenced in PublicSurfaceOf(type))
            {
                var assembly = referenced.Assembly;

                Assert.True(
                    assembly == core || assembly == typeof(object).Assembly || assembly == typeof(Task).Assembly,
                    $"{type.Name} exposes {referenced.FullName} from {assembly.GetName().Name} - " +
                    "the command contract must stay Core-only.");
            }
        }

        // And the context carries a selection and nothing else - no service
        // provider, no view, no project, no property bag.
        Assert.Equal(
            ["Empty", "Primary", "Selection"],
            typeof(CommandContext).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
    }

    private static IEnumerable<Type> PublicSurfaceOf(Type type)
    {
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            foreach (var part in Unwrap(property.PropertyType))
                yield return part;

        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
        {
            if (method.DeclaringType == typeof(object))
                continue;

            foreach (var part in Unwrap(method.ReturnType))
                yield return part;

            foreach (var parameter in method.GetParameters())
                foreach (var part in Unwrap(parameter.ParameterType))
                    yield return part;
        }

        foreach (var constructor in type.GetConstructors())
            foreach (var parameter in constructor.GetParameters())
                foreach (var part in Unwrap(parameter.ParameterType))
                    yield return part;
    }

    private static IEnumerable<Type> Unwrap(Type type)
    {
        if (type == typeof(void))
            yield break;

        var bare = type.IsByRef || type.IsArray ? type.GetElementType()! : type;

        yield return bare.IsGenericType ? bare.GetGenericTypeDefinition() : bare;

        if (!bare.IsGenericType)
            yield break;

        foreach (var argument in bare.GetGenericArguments())
            foreach (var part in Unwrap(argument))
                yield return part;
    }
}
