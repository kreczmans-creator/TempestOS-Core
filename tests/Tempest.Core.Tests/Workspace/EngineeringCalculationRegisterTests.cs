using Tempest.App.Engineering;
using Tempest.App.Workspace;
using Tempest.App.Workspace.Calculations;
using Tempest.Core.Calculations;
using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;
using Tempest.Core.UnitsAndQuantities;

namespace Tempest.Core.Tests.Workspace;

/// <summary>
/// The two governance answers the Engineering Calculations workspace's own
/// naming, renaming and retirement rest on — pinned here so a later change
/// to either has to be a deliberate one.
/// </summary>
/// <remarks>
/// <para>
/// The behaviour itself is proved through the real Desktop shell, in
/// <c>Tempest.Desktop.Tests.EngineeringCalculationLifecycleTests</c>. What
/// is pinned here is narrower and belongs at this layer: that the platform's
/// canonical lifecycle really does forbid the transition the workspace would
/// otherwise have used, and that a retirement is never a soft delete.
/// </para>
/// <para>
/// Composed exactly as <see cref="CalculationsWorkspaceIntegrationTests"/>
/// composes it — a real running host and the real
/// <see cref="CalculationsWorkspaceRegistration.Register"/> call — because a
/// register that dispatches governed commands is only worth testing against
/// the handlers the product actually registers.
/// </para>
/// </remarks>
[Collection("Console output capture")]
public class EngineeringCalculationRegisterTests
{
    [Fact]
    public void TheCanonicalLifecycle_ForbidsArchivingSomethingThatIsStillDraft()
    {
        var table = new LifecycleTransitionTable();

        // This is the constraint that decides how a freshly recorded
        // calculation leaves the active list. Archived is the state the
        // workspace would prefer, and it is simply not reachable from where
        // a new object starts — it is reached only through Released, then
        // Superseded or Obsolete.
        Assert.False(table.IsPermitted(LifecycleState.Draft, LifecycleState.Archived));
        Assert.True(table.IsPermitted(LifecycleState.Superseded, LifecycleState.Archived));
        Assert.True(table.IsPermitted(LifecycleState.Obsolete, LifecycleState.Archived));

        // Cancelled is the one retained terminal state a Draft object can
        // reach, which is why retirement uses it there.
        Assert.True(table.IsPermitted(LifecycleState.Draft, LifecycleState.Cancelled));

        // Both are states this workspace counts as "out of the active list",
        // and neither is a deletion.
        Assert.True(EngineeringCalculationRegister.IsRetired(LifecycleState.Archived));
        Assert.True(EngineeringCalculationRegister.IsRetired(LifecycleState.Cancelled));
        Assert.False(EngineeringCalculationRegister.IsRetired(LifecycleState.Draft));
    }

    [Fact]
    public async Task RetiringACalculation_LeavesItHeldAndUndeleted_AndRetiringItTwiceIsRefused()
    {
        using var temp = new TempDirectory();
        var (register, domain, host) = await StartAsync(temp.Path);

        // A real executed record, because naming links the governed object
        // to the record's own document and the store refuses a link to a
        // document that does not exist — which is the correct refusal, and
        // is why the product only ever names a calculation that has run.
        var recordId = await ExecuteOneAsync(host);
        var named = await register.NameAsync(recordId, "Bracket check — to be retired");

        Assert.Equal("Bracket check — to be retired", named.DisplayName);
        Assert.Equal(recordId, named.RecordId);
        Assert.False(named.IsRetired);

        var retired = await register.RetireAsync(named.ObjectId);
        Assert.True(retired.Succeeded);
        Assert.Contains("Nothing was deleted", retired.Message, StringComparison.Ordinal);

        // Still held, still not deleted — a retirement is a lifecycle
        // transition, never IDeletable.DeleteAsync, which the platform has
        // no undo for anywhere.
        var subject = await domain.Repository.FindAsync(named.ObjectId);
        Assert.NotNull(subject);
        Assert.False(((IDeletable)subject!).IsDeleted);
        Assert.True(EngineeringCalculationRegister.IsRetired(((IHasLifecycle)subject!).Status));

        // Still listed, and still carrying the record it produced.
        var listed = (await register.ListAsync()).Single(n => n.ObjectId == named.ObjectId);
        Assert.True(listed.IsRetired);
        Assert.Equal(recordId, listed.RecordId);

        // And retiring it again is refused rather than repeated.
        var again = await register.RetireAsync(named.ObjectId);
        Assert.False(again.Succeeded);
        Assert.Contains("already", again.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RenamingChangesTheDisplayNameOnly_AndABlankNameIsRefused()
    {
        using var temp = new TempDirectory();
        var (register, domain, host) = await StartAsync(temp.Path);

        var recordId = await ExecuteOneAsync(host);
        var named = await register.NameAsync(recordId, "Bracket check — first name");

        var before = await domain.Repository.FindAsync(named.ObjectId);
        var revisionsBefore = before!.CurrentRevisionNumber;
        var linksBefore = await ((IHasRelationships)before!).GetRelationshipsAsync();

        var blank = await register.RenameAsync(named.ObjectId, "   ");
        Assert.False(blank.Succeeded);
        Assert.Equal("Bracket check — first name", ((IHasBusinessIdentifier)before!).DisplayName);

        var renamed = await register.RenameAsync(named.ObjectId, "Bracket check — second name");
        Assert.True(renamed.Succeeded);

        var after = await domain.Repository.FindAsync(named.ObjectId);
        Assert.Equal(named.ObjectId, after!.Id);
        Assert.Equal("Bracket check — second name", ((IHasBusinessIdentifier)after!).DisplayName);
        Assert.Equal(revisionsBefore, after.CurrentRevisionNumber);
        Assert.Equal(LifecycleState.Draft, ((IHasLifecycle)after!).Status);

        var linksAfter = await ((IHasRelationships)after!).GetRelationshipsAsync();
        Assert.Equal(linksBefore.Count, linksAfter.Count);
        Assert.Contains(linksAfter, l => l.TargetId == recordId);
    }

    /// <summary>
    /// Executes one of the product's own registered calculations and
    /// returns the record it produced — a real record, with a real
    /// document behind it.
    /// </summary>
    /// <remarks>
    /// The bolt shear capacity check is used rather than the bracket
    /// section check because it needs no released reference material: what
    /// this file is testing is naming, renaming and retirement, and which
    /// calculation produced the record is immaterial to all three.
    /// </remarks>
    private static async Task<Guid> ExecuteOneAsync(ITempestHost host)
    {
        var engine = (ICalculationEngine)host.Services!.GetService(typeof(ICalculationEngine));

        var record = await engine.ExecuteAsync<BoltShearCapacityInput, BoltShearCapacityResult>(
            BoltShearCapacityCalculationDefinition.Id,
            new BoltShearCapacityInput(
                new Quantity<Length>(10, LengthUnits.Millimetre),
                new Quantity<Pressure>(330, PressureUnits.Megapascal),
                ShearPlanes: 1,
                SafetyFactor: 1.5));

        return record.Id;
    }

    private static async Task<(EngineeringCalculationRegister Register, EngineeringDomainContext Domain, ITempestHost Host)> StartAsync(string rootPath)
    {
        var host = new TempestHostBuilder([])
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, rootPath),
            ]))
            .Build();
        var manager = new WorkspaceManager(host);

        var originalOut = Console.Out;
        try
        {
            Console.SetOut(new StringWriter());
            await manager.StartAsync();
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var services = host.Services!;
        var domain = (EngineeringDomainContext)services.GetService(typeof(EngineeringDomainContext));
        var dispatcher = (ICommandDispatcher)services.GetService(typeof(ICommandDispatcher));

        CalculationsWorkspaceRegistration.Register(
            manager,
            domain,
            (ICalculationEngine)services.GetService(typeof(ICalculationEngine)),
            dispatcher,
            (ICommandRegistry)services.GetService(typeof(ICommandRegistry)));

        // No project context: a calculation named outside a project is
        // top-level, which is the honest shape for this layer's tests.
        return (new EngineeringCalculationRegister(domain, dispatcher), domain, host);
    }
}
