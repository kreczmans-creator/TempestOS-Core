using Tempest.App.Projects;
using Tempest.App.Workspace.Documents;
using Tempest.App.Workspace.Mechanical;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.EngineeringData;
using Tempest.Core.Configuration;
using Tempest.Core.Identity;
using Tempest.Core.Persistence;
using Tempest.Core.Requirements;
using Tempest.Core.Verification;

namespace Tempest.Core.Tests.Shell;

/// <summary>
/// The two project-area read models: which documents and which
/// requirements genuinely belong to a project.
/// </summary>
/// <remarks>
/// Both registers exist because the Project Workspace's Documents and
/// Requirements areas were declared <c>Implemented</c> and drew a
/// declared-capability card. These tests pin the answers those surfaces
/// now render — above all <b>project isolation</b> (one project's
/// documents are never another's) and <b>transitivity</b> (a drawing on a
/// Part three levels down is in the project).
/// </remarks>
public sealed class ProjectAreaRegisterTests
{
    // ================================================================
    // Documents
    // ================================================================

    [Fact]
    public async Task DocumentRegister_ListsTheProjectsOwnDocuments_AndNeverAnotherProjects()
    {
        var fixture = await RegisterFixture.CreateAsync();

        var apollo = await fixture.CreateProjectAsync("P-0027", "Apollo");
        var vulcan = await fixture.CreateProjectAsync("P-0031", "Vulcan");

        await fixture.CreateDocumentAsync("DWG-1", "Apollo pump head", apollo.Id, "apollo.pdf");
        await fixture.CreateDocumentAsync("DWG-2", "Vulcan manifold", vulcan.Id, "vulcan.pdf");

        var apolloDocuments = await fixture.Documents.ListAsync(apollo.Id);
        var vulcanDocuments = await fixture.Documents.ListAsync(vulcan.Id);

        Assert.Equal(["DWG-1"], apolloDocuments.Select(d => d.Identifier));
        Assert.Equal(["DWG-2"], vulcanDocuments.Select(d => d.Identifier));
        Assert.Equal(["apollo.pdf"], apolloDocuments.Single().Attachments.Select(a => a.FileName));
    }

    [Fact]
    public async Task DocumentRegister_ReachesADrawingNestedThreeLevelsDown_NotDirectChildrenOnly()
    {
        // Project → Assembly → Sub-Assembly → Part, with the drawing
        // attached to the Part. Direct-child-only membership would find
        // nothing here, and that is exactly how a real product structure
        // is shaped.
        var fixture = await RegisterFixture.CreateAsync();
        var project = await fixture.CreateProjectAsync("P-0027", "Apollo");

        var assembly = await fixture.CreatePartAsync("ASM-1", "Pump assembly", project.Id);
        var subAssembly = await fixture.CreatePartAsync("SUB-1", "Head sub-assembly", assembly.Id);
        var part = await fixture.CreatePartAsync("PRT-1", "Impeller", subAssembly.Id);

        await part.AttachContentAsync("impeller.pdf", "application/pdf", "%PDF-1.4 impeller"u8.ToArray());

        var documents = await fixture.Documents.ListAsync(project.Id);

        var entry = Assert.Single(documents);
        Assert.Equal(part.Id, entry.ObjectId);
        Assert.Equal("impeller.pdf", Assert.Single(entry.Attachments).FileName);
        Assert.True(entry.HasFiles);
    }

    [Fact]
    public async Task DocumentRegister_ListsADocumentWithNoFile_AsADocumentWithNoFile()
    {
        // "This project has no documents" and "this document has no file"
        // are different statements, and a user has to be able to tell them
        // apart.
        var fixture = await RegisterFixture.CreateAsync();
        var project = await fixture.CreateProjectAsync("P-0027", "Apollo");

        await fixture.CreateDocumentAsync("DOC-1", "Specification", project.Id, attachmentFileName: null);

        var entry = Assert.Single(await fixture.Documents.ListAsync(project.Id));

        Assert.Equal("DOC-1", entry.Identifier);
        Assert.Empty(entry.Attachments);
        Assert.False(entry.HasFiles);
    }

    [Fact]
    public async Task DocumentRegister_AnEmptyProject_IsEmpty_NotAnError()
    {
        var fixture = await RegisterFixture.CreateAsync();
        var project = await fixture.CreateProjectAsync("P-0099", "Nothing yet");

        Assert.Empty(await fixture.Documents.ListAsync(project.Id));
    }

    [Fact]
    public async Task DocumentRegister_SkipsProjectObjectsCarryingNeitherDocumentNorFile()
    {
        var fixture = await RegisterFixture.CreateAsync();
        var project = await fixture.CreateProjectAsync("P-0027", "Apollo");

        await fixture.CreatePartAsync("PRT-9", "Plain part, no drawing", project.Id);

        Assert.Empty(await fixture.Documents.ListAsync(project.Id));
    }

    [Fact]
    public async Task DocumentRegister_OrdersDeterministically_AcrossRepeatedReads()
    {
        var fixture = await RegisterFixture.CreateAsync();
        var project = await fixture.CreateProjectAsync("P-0027", "Apollo");

        await fixture.CreateDocumentAsync("DWG-3", "Third", project.Id, "c.pdf");
        await fixture.CreateDocumentAsync("DWG-1", "First", project.Id, "a.pdf");
        await fixture.CreateDocumentAsync("DWG-2", "Second", project.Id, "b.pdf");

        var first = await fixture.Documents.ListAsync(project.Id);
        var second = await fixture.Documents.ListAsync(project.Id);

        Assert.Equal(["DWG-1", "DWG-2", "DWG-3"], first.Select(d => d.Identifier));
        Assert.Equal(first.Select(d => d.ObjectId), second.Select(d => d.ObjectId));
    }

    // ================================================================
    // Requirements
    // ================================================================

    [Fact]
    public async Task RequirementRegister_ListsARequirementAllocatedIntoTheProject_AndNotOneAllocatedElsewhere()
    {
        var fixture = await RegisterFixture.CreateAsync();

        var apollo = await fixture.CreateProjectAsync("P-0027", "Apollo");
        var vulcan = await fixture.CreateProjectAsync("P-0031", "Vulcan");

        var apolloPart = await fixture.CreatePartAsync("PRT-A", "Apollo impeller", apollo.Id);
        var vulcanPart = await fixture.CreatePartAsync("PRT-V", "Vulcan manifold", vulcan.Id);

        await fixture.AllocateRequirementAsync("REQ-100", "The impeller shall withstand 40 bar.", apolloPart.Id);
        await fixture.AllocateRequirementAsync("REQ-200", "The manifold shall not leak.", vulcanPart.Id);

        var apolloRequirements = await fixture.Requirements.ListAsync(apollo.Id);
        var vulcanRequirements = await fixture.Requirements.ListAsync(vulcan.Id);

        Assert.Equal(["REQ-100"], apolloRequirements.Select(r => r.Identifier));
        Assert.Equal(["REQ-200"], vulcanRequirements.Select(r => r.Identifier));
        Assert.Equal([apolloPart.Id], apolloRequirements.Single().LinkedObjectIds);
    }

    [Fact]
    public async Task RequirementRegister_AnUnallocatedRequirement_BelongsToNoProject()
    {
        // The honest consequence of joining on the link the platform
        // actually records: a requirement nobody has allocated is not yet
        // part of any project's work, and no register should pretend it is.
        var fixture = await RegisterFixture.CreateAsync();
        var project = await fixture.CreateProjectAsync("P-0027", "Apollo");
        await fixture.CreatePartAsync("PRT-A", "Impeller", project.Id);

        await fixture.RequirementsService.CreateAsync("REQ-ORPHAN", "Nobody allocated this.");

        Assert.Empty(await fixture.Requirements.ListAsync(project.Id));
    }

    [Fact]
    public async Task RequirementRegister_ReportsWhatVerificationRecorded_NotWhatTheStatusClaims()
    {
        var fixture = await RegisterFixture.CreateAsync();
        var project = await fixture.CreateProjectAsync("P-0027", "Apollo");
        var part = await fixture.CreatePartAsync("PRT-A", "Impeller", project.Id);

        var verified = await fixture.AllocateRequirementAsync("REQ-1", "Shall pass.", part.Id);
        var failed = await fixture.AllocateRequirementAsync("REQ-2", "Shall fail.", part.Id);
        var claimed = await fixture.AllocateRequirementAsync("REQ-3", "Claims verification.", part.Id);

        await fixture.VerifyAsync(verified.Id, VerificationOutcome.Pass);
        await fixture.VerifyAsync(failed.Id, VerificationOutcome.Fail);

        // Declared Verified, with nothing recorded behind it. Walked
        // through the real lifecycle rather than forced, so the state the
        // register reads is one the domain would actually allow.
        await fixture.RequirementsService.SetStatusAsync(claimed.Id, RequirementStatus.Reviewed);
        await fixture.RequirementsService.SetStatusAsync(claimed.Id, RequirementStatus.Approved);
        await fixture.RequirementsService.SetStatusAsync(claimed.Id, RequirementStatus.Allocated);
        await fixture.RequirementsService.SetStatusAsync(claimed.Id, RequirementStatus.Verified);

        var entries = (await fixture.Requirements.ListAsync(project.Id)).ToDictionary(e => e.Identifier);

        Assert.Equal(RequirementVerificationState.Passed, entries["REQ-1"].Verification);
        Assert.Equal(1, entries["REQ-1"].VerificationCount);
        Assert.False(entries["REQ-1"].ClaimsUnrecordedVerification);

        Assert.Equal(RequirementVerificationState.Failed, entries["REQ-2"].Verification);

        Assert.Equal(RequirementVerificationState.NotVerified, entries["REQ-3"].Verification);
        Assert.Equal(RequirementStatus.Verified, entries["REQ-3"].Status);
        Assert.True(entries["REQ-3"].ClaimsUnrecordedVerification);
    }

    [Fact]
    public async Task RequirementRegister_TakesTheLatestVerification_NotTheWorstEverRecorded()
    {
        // A requirement that failed, was fixed, and passed is verified.
        // Reporting the worst outcome ever recorded would leave it looking
        // failed forever.
        var fixture = await RegisterFixture.CreateAsync();
        var project = await fixture.CreateProjectAsync("P-0027", "Apollo");
        var part = await fixture.CreatePartAsync("PRT-A", "Impeller", project.Id);

        var requirement = await fixture.AllocateRequirementAsync("REQ-1", "Shall eventually pass.", part.Id);

        await fixture.VerifyAsync(requirement.Id, VerificationOutcome.Fail);
        await fixture.VerifyAsync(requirement.Id, VerificationOutcome.Pass);

        var entry = Assert.Single(await fixture.Requirements.ListAsync(project.Id));

        Assert.Equal(RequirementVerificationState.Passed, entry.Verification);
        Assert.Equal(2, entry.VerificationCount);
    }

    [Fact]
    public async Task RequirementRegister_APrincipalWhoMayNotReadVerification_StillGetsTheRequirements_AndIsToldSo()
    {
        // "Nothing was recorded" and "you may not see what was recorded"
        // are different facts. Reporting the second as the first would have
        // the surface state something false about the engineering data on
        // the strength of a permission check — and throwing would empty a
        // register the user is perfectly entitled to see.
        var fixture = await RegisterFixture.CreateAsync();
        var project = await fixture.CreateProjectAsync("P-0027", "Apollo");
        var part = await fixture.CreatePartAsync("PRT-A", "Impeller", project.Id);

        var requirement = await fixture.AllocateRequirementAsync("REQ-1", "Shall pass.", part.Id);
        await fixture.VerifyAsync(requirement.Id, VerificationOutcome.Pass);

        fixture.SignInWithoutVerificationPermission();

        var entry = Assert.Single(await fixture.Requirements.ListAsync(project.Id));

        Assert.Equal("REQ-1", entry.Identifier);
        Assert.Equal(RequirementVerificationState.Unknown, entry.Verification);
        Assert.Equal(0, entry.VerificationCount);
        Assert.False(entry.ClaimsUnrecordedVerification);
    }

    [Fact]
    public async Task RequirementRegister_AnEmptyProject_IsEmpty()
    {
        var fixture = await RegisterFixture.CreateAsync();
        var project = await fixture.CreateProjectAsync("P-0099", "Nothing yet");

        Assert.Empty(await fixture.Requirements.ListAsync(project.Id));
    }

    // ================================================================
    // Fixture
    // ================================================================

    private sealed class RegisterFixture
    {
        private RegisterFixture(
            EngineeringDomainContext domain,
            IRequirementsService requirements,
            IVerificationService verification,
            CurrentPrincipalAccessor principal)
        {
            Domain = domain;
            RequirementsService = requirements;
            VerificationService = verification;
            Principal = principal;
            Documents = new ProjectDocumentRegister(domain);
            Requirements = new ProjectRequirementRegister(requirements, domain);
        }

        public EngineeringDomainContext Domain { get; }

        public CurrentPrincipalAccessor Principal { get; }

        public IRequirementsService RequirementsService { get; }

        public IVerificationService VerificationService { get; }

        public IProjectDocumentRegister Documents { get; }

        public IProjectRequirementRegister Requirements { get; }

        public static Task<RegisterFixture> CreateAsync()
        {
            // A real PersistenceStore: attachment content is bytes, and the
            // in-memory double implements only the text contract. Using the
            // real store also means these tests exercise the same
            // persistence the product does.
            var root = Path.Combine(Path.GetTempPath(), "tempest-project-areas-" + Guid.NewGuid().ToString("N"));
            var configuration = new ConfigurationBuilder()
                .AddSource(new MemoryConfigurationSource(
                [
                    new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, root),
                ]))
                .Build();

            var store = new PersistenceStore(configuration);
            var principal = new CurrentPrincipalAccessor();
            var documents = new EngineeringDocumentStore(store, principal);
            var repository = new InMemoryEngineeringObjectRepository();
            var relationships = new InMemoryEngineeringRelationshipRepository();
            var discovery = new RelationshipDiscoveryService(relationships, repository);

            var domain = new EngineeringDomainContext(
                documents, repository, relationships, new LifecycleTransitionTable(), new ValidationRuleSet(),
                new EvidenceComposer(discovery, repository), principal,
                new EngineeringObjectStateStore(store), new AttachmentContentStore(store));

            var verification = new VerificationService(documents, principal, new PermissionEvaluator());
            var requirements = new RequirementsService(documents, store, principal, verification);

            // Verification reads are permission-gated, so the fixture signs
            // in as a principal that holds the permission — the product's
            // own established principal does. The denied case has its own
            // test rather than being the accidental default here.
            principal.SetCurrent(new PlatformPrincipal(new PlatformIdentity("engineer", "engineer"), [Core.Verification.VerificationService.ReadPermission]));

            return Task.FromResult(new RegisterFixture(domain, requirements, verification, principal));
        }

        public async Task<IProject> CreateProjectAsync(string identifier, string name)
        {
            var factory = new EngineeringObjectFactory<Project>(
                ProjectDirectory.ProjectKind, Domain,
                (d, r) => new Project(d, r, Domain, identifier, name, EngineeringObjectMetadata.Empty));

            return (Project)await factory.CreateAsync($"Project {identifier}.");
        }

        public async Task<Part> CreatePartAsync(string identifier, string name, Guid parentId)
        {
            var factory = new EngineeringObjectFactory<Part>(
                MechanicalObjectFactoryRegistry.Part, Domain,
                (d, r) => new Part(d, r, Domain, identifier, name, EngineeringObjectMetadata.Empty));

            var part = (Part)await factory.CreateAsync($"Part {identifier}.");
            await ((IHasParent)part).MoveAsync(parentId);
            return part;
        }

        public async Task<Document> CreateDocumentAsync(string identifier, string name, Guid parentId, string? attachmentFileName)
        {
            var factory = new EngineeringObjectFactory<Document>(
                DocumentObjectFactoryRegistry.Document, Domain,
                (d, r) => new Document(d, r, Domain, identifier, name, EngineeringObjectMetadata.Empty));

            var document = (Document)await factory.CreateAsync($"Document {identifier}.");
            await ((IHasParent)document).MoveAsync(parentId);

            if (attachmentFileName is not null)
            {
                await ((IHasAttachments)document).AttachContentAsync(
                    attachmentFileName, "application/pdf", "%PDF-1.4 content"u8.ToArray());
            }

            return document;
        }

        public async Task<Core.Requirements.IRequirement> AllocateRequirementAsync(string identifier, string statement, Guid targetObjectId)
        {
            var requirement = await RequirementsService.CreateAsync(identifier, statement);
            await RequirementsService.LinkAsync(requirement.Id, targetObjectId, RequirementRelationshipKinds.AllocatedTo);
            return requirement;
        }

        public void SignInWithoutVerificationPermission() =>
            Principal.SetCurrent(new PlatformPrincipal(new PlatformIdentity("reader", "reader"), []));

        public Task VerifyAsync(Guid requirementId, VerificationOutcome outcome)
        {
            var context = new VerificationContext();
            context.RecordEvidence("Test evidence.");
            return VerificationService.RecordAsync(requirementId, outcome, "Test", context);
        }
    }
}
