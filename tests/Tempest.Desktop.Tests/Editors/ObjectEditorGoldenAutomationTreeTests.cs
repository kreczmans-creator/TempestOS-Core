using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Evidence;
using Tempest.Core.Invoicing;
using Tempest.Core.People;
using Tempest.Core.Quotations;
using Tempest.Core.ReferenceData.Review;
using Tempest.Core.Requirements;
using Tempest.Desktop.Editors;
using Tempest.Workspace;
using Tempest.Workspace.Mechanical;
using Tempest.Workspace.Requirements;
using Tempest.Workspace.Verification;

namespace Tempest.Desktop.Tests.Editors;

/// <summary>
/// `WP 21.1B` scope item 4 — the god-object split's own "proof of
/// identity": one golden <see cref="EditorAutomationTreeWalker"/> tree per
/// Kind, captured from the real, running <see cref="ObjectEditorView"/>
/// before the shell/section split (`tests/Tempest.Desktop.Tests/Editors/Golden/*.txt`,
/// committed alongside the split itself) and re-asserted, byte-for-byte,
/// after every section moved into its own file. A mismatch here means the
/// split changed a control's type, order, automation name or default
/// visibility for that Kind — exactly the "no behaviour change of any
/// kind" the brief holds the split to.
/// </summary>
/// <remarks>
/// Nine fixtures, deliberately chosen so every one of the platform's
/// twenty-three sections is exercised by at least one of them: Part
/// (Identity/Content/Lifecycle/Relationships/Validation/Bill of
/// Materials/Description/Where used/Attachments), Assembly (the same
/// shape, top-level — Where used differs from Part's parented case),
/// Project (Commercial; proves Bill of Materials/Execute/Calculation stay
/// hidden), Calculation (Execute — permanently hidden, see
/// <c>PopulateCalculationExecutionAsync</c>'s own remarks —, the
/// Calculation pointer, Due), Requirement (Owner / Priority, and the
/// Requirement-only population path's own Identity/Content/Relationships),
/// Verification Activity (Record Result), Evidence (Subject/Citations/
/// Declared figures/its own Lifecycle/Audit), Invoice Request (Lines/
/// Connector), Quotation (Lines). Every fixture is built with fixed,
/// deterministic display names/ids (never a bare <see cref="Guid"/> read
/// back into an automation name) so the walk is stable run to run — see
/// <see cref="EditorAutomationTreeWalker"/>'s own remarks for why it never
/// renders a control's <c>Text</c>/<c>Content</c> either.
/// </remarks>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class ObjectEditorGoldenAutomationTreeTests
{
    [AvaloniaFact]
    public async Task Part_MatchesItsGoldenAutomationTree()
    {
        await RunAsync("Part", async ctx =>
        {
            await ctx.Host.Workspace!.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);
            var target = await FindFirstObjectNodeOfKindAsync(ctx.Host.Workspace!.ProjectExplorer, await ctx.Host.Workspace.ProjectExplorer.GetRootNodesAsync(), MechanicalObjectFactoryRegistry.Part);
            if (target is null)
                return null;

            var editor = ObjectEditorView.TryCreate(target.Id, target.Kind!, ctx.DomainContext, ctx.Host.Manager!, (_, _) => { }, ctx.CommandDispatcher)!;
            await SettleAsync(() => editor.GetLogicalDescendants().OfType<Expander>().Any(e => Equals(e.Header, "Validation") && e.GetLogicalDescendants().Any()));
            return editor;
        });
    }

    [AvaloniaFact]
    public async Task Assembly_MatchesItsGoldenAutomationTree()
    {
        await RunAsync("Assembly", async ctx =>
        {
            await ctx.Host.Workspace!.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);
            var target = await FindFirstObjectNodeOfKindAsync(ctx.Host.Workspace!.ProjectExplorer, await ctx.Host.Workspace.ProjectExplorer.GetRootNodesAsync(), MechanicalObjectFactoryRegistry.Assembly);
            if (target is null)
                return null;

            var editor = ObjectEditorView.TryCreate(target.Id, target.Kind!, ctx.DomainContext, ctx.Host.Manager!, (_, _) => { }, ctx.CommandDispatcher)!;
            await SettleAsync(() => editor.GetLogicalDescendants().OfType<Expander>().Any(e => Equals(e.Header, "Validation") && e.GetLogicalDescendants().Any()));
            return editor;
        });
    }

    [AvaloniaFact]
    public async Task Project_MatchesItsGoldenAutomationTree()
    {
        await RunAsync("Project", async ctx =>
        {
            var project = await ctx.Host.ProjectDirectory!.CreateAsync("P-GOLDEN", "Golden Tree Project");
            var editor = ObjectEditorView.TryCreate(project.Id, MechanicalObjectFactoryRegistry.Project, ctx.DomainContext, ctx.Host.Manager!, (_, _) => { }, ctx.CommandDispatcher)!;
            await SettleAsync(() => editor.GetLogicalDescendants().OfType<Expander>().Any(e => Equals(e.Header, "Commercial") && e.IsVisible));
            return editor;
        });
    }

    [AvaloniaFact]
    public async Task Calculation_MatchesItsGoldenAutomationTree()
    {
        await RunAsync("Calculation", async ctx =>
        {
            var created = await ctx.CommandDispatcher.DispatchAsync(
                new Tempest.Workspace.Calculations.CreateCalculationObjectCommand(
                    Tempest.Workspace.Calculations.CalculationObjectFactoryRegistry.CalculationKind, "Golden Tree Calculation", "GT-CALC-1"),
                CancellationToken.None);
            Assert.True(created.Succeeded, created.Message);
            // `WP 21.5B`: list results are index rows, which carry Identifier and Kind themselves.
            var calculation = (await ctx.DomainContext.Repository.ListByKindAsync(Tempest.Workspace.Calculations.CalculationObjectFactoryRegistry.CalculationKind))
                .Single(entry => entry.Identifier == "GT-CALC-1");

            var editor = ObjectEditorView.TryCreate(calculation.Id, calculation.Kind!, ctx.DomainContext, ctx.Host.Manager!, (_, _) => { }, ctx.CommandDispatcher)!;
            await SettleAsync(() => editor.GetLogicalDescendants().OfType<Expander>().Any(e => Equals(e.Header, "Due") && e.IsVisible));
            return editor;
        });
    }

    [AvaloniaFact]
    public async Task Requirement_MatchesItsGoldenAutomationTree()
    {
        await RunAsync("Requirement", async ctx =>
        {
            var requirementsService = (IRequirementsService)ctx.Host.Services!.GetService(typeof(IRequirementsService));
            var personCatalog = (IPersonCatalog)ctx.Host.Services!.GetService(typeof(IPersonCatalog));

            const string personId = "person-wp211b-golden-owner";
            const string personDisplayName = "WP 21.1B Golden Owner";
            await personCatalog.RegisterAsync(personId, new Person { DisplayName = personDisplayName }, PersonProvenance.Default);
            var statement = new ReferenceReviewStatement("Golden-tree fixture — registered directly, not through the People library's own UI.");
            await ctx.Host.ReferenceReview!.VerifyAsync(personCatalog, personId, statement);
            await ctx.Host.ReferenceReview!.ReleaseAsync(personCatalog, personId, "Golden-tree fixture.");
            var ownerSupport = new RequirementOwnerEditorSupport(personCatalog, _ => Task.FromResult<(string RecordId, string DisplayName)?>(null));

            var created = await ctx.CommandDispatcher.DispatchAsync(
                new CreateRequirementCommand("REQ-GOLDEN-1", "Golden tree requirement statement."), CancellationToken.None);
            Assert.True(created.Succeeded, created.Message);

            var editor = ObjectEditorView.TryCreate(
                created.SubjectId!.Value, RequirementsService.RequirementDocumentKind, ctx.DomainContext, ctx.Host.Manager!, (_, _) => { }, ctx.CommandDispatcher,
                requirementsService, ownerSupport: ownerSupport)!;

            await SettleAsync(() => editor.GetLogicalDescendants().OfType<ComboBox>().Any(c => Avalonia.Automation.AutomationProperties.GetName(c) == "Owner" && c.ItemsSource is not null && c.ItemsSource!.Cast<object>().Any()));
            return editor;
        });
    }

    [AvaloniaFact]
    public async Task VerificationActivity_MatchesItsGoldenAutomationTree()
    {
        await RunAsync("VerificationActivity", async ctx =>
        {
            var created = await ctx.CommandDispatcher.DispatchAsync(
                new CreateVerificationActivityCommand("Golden Tree Verification", Guid.NewGuid(), "Inspection"), CancellationToken.None);
            Assert.True(created.Succeeded, created.Message);

            var editor = ObjectEditorView.TryCreate(created.SubjectId!.Value, "VerificationActivity", ctx.DomainContext, ctx.Host.Manager!, (_, _) => { }, ctx.CommandDispatcher)!;
            await SettleAsync(() => editor.GetLogicalDescendants().OfType<Expander>().Any(e => Equals(e.Header, "Record Result") && e.IsVisible));
            return editor;
        });
    }

    [AvaloniaFact]
    public async Task Evidence_MatchesItsGoldenAutomationTree()
    {
        await RunAsync("Evidence", async ctx =>
        {
            var project = await ctx.Host.ProjectDirectory!.CreateAsync("P-GOLDEN-EV", "Golden Tree Evidence Project");
            var evidence = await ctx.Host.EvidenceService!.CreateAsync(project.Id, "Golden Tree Evidence", EvidenceClassification.Calculation);

            var editor = ObjectEditorView.TryCreate(evidence.Id, Tempest.Core.Evidence.Evidence.CanonicalKind, ctx.DomainContext, ctx.Host.Manager!, (_, _) => { }, ctx.CommandDispatcher)!;
            await SettleAsync(() => editor.GetLogicalDescendants().OfType<Expander>().Any(e => Equals(e.Header, "Audit") && e.IsVisible));
            return editor;
        });
    }

    [AvaloniaFact]
    public async Task InvoiceRequest_MatchesItsGoldenAutomationTree()
    {
        await RunAsync("InvoiceRequest", async ctx =>
        {
            var project = await ctx.Host.ProjectDirectory!.CreateAsync("P-GOLDEN-INV", "Golden Tree Invoice Project");
            var line = new InvoiceRequestLine("DeliverableCompletion", Guid.NewGuid(), "Golden fixture line", 1m, new Money(100m, CurrencyCode.Gbp), new Money(100m, CurrencyCode.Gbp));
            var lines = new List<InvoiceRequestLine> { line };
            var total = Money.Sum(lines.Select(l => l.Amount), CurrencyCode.Gbp);

            var created = await new EngineeringObjectFactory<InvoiceRequest>(
                InvoiceRequest.CanonicalKind, ctx.DomainContext,
                (doc, rev) => new InvoiceRequest(
                    doc, rev, ctx.DomainContext, identifier: null, "Golden Tree Invoice Request", EngineeringObjectMetadata.Empty,
                    "ORG-GOLDEN", purchaseOrderReference: null, CurrencyCode.Gbp, lines, total, InvoiceRequestStatus.Draft,
                    externalId: null, externalInvoiceNumber: null, externalStatus: null, issuedDate: null, paidDate: null, lastError: null,
                    connector: null, sentAtUtc: null, dueOn: null))
                .CreateAsync("Golden-tree fixture — invoice request.", CancellationToken.None);

            if (created is IHasParent hasParent)
                await hasParent.MoveAsync(project.Id, CancellationToken.None);

            var editor = ObjectEditorView.TryCreate(created.Id, InvoiceRequest.CanonicalKind, ctx.DomainContext, ctx.Host.Manager!, (_, _) => { }, ctx.CommandDispatcher)!;
            await SettleAsync(() => editor.GetLogicalDescendants().OfType<Expander>().Any(e => Equals(e.Header, "Connector") && e.IsVisible));
            return editor;
        });
    }

    [AvaloniaFact]
    public async Task Quotation_MatchesItsGoldenAutomationTree()
    {
        await RunAsync("Quotation", async ctx =>
        {
            var project = await ctx.Host.ProjectDirectory!.CreateAsync("P-GOLDEN-QUO", "Golden Tree Quotation Project");
            var line = new QuotationLine(Guid.NewGuid(), "Golden fixture line", 5m, new Money(100m, CurrencyCode.Gbp), null, new Money(500m, CurrencyCode.Gbp), QuotationLineBasis.Hourly);

            var created = await new EngineeringObjectFactory<Quotation>(
                Quotation.CanonicalKind, ctx.DomainContext,
                (doc, rev) => new Quotation(
                    doc, rev, ctx.DomainContext, identifier: null, "Golden Tree Quotation", EngineeringObjectMetadata.Empty,
                    "Q-GOLDEN-1", DateOnly.FromDateTime(DateTime.UtcNow), "ORG-GOLDEN", CurrencyCode.Gbp, 30, terms: null,
                    [line], status: QuotationStatus.Draft, sentOn: null))
                .CreateAsync("Golden-tree fixture — quotation.", CancellationToken.None);

            if (created is IHasParent hasParent)
                await hasParent.MoveAsync(project.Id, CancellationToken.None);

            var editor = ObjectEditorView.TryCreate(created.Id, Quotation.CanonicalKind, ctx.DomainContext, ctx.Host.Manager!, (_, _) => { }, ctx.CommandDispatcher)!;
            await SettleAsync(() => editor.GetLogicalDescendants().OfType<Expander>().Any(e => Equals(e.Header, "Lines") && e.IsVisible));
            return editor;
        });
    }

    // ------------------------------------------------------------
    // Shared machinery.
    // ------------------------------------------------------------

    private sealed record Context(WorkspaceHost Host, EngineeringDomainContext DomainContext, ICommandDispatcher CommandDispatcher);

    private static async Task RunAsync(string kind, Func<Context, Task<ObjectEditorView?>> buildAsync)
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var domainContext = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
            var commandDispatcher = (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));

            var editor = await buildAsync(new Context(host, domainContext, commandDispatcher));
            if (editor is null)
                return; // no real fixture of this Kind in the sample set — honestly nothing to prove here (mirrors ObjectEditorViewTests's own convention).

            AssertGoldenTree(kind, editor);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>Pumps the UI dispatcher until <paramref name="isSettled"/> is true or a generous deadline elapses — <see cref="ObjectEditorView.TryCreate"/>'s own population runs fire-and-forget (that method's own remarks).</summary>
    private static async Task SettleAsync(Func<bool> isSettled)
    {
        var deadline = DesktopTestHelpers.Deadline(2);
        while (!isSettled() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
        }

        // A few extra pumps regardless — the settle signal above is the
        // last field this fixture's own Kind populates in
        // PopulateFromAsync's/PopulateFromRequirementAsync's single
        // sequential await chain (see this class's own remarks), so
        // everything before it has already completed by the time it
        // fires; these final pumps only let the dispatcher flush whatever
        // that last await's own continuation already queued.
        for (var i = 0; i < 3; i++)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void AssertGoldenTree(string kind, Control editor)
    {
        var tree = EditorAutomationTreeWalker.Walk(editor);
        var path = GoldenPath(kind);

        if (!File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, tree);
            return; // First capture — recorded to source for the developer to review and commit; every subsequent run asserts against it.
        }

        // Line endings are not part of the contract: the walker emits LF, the goldens are committed LF, and a
        // checkout under core.autocrlf=true (this machine, the hosted Windows runners) reads them back CRLF.
        var expected = NormaliseLineEndings(File.ReadAllText(path));
        Assert.Equal(expected, NormaliseLineEndings(tree));
    }

    private static string NormaliseLineEndings(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string GoldenPath(string kind) =>
        Path.Combine(DesktopTestHelpers.RepositoryRoot, "tests", "Tempest.Desktop.Tests", "Editors", "Golden", $"{kind}.txt");

    private static async Task<ProjectExplorerNode?> FindFirstObjectNodeOfKindAsync(IProjectExplorer explorer, IReadOnlyList<ProjectExplorerNode> nodes, string kind)
    {
        foreach (var node in nodes)
        {
            if (node.NodeType == ProjectExplorerNodeType.Object && node.Kind == kind)
                return node;

            if (node.HasChildren)
            {
                var found = await FindFirstObjectNodeOfKindAsync(explorer, await explorer.GetChildrenAsync(node.Id), kind);
                if (found is not null)
                    return found;
            }
        }

        return null;
    }
}
