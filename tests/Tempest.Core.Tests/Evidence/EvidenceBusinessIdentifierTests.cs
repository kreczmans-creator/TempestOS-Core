using Tempest.Workspace.Composition;
using Tempest.Workspace.Mechanical;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Evidence;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Evidence;

/// <summary>
/// `TD-38` for Evidence: Evidence's own business identifier is its
/// Title/Name (`EngineeringObjectBase`'s own default —
/// <c>EvidenceService.CreateAsync</c> always passes <c>identifier: null</c>,
/// so <see cref="IEngineeringObject.BusinessIdentifier"/> falls back to
/// <see cref="Core.EngineeringDomain.IHasBusinessIdentifier.DisplayName"/>
/// exactly as the brief's design decision names it), enforced the same
/// generic way as every other enforced Kind, but reached through
/// <see cref="IEvidenceService.CreateAsync"/> rather than a factory
/// registry — hence its own test file rather than a row in
/// <c>BusinessIdentifierUniquenessTests</c>'s per-Kind theory.
/// </summary>
public sealed class EvidenceBusinessIdentifierTests
{
    [Fact]
    public async Task Create_DuplicateTitleInSameProject_IsRefused_NamingTheClash()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await EvidenceTestHost.StartAsync(temp.Path);
        try
        {
            EvidenceTestHost.SignIn(host);
            var projectId = await EvidenceTestHost.CreateProjectAsync(host);
            var service = EvidenceTestHost.Service(host);

            var first = await service.CreateAsync(projectId, "Bracket calculation", EvidenceClassification.Calculation);

            var ex = await Assert.ThrowsAsync<DuplicateBusinessIdentifierException>(
                () => service.CreateAsync(projectId, "Bracket calculation", EvidenceClassification.Calculation));

            Assert.Equal(Core.Evidence.Evidence.CanonicalKind, ex.Kind);
            Assert.Equal(projectId, ex.ProjectId);
            Assert.Equal(first.Id, ex.ExistingObjectId);
            Assert.Contains("A Evidence named 'Bracket calculation' already exists in project", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task Create_SameTitleInAnotherProject_IsAccepted()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await EvidenceTestHost.StartAsync(temp.Path);
        try
        {
            EvidenceTestHost.SignIn(host);
            var domain = EvidenceTestHost.Domain(host);
            var projectA = await EvidenceTestHost.CreateProjectAsync(host);
            var projectB = (await new MechanicalObjectFactoryRegistry(domain)
                .CreateAsync(MechanicalObjectFactoryRegistry.Project, null, "Second Test Project", "content", null)).Id;

            var service = EvidenceTestHost.Service(host);

            var inA = await service.CreateAsync(projectA, "Bracket calculation", EvidenceClassification.Calculation);
            var inB = await service.CreateAsync(projectB, "Bracket calculation", EvidenceClassification.Calculation);

            Assert.NotEqual(inA.Id, inB.Id);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task Rename_ToAnExistingTitleInTheSameProject_IsRefused()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await EvidenceTestHost.StartAsync(temp.Path);
        try
        {
            EvidenceTestHost.SignIn(host);
            var projectId = await EvidenceTestHost.CreateProjectAsync(host);
            var service = EvidenceTestHost.Service(host);

            await service.CreateAsync(projectId, "Bracket calculation", EvidenceClassification.Calculation);
            var other = await service.CreateAsync(projectId, "Bolt calculation", EvidenceClassification.Calculation);

            await Assert.ThrowsAsync<DuplicateBusinessIdentifierException>(
                () => ((IRenamable)other).RenameAsync("Bracket calculation"));

            Assert.Equal("Bolt calculation", other.DisplayName);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task SoftDelete_FreesTheTitle()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await EvidenceTestHost.StartAsync(temp.Path);
        try
        {
            EvidenceTestHost.SignIn(host);
            var projectId = await EvidenceTestHost.CreateProjectAsync(host);
            var service = EvidenceTestHost.Service(host);

            var first = await service.CreateAsync(projectId, "Bracket calculation", EvidenceClassification.Calculation);
            await ((IDeletable)first).DeleteAsync();

            var second = await service.CreateAsync(projectId, "Bracket calculation", EvidenceClassification.Calculation);

            Assert.NotEqual(first.Id, second.Id);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task Rehydration_RebuildsTheIndex_CreateRestartDuplicateRefused()
    {
        using var temp = new TempDirectory();
        Guid projectId;

        {
            var (host, manager) = await EvidenceTestHost.StartAsync(temp.Path);
            EvidenceTestHost.SignIn(host);
            projectId = await EvidenceTestHost.CreateProjectAsync(host);
            await EvidenceTestHost.Service(host).CreateAsync(projectId, "Bracket calculation", EvidenceClassification.Calculation);

            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }

        {
            var (host, manager) = await EvidenceTestHost.StartAsync(temp.Path);
            try
            {
                var result = await EngineeringWorkspaceComposer.RehydrateEngineeringObjectsAsync(host);
                Assert.True(result.IsComplete);

                EvidenceTestHost.SignIn(host);
                var service = EvidenceTestHost.Service(host);

                await Assert.ThrowsAsync<DuplicateBusinessIdentifierException>(
                    () => service.CreateAsync(projectId, "Bracket calculation", EvidenceClassification.Calculation));
            }
            finally
            {
                await manager.ShutdownAsync();
                await host.DisposeAsync();
            }
        }
    }
}
