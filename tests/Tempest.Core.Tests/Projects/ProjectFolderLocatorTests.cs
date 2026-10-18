using System.Reflection;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.Projects;
using Tempest.Core.Tests.BusinessOperations;
using Tempest.Core.Tests.Plugins;
using Tempest.Workspace.Projects;

namespace Tempest.Core.Tests.Projects;

/// <summary>
/// <see cref="ProjectFolderLocator"/> against a real host (PO decision
/// 2026-10-01): a real project's own identifier and name, and its client
/// read from the real Organisation catalogue, become the folder tree — and
/// a project with no client is still filed.
/// </summary>
public sealed class ProjectFolderLocatorTests
{
    [Fact]
    public async Task EnsureAsync_FilesARealProject_UnderItsCatalogueCustomer_ThenUnderNoCustomerWithoutOne()
    {
        using var temp = new TempDirectory();
        using var folderRoot = new TempDirectory();
        var (host, manager) = await ProjectCommercialTestHost.StartAsync(temp.Path);
        ProjectCommercialTestHost.SignIn(host);

        var directory = new ProjectDirectory(ProjectCommercialTestHost.Domain(host));
        var organisations = ProjectCommercialTestHost.Organisations(host);
        var locator = new ProjectFolderLocator(new ProjectFolderService(new ProjectFolderOptions(folderRoot.Path, [])), directory, organisations);

        var withClient = await directory.CreateAsync("ACME1-LOCAT1", "Located project");
        await organisations.RegisterAsync("ACMEX", OperationsFixtures.Organisation("ACMEX") with { CustomerCode = "ACME1" }, OperationsFixtures.Verified());
        Assert.True((await ProjectCommercialTestHost.ProjectCommercial(host).SetClientAsync(withClient.Id, "ACMEX")).Succeeded);

        var filed = await locator.EnsureAsync(withClient.Id);
        Assert.Equal(ProjectFolderStatus.Created, filed.Status);
        Assert.Equal(Path.Combine(folderRoot.Path, "ACME1", "ACME1-LOCAT1"), filed.ProjectFolder);
        Assert.Equal(filed.ProjectFolder, await locator.QuoteFolderForAsync(withClient.Id));

        var withUncodedClient = await directory.CreateAsync("LOC-0003", "Uncoded client project");
        await organisations.RegisterAsync("NOCODE", OperationsFixtures.Organisation("NOCODE"), OperationsFixtures.Verified());
        Assert.True((await ProjectCommercialTestHost.ProjectCommercial(host).SetClientAsync(withUncodedClient.Id, "NOCODE")).Succeeded);
        var byName = await locator.EnsureAsync(withUncodedClient.Id);
        Assert.Equal(Path.Combine(folderRoot.Path, "Fictional Client Ltd", "LOC-0003"), byName.ProjectFolder);

        var withoutClient = await directory.CreateAsync("LOC-0002", "Internal project");
        var unfiled = await locator.EnsureAsync(withoutClient.Id);
        Assert.Equal(Path.Combine(folderRoot.Path, ProjectFolderService.NoCustomerFolderName, "LOC-0002"), unfiled.ProjectFolder);

        var missing = await locator.EnsureAsync(Guid.NewGuid());
        Assert.Equal(ProjectFolderStatus.Failed, missing.Status);

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task EnsureAsync_FilesUnderTheCodeFrozenInTheIdentifier_AndKeepsTheTreeWhenAClientIsSetLater()
    {
        using var temp = new TempDirectory();
        using var folderRoot = new TempDirectory();
        var (host, manager) = await ProjectCommercialTestHost.StartAsync(temp.Path);
        ProjectCommercialTestHost.SignIn(host);

        var directory = new ProjectDirectory(ProjectCommercialTestHost.Domain(host));
        var organisations = ProjectCommercialTestHost.Organisations(host);
        var locator = new ProjectFolderLocator(new ProjectFolderService(new ProjectFolderOptions(folderRoot.Path, [])), directory, organisations);

        // The client's code has since been edited from ACME1 to ACME2.
        var frozen = await directory.CreateAsync("ACME1-FROZE1", "Frozen code project");
        await organisations.RegisterAsync("ACMEX", OperationsFixtures.Organisation("ACMEX") with { CustomerCode = "ACME2" }, OperationsFixtures.Verified());
        Assert.True((await ProjectCommercialTestHost.ProjectCommercial(host).SetClientAsync(frozen.Id, "ACMEX")).Succeeded);

        var filed = await locator.EnsureAsync(frozen.Id);
        Assert.Equal(Path.Combine(folderRoot.Path, "ACME1", "ACME1-FROZE1"), filed.ProjectFolder);
        Assert.False(Directory.Exists(Path.Combine(folderRoot.Path, "ACME2")));

        // Filed under "_No customer" first, then given a client.
        var late = await directory.CreateAsync("LOC-0009", "Client set later");
        var first = await locator.EnsureAsync(late.Id);
        Assert.Equal(Path.Combine(folderRoot.Path, ProjectFolderService.NoCustomerFolderName, "LOC-0009"), first.ProjectFolder);
        Assert.True((await ProjectCommercialTestHost.ProjectCommercial(host).SetClientAsync(late.Id, "ACMEX")).Succeeded);

        var second = await locator.EnsureAsync(late.Id);
        Assert.Equal(ProjectFolderStatus.AlreadyExisted, second.Status);
        Assert.Equal(first.ProjectFolder, second.ProjectFolder);
        Assert.Equal(first.ProjectFolder, await locator.QuoteFolderForAsync(late.Id));

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    [Fact]
    public async Task EnsureAsync_WhenTheCatalogueCannotBeRead_Fails_AndNeverFilesUnderTheRawClientId()
    {
        using var temp = new TempDirectory();
        using var folderRoot = new TempDirectory();
        var (host, manager) = await ProjectCommercialTestHost.StartAsync(temp.Path);
        ProjectCommercialTestHost.SignIn(host);

        var directory = new ProjectDirectory(ProjectCommercialTestHost.Domain(host));
        var organisations = ProjectCommercialTestHost.Organisations(host);
        var project = await directory.CreateAsync("LOC-0010", "Unreadable client");
        await organisations.RegisterAsync("RAWID", OperationsFixtures.Organisation("RAWID"), OperationsFixtures.Verified());
        Assert.True((await ProjectCommercialTestHost.ProjectCommercial(host).SetClientAsync(project.Id, "RAWID")).Succeeded);

        var locator = new ProjectFolderLocator(
            new ProjectFolderService(new ProjectFolderOptions(folderRoot.Path, [])), directory, UnreadableOrganisationCatalog.Create());

        var outcome = await locator.EnsureAsync(project.Id);

        Assert.Equal(ProjectFolderStatus.Failed, outcome.Status);
        Assert.Contains("RAWID", outcome.Message, StringComparison.Ordinal);
        Assert.Null(outcome.ProjectFolder);
        Assert.Empty(Directory.GetDirectories(folderRoot.Path));
        Assert.Null(await locator.QuoteFolderForAsync(project.Id));

        await manager.ShutdownAsync();
        await host.DisposeAsync();
    }

    /// <summary>An Organisation catalogue every call to which throws, as a corrupt or locked store would.</summary>
    public class UnreadableOrganisationCatalog : DispatchProxy
    {
        /// <summary>Creates the throwing catalogue.</summary>
        public static IOrganisationCatalog Create() => Create<IOrganisationCatalog, UnreadableOrganisationCatalog>();

        /// <inheritdoc />
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            throw new IOException("The organisation catalogue could not be read.");
    }
}
