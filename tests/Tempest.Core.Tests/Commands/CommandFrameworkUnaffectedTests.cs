using Tempest.Core.Commands;

namespace Tempest.Core.Tests.Commands;

// TD-77 Stage 2 - the other half of an additive claim.
//
// Adding a binding to CommandDescriptor is only honest if everything that
// worked before still works, identically. These assert the parts of the
// command framework TD-77 promised not to touch: the descriptor's original
// constructor shape, CreateDefault, the Id-only InvokeAsync (behaviour and
// exceptions), the dispatcher, and the shared handler table.
public class CommandFrameworkUnaffectedTests
{
    private static (CommandRegistry Registry, CommandDispatcher Dispatcher, CommandHandlerTable Table) Create()
    {
        var table = new CommandHandlerTable();
        return (new CommandRegistry(table), new CommandDispatcher(table), table);
    }

    [Fact]
    public void TheOriginalSevenParameterConstructor_StillCompilesAndBehavesIdentically()
    {
        Func<bool> canExecute = () => true;
        Func<ICommand> createDefault = () => new RecordedCommandA();

        var descriptor = new CommandDescriptor(
            "sample.legacy", "Legacy", "Category", "Description", "icon", canExecute, createDefault);

        Assert.Equal("sample.legacy", descriptor.Id);
        Assert.Equal("Legacy", descriptor.DisplayName);
        Assert.Equal("Category", descriptor.Category);
        Assert.Equal("Description", descriptor.Description);
        Assert.Equal("icon", descriptor.Icon);
        Assert.Same(canExecute, descriptor.CanExecute);
        Assert.Same(createDefault, descriptor.CreateDefault);

        // The one new property, and its only default.
        Assert.Null(descriptor.Binding);
    }

    [Fact]
    public void TheConstructorSignatureIsUnchanged_SoACompiledCallerStillBinds()
    {
        // Binary compatibility, not merely source compatibility. Appending
        // an optional parameter would have compiled every call site in this
        // repository unchanged while breaking any already-compiled assembly
        // bound to this exact signature - and this platform loads plugin
        // assemblies it did not compile (ADR-0111), one of whose sanctioned
        // acts is registering a command descriptor. This is why Binding is
        // an init accessor rather than a constructor parameter.
        var constructors = typeof(CommandDescriptor).GetConstructors();

        var original = Assert.Single(constructors);

        Assert.Equal(
            [
                typeof(string), typeof(string), typeof(string), typeof(string),
                typeof(string), typeof(Func<bool>), typeof(Func<ICommand>),
            ],
            original.GetParameters().Select(p => p.ParameterType));

        // And it still behaves identically when invoked the way a compiled
        // caller invokes it.
        var descriptor = (CommandDescriptor)original.Invoke(["sample.reflected", "Reflected", null, null, null, null, null]);

        Assert.Equal("sample.reflected", descriptor.Id);
        Assert.Null(descriptor.Binding);
    }

    [Fact]
    public void ADescriptorWithNoBinding_HasANullBinding_NotAnImpliedOne()
    {
        Assert.Null(new CommandDescriptor("sample.plain", "Plain").Binding);
        Assert.Null(new CommandDescriptor("sample.byname", "By Name", category: "C").Binding);
    }

    [Fact]
    public void DescriptorValidation_IsUnchanged()
    {
        Assert.Throws<ArgumentException>(() => new CommandDescriptor(" ", "Display"));
        Assert.Throws<ArgumentException>(() => new CommandDescriptor("sample.id", " "));
    }

    [Fact]
    public async Task TheIdOnlyInvokeAsync_StillDispatchesACreateDefaultCommand()
    {
        var (registry, dispatcher, _) = Create();
        var handler = new RecordingCommandHandler<RecordedCommandA>();
        dispatcher.RegisterHandler(handler);

        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.legacy", "Legacy", createDefault: () => new RecordedCommandA("legacy")));

        var result = await registry.InvokeAsync("sample.legacy");

        Assert.True(result.Succeeded);
        Assert.Equal("legacy", Assert.Single(handler.Received).Payload);
    }

    [Fact]
    public async Task TheIdOnlyInvokeAsync_KeepsEveryOneOfItsExceptions()
    {
        var (registry, dispatcher, _) = Create();
        dispatcher.RegisterHandler(new RecordingCommandHandler<RecordedCommandA>(
            (_, _) => throw new InvalidOperationException("boom")));

        registry.RegisterDescriptor(new CommandDescriptor("sample.nofactory", "No Factory"));
        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.nohandler", "No Handler", createDefault: () => new RecordedCommandB()));
        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.throws", "Throws", createDefault: () => new RecordedCommandA()));

        await Assert.ThrowsAsync<CommandNotFoundException>(() => registry.InvokeAsync("sample.missing"));
        await Assert.ThrowsAsync<CommandException>(() => registry.InvokeAsync("sample.nofactory"));
        await Assert.ThrowsAsync<CommandHandlerNotRegisteredException>(() => registry.InvokeAsync("sample.nohandler"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => registry.InvokeAsync("sample.throws"));
        await Assert.ThrowsAsync<ArgumentNullException>(() => registry.InvokeAsync(null!));
    }

    [Fact]
    public async Task TheIdOnlyInvokeAsync_StillDoesNotConsultCanExecute()
    {
        // Documented behaviour: a caller that already decided to invoke a
        // command has already made that judgement. Only the context-aware
        // overload - whose caller supplied nothing but an Id - checks.
        var (registry, dispatcher, _) = Create();
        var handler = new RecordingCommandHandler<RecordedCommandA>();
        dispatcher.RegisterHandler(handler);

        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.gated", "Gated", canExecute: () => false, createDefault: () => new RecordedCommandA()));

        Assert.True((await registry.InvokeAsync("sample.gated")).Succeeded);
        Assert.Single(handler.Received);

        Assert.Equal(
            CommandOutcome.Unavailable,
            (await registry.InvokeAsync("sample.gated", CommandContext.Empty)).Outcome);
        Assert.Single(handler.Received);
    }

    [Fact]
    public void RegistrationInvariants_AreUnchanged()
    {
        var (registry, dispatcher, _) = Create();

        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.one", "One")
        {
            Binding = new CommandBinding(CommandContextRequirement.None, (_, _) => new RecordedCommandA()),
        });

        Assert.Throws<DuplicateCommandIdException>(
            () => registry.RegisterDescriptor(new CommandDescriptor("sample.one", "One Again")));

        dispatcher.RegisterHandler(new RecordingCommandHandler<RecordedCommandA>());

        Assert.Throws<DuplicateCommandHandlerException>(
            () => dispatcher.RegisterHandler(new RecordingCommandHandler<RecordedCommandA>()));
    }

    [Fact]
    public async Task DispatchAsync_IsUntouchedByBindings()
    {
        var (_, dispatcher, _) = Create();
        var handler = new RecordingCommandHandler<RecordedCommandA>();
        dispatcher.RegisterHandler(handler);

        var result = await dispatcher.DispatchAsync(new RecordedCommandA("typed"), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("typed", Assert.Single(handler.Received).Payload);
    }

    [Fact]
    public async Task BothRegistryPaths_DispatchThroughTheSameSharedHandlerTable()
    {
        // The registry and the dispatcher operate on one handler set - the
        // property CommandHandlerTable exists to guarantee, and the one a
        // second dispatch mechanism would have broken.
        var (registry, dispatcher, _) = Create();
        var handler = new RecordingCommandHandler<RecordedCommandA>();
        dispatcher.RegisterHandler(handler);

        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.default", "Default", createDefault: () => new RecordedCommandA("a")));
        registry.RegisterDescriptor(new CommandDescriptor(
            "sample.bound", "Bound")
        {
            Binding = new CommandBinding(CommandContextRequirement.None, (_, _) => new RecordedCommandA("b")),
        });

        await registry.InvokeAsync("sample.default");
        await registry.InvokeAsync("sample.bound", CommandContext.Empty);
        await dispatcher.DispatchAsync(new RecordedCommandA("c"), CancellationToken.None);

        Assert.Equal(["a", "b", "c"], handler.Received.Select(r => r.Payload));
    }

    [Fact]
    public void Items_StillReturnsEveryDescriptor_RegardlessOfAvailability()
    {
        // ADR-0070: an unavailable command is shown disabled, never hidden,
        // so the catalogue must not start filtering itself.
        var (registry, _, _) = Create();

        registry.RegisterDescriptor(new CommandDescriptor("b.two", "Two", "B", canExecute: () => false));
        registry.RegisterDescriptor(new CommandDescriptor("a.one", "One", "A")
        {
            Binding = CommandBinding.Unavailable("Not wired."),
        });

        Assert.Equal(["a.one", "b.two"], registry.Items.Select(d => d.Id));
    }
}
