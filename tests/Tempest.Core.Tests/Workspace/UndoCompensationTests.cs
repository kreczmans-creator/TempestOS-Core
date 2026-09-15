using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Core.Macros;
using Tempest.Core.Projects;
using Tempest.Core.Runtime;
using Tempest.Core.Settings;
using Tempest.Core.Tests.Macros;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Tests.Quotations;
using Tempest.Workspace;
using Tempest.Workspace.Documents;

namespace Tempest.Core.Tests.Workspace;

/// <summary>
/// `WP 21.1A` — a compensation per command family, run through the real
/// <see cref="ICommandDispatcher"/>, including each refusal named in the
/// brief: a compensation refused because its own destination/parent has
/// since been deleted, a status transition the lifecycle table will not
/// let reverse, and a compensation that would cross the archived-project
/// guard. Exercised against the real, fully-wired composition root
/// (<c>QuotationTestHost</c>, the identical harness
/// <c>Commands.ArchivedProjectCommandGuardTests</c> already uses) for the
/// Documents discipline — a real Id, a real handler, a real
/// <see cref="EngineeringDomainContext"/> — rather than a hand-rolled
/// fixture, so what is proven is what actually ships. The macro compound
/// action is isolated instead (<c>Macros.MacroStepRecordingTests</c>'s own
/// lightweight harness), since assembling it needs no real discipline at
/// all.
/// </summary>
public sealed class UndoCompensationTests
{
    private static async Task<(ITempestHost Host, WorkspaceManager Manager, EngineeringDomainContext Domain, ICommandDispatcher Dispatcher, Guid ProjectId)> StartAsync(TempDirectory temp, string projectIdentifier)
    {
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        QuotationTestHost.SignIn(host);
        var projectId = await QuotationTestHost.CreateProjectAsync(host, projectIdentifier, $"{projectIdentifier} Project");
        var domain = QuotationTestHost.Domain(host);
        var dispatcher = (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));

        return (host, manager, domain, dispatcher, projectId);
    }

    // ==================================================================
    // Create
    // ==================================================================

    [Fact]
    public async Task Create_Compensation_Undo_SoftDeletes_Redo_Restores()
    {
        using var temp = new TempDirectory();
        var (host, manager, domain, dispatcher, projectId) = await StartAsync(temp, "UNDO-CREATE-1");
        try
        {
            var result = await dispatcher.DispatchAsync(
                new CreateDocumentObjectCommand("Document", "Interface Control Document", parentId: projectId), default);
            Assert.True(result.Succeeded, result.Message);
            var createdId = result.SubjectId!.Value;
            Assert.NotNull(result.Compensation);
            var compensation = result.Compensation!;

            var undone = await compensation.Undo(default);
            Assert.True(undone.Succeeded, undone.Message);
            Assert.True(((IDeletable)(await domain.Repository.FindAsync(createdId))!).IsDeleted);

            var redone = await compensation.Redo(default);
            Assert.True(redone.Succeeded, redone.Message);
            Assert.False(((IDeletable)(await domain.Repository.FindAsync(createdId))!).IsDeleted);

            // The same identity throughout — never a second object minted.
            Assert.Equal(createdId, redone.SubjectId);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ==================================================================
    // Delete
    // ==================================================================

    [Fact]
    public async Task Delete_Compensation_Undo_Restores_Redo_SoftDeletes()
    {
        using var temp = new TempDirectory();
        var (host, manager, domain, dispatcher, projectId) = await StartAsync(temp, "UNDO-DELETE-1");
        try
        {
            var created = await dispatcher.DispatchAsync(
                new CreateDocumentObjectCommand("Document", "To Delete", parentId: projectId), default);
            var targetId = created.SubjectId!.Value;

            var deleted = await dispatcher.DispatchAsync(new DeleteDocumentObjectCommand(targetId, "Document"), default);
            Assert.True(deleted.Succeeded, deleted.Message);
            var compensation = deleted.Compensation!;
            Assert.True(((IDeletable)(await domain.Repository.FindAsync(targetId))!).IsDeleted);

            var undone = await compensation.Undo(default);
            Assert.True(undone.Succeeded, undone.Message);
            Assert.False(((IDeletable)(await domain.Repository.FindAsync(targetId))!).IsDeleted);

            var redone = await compensation.Redo(default);
            Assert.True(redone.Succeeded, redone.Message);
            Assert.True(((IDeletable)(await domain.Repository.FindAsync(targetId))!).IsDeleted);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task Delete_Compensation_Undo_Refused_WhenParentSinceDeleted()
    {
        using var temp = new TempDirectory();
        var (host, manager, domain, dispatcher, projectId) = await StartAsync(temp, "UNDO-DELETE-2");
        try
        {
            var parent = await dispatcher.DispatchAsync(
                new CreateDocumentObjectCommand("Document", "Parent", parentId: projectId), default);
            var parentId = parent.SubjectId!.Value;

            var child = await dispatcher.DispatchAsync(
                new CreateDocumentObjectCommand("Document", "Child", parentId: parentId), default);
            var childId = child.SubjectId!.Value;

            var deletedChild = await dispatcher.DispatchAsync(new DeleteDocumentObjectCommand(childId, "Document"), default);
            Assert.True(deletedChild.Succeeded, deletedChild.Message);

            // The child is soft-deleted now, so deleting the parent is no
            // longer blocked by a live child (`EngineeringObjectHasChildrenException`
            // only counts live ones).
            var deletedParent = await dispatcher.DispatchAsync(new DeleteDocumentObjectCommand(parentId, "Document"), default);
            Assert.True(deletedParent.Succeeded, deletedParent.Message);

            var refused = await deletedChild.Compensation!.Undo(default);
            Assert.False(refused.Succeeded);
            Assert.Contains(parentId.ToString(), refused.Message, StringComparison.OrdinalIgnoreCase);

            // Still deleted: the refusal changed nothing.
            Assert.True(((IDeletable)(await domain.Repository.FindAsync(childId))!).IsDeleted);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ==================================================================
    // Move
    // ==================================================================

    [Fact]
    public async Task Move_Compensation_Undo_MovesBack_Redo_MovesForward()
    {
        using var temp = new TempDirectory();
        var (host, manager, domain, dispatcher, projectId) = await StartAsync(temp, "UNDO-MOVE-1");
        try
        {
            var parentA = (await dispatcher.DispatchAsync(new CreateDocumentObjectCommand("Document", "Parent A", parentId: projectId), default)).SubjectId!.Value;
            var parentB = (await dispatcher.DispatchAsync(new CreateDocumentObjectCommand("Document", "Parent B", parentId: projectId), default)).SubjectId!.Value;
            var targetId = (await dispatcher.DispatchAsync(new CreateDocumentObjectCommand("Document", "Movable", parentId: parentA), default)).SubjectId!.Value;

            var moved = await dispatcher.DispatchAsync(new MoveDocumentObjectCommand(targetId, "Document", parentB), default);
            Assert.True(moved.Succeeded, moved.Message);
            Assert.Equal(parentB, ((IHasParent)(await domain.Repository.FindAsync(targetId))!).ParentId);

            var undone = await moved.Compensation!.Undo(default);
            Assert.True(undone.Succeeded, undone.Message);
            Assert.Equal(parentA, ((IHasParent)(await domain.Repository.FindAsync(targetId))!).ParentId);

            var redone = await moved.Compensation!.Redo(default);
            Assert.True(redone.Succeeded, redone.Message);
            Assert.Equal(parentB, ((IHasParent)(await domain.Repository.FindAsync(targetId))!).ParentId);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task Move_Compensation_Undo_Refused_WhenOldParentSinceDeleted()
    {
        using var temp = new TempDirectory();
        var (host, manager, domain, dispatcher, projectId) = await StartAsync(temp, "UNDO-MOVE-2");
        try
        {
            var parentA = (await dispatcher.DispatchAsync(new CreateDocumentObjectCommand("Document", "Parent A", parentId: projectId), default)).SubjectId!.Value;
            var parentB = (await dispatcher.DispatchAsync(new CreateDocumentObjectCommand("Document", "Parent B", parentId: projectId), default)).SubjectId!.Value;
            var targetId = (await dispatcher.DispatchAsync(new CreateDocumentObjectCommand("Document", "Movable", parentId: parentA), default)).SubjectId!.Value;

            var moved = await dispatcher.DispatchAsync(new MoveDocumentObjectCommand(targetId, "Document", parentB), default);
            Assert.True(moved.Succeeded, moved.Message);

            var deletedOldParent = await dispatcher.DispatchAsync(new DeleteDocumentObjectCommand(parentA, "Document"), default);
            Assert.True(deletedOldParent.Succeeded, deletedOldParent.Message);

            var refused = await moved.Compensation!.Undo(default);
            Assert.False(refused.Succeeded);
            Assert.Contains(parentA.ToString(), refused.Message, StringComparison.OrdinalIgnoreCase);

            // Still under B: the refused undo changed nothing.
            Assert.Equal(parentB, ((IHasParent)(await domain.Repository.FindAsync(targetId))!).ParentId);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ==================================================================
    // Copy
    // ==================================================================

    [Fact]
    public async Task Copy_Compensation_Undo_DeletesCopy_Redo_RestoresCopy()
    {
        using var temp = new TempDirectory();
        var (host, manager, domain, dispatcher, projectId) = await StartAsync(temp, "UNDO-COPY-1");
        try
        {
            var sourceId = (await dispatcher.DispatchAsync(new CreateDocumentObjectCommand("Document", "Source", parentId: projectId), default)).SubjectId!.Value;

            var copied = await dispatcher.DispatchAsync(new CopyDocumentObjectCommand(sourceId, "Document", projectId), default);
            Assert.True(copied.Succeeded, copied.Message);
            var copyId = copied.SubjectId!.Value;
            Assert.NotEqual(sourceId, copyId);

            var undone = await copied.Compensation!.Undo(default);
            Assert.True(undone.Succeeded, undone.Message);
            Assert.True(((IDeletable)(await domain.Repository.FindAsync(copyId))!).IsDeleted);
            // The source is never touched by the copy's own compensation.
            Assert.False(((IDeletable)(await domain.Repository.FindAsync(sourceId))!).IsDeleted);

            var redone = await copied.Compensation!.Redo(default);
            Assert.True(redone.Succeeded, redone.Message);
            Assert.False(((IDeletable)(await domain.Repository.FindAsync(copyId))!).IsDeleted);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ==================================================================
    // SetStatus
    // ==================================================================

    [Fact]
    public async Task SetStatus_Compensation_RoundTrips_WhenLifecyclePermitsReverse()
    {
        using var temp = new TempDirectory();
        var (host, manager, domain, dispatcher, projectId) = await StartAsync(temp, "UNDO-STATUS-1");
        try
        {
            var targetId = (await dispatcher.DispatchAsync(new CreateDocumentObjectCommand("Document", "Reviewable", parentId: projectId), default)).SubjectId!.Value;

            // Draft -> InReview is permitted both ways (`LifecycleTransitionTable`).
            var transitioned = await dispatcher.DispatchAsync(
                new SetDocumentStatusCommand(targetId, "Document", LifecycleState.InReview), default);
            Assert.True(transitioned.Succeeded, transitioned.Message);
            Assert.NotNull(transitioned.Compensation);
            Assert.Null(transitioned.UndoUnavailableReason);

            var undone = await transitioned.Compensation!.Undo(default);
            Assert.True(undone.Succeeded, undone.Message);
            Assert.Equal(LifecycleState.Draft, ((IHasLifecycle)(await domain.Repository.FindAsync(targetId))!).Status);

            var redone = await transitioned.Compensation!.Redo(default);
            Assert.True(redone.Succeeded, redone.Message);
            Assert.Equal(LifecycleState.InReview, ((IHasLifecycle)(await domain.Repository.FindAsync(targetId))!).Status);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task SetStatus_NoCompensation_WhenLifecycleDoesNotPermitReverse()
    {
        using var temp = new TempDirectory();
        var (host, manager, domain, dispatcher, projectId) = await StartAsync(temp, "UNDO-STATUS-2");
        try
        {
            var targetId = (await dispatcher.DispatchAsync(new CreateDocumentObjectCommand("Document", "Releasable", parentId: projectId), default)).SubjectId!.Value;

            await dispatcher.DispatchAsync(new SetDocumentStatusCommand(targetId, "Document", LifecycleState.InReview), default);
            await dispatcher.DispatchAsync(new SetDocumentStatusCommand(targetId, "Document", LifecycleState.Approved), default);

            // Approved -> Released is one-way: Released permits only
            // Superseded/Obsolete, never a return to Approved.
            var released = await dispatcher.DispatchAsync(
                new SetDocumentStatusCommand(targetId, "Document", LifecycleState.Released), default);

            Assert.True(released.Succeeded, released.Message);
            Assert.Null(released.Compensation);
            Assert.NotNull(released.UndoUnavailableReason);
            Assert.Contains("Approved", released.UndoUnavailableReason, StringComparison.Ordinal);
            Assert.Contains("Released", released.UndoUnavailableReason, StringComparison.Ordinal);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ==================================================================
    // The archived-project guard
    // ==================================================================

    [Fact]
    public async Task Compensation_NeverCrossesTheArchivedProjectGuard()
    {
        using var temp = new TempDirectory();
        var (host, manager, domain, dispatcher, projectId) = await StartAsync(temp, "UNDO-GUARD-1");
        try
        {
            var created = await dispatcher.DispatchAsync(
                new CreateDocumentObjectCommand("Document", "Guarded", parentId: projectId), default);
            Assert.True(created.Succeeded, created.Message);
            var compensation = created.Compensation!;

            // Archive the project after the action, before anyone tries to
            // undo it — the identical backdated-close-then-check pattern
            // `ArchivedProjectCommandGuardTests` already establishes.
            var backdated = new FakeTimeProvider(DateTimeOffset.UtcNow.AddDays(-91));
            var closed = await new ProjectLifecycleService(domain, backdated).SignOffAsync(projectId, "Closed for the compensation guard test.");
            Assert.True(closed.Succeeded, closed.Reason);

            var refused = await compensation.Undo(default);
            Assert.False(refused.Succeeded);
            Assert.Contains("archived", refused.Message, StringComparison.OrdinalIgnoreCase);

            // Nothing wrote: the created object is still live.
            Assert.False(((IDeletable)(await domain.Repository.FindAsync(created.SubjectId!.Value))!).IsDeleted);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ==================================================================
    // Undelete — the domain-level refusals directly (`IDeletable.UndeleteAsync`)
    // ==================================================================

    [Fact]
    public async Task Undelete_OnALiveObject_Refuses()
    {
        using var temp = new TempDirectory();
        var (host, manager, domain, dispatcher, projectId) = await StartAsync(temp, "UNDO-UNDELETE-1");
        try
        {
            var targetId = (await dispatcher.DispatchAsync(new CreateDocumentObjectCommand("Document", "Never Deleted", parentId: projectId), default)).SubjectId!.Value;

            var refused = await dispatcher.DispatchAsync(new UndeleteDocumentObjectCommand(targetId, "Document"), default);

            Assert.False(refused.Succeeded);
            Assert.False(((IDeletable)(await domain.Repository.FindAsync(targetId))!).IsDeleted);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ==================================================================
    // The macro compound action — isolated, no real discipline needed.
    // ==================================================================

    private static (CommandRegistry Registry, CommandDispatcher Dispatcher, MacroManager MacroManager) CreateMacroHarness()
    {
        var table = new CommandHandlerTable();
        var registry = new CommandRegistry(table);
        var dispatcher = new CommandDispatcher(table);
        var settingsProvider = new SettingsProvider(new InMemoryPersistenceStore(), new EventBus());
        var macroManager = new MacroManager(settingsProvider, registry);

        dispatcher.RegisterHandler(new RunMacroCommandHandler(macroManager, registry));

        return (registry, dispatcher, macroManager);
    }

    /// <summary>
    /// Two distinct trivial commands (`A`/`B`) — distinct concrete types,
    /// because <see cref="CommandHandlerTable"/> dispatches by
    /// <see cref="object.GetType"/>, one handler per type — each either
    /// compensable or not, enough to prove
    /// <see cref="RunMacroCommandHandler"/>'s own compound-assembly logic
    /// (ordering; "any step without one" refusal) without any real
    /// discipline at all.
    /// </summary>
    private sealed class BumpACommand : ICommand
    {
        public BumpACommand(bool compensable) => Compensable = compensable;
        public bool Compensable { get; }
    }

    private sealed class BumpBCommand : ICommand
    {
        public BumpBCommand(bool compensable) => Compensable = compensable;
        public bool Compensable { get; }
    }

    private static CommandResult BumpResult(List<string> log, string name, bool compensable)
    {
        log.Add($"do {name}");

        if (!compensable)
            return CommandResult.Success($"{name} done.", undoUnavailableReason: $"{name} is not compensable, for the test");

        var compensation = new CommandCompensation(
            name,
            undo: _ => { log.Add($"undo {name}"); return Task.FromResult(CommandResult.Success()); },
            redo: _ => { log.Add($"redo {name}"); return Task.FromResult(CommandResult.Success()); });

        return CommandResult.Success($"{name} done.", compensation: compensation);
    }

    private sealed class BumpACommandHandler : ICommandHandler<BumpACommand>
    {
        private readonly List<string> _log;
        public BumpACommandHandler(List<string> log) => _log = log;
        public Task<CommandResult> HandleAsync(BumpACommand command, CancellationToken cancellationToken) =>
            Task.FromResult(BumpResult(_log, "A", command.Compensable));
    }

    private sealed class BumpBCommandHandler : ICommandHandler<BumpBCommand>
    {
        private readonly List<string> _log;
        public BumpBCommandHandler(List<string> log) => _log = log;
        public Task<CommandResult> HandleAsync(BumpBCommand command, CancellationToken cancellationToken) =>
            Task.FromResult(BumpResult(_log, "B", command.Compensable));
    }

    [Fact]
    public async Task Macro_FullyCompensableRun_Undo_ReversesStepsInReverseOrder_Redo_ReplaysForward()
    {
        var (registry, dispatcher, macroManager) = CreateMacroHarness();
        var log = new List<string>();

        registry.RegisterDescriptor(new CommandDescriptor("test.bump-a", "Bump A", createDefault: () => new BumpACommand(true)));
        registry.RegisterDescriptor(new CommandDescriptor("test.bump-b", "Bump B", createDefault: () => new BumpBCommand(true)));
        dispatcher.RegisterHandler(new BumpACommandHandler(log));
        dispatcher.RegisterHandler(new BumpBCommandHandler(log));

        var macro = await macroManager.CreateAsync("Bump Both", ["test.bump-a", "test.bump-b"]);

        var run = await dispatcher.DispatchAsync(new RunMacroCommand(macro.Id), default);
        Assert.True(run.Succeeded, run.Message);
        Assert.Equal(["do A", "do B"], log);
        Assert.NotNull(run.Compensation);
        Assert.Null(run.UndoUnavailableReason);

        log.Clear();
        var undone = await run.Compensation!.Undo(default);
        Assert.True(undone.Succeeded, undone.Message);
        Assert.Equal(["undo B", "undo A"], log); // reverse order: the last thing done is the first thing undone.

        log.Clear();
        var redone = await run.Compensation!.Redo(default);
        Assert.True(redone.Succeeded, redone.Message);
        Assert.Equal(["redo A", "redo B"], log); // forward order again.
    }

    [Fact]
    public async Task Macro_PartiallyCompensableRun_ReportsWhichStepAndWhy_OffersNoPartialUndo()
    {
        var (registry, dispatcher, macroManager) = CreateMacroHarness();
        var log = new List<string>();

        registry.RegisterDescriptor(new CommandDescriptor("test.bump-a", "Bump A", createDefault: () => new BumpACommand(true)));
        registry.RegisterDescriptor(new CommandDescriptor("test.bump-b", "Bump B", createDefault: () => new BumpBCommand(false)));
        dispatcher.RegisterHandler(new BumpACommandHandler(log));
        dispatcher.RegisterHandler(new BumpBCommandHandler(log));

        var macro = await macroManager.CreateAsync("Bump Both, One Not Compensable", ["test.bump-a", "test.bump-b"]);

        var run = await dispatcher.DispatchAsync(new RunMacroCommand(macro.Id), default);

        Assert.True(run.Succeeded, run.Message);
        Assert.Null(run.Compensation);
        Assert.NotNull(run.UndoUnavailableReason);
        Assert.Contains("step 2", run.UndoUnavailableReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("test.bump-b", run.UndoUnavailableReason, StringComparison.Ordinal);
    }
}
