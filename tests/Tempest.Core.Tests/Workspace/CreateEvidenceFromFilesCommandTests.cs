using Tempest.Workspace;
using Tempest.Workspace.Composition;
using Tempest.Workspace.Editors;
using Tempest.Workspace.Evidence;
using Tempest.Workspace.Files;
using Tempest.Workspace.Mechanical;
using Tempest.Core.Commands;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Evidence;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Workspace;

/// <summary>
/// `WP 18.2A`: <c>evidence.create-from-files</c> creates and attaches every
/// picked file's bytes in one operation; <c>RemoveEvidenceCitationCommand</c>
/// removes a citation; the Evidence Kind opens through a real, registered
/// <see cref="IWorkspaceViewFactory"/> (without which <c>OpenAsync</c>
/// throws, and a created record could never open right up — `WP 17.9.4`).
/// </summary>
public sealed class CreateEvidenceFromFilesCommandTests : IAsyncLifetime
{
    private TempDirectory _temp = null!;
    private ITempestHost _host = null!;
    private WorkspaceManager _manager = null!;
    private ICommandDispatcher _dispatcher = null!;
    private EngineeringDomainContext _domain = null!;
    private IWorkspace _workspace = null!;

    public async Task InitializeAsync()
    {
        _temp = new TempDirectory();

        _host = new TempestHostBuilder([typeof(MechanicalWorkspaceExplorerModule)])
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(SqlitePersistenceStore.RootPathConfigurationKey, _temp.Path),
            ]))
            .Build();
        _manager = new WorkspaceManager(_host);

        _workspace = await _manager.StartAsync();

        EngineeringWorkspaceComposer.RegisterEngineeringDisciplines(_manager, _host);

        _dispatcher = (ICommandDispatcher)_host.Services!.GetService(typeof(ICommandDispatcher));
        _domain = (EngineeringDomainContext)_host.Services!.GetService(typeof(EngineeringDomainContext));
    }

    public async Task DisposeAsync()
    {
        await _manager.ShutdownAsync();
        await _host.DisposeAsync();
        _temp.Dispose();
    }

    private async Task<Guid> CreateProjectAsync()
    {
        var factory = new EngineeringObjectFactory<Project>(
            MechanicalObjectFactoryRegistry.Project, _domain,
            (doc, rev) => new Project(doc, rev, _domain, "EVD-FILES-PRJ", "Evidence Files Test Project", EngineeringObjectMetadata.Empty));
        var project = await factory.CreateAsync("Evidence-from-files test project.");
        return project.Id;
    }

    private static PickedFile PickedFileOf(string name, string contentType, byte[] content) =>
        new(name, contentType, () => Task.FromResult<ReadOnlyMemory<byte>>(content));

    [Fact]
    public async Task HandleAsync_OneFile_CreatesEvidenceAndAttachesItsBytes()
    {
        var projectId = await CreateProjectAsync();
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        var file = PickedFileOf("calc-sheet.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", bytes);

        var command = new CreateEvidenceFromFilesCommand("Bracket Calc", EvidenceClassification.Calculation, [file], projectId);
        var result = await _dispatcher.DispatchAsync(command, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        Assert.NotNull(result.SubjectId);
        Assert.Equal(Core.Evidence.Evidence.CanonicalKind, result.SubjectKind);

        var created = await _domain.Repository.FindAsync(result.SubjectId!.Value) as Core.Evidence.Evidence;
        Assert.NotNull(created);
        Assert.Equal(projectId, created!.ParentId);
        Assert.Equal("Bracket Calc", created.DisplayName);
        Assert.Equal(EvidenceClassification.Calculation, created.Classification);

        var attachments = await ((IHasAttachments)created).GetAttachmentsAsync();
        var attachment = Assert.Single(attachments);
        Assert.Equal("calc-sheet.xlsx", attachment.FileName);
        Assert.Equal(bytes.Length, attachment.SizeInBytes);
        Assert.NotNull(attachment.ContentHash);

        var readBack = await ((IHasAttachments)created).ReadAttachmentContentAsync(attachment.Id);
        Assert.Equal(AttachmentContentStatus.Available, readBack.Status);
        Assert.Equal(bytes, readBack.Bytes!.ToArray());
    }

    [Fact]
    public async Task HandleAsync_TwoFiles_AttachesBoth()
    {
        var projectId = await CreateProjectAsync();
        var fileA = PickedFileOf("a.pdf", "application/pdf", [10, 20]);
        var fileB = PickedFileOf("b.pdf", "application/pdf", [30, 40, 50]);

        var command = new CreateEvidenceFromFilesCommand("Report", EvidenceClassification.Report, [fileA, fileB], projectId);
        var result = await _dispatcher.DispatchAsync(command, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        var created = (Core.Evidence.Evidence)(await _domain.Repository.FindAsync(result.SubjectId!.Value))!;
        var attachments = await ((IHasAttachments)created).GetAttachmentsAsync();
        Assert.Equal(2, attachments.Count);
        Assert.Contains(attachments, a => a.FileName == "a.pdf");
        Assert.Contains(attachments, a => a.FileName == "b.pdf");
    }

    [Fact]
    public async Task HandleAsync_NoParent_CreatesStandaloneEvidence()
    {
        var file = PickedFileOf("standalone.txt", "text/plain", [1]);
        var command = new CreateEvidenceFromFilesCommand("Standalone", EvidenceClassification.Other, [file]);
        var result = await _dispatcher.DispatchAsync(command, CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        var created = (Core.Evidence.Evidence)(await _domain.Repository.FindAsync(result.SubjectId!.Value))!;
        Assert.Null(created.ParentId);
    }

    [Fact]
    public async Task HandleAsync_UnknownParent_Fails()
    {
        var file = PickedFileOf("x.txt", "text/plain", [1]);
        var command = new CreateEvidenceFromFilesCommand("X", EvidenceClassification.Other, [file], Guid.NewGuid());
        var result = await _dispatcher.DispatchAsync(command, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Message);
    }

    [Fact]
    public async Task RemoveEvidenceCitationCommand_RemovesACitation()
    {
        var projectId = await CreateProjectAsync();
        var evidenceService = (IEvidenceService)_host.Services!.GetService(typeof(IEvidenceService));
        var materials = (Tempest.Core.Materials.IMaterialCatalog)_host.Services!.GetService(typeof(Tempest.Core.Materials.IMaterialCatalog));

        var definition = new Tempest.Core.Materials.MaterialDefinition { Name = "Removable", Family = Tempest.Core.Materials.MaterialFamily.Steel, Designation = "evd-rm-1" };
        var provenance = new ReferenceProvenance(SourceOrganisation: "Org", SourceDocument: "Doc");
        await materials.RegisterAsync("evd-rm-1", definition, provenance);

        var principals = (Tempest.Core.Identity.ICurrentPrincipalAccessor)_host.Services!.GetService(typeof(Tempest.Core.Identity.ICurrentPrincipalAccessor));
        ((Tempest.Core.Identity.CurrentPrincipalAccessor)principals).SetCurrent(new Tempest.Core.Identity.PlatformPrincipal(
            new Tempest.Core.Identity.PlatformIdentity("reviewer-1", "reviewer-1"), Tempest.Core.Identity.ApplicationPermissions.LocalSession));
        var review = new Tempest.Core.ReferenceData.Review.ReferenceReviewService(principals);
        await review.VerifyAsync(materials, "evd-rm-1", new Tempest.Core.ReferenceData.Review.ReferenceReviewStatement("Doc"));
        await review.ReleaseAsync(materials, "evd-rm-1", "Released for this test.");

        var evidence = await evidenceService.CreateAsync(projectId, "Cited Evidence", EvidenceClassification.Calculation);
        var citeResult = await evidenceService.CiteAsync(evidence.Id, materials.LibraryName, "evd-rm-1");
        Assert.True(citeResult.Succeeded, citeResult.Reason);

        var refreshed = (Core.Evidence.Evidence)(await _domain.Repository.FindAsync(evidence.Id))!;
        Assert.Single(refreshed.Citations);

        var removeResult = await _dispatcher.DispatchAsync(
            new RemoveEvidenceCitationCommand(evidence.Id, Core.Evidence.Evidence.CanonicalKind, refreshed.Citations[0].Pin), CancellationToken.None);

        Assert.True(removeResult.Succeeded, removeResult.Message);
        var afterRemoval = (Core.Evidence.Evidence)(await _domain.Repository.FindAsync(evidence.Id))!;
        Assert.Empty(afterRemoval.Citations);
    }

    [Fact]
    public async Task EvidenceKind_OpensThroughARegisteredViewFactory()
    {
        // `WP 18.2A`: without this registration, `OpenAsync` throws
        // `WorkspaceViewFactoryNotFoundException` and a created record can
        // never actually open right up (Product Owner guard, `WP 17.9.4`).
        var projectId = await CreateProjectAsync();
        var evidenceService = (IEvidenceService)_host.Services!.GetService(typeof(IEvidenceService));
        var created = await evidenceService.CreateAsync(projectId, "Opens Right Up", EvidenceClassification.Test);

        var view = await _workspace.Navigation.OpenAsync(created.Id, Core.Evidence.Evidence.CanonicalKind);

        Assert.Equal(created.Id, view.ObjectId);
        Assert.Equal(Core.Evidence.Evidence.CanonicalKind, view.ObjectKind);
        Assert.Equal("Opens Right Up", view.Title);
    }
}

/// <summary>`WP 18.2A`: the declaration-per-Kind registry itself — every declared Kind, its own sections, in the order the brief names them.</summary>
public sealed class KindEditorDeclarationsTests
{
    [Fact]
    public void RegisterAll_RegistersExactlyEvidencePartAssemblyComponent()
    {
        var registry = new KindEditorDeclarationRegistry();
        KindEditorDeclarations.RegisterAll(registry);

        Assert.NotNull(registry.For(Core.Evidence.Evidence.CanonicalKind));
        Assert.NotNull(registry.For(MechanicalObjectFactoryRegistry.Part));
        Assert.NotNull(registry.For(MechanicalObjectFactoryRegistry.Assembly));
        Assert.NotNull(registry.For(MechanicalObjectFactoryRegistry.Component));

        // Every other Kind falls back to the Object Editor's own existing
        // generic path (Execution Plan §6 risk table's own explicit,
        // narrow scope) — no declaration is registered for it.
        Assert.Null(registry.For("Requirement"));
        Assert.Null(registry.For("Document"));
        Assert.Null(registry.For(MechanicalObjectFactoryRegistry.SubAssembly));
        Assert.Null(registry.For(MechanicalObjectFactoryRegistry.Configuration));
    }

    [Fact]
    public void Evidence_DeclaresIdentityFilesSubjectCitationsFiguresLifecycleAudit_InOrder()
    {
        var declaration = KindEditorDeclarations.Evidence();

        Assert.Equal(
            [
                EditorSectionKeys.Identity, EditorSectionKeys.Files, EditorSectionKeys.Subject,
                EditorSectionKeys.Citations, EditorSectionKeys.DeclaredFigures, EditorSectionKeys.CheckAndIssue, EditorSectionKeys.Audit,
            ],
            declaration.Sections.Select(s => s.Key));

        Assert.True(declaration.HasSection(EditorSectionKeys.Citations));
        Assert.False(declaration.HasSection(EditorSectionKeys.BillOfMaterials));

        var citations = declaration.Sections.Single(s => s.Key == EditorSectionKeys.Citations);
        Assert.Equal("evidence.cite", citations.Fields.Single().WriteCommandId);
    }

    [Fact]
    public void Part_HasNoBillOfMaterialsSection_ButHasWhereUsed()
    {
        var declaration = KindEditorDeclarations.Part();

        Assert.False(declaration.HasSection(EditorSectionKeys.BillOfMaterials));
        Assert.True(declaration.HasSection(EditorSectionKeys.WhereUsed));
    }

    [Fact]
    public void Assembly_HasBothWhereUsedAndBillOfMaterials()
    {
        var declaration = KindEditorDeclarations.Assembly();

        Assert.True(declaration.HasSection(EditorSectionKeys.WhereUsed));
        Assert.True(declaration.HasSection(EditorSectionKeys.BillOfMaterials));
    }

    [Fact]
    public void Component_HasWhereUsed_ButNoBillOfMaterials()
    {
        var declaration = KindEditorDeclarations.Component();

        Assert.True(declaration.HasSection(EditorSectionKeys.WhereUsed));
        Assert.False(declaration.HasSection(EditorSectionKeys.BillOfMaterials));
    }

    [Fact]
    public void Register_Twice_ThrowsForTheDuplicateKind()
    {
        var registry = new KindEditorDeclarationRegistry();
        registry.Register(KindEditorDeclarations.Evidence());

        Assert.Throws<ArgumentException>(() => registry.Register(KindEditorDeclarations.Evidence()));
    }
}
