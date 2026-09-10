using Tempest.Core.BusinessGovernance;
using Tempest.Core.Invoicing;

namespace Tempest.Core.Tests.Invoicing;

/// <summary>
/// <c>FakeInvoicingConnector</c>'s own scripting and recording behaviour,
/// exercised directly rather than only incidentally through
/// <c>InvoicingServiceJourneyTests</c> (`WP 19.1A`, `ADR-0151`).
/// </summary>
public sealed class FakeInvoicingConnectorTests
{
    private static readonly InvoiceRequestSnapshot Snapshot = new(
        Guid.NewGuid(), "ORG-1", "PO-1", CurrencyCode.Gbp, [], new(0m, CurrencyCode.Gbp));

    [Fact]
    public async Task ScriptNextCreate_IsConsumedOnce_ThenTheNextCallIsOrdinary()
    {
        var connector = new FakeInvoicingConnector();
        connector.ScriptNextCreate(ConnectorOutcome.Rejected, "First one only.");

        var first = await connector.CreateDraftInvoiceAsync(Snapshot, "key-1");
        Assert.Equal(ConnectorOutcome.Rejected, first.Outcome);
        Assert.Equal("First one only.", first.Reason);

        var second = await connector.CreateDraftInvoiceAsync(Snapshot, "key-2");
        Assert.Equal(ConnectorOutcome.Ok, second.Outcome);
        Assert.Equal("key-2", second.Value!.Reference);
    }

    [Fact]
    public async Task ScriptCreateFor_IsPinnedToOneIdempotencyKey_NeverConsumed()
    {
        var connector = new FakeInvoicingConnector();
        connector.ScriptCreateFor("retry-key", ConnectorOutcome.Unavailable, "Down for maintenance.");

        var first = await connector.CreateDraftInvoiceAsync(Snapshot, "retry-key");
        var second = await connector.CreateDraftInvoiceAsync(Snapshot, "retry-key");

        Assert.Equal(ConnectorOutcome.Unavailable, first.Outcome);
        Assert.Equal(ConnectorOutcome.Unavailable, second.Outcome);
        Assert.Equal("Down for maintenance.", second.Reason);

        // An unrelated key is unaffected.
        var other = await connector.CreateDraftInvoiceAsync(Snapshot, "unrelated-key");
        Assert.Equal(ConnectorOutcome.Ok, other.Outcome);
    }

    [Fact]
    public async Task UnknownOutcome_StillRecordsTheInvoice_FindableByReferenceAfterwards()
    {
        var connector = new FakeInvoicingConnector();
        connector.ScriptNextCreate(ConnectorOutcome.Unknown);

        var created = await connector.CreateDraftInvoiceAsync(Snapshot, "lost-key");
        Assert.Equal(ConnectorOutcome.Unknown, created.Outcome);

        var found = await connector.FindByReferenceAsync("lost-key");
        Assert.Equal(ConnectorOutcome.Ok, found.Outcome);
        Assert.NotNull(found.Value);
        Assert.Equal("lost-key", found.Value!.Reference);
    }

    [Fact]
    public async Task ForgetReference_MakesFindByReferenceAsync_AnswerNotFound()
    {
        var connector = new FakeInvoicingConnector();
        await connector.CreateDraftInvoiceAsync(Snapshot, "will-forget");

        connector.ForgetReference("will-forget");

        var found = await connector.FindByReferenceAsync("will-forget");
        Assert.Equal(ConnectorOutcome.Ok, found.Outcome);
        Assert.Null(found.Value);
    }

    [Fact]
    public async Task FindByReferenceAsync_AnswersNotFound_ForAReferenceNeverRecorded()
    {
        var connector = new FakeInvoicingConnector();

        var found = await connector.FindByReferenceAsync("never-seen");

        Assert.Equal(ConnectorOutcome.Ok, found.Outcome);
        Assert.Null(found.Value);
    }

    [Fact]
    public async Task ReadStatusAsync_AnswersAPlainSubmittedReading_UnlessScripted()
    {
        var connector = new FakeInvoicingConnector();

        var unscripted = await connector.ReadStatusAsync("ext-1");
        Assert.Equal("SUBMITTED", unscripted.Value!.ExternalStatus);
        Assert.Null(unscripted.Value.PaidDate);

        connector.ScriptStatus("ext-1", new InvoiceStatusReading("PAID", "INV-0001", new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 15)));

        var scripted = await connector.ReadStatusAsync("ext-1");
        Assert.Equal("PAID", scripted.Value!.ExternalStatus);
        Assert.Equal(new DateOnly(2026, 3, 15), scripted.Value.PaidDate);
    }

    [Fact]
    public async Task AuthorisationStateAsync_DefaultsToAuthorised_UntilScriptedOtherwise()
    {
        var connector = new FakeInvoicingConnector();

        var initial = await connector.AuthorisationStateAsync();
        Assert.Equal(ConnectorAuthorisation.Authorised, initial.Status);

        connector.ScriptAuthorisationState(new ConnectorAuthorisationState(ConnectorAuthorisation.Expired, "Token expired 2026-03-01."));

        var afterScript = await connector.AuthorisationStateAsync();
        Assert.Equal(ConnectorAuthorisation.Expired, afterScript.Status);
        Assert.Equal("Token expired 2026-03-01.", afterScript.Detail);
    }

    [Fact]
    public async Task ListContactsAsync_ReturnsEveryAddedContact_InAdditionOrder()
    {
        var connector = new FakeInvoicingConnector();
        connector.AddContact(new ConnectorContact("c-1", "Fictional Client Ltd"));
        connector.AddContact(new ConnectorContact("c-2", "Another Client Co"));

        var result = await connector.ListContactsAsync();

        Assert.Equal(ConnectorOutcome.Ok, result.Outcome);
        Assert.Equal(["c-1", "c-2"], result.Value!.Select(c => c.ExternalId));
    }

    [Fact]
    public async Task EveryCall_IsRecorded_InCallOrder_WithItsOwnKeyArgument()
    {
        var connector = new FakeInvoicingConnector();

        await connector.CreateDraftInvoiceAsync(Snapshot, "key-a");
        await connector.ReadStatusAsync("ext-a");
        await connector.FindByReferenceAsync("key-a");
        await connector.ListContactsAsync();
        await connector.AuthorisationStateAsync();

        Assert.Equal(
            [
                (nameof(IInvoicingConnector.CreateDraftInvoiceAsync), "key-a"),
                (nameof(IInvoicingConnector.ReadStatusAsync), "ext-a"),
                (nameof(IInvoicingConnector.FindByReferenceAsync), "key-a"),
                (nameof(IInvoicingConnector.ListContactsAsync), (string?)null),
                (nameof(IInvoicingConnector.AuthorisationStateAsync), (string?)null),
            ],
            connector.Calls.Select(c => (c.Member, c.Argument)));
    }

    [Fact]
    public void Name_IsFake()
    {
        Assert.Equal("Fake", new FakeInvoicingConnector().Name);
    }
}
