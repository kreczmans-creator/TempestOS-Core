using Tempest.Workspace.Projects;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Expenses;
using Tempest.Core.Invoicing;
using Tempest.Core.Projects;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.BusinessGovernance;
using Tempest.Core.Tests.BusinessOperations;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Expenses;

/// <summary>
/// The `WP 21.3B` acceptance journey: an expense recorded against a
/// project, round-tripped across a restart, marked invoiced once, and
/// raising an invoice request of its own — the second, expense-only way
/// into <see cref="IInvoicingService"/> this Work Package adds
/// (<see cref="IInvoicingService.RaiseFromExpenseAsync"/>).
/// </summary>
public sealed class ExpenseServiceJourneyTests
{
    [Fact]
    public async Task RecordAmendMarkInvoiced_RestartAndEverythingIsStillThere()
    {
        using var temp = new TempDirectory();

        Guid projectId;
        Guid expenseId;

        // ================================================================
        // FIRST HOST
        // ================================================================
        {
            var (host, manager) = await ExpenseTestHost.StartAsync(temp.Path);
            ExpenseTestHost.SignIn(host);

            projectId = await ExpenseTestHost.CreateProjectAsync(host, "EXP-CORE");
            var expenses = ExpenseTestHost.Expenses(host);

            var recorded = await expenses.RecordAsync(
                projectId, new DateOnly(2026, 9, 10), "Train to site visit", ExpenseCategory.Travel,
                new Money(100m, CurrencyCode.Gbp), new Money(20m, CurrencyCode.Gbp), billable: true);

            Assert.True(recorded.Succeeded, recorded.Reason);
            var expense = recorded.Expense!;
            expenseId = expense.Id;

            Assert.Equal(projectId, expense.ProjectId);
            Assert.Equal(ExpenseCategory.Travel, expense.Category);
            Assert.Equal(new Money(100m, CurrencyCode.Gbp), expense.NetAmount);
            Assert.Equal(new Money(20m, CurrencyCode.Gbp), expense.VatAmount);
            Assert.Equal(new Money(120m, CurrencyCode.Gbp), expense.GrossAmount);
            Assert.True(expense.Billable);
            Assert.Null(expense.InvoicedBy);

            // ---- Amend, while unbilled ----
            var amended = await expenses.AmendAsync(
                expenseId, "Train to site visit (return)", ExpenseCategory.Travel,
                new Money(150m, CurrencyCode.Gbp), new Money(30m, CurrencyCode.Gbp), billable: true);
            Assert.True(amended.Succeeded, amended.Reason);
            Assert.Equal("Train to site visit (return)", amended.Expense!.Description);
            Assert.Equal(new Money(180m, CurrencyCode.Gbp), amended.Expense.GrossAmount);

            // ---- Listed for the project ----
            var forProject = await expenses.ListForProjectAsync(projectId);
            Assert.Single(forProject, e => e.Id == expenseId);

            var unbilled = await expenses.ListUnbilledForProjectAsync(projectId);
            Assert.Single(unbilled, e => e.Id == expenseId);

            // ---- Mark invoiced, once ----
            var requestId = Guid.NewGuid();
            var invoiced = await expenses.MarkInvoicedAsync(expenseId, requestId);
            Assert.True(invoiced.Succeeded, invoiced.Reason);
            Assert.Equal(requestId, invoiced.Expense!.InvoicedBy);

            // ---- A second MarkInvoiced, an Amend and a Delete are all refused once invoiced ----
            var secondInvoice = await expenses.MarkInvoicedAsync(expenseId, Guid.NewGuid());
            Assert.False(secondInvoice.Succeeded);
            Assert.Equal(ExpenseRefusal.ExpenseInvoiced, secondInvoice.Refusal);

            var amendAfterInvoiced = await expenses.AmendAsync(
                expenseId, "Too late", ExpenseCategory.Other, new Money(1m, CurrencyCode.Gbp), new Money(0m, CurrencyCode.Gbp), billable: true);
            Assert.False(amendAfterInvoiced.Succeeded);
            Assert.Equal(ExpenseRefusal.ExpenseInvoiced, amendAfterInvoiced.Refusal);

            var deleteAfterInvoiced = await expenses.DeleteAsync(expenseId);
            Assert.False(deleteAfterInvoiced.Succeeded);
            Assert.Equal(ExpenseRefusal.ExpenseInvoiced, deleteAfterInvoiced.Refusal);

            // ---- Now invoiced, so no longer unbilled ----
            var unbilledAfter = await expenses.ListUnbilledForProjectAsync(projectId);
            Assert.DoesNotContain(unbilledAfter, e => e.Id == expenseId);

            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }

        // ================================================================
        // SECOND HOST — restart, read everything back
        // ================================================================
        {
            var (host, manager) = await ExpenseTestHost.StartAsync(temp.Path);

            var rehydration = await Tempest.Workspace.Composition.EngineeringWorkspaceComposer.RehydrateEngineeringObjectsAsync(host);
            Assert.True(rehydration.IsComplete, "Expected a clean rehydration.");

            var expenses = ExpenseTestHost.Expenses(host);
            var recovered = (await expenses.ListForProjectAsync(projectId)).Single(e => e.Id == expenseId);

            Assert.Equal("Train to site visit (return)", recovered.Description);
            Assert.Equal(new Money(150m, CurrencyCode.Gbp), recovered.NetAmount);
            Assert.Equal(new Money(30m, CurrencyCode.Gbp), recovered.VatAmount);
            Assert.Equal(new Money(180m, CurrencyCode.Gbp), recovered.GrossAmount);
            Assert.NotNull(recovered.InvoicedBy);

            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task Record_RefusesMismatchedCurrencyAndNegativeAmounts()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await ExpenseTestHost.StartAsync(temp.Path);

        try
        {
            ExpenseTestHost.SignIn(host);
            var projectId = await ExpenseTestHost.CreateProjectAsync(host);
            var expenses = ExpenseTestHost.Expenses(host);

            var mismatched = await expenses.RecordAsync(
                projectId, DateOnly.FromDateTime(DateTime.UtcNow), "Mismatched", ExpenseCategory.Other,
                new Money(100m, CurrencyCode.Gbp), new Money(20m, new CurrencyCode("USD")), billable: true);
            Assert.False(mismatched.Succeeded);
            Assert.Equal(ExpenseRefusal.CurrencyMismatch, mismatched.Refusal);

            var negative = await expenses.RecordAsync(
                projectId, DateOnly.FromDateTime(DateTime.UtcNow), "Negative", ExpenseCategory.Other,
                new Money(-1m, CurrencyCode.Gbp), new Money(0m, CurrencyCode.Gbp), billable: true);
            Assert.False(negative.Succeeded);
            Assert.Equal(ExpenseRefusal.InvalidAmount, negative.Refusal);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// `WP 21.3B` — "A billable expense becomes a line on the next invoice
    /// request exactly as a timesheet entry does": raised here with no
    /// deliverable completion at all, through the expense-only entry point
    /// <see cref="IInvoicingService.RaiseFromExpenseAsync"/> — the project's
    /// only unbilled work, right now, is the expense.
    /// </summary>
    [Fact]
    public async Task RaiseFromExpense_BuildsARequestCarryingTheExpenseLine_AndRefusesASecondRaise()
    {
        using var temp = new TempDirectory();
        var (host, manager) = await ExpenseTestHost.StartAsync(temp.Path);

        try
        {
            ExpenseTestHost.SignIn(host);

            const string organisationId = "EXP-CLIENT-1";
            const string rateCardId = "EXP-CARD-1";

            await ExpenseTestHost.Organisations(host).RegisterAsync(organisationId, OperationsFixtures.Organisation(organisationId), OperationsFixtures.Verified());

            var card = new RateCard
            {
                Code = rateCardId,
                Name = "Expense journey rate card",
                EffectivePeriod = new EffectivePeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)),
                Currency = CurrencyCode.Gbp,
                Governance = BusinessGovernanceFixtures.Governance() with { Authorisations = [BusinessGovernanceFixtures.Authority(BusinessAuthorityKind.InternalApproval)] },
                Entries = [new RateCardEntry("ENG-1", "Engineer", PricingBasis.Hourly, new Money(100m, CurrencyCode.Gbp), new Money(60m, CurrencyCode.Gbp), Grade: "Engineer")],
            };
            await ExpenseTestHost.RateCards(host).RegisterAsync(rateCardId, card, BusinessGovernanceFixtures.Verified());
            await BusinessGovernanceFixtures.ReleaseAsync((RateCardCatalog)ExpenseTestHost.RateCards(host), rateCardId);

            var projectId = await ExpenseTestHost.CreateProjectAsync(host, "EXP-RAISE");
            var commercial = ExpenseTestHost.ProjectCommercial(host);
            Assert.True((await commercial.PinRateCardAsync(projectId, rateCardId)).Succeeded);
            Assert.True((await commercial.SetClientAsync(projectId, organisationId)).Succeeded);

            var expenses = ExpenseTestHost.Expenses(host);
            var recorded = await expenses.RecordAsync(
                projectId, DateOnly.FromDateTime(DateTime.UtcNow), "Materials for prototype", ExpenseCategory.Materials,
                new Money(200m, CurrencyCode.Gbp), new Money(40m, CurrencyCode.Gbp), billable: true);
            Assert.True(recorded.Succeeded, recorded.Reason);
            var expenseId = recorded.Expense!.Id;

            var invoicing = ExpenseTestHost.Invoicing(host);
            var raised = await invoicing.RaiseFromExpenseAsync(expenseId);
            Assert.True(raised.Succeeded, raised.Reason);

            var request = raised.Request!;
            var line = Assert.Single(request.Lines, l => l.SourceId == expenseId);
            Assert.Equal(ProjectExpense.CanonicalKind, line.SourceKind);
            Assert.Equal(new Money(200m, CurrencyCode.Gbp), line.Amount);

            // `InvoicingService.InferVatRate`: 40 / 200 = 20% exactly, so
            // the receipt's own VAT rounds back to Standard, honestly.
            Assert.Equal(VatRate.Standard, line.VatRate);
            Assert.Equal(new Money(40m, CurrencyCode.Gbp), line.VatAmount);

            // ---- A second raise for the same expense is refused, naming the first request ----
            var secondRaise = await invoicing.RaiseFromExpenseAsync(expenseId);
            Assert.False(secondRaise.Succeeded);
            Assert.Equal(InvoiceRequestRefusal.AlreadyInvoiced, secondRaise.Refusal);
            Assert.Equal(request.Id, secondRaise.Request!.Id);

            // ---- Raising from an unknown expense id is refused ----
            var unknown = await invoicing.RaiseFromExpenseAsync(Guid.NewGuid());
            Assert.False(unknown.Succeeded);
            Assert.Equal(InvoiceRequestRefusal.ExpenseNotFound, unknown.Refusal);
        }
        finally
        {
            await manager.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}
