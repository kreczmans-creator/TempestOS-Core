using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Projects;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Tests.Quotations;
using Tempest.Workspace.Mechanical;

namespace Tempest.Core.Tests.Commands;

/// <summary>
/// `WP 19.10R`, `TD-179`'s residual: the one rule
/// <see cref="CommandRegistry.Evaluate(string, CommandContext)"/> and
/// <see cref="ICommandRegistry.InvokeAsync(string, CommandContext, CommandParameterPrompt?, CancellationToken)"/>
/// both consult before letting a mutating engineering command run against
/// an archived project — the gap the Structure tab's Ribbon and the
/// Command Palette (and a macro replaying either) shared, because neither
/// surface has ever had its own notion of "archived"; both simply ask the
/// registry. Exercised through the real production registration
/// (<see cref="Tempest.Workspace.Mechanical.MechanicalWorkspaceRegistration"/>,
/// via <c>QuotationTestHost</c>'s own real composition root) rather than a
/// hand-rolled descriptor, so a real Id, a real binding and the real
/// Mechanical object model are what is actually proven.
/// </summary>
public sealed class ArchivedProjectCommandGuardTests
{
    [Fact]
    public async Task AMutatingCommand_OnAPartUnderAnArchivedProject_IsUnavailable_AndInvokeRefusesWithoutWriting()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        try
        {
            QuotationTestHost.SignIn(host);

            var projectId = await QuotationTestHost.CreateProjectAsync(host, "ARCH-CMD-1", "Archived Command Project");
            var domain = QuotationTestHost.Domain(host);
            var part = await new MechanicalObjectFactoryRegistry(domain).CreateAsync(
                MechanicalObjectFactoryRegistry.Part, "PT-ARCH-1", "Bracket", "Initial content.", parentId: projectId);

            // Closed 91 days ago (backdated clock, sign-off only) — Archive,
            // not merely Closed (`ProjectArchival.ArchiveAfterDays` is 90).
            // Every check below reads the real clock, exactly as
            // `WP 19.10H`'s own Desktop journey test
            // (`ArchivedProjectReadOnlyTests`) already establishes this
            // pattern.
            var backdated = new FakeTimeProvider(DateTimeOffset.UtcNow.AddDays(-91));
            var closed = await new ProjectLifecycleService(domain, backdated).SignOffAsync(projectId, "Closed for the guard test.");
            Assert.True(closed.Succeeded, closed.Reason);

            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var context = CommandContext.For(part.Id, "Part");

            var availability = registry.Evaluate("mechanical.rename", context);
            Assert.False(availability.IsAvailable);
            Assert.Equal("Project 'ARCH-CMD-1' is archived — read only.", availability.Reason);

            var invocation = await registry.InvokeAsync(
                "mechanical.rename", context,
                (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(
                    new Dictionary<string, string> { ["newDisplayName"] = "Should never land" }));

            Assert.Equal(CommandOutcome.Unavailable, invocation.Outcome);
            Assert.Equal(availability.Reason, invocation.Reason);
            Assert.Null(invocation.Result);

            // Nothing wrote: the object's own display name is unchanged.
            var reread = (IHasBusinessIdentifier)(await domain.Repository.FindAsync(part.Id))!;
            Assert.Equal("Bracket", reread.DisplayName);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task TheSameCommand_OnAPartUnderAnOpenProject_StaysAvailable()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        try
        {
            QuotationTestHost.SignIn(host);

            var projectId = await QuotationTestHost.CreateProjectAsync(host, "OPEN-CMD-1", "Open Command Project");
            var domain = QuotationTestHost.Domain(host);
            var part = await new MechanicalObjectFactoryRegistry(domain).CreateAsync(
                MechanicalObjectFactoryRegistry.Part, "PT-OPEN-1", "Bracket", "Initial content.", parentId: projectId);

            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var context = CommandContext.For(part.Id, "Part");

            Assert.True(registry.Evaluate("mechanical.rename", context).IsAvailable);

            var invocation = await registry.InvokeAsync(
                "mechanical.rename", context,
                (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(
                    new Dictionary<string, string> { ["newDisplayName"] = "Renamed Bracket" }));

            Assert.Equal(CommandOutcome.Executed, invocation.Outcome);
            Assert.True(invocation.Result!.Succeeded, invocation.Result.Message);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task ACreateCommand_WithTheCurrentProjectScopeArchived_Refuses()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        try
        {
            QuotationTestHost.SignIn(host);

            var projectId = await QuotationTestHost.CreateProjectAsync(host, "ARCH-CREATE-1", "Archived Create Project");
            var domain = QuotationTestHost.Domain(host);

            var backdated = new FakeTimeProvider(DateTimeOffset.UtcNow.AddDays(-91));
            var closed = await new ProjectLifecycleService(domain, backdated).SignOffAsync(projectId, "Closed for the create-scope guard test.");
            Assert.True(closed.Succeeded, closed.Reason);

            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));

            // Nothing selected: the target is the shell's own open project
            // scope alone (`CommandContext.ProjectId`) — the identical
            // shape `MechanicalCreateParentPolicy` already resolves a
            // create's own parent from.
            var context = new CommandContext([], projectId);

            var availability = registry.Evaluate("mechanical.create", context);
            Assert.False(availability.IsAvailable);
            Assert.Equal("Project 'ARCH-CREATE-1' is archived — read only.", availability.Reason);

            var invocation = await registry.InvokeAsync(
                "mechanical.create", context,
                (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(
                    new Dictionary<string, string> { ["kind"] = "Part", ["displayName"] = "Should never be created" }));

            Assert.Equal(CommandOutcome.Unavailable, invocation.Outcome);
            Assert.Equal(availability.Reason, invocation.Reason);

            // Nothing was created under the archived project.
            var contents = await domain.Repository.ListChildrenAsync(projectId);
            Assert.Empty(contents);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task AReadingCommand_OnTheArchivedProjectsObject_StaysAvailable()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        try
        {
            QuotationTestHost.SignIn(host);

            var projectId = await QuotationTestHost.CreateProjectAsync(host, "ARCH-READ-1", "Archived Read Project");
            var domain = QuotationTestHost.Domain(host);
            var baseline = await new MechanicalObjectFactoryRegistry(domain).CreateAsync(
                MechanicalObjectFactoryRegistry.Baseline, "BL-ARCH-1", "Baseline One", "Initial content.", parentId: projectId);

            var backdated = new FakeTimeProvider(DateTimeOffset.UtcNow.AddDays(-91));
            var closed = await new ProjectLifecycleService(domain, backdated).SignOffAsync(projectId, "Closed for the reading-command test.");
            Assert.True(closed.Succeeded, closed.Reason);

            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var context = CommandContext.For(baseline.Id, "Baseline");

            // mechanical.validate-configuration is the one production
            // engineering binding this Work Package leaves Mutates: false
            // — a consistency check, never a write — so it stays available
            // and invocable even against an archived project's own object.
            Assert.True(registry.Evaluate("mechanical.validate-configuration", context).IsAvailable);

            var invocation = await registry.InvokeAsync("mechanical.validate-configuration", context);
            Assert.Equal(CommandOutcome.Executed, invocation.Outcome);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public void ANonMutatingBinding_IsNeverCheckedAgainstAnArchivedProject_RegardlessOfSelection()
    {
        // Isolated at the guard's own level, independent of any specific
        // discipline's Kind scoping: proves the mechanism
        // (`CommandBinding.Mutates`) rather than one production binding's
        // own accidental shape.
        var domain = Tempest.Core.Tests.EngineeringDomain.TestEngineeringDomain.NewContext();
        var table = new CommandHandlerTable();
        table.Register<NoOpCommand>(new NoOpCommandHandler());

        var guard = new ArchivedProjectCommandGuard(domain);
        var registry = new CommandRegistry(table, archivedProjectGuard: guard);

        registry.RegisterDescriptor(new CommandDescriptor("fixture.read", "Fixture Read", createDefault: () => new NoOpCommand())
        {
            Binding = new CommandBinding(
                CommandContextRequirement.SelectedObject,
                (_, _) => new NoOpCommand(),
                mutates: false),
        });

        // A selected object that does not even exist: the guard cannot
        // resolve any project for it, so even a mutating binding would
        // pass here — the point is that a non-mutating binding never asks
        // the guard the question at all.
        var context = CommandContext.For(Guid.NewGuid(), "Anything");
        Assert.True(registry.Evaluate("fixture.read", context).IsAvailable);
    }

    private sealed class NoOpCommand : ICommand
    {
    }

    private sealed class NoOpCommandHandler : ICommandHandler<NoOpCommand>
    {
        public Task<CommandResult> HandleAsync(NoOpCommand command, CancellationToken cancellationToken) =>
            Task.FromResult(CommandResult.Success());
    }
}
