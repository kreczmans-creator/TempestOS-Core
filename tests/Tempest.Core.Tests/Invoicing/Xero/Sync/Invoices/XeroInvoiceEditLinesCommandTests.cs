using Tempest.Core.Commands;
using Tempest.Core.Invoicing;
using Tempest.Workspace.Invoicing;

namespace Tempest.Core.Tests.Invoicing.Xero.Sync.Invoices;

/// <summary>
/// `v0.24.0` review-board fix M4, over the simulator: <c>invoicing.reviseLines</c>
/// (<see cref="ReviseInvoiceLinesCommand"/>) through <see cref="IInvoicingService.ReviseLinesAsync"/>
/// edits a Sent invoice's Xero draft (blank keeps a value), and
/// <c>invoicing.void</c> on a Sent request deletes its Xero draft — never past
/// DRAFT, nothing emailed (the kit's safety check).
/// </summary>
public sealed class XeroInvoiceEditLinesCommandTests
{
    private static ReviseInvoiceLinesCommandHandler Handler(InvoiceExportKit kit) =>
        new(kit.Service, async (id, ct) => await kit.Domain.Repository.FindAsync(id, ct) as InvoiceRequest);

    [Fact]
    public async Task EditLines_OnASentInvoice_UpdatesTheXeroDraft_KeepingWhatWasLeftBlank()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (request, _) = await kit.SendNewAsync("EL1");
        var line = Assert.Single(request.Lines);
        IInvoicingService service = kit.Service; // exposed on the interface now

        var result = await Handler(kit).HandleAsync(
            new ReviseInvoiceLinesCommand(request.Id, InvoiceRequest.CanonicalKind, "1", description: " ", quantity: "2", unitRate: string.Empty, vatRate: string.Empty),
            CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        var after = Assert.Single((await kit.ReloadAsync(request.Id)).Lines);
        Assert.Equal(line.Description, after.Description);
        Assert.Equal(2m, after.Quantity);
        Assert.Equal(line.UnitRate, after.UnitRate);

        var invoice = kit.Invoice(request.ExternalId!);
        Assert.Equal("DRAFT", invoice.Status);
        Assert.Equal(2m, (decimal?)Assert.Single(invoice.Body["LineItems"]!.AsArray())!["Quantity"]);
        Assert.NotNull(service);
        kit.AssertSafe();
    }

    [Fact]
    public async Task EditLines_RefusesALineThatDoesNotExist_OrABadValue_ChangingNothing()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (request, _) = await kit.SendNewAsync("EL2");
        var mark = kit.Mark;

        var missing = await Handler(kit).HandleAsync(new ReviseInvoiceLinesCommand(request.Id, InvoiceRequest.CanonicalKind, "3"), CancellationToken.None);
        Assert.False(missing.Succeeded);
        Assert.Contains("no line 3", missing.Message, StringComparison.Ordinal);

        var bad = await Handler(kit).HandleAsync(new ReviseInvoiceLinesCommand(request.Id, InvoiceRequest.CanonicalKind, "1", vatRate: "Huge"), CancellationToken.None);
        Assert.False(bad.Succeeded);
        Assert.DoesNotContain(kit.RequestsSince(mark), r => r.Method != HttpMethod.Get);
        kit.AssertSafe();
    }

    [Fact]
    public async Task Void_OnASentInvoice_DeletesItsXeroDraft()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var (request, _) = await kit.SendNewAsync("EL3");

        var result = await new VoidInvoiceCommandHandler(kit.Service).HandleAsync(new VoidInvoiceCommand(request.Id, InvoiceRequest.CanonicalKind), CancellationToken.None);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(InvoiceRequestStatus.Voided, (await kit.ReloadAsync(request.Id)).Status);
        Assert.Equal("DELETED", kit.Invoice(request.ExternalId!).Status);
        kit.AssertSafe();
    }

    [Fact]
    public async Task TheVoidAndEditLinesDescriptions_SayWhatTheyDoForASentInvoice()
    {
        await using var kit = await InvoiceExportKit.CreateAsync();
        var registry = (ICommandRegistry)kit.Host.Services!.GetService(typeof(ICommandRegistry));

        var voidDescriptor = registry.Items.Single(d => d.Id == InvoicingCommandIds.Void);
        Assert.DoesNotContain("Only a Draft or Rejected", voidDescriptor.Description, StringComparison.Ordinal);
        Assert.Contains("Sent", voidDescriptor.Description, StringComparison.Ordinal);
        var edit = registry.Items.Single(d => d.Id == InvoicingCommandIds.ReviseLines);
        Assert.Equal(["Line", "Description", "Quantity", "UnitRate", "VatRate"], edit.Binding!.Parameters.Select(p => p.Name).ToArray());
        Assert.Contains("nothing is approved or emailed", registry.Items.Single(d => d.Id == InvoicingCommandIds.Send).Binding!.ConfirmationMessage, StringComparison.Ordinal);
    }
}
