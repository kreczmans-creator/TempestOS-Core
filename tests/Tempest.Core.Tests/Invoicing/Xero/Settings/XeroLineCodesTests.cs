using System.Net;
using System.Text.Json.Nodes;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.Expenses;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero.Settings;
using Tempest.Core.Settings;
using Tempest.Core.Tests.Invoicing.Xero.Api;
using Tempest.Core.Tests.Invoicing.Xero.Simulator;

namespace Tempest.Core.Tests.Invoicing.Xero.Settings;

/// <summary>
/// `v0.24.0` X1 (D6, design §8, Q10): the tax type and account code each line
/// carries are checked against Xero's own reading, so a line can never carry
/// a code Xero lacks — and the codes they resolve to are accepted by the
/// simulator's own validator, with no violation.
/// </summary>
public sealed class XeroLineCodesTests
{
    [Fact]
    public async Task EveryDefault_ResolvesAgainstTheDemoCompany_AndTheSimulatorAcceptsLinesCarryingThem()
    {
        await using var rig = await SettingsRig.CreateAsync();
        var reader = rig.NewReader();
        await reader.RefreshAsync();
        var settings = new InMemorySettingsProvider();
        var taxTypes = new XeroTaxTypeResolver(reader, settings);
        var accounts = new XeroAccountCodeMap(reader, settings);

        var contact = await rig.Kit.PutAsync("Contacts?summarizeErrors=true", new JsonObject { ["Contacts"] = new JsonArray { new JsonObject { ["Name"] = "Acme Ltd" } } });
        var contactId = contact.First("Contacts")["ContactID"]!.GetValue<string>();

        var sales = await accounts.ResolveSalesAsync();
        Assert.Equal("200", sales.Code);

        var number = 0;
        foreach (var rate in Enum.GetValues<VatRate>())
        {
            var output = await taxTypes.ResolveAsync(rate, VatTaxDirection.Sales);
            Assert.False(output.IsBlocked, output.BlockedReason);
            var invoice = SimulatorTestKit.Invoice(contactId, $"X1-INV-{++number}", line: SimulatorTestKit.Line(taxType: output.Code, accountCode: sales.Code));
            Assert.Equal(HttpStatusCode.OK, (await rig.Kit.PutAsync("Invoices?summarizeErrors=true", invoice)).Status);

            foreach (var category in Enum.GetValues<ExpenseCategory>())
            {
                var input = await taxTypes.ResolveAsync(rate, VatTaxDirection.Purchases);
                var expense = await accounts.ResolveExpenseAsync(category);
                Assert.False(input.IsBlocked, input.BlockedReason);
                Assert.False(expense.IsBlocked, expense.BlockedReason);
                var bill = SimulatorTestKit.Invoice(contactId, $"X1-BILL-{++number}", "ACCPAY", line: SimulatorTestKit.Line("Receipt", 1m, 10m, input.Code, expense.Code));
                Assert.Equal(HttpStatusCode.OK, (await rig.Kit.PutAsync("Invoices?summarizeErrors=true", bill)).Status);
            }
        }

        Assert.Empty(rig.Simulator.Violations);
    }

    [Fact]
    public async Task WithNoReading_EveryLineIsBlocked_WithTheReason()
    {
        await using var rig = await SettingsRig.CreateAsync();
        var reader = rig.NewReader();
        var settings = new InMemorySettingsProvider();

        var tax = await new XeroTaxTypeResolver(reader, settings).ResolveAsync(VatRate.Standard, VatTaxDirection.Sales);
        var account = await new XeroAccountCodeMap(reader, settings).ResolveSalesAsync();

        Assert.True(tax.IsBlocked);
        Assert.Null(tax.Code);
        Assert.Contains("tax rates have not been read", tax.BlockedReason, StringComparison.Ordinal);
        Assert.True(account.IsBlocked);
        Assert.Contains("chart of accounts has not been read", account.BlockedReason, StringComparison.Ordinal);
        Assert.Empty(rig.Simulator.Requests);
    }

    [Theory]
    [InlineData("OUTPUT9", VatTaxDirection.Sales, "Xero has no active tax rate OUTPUT9.")]
    [InlineData("OUTPUT", VatTaxDirection.Sales, "Xero has no active tax rate OUTPUT.")]
    [InlineData("INPUT2", VatTaxDirection.Sales, "Xero's tax rate INPUT2 (20% (VAT on Expenses)) cannot be used on sales lines.")]
    [InlineData("OUTPUT2", VatTaxDirection.Purchases, "Xero's tax rate OUTPUT2 (20% (VAT on Income)) cannot be used on purchase lines.")]
    public async Task AChosenTaxTypeXeroLacks_IsInactive_OrDoesNotApply_IsBlocked(string chosen, VatTaxDirection direction, string reason)
    {
        await using var rig = await SettingsRig.CreateAsync();
        var reader = rig.NewReader();
        await reader.RefreshAsync();
        var settings = new InMemorySettingsProvider();
        var resolver = new XeroTaxTypeResolver(reader, settings);
        await resolver.ResolveAsync(VatRate.Standard, direction);
        await settings.SetValueAsync(XeroTaxTypeResolver.SettingKey(direction, VatRate.Standard), chosen);

        var resolution = await resolver.ResolveAsync(VatRate.Standard, direction);

        Assert.Equal(XeroCodeResolution.Blocked(reason), resolution);
        Assert.Empty(rig.Simulator.Violations);
    }

    [Fact]
    public async Task AChosenTaxType_IsAnsweredInXerosOwnSpelling_AndBlankMeansTheDefault()
    {
        await using var rig = await SettingsRig.CreateAsync();
        var reader = rig.NewReader();
        await reader.RefreshAsync();
        var settings = new InMemorySettingsProvider();
        var resolver = new XeroTaxTypeResolver(reader, settings);

        Assert.Equal("ZERORATEDOUTPUT", (await resolver.ResolveAsync(VatRate.Zero, VatTaxDirection.Sales)).Code);

        await settings.SetValueAsync(XeroTaxTypeResolver.SettingKey(VatTaxDirection.Sales, VatRate.Zero), " exemptoutput ");
        Assert.Equal("EXEMPTOUTPUT", (await resolver.ResolveAsync(VatRate.Zero, VatTaxDirection.Sales)).Code);

        await settings.SetValueAsync(XeroTaxTypeResolver.SettingKey(VatTaxDirection.Sales, VatRate.Zero), "");
        Assert.Equal("ZERORATEDOUTPUT", (await resolver.ResolveAsync(VatRate.Zero, VatTaxDirection.Sales)).Code);
    }

    [Fact]
    public void AnUndeclaredRate_IsBlocked_EvenWithAReading()
    {
        var reading = FakeSettingsReader.Reading("t", isDemoCompany: true);

        var resolution = XeroTaxTypeResolver.Resolve((VatRate)99, VatTaxDirection.Sales, reading);

        Assert.True(resolution.IsBlocked);
        Assert.Contains("has no Xero tax type", resolution.BlockedReason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TaxTypeChoices_AreTheActiveRatesThatApplyToTheSide()
    {
        await using var rig = await SettingsRig.CreateAsync();
        var reading = (await rig.NewReader().RefreshAsync()).Value!;

        Assert.Equal(
            ["OUTPUT2", "RROUTPUT", "ZERORATEDOUTPUT", "EXEMPTOUTPUT", "NONE"],
            XeroTaxTypeResolver.Choices(reading, VatTaxDirection.Sales).Select(r => r.TaxType));
        Assert.Equal(
            ["INPUT2", "RRINPUT", "ZERORATEDINPUT", "EXEMPTINPUT", "NONE"],
            XeroTaxTypeResolver.Choices(reading, VatTaxDirection.Purchases).Select(r => r.TaxType));
    }

    [Fact]
    public void Definitions_CoverEveryRateAndSideAndCategory_WithTheDefaults_AndAreIdempotent()
    {
        var settings = new InMemorySettingsProvider();

        XeroTaxTypeResolver.EnsureDefinitions(settings);
        XeroTaxTypeResolver.EnsureDefinitions(settings);
        XeroAccountCodeMap.EnsureDefinitions(settings);
        XeroAccountCodeMap.EnsureDefinitions(settings);

        var byKey = settings.Definitions.ToDictionary(d => d.Key, d => d.DefaultValue);
        Assert.Equal((Enum.GetValues<VatRate>().Length * 2) + 1 + Enum.GetValues<ExpenseCategory>().Length, byKey.Count);
        Assert.Equal("OUTPUT2", byKey["Xero.TaxType.Sales.Standard"]);
        Assert.Equal("INPUT2", byKey["Xero.TaxType.Purchases.Standard"]);
        Assert.Equal("NONE", byKey["Xero.TaxType.Purchases.OutOfScope"]);
        Assert.Equal("200", byKey["Xero.AccountCode.Sales"]);
        Assert.Equal("493", byKey["Xero.AccountCode.Expense.Travel"]);
        Assert.Equal("493", byKey["Xero.AccountCode.Expense.Subsistence"]);
        Assert.Equal("429", byKey["Xero.AccountCode.Expense.Materials"]);
        Assert.Equal("412", byKey["Xero.AccountCode.Expense.Subcontract"]);
        Assert.Equal("429", byKey["Xero.AccountCode.Expense.Other"]);
    }

    [Theory]
    [InlineData("999", XeroAccountPurpose.Sales, "Xero has no account 999; choose a sales account Xero holds in Settings.")]
    [InlineData("499", XeroAccountPurpose.Expense, "Xero's account 499 (Old Overheads (simulator-only, archived)) is ARCHIVED, not active; choose another in Settings.")]
    [InlineData("200", XeroAccountPurpose.Expense, "Xero's account 200 (Sales) is not an expense account; choose another in Settings.")]
    [InlineData("493", XeroAccountPurpose.Sales, "Xero's account 493 (Travel - National) is not a revenue account; choose another in Settings.")]
    [InlineData("090", XeroAccountPurpose.Expense, "Xero's account 090 (Business Bank Account) is not an expense account; choose another in Settings.")]
    [InlineData(" ", XeroAccountPurpose.Sales, "No Xero sales account is chosen; choose one in Settings.")]
    public async Task AnAccountXeroLacks_IsArchived_OrIsOfTheWrongClass_IsBlocked(string code, XeroAccountPurpose purpose, string reason)
    {
        await using var rig = await SettingsRig.CreateAsync();
        var reading = (await rig.NewReader().RefreshAsync()).Value!;

        Assert.Equal(XeroCodeResolution.Blocked(reason), XeroAccountCodeMap.Check(code, purpose, reading));
        Assert.Equal(XeroCodeResolution.Usable("310"), XeroAccountCodeMap.Check("310", XeroAccountPurpose.Expense, reading));
    }

    [Fact]
    public async Task AnAccountArchivedInXero_BlocksItsCategory_AfterTheNextRefresh_ButNotBefore()
    {
        await using var rig = await SettingsRig.CreateAsync();
        var reader = rig.NewReader();
        await reader.RefreshAsync();
        var map = new XeroAccountCodeMap(reader, new InMemorySettingsProvider());

        rig.Clock.Advance(TimeSpan.FromHours(1));
        rig.Simulator.ArchiveAccountInXero("493");
        Assert.Equal("493", (await map.ResolveExpenseAsync(ExpenseCategory.Travel)).Code);

        rig.Clock.Advance(TimeSpan.FromHours(1));
        await reader.RefreshAsync();

        var travel = await map.ResolveExpenseAsync(ExpenseCategory.Travel);
        Assert.True(travel.IsBlocked);
        Assert.Contains("493 (Travel - National) is ARCHIVED", travel.BlockedReason, StringComparison.Ordinal);
        Assert.Equal("429", (await map.ResolveExpenseAsync(ExpenseCategory.Other)).Code);
        Assert.Empty(rig.Simulator.Violations);
    }

    [Fact]
    public async Task AChosenAccount_IsUsed_AndAnUndeclaredCategory_IsBlocked()
    {
        await using var rig = await SettingsRig.CreateAsync();
        var reader = rig.NewReader();
        await reader.RefreshAsync();
        var settings = new InMemorySettingsProvider();
        var map = new XeroAccountCodeMap(reader, settings);
        await map.ResolveSalesAsync();

        await settings.SetValueAsync(XeroAccountCodeMap.SalesSettingKey, "260");
        await settings.SetValueAsync(XeroAccountCodeMap.ExpenseSettingKey(ExpenseCategory.Materials), "310");

        Assert.Equal("260", (await map.ResolveSalesAsync()).Code);
        Assert.Equal("310", (await map.ResolveExpenseAsync(ExpenseCategory.Materials)).Code);
        Assert.True((await map.ResolveExpenseAsync((ExpenseCategory)99)).IsBlocked);
    }

    [Fact]
    public async Task AccountChoices_AreTheActiveAccountsOfThePurposesClass_InCodeOrder()
    {
        await using var rig = await SettingsRig.CreateAsync();
        var reading = (await rig.NewReader().RefreshAsync()).Value!;

        Assert.Equal(["200", "260"], XeroAccountCodeMap.Choices(reading, XeroAccountPurpose.Sales).Select(a => a.Code));

        var expense = XeroAccountCodeMap.Choices(reading, XeroAccountPurpose.Expense).Select(a => a.Code).ToList();
        Assert.Equal("310", expense[0]);
        Assert.Contains("493", expense);
        Assert.DoesNotContain("499", expense);
        Assert.DoesNotContain("090", expense);
        Assert.DoesNotContain("200", expense);
    }

    [Fact]
    public void WithoutAClass_TheAccountTypeDecides()
    {
        var reading = FakeSettingsReader.Reading("t", isDemoCompany: true) with
        {
            Accounts =
            [
                new XeroAccount("a", "201", "Sales", "SALES", null, "ACTIVE", null),
                new XeroAccount("b", "401", "Overheads", "OVERHEADS", null, "ACTIVE", null),
            ],
        };

        Assert.False(XeroAccountCodeMap.Check("201", XeroAccountPurpose.Sales, reading).IsBlocked);
        Assert.True(XeroAccountCodeMap.Check("201", XeroAccountPurpose.Expense, reading).IsBlocked);
        Assert.False(XeroAccountCodeMap.Check("401", XeroAccountPurpose.Expense, reading).IsBlocked);
    }

    // Backlog X1-5: the "ensured" flag was set before the definitions were registered, so a call racing the
    // first one read before registration and answered the default instead of the user's choice.
    [Fact]
    public async Task ACallRacingTheFirstOne_StillAnswersTheUsersChoice_NotTheDefault()
    {
        await using var rig = await SettingsRig.CreateAsync();
        var reader = rig.NewReader();
        await reader.RefreshAsync();

        var accountSettings = new GatedSettingsProvider(new() { [XeroAccountCodeMap.SalesSettingKey] = "260" });
        var accounts = new XeroAccountCodeMap(reader, accountSettings);
        Assert.Equal("260", await RaceAsync(accountSettings, async () => (await accounts.ResolveSalesAsync()).Code));

        var taxSettings = new GatedSettingsProvider(new() { [XeroTaxTypeResolver.SettingKey(VatTaxDirection.Sales, VatRate.Standard)] = "ZERORATEDOUTPUT" });
        var taxTypes = new XeroTaxTypeResolver(reader, taxSettings);
        Assert.Equal("ZERORATEDOUTPUT", await RaceAsync(taxSettings, async () => (await taxTypes.ResolveAsync(VatRate.Standard, VatTaxDirection.Sales)).Code));

        Assert.Empty(rig.Simulator.Violations);
    }

    /// <summary>Starts a first resolution that stalls inside its first definition registration, then answers a second one made meanwhile.</summary>
    private static async Task<string?> RaceAsync(GatedSettingsProvider settings, Func<Task<string?>> resolve)
    {
        var first = Task.Run(resolve);
        Assert.True(settings.FirstRegistrationEntered.Wait(TimeSpan.FromSeconds(10)));

        var second = await resolve();

        settings.ReleaseFirstRegistration.Set();
        await first;
        return second;
    }

    /// <summary>Settings whose first definition registration waits for the test; values are held for keys not yet defined, as the persisted store does.</summary>
    private sealed class GatedSettingsProvider(Dictionary<string, string> values) : ISettingsProvider
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, ISettingDefinition> _definitions = new(StringComparer.Ordinal);
        private int _registrations;

        public ManualResetEventSlim FirstRegistrationEntered { get; } = new();

        public ManualResetEventSlim ReleaseFirstRegistration { get; } = new();

        public IReadOnlyCollection<ISettingDefinition> Definitions => [.. _definitions.Values];

        public void RegisterDefinition(ISettingDefinition definition)
        {
            if (Interlocked.Increment(ref _registrations) == 1)
            {
                FirstRegistrationEntered.Set();
                ReleaseFirstRegistration.Wait(TimeSpan.FromSeconds(10));
            }

            if (!_definitions.TryAdd(definition.Key, definition))
                throw new DuplicateSettingDefinitionException(definition.Key);
        }

        public Task<string> GetValueAsync(string key, CancellationToken cancellationToken = default) =>
            _definitions.TryGetValue(key, out var definition)
                ? Task.FromResult(values.TryGetValue(key, out var value) ? value : definition.DefaultValue)
                : throw new SettingNotFoundException(key);

        public Task SetValueAsync(string key, string value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
