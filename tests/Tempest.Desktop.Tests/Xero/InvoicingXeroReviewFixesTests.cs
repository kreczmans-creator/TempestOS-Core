using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Quotations;
using Tempest.Desktop.Documents.Invoicing;
using Tempest.Desktop.Tests.Documents;
using Tempest.Desktop.Tests.Quotations;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests.Xero;

/// <summary>
/// `v0.24.0` review-board fixes in the Invoicing area with Xero as the
/// connector, headless over a real <see cref="WorkspaceHost"/> (its Fake
/// connector stands in for the send; the badge source is a fake): Sent rows
/// offer <b>Edit lines</b> and <b>Void</b> (M4); a refusal names the request,
/// never its raw id, and an unreachable Xero says the send was queued (m13);
/// the re-authorise pointer names the real Settings section (m12); a quote
/// invoiced in Xero warns before Send (m14); the invoice PDF carries its
/// number and send date (m17).
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class InvoicingXeroReviewFixesTests
{
    private const string ClientOrganisationId = "ORG-XERO-FIXES";

    [AvaloniaFact]
    public async Task ASentRow_OffersEditLinesAndVoid_OnlyWithXero_AndEditLinesRunsTheCommand_NamingTheRequest()
    {
        await using var fixture = await Fixture.StartAsync();
        var sent = await fixture.RequestAsync(InvoiceRequestStatus.Sent, externalId: "EXT-1", externalInvoiceNumber: "INV-0001");

        // Without Xero: neither.
        var plain = fixture.View(xero: null);
        await plain.RefreshAsync();
        Assert.Null(FindButtonOrNull(plain, $"Edit lines of {sent.DisplayName}"));
        Assert.Null(FindButtonOrNull(plain, $"Void {sent.DisplayName}"));

        var view = fixture.View(new ReviewFixBadgeSource());
        await view.RefreshAsync();
        Assert.NotNull(FindButtonOrNull(view, $"Void {sent.DisplayName}"));
        var panel = view.GetLogicalDescendants().OfType<StackPanel>().Single(p => AutomationProperties.GetName(p) == $"Edit lines panel for {sent.DisplayName}");
        Assert.False(panel.IsVisible);

        Click(view, $"Edit lines of {sent.DisplayName}");
        Assert.True(panel.IsVisible);
        var quantity = view.GetLogicalDescendants().OfType<TextBox>().Single(t => AutomationProperties.GetName(t) == InvoicingView.LineFieldName(1, "quantity", sent.DisplayName));
        Assert.Equal("1", quantity.Text);
        quantity.Text = "3";

        string? reported = null;
        view.ActionCompleted += (message, _) => reported = message;
        Click(view, $"Save lines of {sent.DisplayName}");
        await UntilAsync(() => reported is not null);

        // The Fake connector holds no draft to edit: refused, with the request's name — never its raw id.
        Assert.Contains($"'{sent.DisplayName}' is Sent through Fake", reported, StringComparison.Ordinal);
        Assert.DoesNotContain(sent.Id.ToString(), reported, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1m, Assert.Single(((InvoiceRequest)(await fixture.Domain.Repository.FindAsync(sent.Id))!).Lines).Quantity);
    }

    [AvaloniaFact]
    public async Task SendingWhileXeroIsUnreachable_SaysTheSendIsQueued()
    {
        await using var fixture = await Fixture.StartAsync();
        var draft = await fixture.RequestAsync(InvoiceRequestStatus.Draft);
        fixture.Connector.ScriptNextCreate(ConnectorOutcome.Unavailable, "unreachable");

        var view = fixture.View(new ReviewFixBadgeSource());
        await view.RefreshAsync();
        string? reported = null;
        view.ActionCompleted += (message, _) => reported = message;
        Click(view, $"Send {draft.DisplayName}");
        await UntilAsync(() => reported is not null);

        Assert.Equal(InvoicingView.XeroQueuedText, reported);
        Assert.Contains("queued, and will send when Xero is reachable", reported, StringComparison.Ordinal);
        Assert.Contains("nothing is emailed", reported, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task TheReauthoriseNote_PointsToTheRealSettingsSection()
    {
        await using var fixture = await Fixture.StartAsync();
        await fixture.RequestAsync(InvoiceRequestStatus.Reauthorise, lastError: "expired");

        var xero = fixture.View(new ReviewFixBadgeSource());
        await xero.RefreshAsync();
        Assert.Contains(xero.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Re-authorise in Settings → Xero to continue.");

        var plain = fixture.View(xero: null);
        await plain.RefreshAsync();
        Assert.Contains(plain.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == "Re-authorise in Settings → Connector authorisation to continue.");
        Assert.DoesNotContain(plain.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.Contains("Settings > Invoicing", StringComparison.Ordinal) == true);
    }

    [AvaloniaFact]
    public async Task AQuoteInvoicedInXero_WarnsBeforeSend_AndNothingIsSentUnlessConfirmed()
    {
        await using var fixture = await Fixture.StartAsync();
        var quotations = (IQuotationService)fixture.Host.Services!.GetService(typeof(IQuotationService));
        var quote = await quotations.CreateAsync(fixture.ProjectId, reference: "Q-2026-077");
        Assert.True(quote.Succeeded, quote.Reason);
        var draft = await fixture.RequestAsync(InvoiceRequestStatus.Draft);

        var source = new ReviewFixBadgeSource();
        source.Set(XeroDocumentRef.For(XeroDocumentKind.Quote, quote.Quotation!.Id), new XeroSyncStatus(XeroSyncBadge.InXero, null, "QU-0077", "INVOICED"));
        source.Confirmations.Enqueue(false);
        var view = fixture.View(source);
        await view.RefreshAsync();

        string? reported = null;
        view.ActionCompleted += (message, _) => reported = message;
        Click(view, $"Send {draft.DisplayName}");
        await UntilAsync(() => reported is not null);

        Assert.Contains("QU-0077", source.ConfirmMessages.Single(), StringComparison.Ordinal);
        Assert.Contains("billed twice", source.ConfirmMessages.Single(), StringComparison.Ordinal);
        Assert.Equal("Not done: Xero shows quote QU-0077 as already invoiced.", reported);
        Assert.DoesNotContain(fixture.Connector.Calls, c => c.Member == nameof(FakeInvoicingConnector.CreateDraftInvoiceAsync));
        Assert.Equal(InvoiceRequestStatus.Draft, ((InvoiceRequest)(await fixture.Domain.Repository.FindAsync(draft.Id))!).Status);

        // Confirmed: it goes ahead.
        reported = null;
        source.Confirmations.Enqueue(true);
        Click(view, $"Send {draft.DisplayName}");
        await UntilAsync(() => reported is not null && !reported.StartsWith("Not done", StringComparison.Ordinal));
        Assert.Contains(fixture.Connector.Calls, c => c.Member == nameof(FakeInvoicingConnector.CreateDraftInvoiceAsync));
    }

    [AvaloniaFact]
    public void TheInvoicePdf_CarriesTheInvoiceNumberAndTheSendDate()
    {
        var model = InvoiceDocumentModelFixtures.Minimal() with { InvoiceNumber = "ACME1-BRIDG1-INV-007", SentDate = new DateOnly(2026, 10, 2) };

        Assert.Equal("Invoice number: ACME1-BRIDG1-INV-007  •  Sent: 2026-10-02", InvoiceDocumentRenderer.NumberLine(model));
        var text = PdfTextExtractor.ExtractText(new InvoiceDocumentRenderer().Render(model).ToArray());
        Assert.Contains("ACME1-BRIDG1-INV-007", text, StringComparison.Ordinal);
        Assert.Contains("Sent: 2026-10-02", text, StringComparison.Ordinal);

        Assert.Null(InvoiceDocumentRenderer.NumberLine(InvoiceDocumentModelFixtures.Minimal()));
    }

    // ------------------------------------------------------------------

    private static Button? FindButtonOrNull(Control root, string automationName) =>
        root.GetLogicalDescendants().OfType<Button>().SingleOrDefault(b => AutomationProperties.GetName(b) == automationName);

    private static void Click(Control root, string automationName)
    {
        var button = FindButtonOrNull(root, automationName) ?? throw new Xunit.Sdk.XunitException($"No button '{automationName}'.");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        var deadline = DesktopTestHelpers.Deadline(10);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(5);
        }

        Assert.True(condition(), "The condition was not met in time.");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly List<Window> _windows = [];

        private Fixture(WorkspaceHost host, Guid projectId)
        {
            Host = host;
            ProjectId = projectId;
        }

        public WorkspaceHost Host { get; }

        public Guid ProjectId { get; }

        public EngineeringDomainContext Domain => (EngineeringDomainContext)Host.Services!.GetService(typeof(EngineeringDomainContext));

        public FakeInvoicingConnector Connector => (FakeInvoicingConnector)Host.Services!.GetService(typeof(IInvoicingConnector));

        public static async Task<Fixture> StartAsync()
        {
            var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
            await host.StartAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-XFIX", "Xero Fixes Project");
            return new Fixture(host, project.Id);
        }

        public InvoicingView View(IXeroBadgeSource? xero)
        {
            var registry = (ICommandRegistry)Host.Services!.GetService(typeof(ICommandRegistry));
            var view = new InvoicingView(Domain, registry, () => ProjectId, (_, _) => { })
            {
                XeroBadges = xero,
                ParameterPrompt = (_, parameters, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(
                    parameters.ToDictionary(p => p.Name, p => p.DefaultValue ?? string.Empty)),
            };
            var window = new Window { Width = 1400, Height = 1000, Content = view };
            window.Show();
            _windows.Add(window);
            return view;
        }

        public async Task<InvoiceRequest> RequestAsync(
            InvoiceRequestStatus status, string? externalId = null, string? externalInvoiceNumber = null, string? lastError = null)
        {
            var domain = Domain;
            IReadOnlyList<InvoiceRequestLine> lines =
                [new InvoiceRequestLine(Tempest.Core.Deliverables.DeliverableCompletion.CanonicalKind, Guid.NewGuid(), "Design work", 1m, new Money(500m, CurrencyCode.Gbp), new Money(500m, CurrencyCode.Gbp))];
            var created = await new EngineeringObjectFactory<InvoiceRequest>(
                InvoiceRequest.CanonicalKind, domain,
                (doc, rev) => new InvoiceRequest(
                    doc, rev, domain, identifier: null, $"Xero fixes request — {status}", EngineeringObjectMetadata.Empty,
                    ClientOrganisationId, purchaseOrderReference: null, CurrencyCode.Gbp, lines, new Money(500m, CurrencyCode.Gbp), status,
                    externalId, externalInvoiceNumber, null, null, null, lastError, status == InvoiceRequestStatus.Draft ? null : "Fake",
                    status == InvoiceRequestStatus.Draft ? null : DateTimeOffset.UtcNow.AddDays(-1),
                    dueOn: DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30)))
                .CreateAsync("Fixture request.", CancellationToken.None);

            if (created is IHasParent hasParent)
                await hasParent.MoveAsync(ProjectId, CancellationToken.None);

            return (InvoiceRequest)created;
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var window in _windows)
                window.Close();
            Dispatcher.UIThread.RunJobs();
            await Host.ShutdownAsync();
            await Host.DisposeAsync();
        }
    }
}
