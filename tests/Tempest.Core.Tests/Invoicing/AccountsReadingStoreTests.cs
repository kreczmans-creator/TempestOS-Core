using Tempest.Core.BusinessGovernance;
using Tempest.Core.Invoicing;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Invoicing;

/// <summary>
/// <see cref="FileAccountsReadingStore"/>'s own contract (`WP 19.8B` scope
/// §2): a saved reading reloads with the same values, and a missing or
/// corrupt file reads back as "nothing saved yet" rather than throwing.
/// </summary>
public sealed class AccountsReadingStoreTests
{
    [Fact]
    public async Task ReadAsync_NothingEverSaved_ReturnsNull()
    {
        using var temp = new TempDirectory();
        var store = new FileAccountsReadingStore(temp.Path);

        Assert.Null(await store.ReadAsync());
    }

    [Fact]
    public async Task SaveThenRead_RoundTripsEveryValue()
    {
        using var temp = new TempDirectory();
        var store = new FileAccountsReadingStore(temp.Path);
        var reading = SampleReading();

        await store.SaveAsync(reading);
        var reloaded = await store.ReadAsync();

        Assert.NotNull(reloaded);
        Assert.Equal(reading.Connector, reloaded!.Connector);
        Assert.Equal(reading.ReadAt, reloaded.ReadAt);

        var bill = Assert.Single(reloaded.Bills);
        Assert.Equal("Acme Supplies Ltd", bill.Supplier);
        Assert.Equal("INV-778", bill.Reference);
        Assert.Equal(new DateOnly(2026, 3, 1), bill.Issued);
        Assert.Equal(new DateOnly(2026, 3, 29), bill.Due);
        Assert.Equal(new Money(452.10m, CurrencyCode.Gbp), bill.Amount);
        Assert.Equal("AUTHORISED", bill.Status);

        var repeating = Assert.Single(reloaded.RepeatingBills);
        Assert.Equal(AccountsCategory.Software, repeating.Category);
        Assert.Equal("Contoso Cloud", repeating.Bill.Supplier);
        Assert.Equal("Software licence", repeating.Bill.Description);
        Assert.Equal(new Money(99.00m, CurrencyCode.Gbp), repeating.Bill.Amount);
        Assert.Equal("MONTHLY", repeating.Bill.Frequency);
        Assert.Equal(new DateOnly(2026, 4, 1), repeating.Bill.NextDue);
        Assert.Equal("Software", repeating.Bill.AccountName);

        var cash = Assert.Single(reloaded.Cash);
        Assert.Equal("Business Current Account", cash.Name);
        Assert.Equal(new Money(15342.67m, CurrencyCode.Gbp), cash.Balance);
        Assert.Equal(new DateOnly(2026, 3, 30), cash.AsOf);
    }

    [Fact]
    public async Task SaveAsync_ReplacesWhatWasThereBefore_NeverAccumulates()
    {
        using var temp = new TempDirectory();
        var store = new FileAccountsReadingStore(temp.Path);

        await store.SaveAsync(SampleReading());
        await store.SaveAsync(SampleReading() with { Connector = "Xero", Bills = [] });

        var reloaded = await store.ReadAsync();
        Assert.NotNull(reloaded);
        Assert.Equal("Xero", reloaded!.Connector);
        Assert.Empty(reloaded.Bills);
    }

    [Fact]
    public async Task ReadAsync_ACorruptFile_ReadsAsNothingSavedYet_NeverThrows()
    {
        using var temp = new TempDirectory();
        var store = new FileAccountsReadingStore(temp.Path);
        await store.SaveAsync(SampleReading());

        await File.WriteAllTextAsync(Path.Combine(temp.Path, FileAccountsReadingStore.ReadingFileName), "{ not valid json");

        Assert.Null(await store.ReadAsync());
    }

    private static AccountsReading SampleReading() => new(
        Bills: [new BillDue("Acme Supplies Ltd", "INV-778", new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 29), new Money(452.10m, CurrencyCode.Gbp), "AUTHORISED")],
        RepeatingBills:
        [
            new CategorisedRepeatingBill(
                new RepeatingBill("Contoso Cloud", "Software licence", new Money(99.00m, CurrencyCode.Gbp), "MONTHLY", new DateOnly(2026, 4, 1), "Software"),
                AccountsCategory.Software),
        ],
        Cash: [new CashAccountBalance("Business Current Account", new Money(15342.67m, CurrencyCode.Gbp), new DateOnly(2026, 3, 30))],
        ReadAt: new DateTimeOffset(2026, 3, 30, 9, 15, 0, TimeSpan.Zero),
        Connector: "Fake");
}
