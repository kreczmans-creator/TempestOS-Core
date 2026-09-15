using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.Commands;
using Tempest.Core.Deliverables;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Invoicing;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `WP 19.10D`: the Business → Invoices grouping the Product Owner
/// sketched (Product Owner comment item 6, sheet 9; `brief-19.7A.md` §5) —
/// every <see cref="InvoiceRequestStatus"/> this platform ever actually
/// persists lands in exactly one of New / Available to invoice / Sent /
/// Outstanding-Overdue / Closed, and an unbilled
/// <see cref="DeliverableCompletion"/> appears in Available to invoice
/// only when no live request already carries it.
/// </summary>
/// <remarks>
/// Every request and completion here is a real, persisted domain object —
/// built directly through <see cref="EngineeringObjectFactory{T}"/> (the
/// same factory <c>InvoicingService</c> itself uses to raise a request)
/// rather than driven through the Fake connector, so every
/// <see cref="InvoiceRequestStatus"/> bar
/// <see cref="InvoiceRequestStatus.Unavailable"/> (never itself a stored
/// status, per its own remarks) and an arbitrary
/// <see cref="InvoiceRequest.SentAtUtc"/> are reachable directly, without
/// waiting on real time or a scripted connector outcome to reach them.
/// <see cref="InvoiceRequestStatus.Sending"/> — otherwise unreachable
/// through the public service, since a real send moves through it and out
/// again inside one call — is included the same way.
/// </remarks>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class InvoicesGroupingTests
{
    private const string ClientOrganisationId = "ORG-GROUPING-FIXTURE";

    private static readonly string[] GroupNames =
        ["New", "Available to invoice", "Sent", "Outstanding / Overdue", "Closed"];

    /// <summary>
    /// One request per persistable status, plus the extra scenario the
    /// brief names — a Sent request older than thirty days — plus two
    /// completions, one carried by a live request's own line and one not:
    /// every row lands in its own group, and only its own group.
    /// </summary>
    [AvaloniaFact]
    public async Task EveryStatusAndCompletion_LandsInItsOwnGroup()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();

            var domainContext = Resolve<EngineeringDomainContext>(host);
            var project = await host.ProjectDirectory!.CreateAsync("P-GROUPING", "Invoices Grouping Project");
            var milestone = await host.ProjectMilestoneWorkflow!.CreateMilestoneAsync(
                project.Id, "M-GROUPING", "Milestone", DateTimeOffset.UtcNow.AddMonths(1));

            // ---- two completions: one carried by a live Draft request's own line, one not ----
            var carriedDeliverable = await host.ProjectMilestoneWorkflow!.CreateDeliverableAsync(project.Id, milestone.Id, "D-CARRIED", "Carried Deliverable");
            var freeDeliverable = await host.ProjectMilestoneWorkflow!.CreateDeliverableAsync(project.Id, milestone.Id, "D-FREE", "Available Deliverable");

            var carriedCompletion = await CreateCompletionAsync(domainContext, project.Id, carriedDeliverable.Id, new DateOnly(2026, 8, 1));
            var freeCompletion = await CreateCompletionAsync(
                domainContext, project.Id, freeDeliverable.Id, new DateOnly(2026, 8, 2), fixedPrice: new Money(250m, CurrencyCode.Gbp));

            var now = DateTimeOffset.UtcNow;

            // ---- one request per persistable status (Unavailable is never stored — its own remarks) ----
            var draft = await CreateRequestAsync(
                domainContext, project.Id, InvoiceRequestStatus.Draft,
                [new InvoiceRequestLine(DeliverableCompletion.CanonicalKind, carriedCompletion.Id, "Carried line", 1m, new Money(250m, CurrencyCode.Gbp), new Money(250m, CurrencyCode.Gbp))]);

            var sending = await CreateRequestAsync(domainContext, project.Id, InvoiceRequestStatus.Sending, OneLine(), connector: "Fake");

            var today = DateOnly.FromDateTime(now.UtcDateTime);

            var sentRecent = await CreateRequestAsync(
                domainContext, project.Id, InvoiceRequestStatus.Sent, OneLine(),
                externalId: "EXT-SENT", externalInvoiceNumber: "INV-SENT", sentAtUtc: now.AddDays(-2), connector: "Fake",
                dueOn: today.AddDays(28));

            var acceptedRecent = await CreateRequestAsync(
                domainContext, project.Id, InvoiceRequestStatus.Accepted, OneLine(),
                externalId: "EXT-ACC", externalInvoiceNumber: "INV-ACC", sentAtUtc: now.AddDays(-3), externalStatus: "AUTHORISED", connector: "Fake",
                dueOn: today.AddDays(27));

            var rejected = await CreateRequestAsync(
                domainContext, project.Id, InvoiceRequestStatus.Rejected, OneLine(), lastError: "Not registered for this client.");

            var voided = await CreateRequestAsync(domainContext, project.Id, InvoiceRequestStatus.Voided, OneLine());

            var unknown = await CreateRequestAsync(
                domainContext, project.Id, InvoiceRequestStatus.Unknown, OneLine(),
                sentAtUtc: now.AddDays(-1), lastError: "The gateway timed out.", connector: "Fake");

            var reauthorise = await CreateRequestAsync(
                domainContext, project.Id, InvoiceRequestStatus.Reauthorise, OneLine(), lastError: "The stored token has expired.", connector: "Fake");

            // ---- the extra scenario: a Sent request past its own due date, unpaid (`TD-180`) ----
            var sentOld = await CreateRequestAsync(
                domainContext, project.Id, InvoiceRequestStatus.Sent, OneLine(),
                externalId: "EXT-OLD", externalInvoiceNumber: "INV-OLD", sentAtUtc: now.AddDays(-45), connector: "Fake",
                dueOn: today.AddDays(-15));

            var commandRegistry = Resolve<ICommandRegistry>(host);
            var view = new InvoicingView(domainContext, commandRegistry, () => project.Id, (_, _) => { });
            await view.RefreshAsync();

            var groups = GroupNames.ToDictionary(name => name, name => FindGroup(view, name));

            AssertOnlyInGroup(groups, "New", draft.Id);
            AssertOnlyInGroup(groups, "Sent", sending.Id);
            AssertOnlyInGroup(groups, "Sent", sentRecent.Id);
            AssertOnlyInGroup(groups, "Sent", acceptedRecent.Id);
            AssertOnlyInGroup(groups, "Outstanding / Overdue", unknown.Id);
            AssertOnlyInGroup(groups, "Outstanding / Overdue", reauthorise.Id);
            AssertOnlyInGroup(groups, "Outstanding / Overdue", sentOld.Id);
            AssertOnlyInGroup(groups, "Closed", rejected.Id);
            AssertOnlyInGroup(groups, "Closed", voided.Id);

            // ---- Available to invoice: the free completion, never the carried one ----
            AssertOnlyInGroup(groups, "Available to invoice", freeCompletion.Id);
            Assert.False(ContainsRow(view, carriedCompletion.Id), "The carried completion must not appear anywhere in this view.");

            // ---- `TD-180` (`WP 20.1B`): a row shows its own terms and due date ----
            var sentText = string.Join(" | ", groups["Sent"].GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));
            Assert.Contains($"Terms {sentRecent.PaymentTerms.DisplayName()}", sentText, StringComparison.Ordinal);
            Assert.Contains($"Due {sentRecent.DueOn:yyyy-MM-dd}", sentText, StringComparison.Ordinal);

            // ---- captions state the counts and, for Outstanding, the own-due-date rule (`TD-180`) ----
            AssertHeaderPresent(view, "New (1)");
            AssertHeaderPresent(view, "Available to invoice (1)");
            AssertHeaderPresent(view, "Sent (3)");
            Assert.Contains(
                view.GetLogicalDescendants().OfType<TextBlock>(),
                t => t.Text != null && t.Text.StartsWith("Outstanding / Overdue (3)", StringComparison.Ordinal)
                     && t.Text.Contains("its own due date", StringComparison.Ordinal));

            // ---- Closed is collapsed by default ----
            var closedExpander = Assert.IsType<Expander>(groups["Closed"]);
            Assert.Equal("Closed (2)", closedExpander.Header);
            Assert.False(closedExpander.IsExpanded);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>Every group says why it has nothing, in its own words, before any request or completion exists.</summary>
    [AvaloniaFact]
    public async Task EmptyProject_EveryGroupShowsItsOwnEmptyTextAndZeroCount()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();

            var domainContext = Resolve<EngineeringDomainContext>(host);
            var project = await host.ProjectDirectory!.CreateAsync("P-GROUPING-EMPTY", "Invoices Grouping Empty Project");

            var commandRegistry = Resolve<ICommandRegistry>(host);
            var view = new InvoicingView(domainContext, commandRegistry, () => project.Id, (_, _) => { });
            await view.RefreshAsync();

            AssertHeaderPresent(view, "New (0)");
            AssertHeaderPresent(view, "Available to invoice (0)");
            AssertHeaderPresent(view, "Sent (0)");
            var closedExpander = Assert.IsType<Expander>(FindGroup(view, "Closed"));
            Assert.Equal("Closed (0)", closedExpander.Header);
            Assert.Contains(
                view.GetLogicalDescendants().OfType<TextBlock>(),
                t => t.Text != null && t.Text.StartsWith("Outstanding / Overdue (0)", StringComparison.Ordinal));

            var texts = view.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
            Assert.Contains("No draft requests.", texts);
            Assert.Contains("Nothing completed is waiting for an invoice.", texts);
            Assert.Contains("Nothing has been sent yet.", texts);
            Assert.Contains("Nothing is outstanding or overdue.", texts);
            Assert.Contains("Nothing has been rejected or voided.", texts);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ---------------------------------------------------------------
    // Fixture helpers — build real, persisted objects directly through
    // EngineeringObjectFactory (the same factory InvoicingService itself
    // uses), so any InvoiceRequestStatus and SentAtUtc are reachable
    // without a scripted connector or real elapsed time.
    // ---------------------------------------------------------------

    private static IReadOnlyList<InvoiceRequestLine> OneLine() =>
        [new InvoiceRequestLine(DeliverableCompletion.CanonicalKind, Guid.NewGuid(), "Fixture line", 1m, new Money(100m, CurrencyCode.Gbp), new Money(100m, CurrencyCode.Gbp))];

    private static async Task<InvoiceRequest> CreateRequestAsync(
        EngineeringDomainContext domainContext, Guid projectId, InvoiceRequestStatus status, IReadOnlyList<InvoiceRequestLine> lines,
        string? externalId = null, string? externalInvoiceNumber = null, string? externalStatus = null,
        DateOnly? issuedDate = null, DateOnly? paidDate = null, string? lastError = null,
        string? connector = null, DateTimeOffset? sentAtUtc = null, DateOnly? dueOn = null)
    {
        var total = Money.Sum(lines.Select(l => l.Amount), CurrencyCode.Gbp);

        var created = await new EngineeringObjectFactory<InvoiceRequest>(
            InvoiceRequest.CanonicalKind, domainContext,
            (doc, rev) => new InvoiceRequest(
                doc, rev, domainContext, identifier: null, $"Fixture request — {status}", EngineeringObjectMetadata.Empty,
                ClientOrganisationId, purchaseOrderReference: null, CurrencyCode.Gbp, lines, total, status,
                externalId, externalInvoiceNumber, externalStatus, issuedDate, paidDate, lastError, connector, sentAtUtc,
                dueOn: dueOn))
            .CreateAsync($"Fixture request — {status}.", CancellationToken.None).ConfigureAwait(true);

        if (created is IHasParent hasParent)
            await hasParent.MoveAsync(projectId, CancellationToken.None).ConfigureAwait(true);

        return (InvoiceRequest)created;
    }

    private static async Task<DeliverableCompletion> CreateCompletionAsync(
        EngineeringDomainContext domainContext, Guid projectId, Guid deliverableId, DateOnly completedOn, Money? fixedPrice = null)
    {
        var created = await new EngineeringObjectFactory<DeliverableCompletion>(
            DeliverableCompletion.CanonicalKind, domainContext,
            (doc, rev) => new DeliverableCompletion(
                doc, rev, domainContext, identifier: null, $"Completion of {deliverableId:N}", EngineeringObjectMetadata.Empty,
                deliverableId, completedOn, "fixture-principal", issuedEvidenceIds: null, documentIds: null, fixedPrice, invoicedBy: null))
            .CreateAsync("Fixture completion.", CancellationToken.None).ConfigureAwait(true);

        if (created is IHasParent hasParent)
            await hasParent.MoveAsync(projectId, CancellationToken.None).ConfigureAwait(true);

        return (DeliverableCompletion)created;
    }

    private static T Resolve<T>(WorkspaceHost host) where T : class => (T)host.Services!.GetService(typeof(T));

    private static Control FindGroup(Control root, string automationName) =>
        root.GetLogicalDescendants().OfType<Control>().First(c => Equals(AutomationProperties.GetName(c), automationName));

    private static bool ContainsRow(Control container, Guid tag) =>
        container.GetLogicalDescendants().OfType<Border>().Any(b => Equals(b.Tag, tag));

    private static void AssertOnlyInGroup(IReadOnlyDictionary<string, Control> groups, string expectedGroupName, Guid tag)
    {
        foreach (var (name, container) in groups)
        {
            var present = ContainsRow(container, tag);
            if (name == expectedGroupName)
                Assert.True(present, $"Row '{tag:N}' was not found in the expected group '{expectedGroupName}'.");
            else
                Assert.False(present, $"Row '{tag:N}' unexpectedly appeared in group '{name}' (expected only '{expectedGroupName}').");
        }
    }

    private static void AssertHeaderPresent(Control root, string text) =>
        Assert.Contains(root.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == text);
}
