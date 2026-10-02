using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Expenses;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero.Sync;
using Tempest.Core.Projects;
using Tempest.Core.PurchaseOrders;
using Tempest.Core.Quotations;
using Tempest.Core.ReferenceData;
using Tempest.Desktop.Documents;
using Tempest.Desktop.Documents.Invoicing;
using Tempest.Desktop.Documents.PurchaseOrders;
using Tempest.Desktop.Quotations;
using Tempest.Desktop.Tests.Quotations;
using Tempest.Desktop.Views;
using Tempest.Workspace.Projects;

namespace Tempest.Desktop.Tests.Xero;

/// <summary>
/// `v0.24.0` U3 on the real views over a real workspace: the Xero badge on
/// the Quotes list, the project's Quote tab, Invoicing (requests and
/// expenses) and Purchase orders; Export (quote, invoice) and Issue (purchase
/// order) keep the rendered PDF on the record — the same bytes the export
/// saved, under the file name it always had — so X6's
/// <see cref="AttachmentXeroDocumentFileSource"/> uploads exactly that; and
/// the expense prompt's optional supplier and supplier invoice number (X5,
/// Q3/Q4).
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class BadgeViewsTests
{
    private static readonly TimeProvider FixedClock = new BadgeTestKit.PinnedClock(new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero));

    [AvaloniaFact]
    public async Task QuotesList_ShowsEachQuotationsBadge_AndExportKeepsTheSavedBytesOnAnIssuedQuote()
    {
        await using var fixture = await Fixture.StartAsync();
        var (quoteId, reference) = await fixture.ApprovedQuoteAsync();
        var fake = new FakeXeroBadgeSource();
        var filePicker = new StubFilePicker();
        var view = new QuotesView(
            fixture.Domain, fixture.Resolve<ICommandDispatcher>(), () => fixture.ProjectId, fixture.Resolve<IOrganisationCatalog>(),
            fixture.Host.ProjectDirectory!, new ProjectPicker(fixture.Host.ProjectDirectory!), filePicker, new QuotationSheetRenderer(),
            () => "Issuer", () => "TempestOS test", (_, _) => { }, FixedClock)
        {
            XeroBadges = fake,
        };
        fixture.Show(view);
        await view.RefreshAsync();

        // An approved R1 is issued: its badge offers Send to Xero (Q8) while Not sent.
        var badge = view.GetLogicalDescendants().OfType<XeroSyncBadgeControl>().First(b => b.Reference == reference);
        Assert.Equal("Xero: Not sent", badge.Text);
        Assert.True(badge.OffersSendToXero);
        Assert.Contains(XeroDocumentRef.For(XeroDocumentKind.Quote, quoteId), fake.Read);

        // Export: the same file name and the same bytes, now also on the quotation.
        var saved = await fixture.ExportAsync(view, filePicker, $"Export {reference}");
        Assert.Equal($"{reference}-R1-quote.pdf", filePicker.SaveRequests[^1].SuggestedFileName);
        var file = await fixture.WaitForFileAsync(XeroDocumentRef.For(XeroDocumentKind.Quote, quoteId));
        Assert.Equal($"{reference}.pdf", file.FileName);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.Equal(saved, file.Content.ToArray());

        // An unchanged re-export adds nothing.
        await fixture.ExportAsync(view, filePicker, $"Export {reference}");
        Assert.Single(await PdfAttachmentsAsync(fixture.Domain, quoteId));
    }

    [AvaloniaFact]
    public async Task QuotesList_ExportOfADraft_KeepsNothing_SoXeroNeverGetsADraftSheet()
    {
        await using var fixture = await Fixture.StartAsync();
        var quotations = fixture.Resolve<IQuotationService>();
        var created = await quotations.CreateAsync(fixture.ProjectId);
        Assert.True(created.Succeeded, created.Reason);
        Assert.True((await quotations.AddLineAsync(created.Quotation!.Id, "Survey", null, null, new Money(500m, CurrencyCode.Gbp))).Succeeded);
        var reference = created.Quotation.Reference;
        var filePicker = new StubFilePicker();
        var view = new QuotesView(
            fixture.Domain, fixture.Resolve<ICommandDispatcher>(), () => fixture.ProjectId, fixture.Resolve<IOrganisationCatalog>(),
            fixture.Host.ProjectDirectory!, new ProjectPicker(fixture.Host.ProjectDirectory!), filePicker, new QuotationSheetRenderer(),
            () => "Issuer", () => "TempestOS test", (_, _) => { }, FixedClock)
        {
            XeroBadges = new FakeXeroBadgeSource(),
        };
        fixture.Show(view);
        await view.RefreshAsync();

        Assert.False(view.GetLogicalDescendants().OfType<XeroSyncBadgeControl>().Single().OffersSendToXero);

        await fixture.ExportAsync(view, filePicker, $"Export {reference}");
        Assert.Empty(await PdfAttachmentsAsync(fixture.Domain, created.Quotation.Id));
    }

    [AvaloniaFact]
    public async Task QuotesList_WithNoXero_ShowsNoBadge()
    {
        await using var fixture = await Fixture.StartAsync();
        await fixture.ApprovedQuoteAsync();
        var view = new QuotesView(
            fixture.Domain, fixture.Resolve<ICommandDispatcher>(), () => fixture.ProjectId, fixture.Resolve<IOrganisationCatalog>(),
            fixture.Host.ProjectDirectory!, new ProjectPicker(fixture.Host.ProjectDirectory!), new StubFilePicker(), new QuotationSheetRenderer(),
            () => "Issuer", () => "TempestOS test", (_, _) => { }, FixedClock);
        fixture.Show(view);
        await view.RefreshAsync();

        Assert.Empty(view.GetLogicalDescendants().OfType<XeroSyncBadgeControl>());
    }

    [AvaloniaFact]
    public async Task ProjectQuoteTab_ShowsTheSelectedQuotationsBadge_AndExportKeepsTheSavedBytes()
    {
        await using var fixture = await Fixture.StartAsync();
        var (quoteId, reference) = await fixture.ApprovedQuoteAsync();
        var fake = new FakeXeroBadgeSource();
        fake.Set(XeroDocumentRef.For(XeroDocumentKind.Quote, quoteId), new XeroSyncStatus(XeroSyncBadge.InXero, null, reference, "SENT"));
        var filePicker = new StubFilePicker();
        var view = new ProjectQuoteView(
            fixture.Domain, fixture.Resolve<ICommandDispatcher>(), fixture.Resolve<ICommandRegistry>(), () => fixture.ProjectId,
            fixture.Resolve<IOrganisationCatalog>(), (_, _) => { }, filePicker, new QuotationSheetRenderer(), () => "Issuer", () => "TempestOS test", FixedClock)
        {
            XeroBadges = fake,
        };
        fixture.Show(view);
        await view.SelectQuoteAsync(quoteId);

        Assert.NotNull(view.XeroBadge);
        Assert.Equal("Xero: Sent in Xero", view.XeroBadge!.Text);
        Assert.Equal($"Xero status for {reference}", AutomationProperties.GetName(view.XeroBadge));

        var saved = await fixture.ExportAsync(view, filePicker, $"Export {reference}");
        Assert.Equal($"{reference}-R1-quote.pdf", filePicker.SaveRequests[^1].SuggestedFileName);
        var file = await fixture.WaitForFileAsync(XeroDocumentRef.For(XeroDocumentKind.Quote, quoteId));
        Assert.Equal(saved, file.Content.ToArray());
    }

    [AvaloniaFact]
    public async Task Invoicing_ShowsInvoiceAndExpenseBadges_AndExportOfASentInvoiceKeepsTheSavedBytes()
    {
        await using var fixture = await Fixture.StartAsync();
        await fixture.WithClientAsync();
        var expenses = fixture.Resolve<IExpenseService>();
        var invoicing = fixture.Resolve<IInvoicingService>();
        var forInvoice = await expenses.RecordAsync(fixture.ProjectId, new DateOnly(2026, 9, 30), "Rail fare", ExpenseCategory.Travel, new Money(40m, CurrencyCode.Gbp), new Money(8m, CurrencyCode.Gbp), billable: true);
        Assert.True(forInvoice.Succeeded, forInvoice.Reason);
        var raised = await invoicing.RaiseFromExpenseAsync(forInvoice.Expense!.Id);
        Assert.True(raised.Succeeded, raised.Reason);
        var sent = await invoicing.SendAsync(raised.Request!.Id);
        Assert.True(sent.Succeeded, sent.Reason);
        var unbilled = await expenses.RecordAsync(fixture.ProjectId, new DateOnly(2026, 10, 1), "Hotel", ExpenseCategory.Subsistence, new Money(90m, CurrencyCode.Gbp), new Money(18m, CurrencyCode.Gbp), billable: true);
        Assert.True(unbilled.Succeeded, unbilled.Reason);
        var request = (InvoiceRequest)(await fixture.Domain.Repository.FindAsync(raised.Request.Id))!;
        Assert.Equal(InvoiceRequestStatus.Sent, request.Status);

        var invoiceRef = XeroDocumentRef.For(XeroDocumentKind.Invoice, request.Id);
        var fake = new FakeXeroBadgeSource();
        fake.Set(invoiceRef, new XeroSyncStatus(XeroSyncBadge.InXeroDraft, "Draft in Xero — review and send from Xero.", "INV-1", "DRAFT"));
        var filePicker = new StubFilePicker();
        var view = new InvoicingView(
            fixture.Domain, fixture.Resolve<ICommandRegistry>(), () => fixture.ProjectId, (_, _) => { },
            fixture.Resolve<IOrganisationCatalog>(), new DocumentExporter(filePicker), new InvoiceDocumentRenderer(), () => "Issuer", () => "TempestOS test")
        {
            XeroBadges = fake,
        };
        fixture.Show(view);
        await view.RefreshAsync();

        var invoiceBadge = view.GetLogicalDescendants().OfType<XeroSyncBadgeControl>().Single(b => b.Document == invoiceRef);
        Assert.Equal("Xero: Draft in Xero — review and send from Xero", invoiceBadge.Text);
        Assert.False(invoiceBadge.OffersSendToXero);

        var expenseBadge = view.GetLogicalDescendants().OfType<XeroSyncBadgeControl>()
            .Single(b => b.Document == XeroDocumentRef.For(XeroDocumentKind.ExpenseBill, unbilled.Expense!.Id));
        Assert.Equal("Xero: Not sent", expenseBadge.Text);
        Assert.True(expenseBadge.OffersSendToXero);

        var saved = await fixture.ExportAsync(view, filePicker, $"Export invoice {request.DisplayName}");
        Assert.EndsWith("-invoice.pdf", filePicker.SaveRequests[^1].SuggestedFileName, StringComparison.Ordinal); // the exporter's own naming, unchanged
        var file = await fixture.WaitForFileAsync(invoiceRef);
        Assert.Equal(saved, file.Content.ToArray());
        Assert.Contains(await PdfAttachmentsAsync(fixture.Domain, request.Id), a => a.FileName == Path.GetFileName(fixture.LastExportPath)); // kept under the name it was saved as
    }

    [AvaloniaFact]
    public async Task Invoicing_ExportOfADraftRequest_KeepsNothing()
    {
        await using var fixture = await Fixture.StartAsync();
        await fixture.WithClientAsync();
        var expense = await fixture.Resolve<IExpenseService>().RecordAsync(
            fixture.ProjectId, new DateOnly(2026, 9, 30), "Rail fare", ExpenseCategory.Travel, new Money(40m, CurrencyCode.Gbp), new Money(8m, CurrencyCode.Gbp), billable: true);
        var raised = await fixture.Resolve<IInvoicingService>().RaiseFromExpenseAsync(expense.Expense!.Id);
        Assert.True(raised.Succeeded, raised.Reason);
        var filePicker = new StubFilePicker();
        var view = new InvoicingView(
            fixture.Domain, fixture.Resolve<ICommandRegistry>(), () => fixture.ProjectId, (_, _) => { },
            fixture.Resolve<IOrganisationCatalog>(), new DocumentExporter(filePicker), new InvoiceDocumentRenderer(), () => "Issuer", () => "TempestOS test")
        {
            XeroBadges = new FakeXeroBadgeSource(),
        };
        fixture.Show(view);
        await view.RefreshAsync();

        await fixture.ExportAsync(view, filePicker, $"Export invoice {raised.Request!.DisplayName}");
        Assert.Empty(await PdfAttachmentsAsync(fixture.Domain, raised.Request.Id));
    }

    [AvaloniaFact]
    public async Task PurchaseOrders_IssueKeepsTheRenderedPdfOnTheOrder_AndAnIssuedOrderShowsItsBadge()
    {
        await using var fixture = await Fixture.StartAsync();
        var orders = fixture.Resolve<IPurchaseOrderService>();
        var created = await orders.CreateAsync(fixture.ProjectId, supplierOrganisationId: null, notes: "Deliver to site office.");
        Assert.True(created.Succeeded, created.Reason);
        var orderId = created.Order!.Id;
        Assert.True((await orders.AddLineAsync(orderId, "Steel angle", 4m, new Money(50m, CurrencyCode.Gbp), VatRate.Standard)).Succeeded);
        var reference = created.Order.Reference;

        var fake = new FakeXeroBadgeSource();
        var view = new PurchaseOrdersView(
            fixture.Domain, fixture.Resolve<ICommandDispatcher>(), fixture.Resolve<ICommandRegistry>(), () => fixture.ProjectId,
            new ProjectPicker(fixture.Host.ProjectDirectory!), new InputDialog(), new PurchaseOrderLinePrompt(), (_, _) => { })
        {
            ParameterPrompt = (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(new Dictionary<string, string>()),
            XeroBadges = fake,
            PurchaseOrderRenderer = new PurchaseOrderDocumentRenderer(),
            Organisations = fixture.Resolve<IOrganisationCatalog>(),
            IssuerName = () => "Issuer",
            ApplicationVersionText = () => "TempestOS test",
            Clock = FixedClock,
        };
        fixture.Show(view);
        await view.RefreshAsync();

        // A draft is not in Xero's scope: no badge.
        Assert.Empty(view.GetLogicalDescendants().OfType<XeroSyncBadgeControl>());

        var issue = view.GetLogicalDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == $"Issue {reference}");
        issue.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        var document = XeroDocumentRef.For(XeroDocumentKind.PurchaseOrder, orderId);
        var file = await fixture.WaitForFileAsync(document);
        Assert.Equal($"{reference}.pdf", file.FileName);
        Assert.Equal("%PDF"u8.ToArray(), file.Content[..4].ToArray());
        var attachment = Assert.Single(await PdfAttachmentsAsync(fixture.Domain, orderId));
        Assert.Equal($"{reference}-purchase-order.pdf", attachment.FileName);
        Assert.Contains("Deliver to site office.", PdfTextExtractor.ExtractText(file.Content.ToArray()), StringComparison.Ordinal);

        await BadgeControlTests.WaitUntilAsync(() => view.GetLogicalDescendants().OfType<XeroSyncBadgeControl>().Any(b => b.Document == document && b.Status is not null));
        var badge = view.GetLogicalDescendants().OfType<XeroSyncBadgeControl>().Single(b => b.Document == document);
        Assert.Equal("Xero: Not sent", badge.Text);
        Assert.True(badge.OffersSendToXero);
    }

    [AvaloniaFact]
    public async Task ExpensePrompt_CollectsTheOptionalSupplierAndInvoiceNumber_AndSavesThemOnTheExpense()
    {
        await using var fixture = await Fixture.StartAsync();
        var expenses = fixture.Resolve<IExpenseService>();
        var prompt = new ExpenseEntryPrompt(fixture.Domain)
        {
            PickSupplierAsync = _ => Task.FromResult<string?>("ORG-SUPPLIER-1"),
            ExpenseService = expenses,
        };
        fixture.Show(prompt);

        var pending = prompt.PromptAsync(fixture.ProjectId);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(ExpenseEntryPrompt.NoSupplierText, Find<TextBlock>(prompt, "Supplier").Text);

        Find<TextBox>(prompt, "Description").Text = "Fixings";
        Find<NumericUpDown>(prompt, "Net amount").Value = 25m;
        Find<Button>(prompt, "Supplier…").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await BadgeControlTests.WaitUntilAsync(() => Find<TextBlock>(prompt, "Supplier").Text == "Supplier: ORG-SUPPLIER-1");
        Find<TextBox>(prompt, "Supplier invoice number").Text = "  SUP-778  ";
        Find<Button>(prompt, "Record").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        var input = await pending;
        Assert.NotNull(input);
        Assert.Equal("ORG-SUPPLIER-1", input!.SupplierOrganisationId);
        Assert.Equal("SUP-778", input.SupplierInvoiceNumber);

        var recorded = await expenses.RecordAsync(input.ProjectId, input.Date, input.Description, input.Category, input.NetAmount, input.VatAmount, input.Billable);
        Assert.Null(await prompt.ApplyPurchasingDetailsAsync(recorded.Expense!.Id, input));

        var expense = (ProjectExpense)(await fixture.Domain.Repository.FindAsync(recorded.Expense.Id))!;
        Assert.Equal("ORG-SUPPLIER-1", expense.SupplierOrganisationId);
        Assert.Equal("SUP-778", expense.SupplierInvoiceNumber);

        // Neither entered: nothing to save. No service: said so, never silently dropped.
        var plain = input with { SupplierOrganisationId = null, SupplierInvoiceNumber = null };
        Assert.Null(await new ExpenseEntryPrompt(fixture.Domain).ApplyPurchasingDetailsAsync(recorded.Expense.Id, plain));
        Assert.NotNull(await new ExpenseEntryPrompt(fixture.Domain).ApplyPurchasingDetailsAsync(recorded.Expense.Id, input));
    }

    [AvaloniaFact]
    public async Task ExpensePrompt_WithNoSupplierPicker_LeavesSupplierUnavailable_AndRecordsNone()
    {
        await using var fixture = await Fixture.StartAsync();
        var prompt = new ExpenseEntryPrompt(fixture.Domain);
        fixture.Show(prompt);

        var pending = prompt.PromptAsync(fixture.ProjectId);
        Dispatcher.UIThread.RunJobs();
        Assert.False(Find<Button>(prompt, "Supplier…").IsEnabled);
        Find<TextBox>(prompt, "Description").Text = "Parking";
        Find<Button>(prompt, "Record").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        var input = await pending;
        Assert.NotNull(input);
        Assert.False(input!.HasPurchasingDetails);
    }

    private static T Find<T>(Control root, string automationName) where T : Control =>
        root.GetLogicalDescendants().OfType<T>().Single(c => AutomationProperties.GetName(c) == automationName);

    private static async Task<IReadOnlyList<IAttachment>> PdfAttachmentsAsync(EngineeringDomainContext domain, Guid id)
    {
        var record = (IHasAttachments)(await domain.Repository.FindAsync(id))!;
        return [.. (await record.GetAttachmentsAsync()).Where(a => a.ContentType == "application/pdf")];
    }

    /// <summary>A started workspace with one project, shut down and cleaned up on dispose.</summary>
    private sealed class Fixture : IAsyncDisposable
    {
        private readonly List<string> _paths = [];
        private Window? _window;

        private Fixture(WorkspaceHost host) => Host = host;

        public WorkspaceHost Host { get; }

        public Guid ProjectId { get; private set; }

        public string? LastExportPath { get; private set; }

        public EngineeringDomainContext Domain => Resolve<EngineeringDomainContext>();

        public static async Task<Fixture> StartAsync()
        {
            var fixture = new Fixture(new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath()));
            await fixture.Host.StartAsync();
            var project = await fixture.Host.ProjectDirectory!.CreateAsync($"P-XB-{Guid.NewGuid():N}"[..14], "Xero Badge Project");
            fixture.ProjectId = project.Id;
            return fixture;
        }

        public T Resolve<T>() where T : class => (T)Host.Services!.GetService(typeof(T));

        public void Show(Control content)
        {
            _window?.Close();
            _window = new Window { Width = 1400, Height = 900, Content = content };
            _window.Show();
            Dispatcher.UIThread.RunJobs();
        }

        /// <summary>Records a client on the project, so an invoice can be raised.</summary>
        public async Task WithClientAsync()
        {
            var organisationId = $"ORG-XB-{Guid.NewGuid():N}"[..14];
            await Resolve<IOrganisationCatalog>().RegisterAsync(
                organisationId, new Organisation { Reference = organisationId, Name = "Xero Badge Client Ltd" }, ReferenceProvenance.Unknown);
            Assert.True((await Resolve<IProjectCommercialService>().SetClientAsync(ProjectId, organisationId)).Succeeded);

            var rateCards = Resolve<IRateCardCatalog>();
            var rateCardId = $"RC-XB-{Guid.NewGuid():N}"[..14];
            var card = new RateCard
            {
                Code = rateCardId,
                Name = "Xero Badge Rate Card",
                EffectivePeriod = new EffectivePeriod(new DateOnly(2020, 1, 1), null),
                Currency = CurrencyCode.Gbp,
                Governance = new BusinessGovernanceFacts { Ownership = new BusinessOwnership("owner-1", "Owner") },
                Entries = [new RateCardEntry("SVC-1", "Senior Engineering", PricingBasis.Hourly, new Money(150m, CurrencyCode.Gbp), Grade: "Senior")],
            };
            await rateCards.RegisterAsync(rateCardId, card, new ReferenceProvenance(SourceOrganisation: "Test Org", SourceDocument: "Test Doc"));
            await Host.ReferenceReview!.VerifyAsync(rateCards, rateCardId, new Tempest.Core.ReferenceData.Review.ReferenceReviewStatement("Consulted for the badge test."));
            await Host.ReferenceReview!.ReleaseAsync(rateCards, rateCardId, "Released for the badge test.");
            Assert.True((await Resolve<IProjectCommercialService>().PinRateCardAsync(ProjectId, rateCardId)).Succeeded);
        }

        public async Task<(Guid Id, string Reference)> ApprovedQuoteAsync()
        {
            var quotations = Resolve<IQuotationService>();
            var created = await quotations.CreateAsync(ProjectId);
            Assert.True(created.Succeeded, created.Reason);
            var id = created.Quotation!.Id;
            Assert.True((await quotations.AddLineAsync(id, "Survey", null, null, new Money(500m, CurrencyCode.Gbp))).Succeeded);
            Assert.True((await quotations.SubmitForReviewAsync(id)).Succeeded);
            var approved = await QuotationReviewSupport.AsReviewerAsync(Host, () => quotations.ApproveAsync(id));
            Assert.True(approved.Succeeded, approved.Reason);
            return (id, created.Quotation.Reference);
        }

        /// <summary>Clicks the export button named <paramref name="automationName"/>, waits for the file, and returns the bytes saved.</summary>
        public async Task<byte[]> ExportAsync(Control view, StubFilePicker filePicker, string automationName)
        {
            var path = Path.Combine(Path.GetTempPath(), $"xero-badge-export-{Guid.NewGuid():N}.pdf");
            _paths.Add(path);
            LastExportPath = path;
            filePicker.SetNextSavePath(path);
            var before = Status(view);

            view.GetLogicalDescendants().OfType<Button>().First(b => AutomationProperties.GetName(b) == automationName)
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            // Done once the view reports (it reports after the attach).
            await BadgeControlTests.WaitUntilAsync(() => File.Exists(path) && Status(view) != before && Status(view).Contains("Exported", StringComparison.Ordinal));
            return await File.ReadAllBytesAsync(path);
        }

        public async Task<XeroDocumentFile> WaitForFileAsync(XeroDocumentRef document)
        {
            var source = new AttachmentXeroDocumentFileSource(Domain);
            XeroDocumentFile? file = null;
            var deadline = DesktopTestHelpers.Deadline(10);
            while (file is null && DateTime.UtcNow < deadline)
            {
                file = await source.FindAsync(document);
                if (file is null)
                {
                    Dispatcher.UIThread.RunJobs();
                    await Task.Delay(5);
                }
            }

            Assert.NotNull(file);
            return file!;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                _window?.Close();
                Dispatcher.UIThread.RunJobs();
            }
            finally
            {
                await Host.ShutdownAsync();
                await Host.DisposeAsync();
                foreach (var path in _paths)
                {
                    try
                    {
                        File.Delete(path);
                    }
                    catch (IOException)
                    {
                        // Best-effort cleanup only.
                    }
                }
            }
        }

        // Every view reports into a caption TextBlock and ActionCompleted; the
        // newest caption text that mentions an export is the signal.
        private static string Status(Control view) =>
            string.Join("|", view.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).Where(t => t is not null && t.Contains("Export", StringComparison.Ordinal)));
    }
}
