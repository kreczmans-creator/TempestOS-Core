using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Macros;
using Tempest.Core.Projects;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.Tests.Quotations;
using Tempest.Workspace.Mechanical;

namespace Tempest.Core.Tests.Macros;

/// <summary>
/// `WP 21.5F` Offensive Security Audit, item 7 (Macros): closes a coverage
/// gap the audit found rather than a vulnerability — every existing test
/// of <see cref="Tempest.Core.Commands.ArchivedProjectCommandGuard"/>
/// exercises <see cref="ICommandRegistry.Evaluate"/>/<see cref="ICommandRegistry.InvokeAsync"/>
/// directly; none dispatched an actual <see cref="RunMacroCommand"/> through
/// <see cref="RunMacroCommandHandler"/>'s own step loop and confirmed the
/// guard fires there too. It does — this pins that, using the real,
/// production-wired command registry and macro manager
/// (<see cref="QuotationTestHost"/>'s own real composition root, mirroring
/// <see cref="Tempest.Core.Tests.Commands.ArchivedProjectCommandGuardTests"/>),
/// not a hand-rolled one.
/// </summary>
public sealed class MacroReplayArchivedProjectGuardTests
{
    [Fact]
    public async Task ARunMacroCommand_WhoseStepMutatesAnObjectUnderAnArchivedProject_StopsAtTheGuard_AndNeverWrites()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await QuotationTestHost.StartAsync(temp.Path);
        try
        {
            QuotationTestHost.SignIn(host);

            var projectId = await QuotationTestHost.CreateProjectAsync(host, "ARCH-MACRO-1", "Archived Macro Project");
            var domain = QuotationTestHost.Domain(host);
            var part = await new MechanicalObjectFactoryRegistry(domain).CreateAsync(
                MechanicalObjectFactoryRegistry.Part, "PT-ARCH-MACRO-1", "Bracket", "Initial content.", parentId: projectId);

            var backdated = new FakeTimeProvider(DateTimeOffset.UtcNow.AddDays(-91));
            var closed = await new ProjectLifecycleService(domain, backdated).SignOffAsync(projectId, "Closed for the macro guard test.");
            Assert.True(closed.Succeeded, closed.Reason);

            var commandDispatcher = (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));
            var macroManager = (IMacroManager)host.Services!.GetService(typeof(IMacroManager));

            var macro = await macroManager.CreateAsync(
                "Rename the archived part",
                [new MacroStep("mechanical.rename", new Dictionary<string, string> { ["newDisplayName"] = "Should never land" })]);

            var context = CommandContext.For(part.Id, "Part");
            var result = await commandDispatcher.DispatchAsync(new RunMacroCommand(macro.Id, context), CancellationToken.None);

            Assert.False(result.Succeeded);
            Assert.Contains("archived", result.Message, StringComparison.OrdinalIgnoreCase);

            // The part's own display name genuinely never changed - the
            // guard stopped the write, not only reported a failure message.
            var reloaded = (IHasBusinessIdentifier)(await domain.Repository.FindAsync(part.Id))!;
            Assert.Equal("Bracket", reloaded.DisplayName);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}
