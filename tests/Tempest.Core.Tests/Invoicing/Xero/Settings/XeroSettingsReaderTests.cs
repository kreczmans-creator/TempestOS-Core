using System.Text.Json.Nodes;
using Tempest.Core.Invoicing;
using Tempest.Core.Invoicing.Xero;
using Tempest.Core.Invoicing.Xero.Api;
using Tempest.Core.Invoicing.Xero.Settings;
using Tempest.Core.Tests.Invoicing.Xero.Api;
using Tempest.Core.Tests.Invoicing.Xero.Simulator;

namespace Tempest.Core.Tests.Invoicing.Xero.Settings;

/// <summary>
/// `v0.24.0` X1 (D6, design §8) end to end: <see cref="XeroSettingsReader"/>
/// → <see cref="XeroAccountingApi"/> → <see cref="XeroWriteSafetyHandler"/>
/// → the S1 simulator, with a real file cache. Every test asserts the
/// simulator saw no contract or safety violation.
/// </summary>
public sealed class XeroSettingsReaderTests
{
    [Fact]
    public async Task Refresh_ReadsOrganisationTaxRatesAndAccounts_InThreeCalls_AndCachesTheReadingStampedWithTheTimeRead()
    {
        await using var rig = await SettingsRig.CreateAsync();
        var reader = rig.NewReader();

        var result = await reader.RefreshAsync();

        Assert.Equal(ConnectorOutcome.Ok, result.Outcome);
        Assert.Equal(["Organisation", "TaxRates", "Accounts"], rig.SettingsCalls());
        Assert.Equal(3, rig.Simulator.Requests.Count);

        var reading = result.Value!;
        Assert.Equal(XeroSettingsReading.CurrentSchemaVersion, reading.SchemaVersion);
        Assert.Equal(XeroTestAuthoriser.TenantId, reading.TenantId);
        Assert.Equal(rig.Clock.GetUtcNow(), reading.ReadAtUtc);

        var organisation = reading.Organisation;
        Assert.Equal(SimulatorSeed.OrganisationId, organisation.OrganisationId);
        Assert.Equal("Demo Company (UK)", organisation.Name);
        Assert.Equal("Demo Company (UK)", organisation.LegalName);
        Assert.Equal("GB 123 4567 89", organisation.TaxNumber);
        Assert.Equal("01234567", organisation.RegistrationNumber);
        Assert.Equal("GBP", organisation.BaseCurrency);
        Assert.Equal("GB", organisation.CountryCode);
        Assert.True(organisation.PaysTax);
        Assert.True(organisation.IsDemoCompany);
        Assert.Equal("01234 567890", organisation.Phone);
        Assert.Equal("https://www.example.co.uk", organisation.Website);

        var address = Assert.IsType<XeroAddress>(organisation.Address);
        Assert.Equal("STREET", address.AddressType);
        Assert.Equal(["23 Main Street", "Central City"], address.Lines);
        Assert.Equal("Marineville", address.City);
        Assert.Equal("Wessex", address.Region);
        Assert.Equal("MA12 3BC", address.PostalCode);

        var bank = Assert.Single(organisation.BankAccounts);
        Assert.Equal(new XeroBankAccount("Business Bank Account", "12-34-56 12345678", "GBP", "BANK"), bank);

        Assert.Equal(SimulatorSeed.UkDemoTaxRates().Count, reading.TaxRates.Count);
        var output2 = Assert.Single(reading.TaxRates, r => r.TaxType == "OUTPUT2");
        Assert.Equal(new XeroTaxRate("OUTPUT2", "20% (VAT on Income)", 20m, "ACTIVE", true, false), output2);
        Assert.Equal("DELETED", Assert.Single(reading.TaxRates, r => r.TaxType == "OUTPUT").Status);

        Assert.Equal(SimulatorSeed.UkDemoAccounts().Count, reading.Accounts.Count);
        var sales = Assert.Single(reading.Accounts, a => a.Code == "200");
        Assert.Equal(new XeroAccount(SimulatorSeed.DeterministicId("account", "200"), "200", "Sales", "REVENUE", "REVENUE", "ACTIVE", "OUTPUT2"), sales);

        // Durable: the file holds it, and the cached read answers it with no call.
        Assert.True(File.Exists(rig.Cache.FilePath));
        Assert.Equal(Path.Combine(rig.Directory.Path, "accounts", "xero-settings.json"), rig.Cache.FilePath);
        Assert.Equivalent(reading, await rig.Cache.ReadAsync(), strict: true);
        Assert.Same(reading, await reader.ReadCachedAsync());
        Assert.Equal(3, rig.Simulator.Requests.Count);

        var audit = Assert.Single(rig.Audit.Rows);
        Assert.Equal(XeroSettingsReader.AuditAction, audit.Action);
        Assert.Equal("true", audit.Detail!["isDemoCompany"]);
        Assert.Equal("full", audit.Detail["accountsRead"]);
        Assert.DoesNotContain(audit.Detail.Values, v => v.Contains(XeroTestAuthoriser.AccessToken, StringComparison.Ordinal));

        Assert.Empty(rig.Simulator.Violations);
    }

    [Fact]
    public async Task Offline_AfterARestart_TheLastReadingIsStillAnswered_AndAFailedRefreshLeavesItUntouched()
    {
        await using var rig = await SettingsRig.CreateAsync();
        var first = (await rig.NewReader().RefreshAsync()).Value!;
        var bytesBefore = await File.ReadAllBytesAsync(rig.Cache.FilePath);

        // A new process, and Xero unreachable.
        var restarted = rig.NewReader();
        rig.Clock.Advance(TimeSpan.FromDays(3));
        rig.Simulator.Inject(new XeroFault(XeroFaultKind.TransportFailure, Times: 10));

        var cached = await restarted.ReadCachedAsync();
        Assert.NotNull(cached);
        Assert.Equivalent(first, cached, strict: true);

        var refreshed = await restarted.RefreshAsync();
        Assert.Equal(ConnectorOutcome.Unavailable, refreshed.Outcome);
        Assert.StartsWith("Reading Xero's organisation failed:", refreshed.Reason, StringComparison.Ordinal);
        Assert.Equal(bytesBefore, await File.ReadAllBytesAsync(rig.Cache.FilePath));

        // Stale (3 days), Xero unreachable: work carries on from the last reading.
        var fresh = await restarted.ReadFreshAsync(rig.Clock);
        Assert.Equivalent(first, fresh, strict: true);
        Assert.Equal(first.ReadAtUtc, (await restarted.ReadCachedAsync())!.ReadAtUtc);

        Assert.Empty(rig.Simulator.Violations);
    }

    [Fact]
    public async Task ASecondRefresh_ReadsAccountsWithIfModifiedSince_AndMergesTheArchivedAccount()
    {
        await using var rig = await SettingsRig.CreateAsync();
        var reader = rig.NewReader();
        var first = (await reader.RefreshAsync()).Value!;

        rig.Clock.Advance(TimeSpan.FromHours(2));
        rig.Simulator.ArchiveAccountInXero("493");
        rig.Clock.Advance(TimeSpan.FromHours(1));

        var second = (await reader.RefreshAsync()).Value!;

        var accountsCalls = rig.Headers.Seen.Where(s => s.Path.EndsWith("/Accounts", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, accountsCalls.Count);
        Assert.Null(accountsCalls[0].IfModifiedSince);
        Assert.Equal(first.ReadAtUtc, accountsCalls[1].IfModifiedSince);

        // Only the one changed account came back; the merge keeps the rest.
        Assert.Equal(first.Accounts.Count, second.Accounts.Count);
        Assert.Equal("ARCHIVED", Assert.Single(second.Accounts, a => a.Code == "493").Status);
        Assert.Equal(first.Accounts.Where(a => a.Code != "493"), second.Accounts.Where(a => a.Code != "493"));
        Assert.Equal(first.Organisation.BankAccounts, second.Organisation.BankAccounts);
        Assert.Equal(rig.Clock.GetUtcNow(), second.ReadAtUtc);
        Assert.Equal(6, rig.SettingsCalls().Count);
        Assert.StartsWith("changed since ", rig.Audit.Rows[^1].Detail!["accountsRead"], StringComparison.Ordinal);

        Assert.Empty(rig.Simulator.Violations);
    }

    [Fact]
    public async Task AReadingOlderThanAWeek_IsReplacedByAFullAccountsRead()
    {
        await using var rig = await SettingsRig.CreateAsync();
        var reader = rig.NewReader();
        await reader.RefreshAsync();

        rig.Clock.Advance(XeroSettingsReader.FullAccountsReadInterval);
        var second = (await reader.RefreshAsync()).Value!;

        Assert.All(rig.Headers.Seen, s => Assert.Null(s.IfModifiedSince));
        Assert.Equal(SimulatorSeed.UkDemoAccounts().Count, second.Accounts.Count);
        Assert.Empty(rig.Simulator.Violations);
    }

    [Fact]
    public async Task AFailureOnTheSecondOrThirdCall_KeepsNothing_AndNamesTheReadThatFailed()
    {
        await using var rig = await SettingsRig.CreateAsync();
        var reader = rig.NewReader();

        rig.Simulator.Inject(new XeroFault(XeroFaultKind.ServiceUnavailable, PathContains: "TaxRates"));
        var taxRatesDown = await reader.RefreshAsync();
        Assert.Equal(ConnectorOutcome.Unavailable, taxRatesDown.Outcome);
        Assert.Contains("tax rates", taxRatesDown.Reason, StringComparison.Ordinal);
        Assert.Null(await reader.ReadCachedAsync());
        Assert.False(File.Exists(rig.Cache.FilePath));

        rig.Simulator.Inject(new XeroFault(XeroFaultKind.RateLimitedMinute, PathContains: "Accounts", RetryAfter: TimeSpan.FromSeconds(30)));
        var accountsLimited = await reader.RefreshAsync();
        Assert.Equal(ConnectorOutcome.Unavailable, accountsLimited.Outcome);
        Assert.Contains("chart of accounts", accountsLimited.Reason, StringComparison.Ordinal);
        Assert.Null(await reader.ReadCachedAsync());
        Assert.Empty(rig.Audit.Rows);

        Assert.Equal(ConnectorOutcome.Ok, (await reader.RefreshAsync()).Outcome);
        Assert.Empty(rig.Simulator.Violations);
    }

    [Fact]
    public async Task AGrantWithoutTheSettingsScope_AsksForReauthorisation()
    {
        var withoutSettings = XeroScopes.Required.Where(s => s != XeroScopes.SettingsRead).ToList();
        await using var rig = await SettingsRig.CreateAsync(grantedScopes: withoutSettings);

        var result = await rig.NewReader().RefreshAsync();

        Assert.Equal(ConnectorOutcome.Reauthorise, result.Outcome);
        Assert.Contains("scope", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Null(await rig.Cache.ReadAsync());
        Assert.Equal([XeroSimulatorRules.Scope], rig.Simulator.Violations.Select(v => v.Rule));
    }

    [Fact]
    public async Task NoConnectedTenant_ReadsNothing_AndCallsNothing()
    {
        await using var rig = await SettingsRig.CreateAsync();
        var reader = rig.NewReader();
        await reader.RefreshAsync();
        await rig.SecretStore.RemoveAsync(XeroSettingsReader.TenantIdSecretKey);
        var before = rig.Simulator.Requests.Count;

        Assert.Null(await reader.ReadCachedAsync());
        var result = await reader.RefreshAsync();

        Assert.Equal(ConnectorOutcome.Reauthorise, result.Outcome);
        Assert.Equal(before, rig.Simulator.Requests.Count);
        Assert.Empty(rig.Simulator.Violations);
    }

    [Fact]
    public async Task AReadingForAnotherTenant_IsNeverAnswered_AndTheNextRefreshReadsEveryAccount()
    {
        await using var rig = await SettingsRig.CreateAsync();
        await rig.NewReader().RefreshAsync();
        var stored = (await rig.Cache.ReadAsync())!;
        await rig.Cache.SaveAsync(stored with { TenantId = "another-tenant" });

        var reader = rig.NewReader();
        Assert.Null(await reader.ReadCachedAsync());

        var refreshed = (await reader.RefreshAsync()).Value!;
        Assert.Equal(XeroTestAuthoriser.TenantId, refreshed.TenantId);
        Assert.Null(rig.Headers.Seen.Single(s => s.Path.EndsWith("/Accounts", StringComparison.Ordinal)).IfModifiedSince);
        Assert.Empty(rig.Simulator.Violations);
    }

    [Fact]
    public async Task ACacheThatCannotBeWritten_StillAnswersTheReading_ForThisSession()
    {
        await using var rig = await SettingsRig.CreateAsync();
        var reader = rig.NewReader(new FailingWriteCache());

        var result = await reader.RefreshAsync();

        Assert.Equal(ConnectorOutcome.Ok, result.Outcome);
        Assert.Same(result.Value, await reader.ReadCachedAsync());
        Assert.Empty(rig.Simulator.Violations);
    }

    [Fact]
    public async Task ReadFresh_AnswersAYoungReadingWithoutACall_AndRefreshesAnOldOne()
    {
        await using var rig = await SettingsRig.CreateAsync();
        var reader = rig.NewReader();

        var none = await reader.ReadFreshAsync(rig.Clock);
        Assert.NotNull(none);
        Assert.Equal(3, rig.SettingsCalls().Count);

        rig.Clock.Advance(TimeSpan.FromHours(23));
        Assert.Same(none, await reader.ReadFreshAsync(rig.Clock));
        Assert.Equal(3, rig.SettingsCalls().Count);

        rig.Clock.Advance(TimeSpan.FromHours(1));
        var daily = await reader.ReadFreshAsync(rig.Clock);
        Assert.Equal(rig.Clock.GetUtcNow(), daily!.ReadAtUtc);
        Assert.Equal(6, rig.SettingsCalls().Count);

        // The first write of a session: always tries Xero.
        await reader.ReadFreshAsync(rig.Clock, TimeSpan.Zero);
        Assert.Equal(9, rig.SettingsCalls().Count);
        Assert.Empty(rig.Simulator.Violations);
    }

    [Fact]
    public async Task TheSafetyHandler_ReadsTheOrganisationThroughTheReader_BeforeTheFirstWrite_AndLetsADemoCompanyWriteThrough()
    {
        await using var rig = await SettingsRig.CreateAsync();
        var api = rig.NewApi();
        rig.HandlerReader = new XeroSettingsReader(rig.NewApi(), rig.Cache, rig.SecretStore, rig.Audit, rig.Clock);

        var created = await api.PutJsonAsync<JsonObject>(
            "Contacts", new JsonObject { ["Contacts"] = new JsonArray { new JsonObject { ["Name"] = "TempestOS Settings Test" } } }, "tos:test:x1-contact");

        Assert.Equal(ConnectorOutcome.Ok, created.Outcome);
        Assert.Equal(["Organisation", "TaxRates", "Accounts", "Contacts"], rig.Simulator.Requests.Select(r => r.Path));
        Assert.True((await rig.Cache.ReadAsync())!.Organisation.IsDemoCompany);
        Assert.Empty(rig.Simulator.Violations);
    }

    [Fact]
    public async Task TheSafetyHandler_BlocksAWriteToALiveOrganisation_ReadThroughTheReader()
    {
        await using var rig = await SettingsRig.CreateAsync(isDemoCompany: false);
        var api = rig.NewApi();
        rig.HandlerReader = new XeroSettingsReader(rig.NewApi(), rig.Cache, rig.SecretStore, rig.Audit, rig.Clock);

        var refused = await api.PutJsonAsync<JsonObject>(
            "Contacts", new JsonObject { ["Contacts"] = new JsonArray { new JsonObject { ["Name"] = "TempestOS Settings Test" } } }, "tos:test:x1-contact");

        Assert.Equal(ConnectorOutcome.Rejected, refused.Outcome);
        Assert.Contains(XeroWriteSafetyHandler.RuleLiveOrganisation, refused.Reason, StringComparison.Ordinal);
        Assert.Equal(["Organisation", "TaxRates", "Accounts"], rig.Simulator.Requests.Select(r => r.Path));
        Assert.False((await rig.Cache.ReadAsync())!.Organisation.IsDemoCompany);
        Assert.Empty(rig.Simulator.Violations);
    }

    // Backlog X1-4: the container builds the reader through its public constructor, which used the system clock
    // rather than the clock the host composed the Xero client with.
    [Fact]
    public async Task TheContainersConstructor_StampsAReadingWithTheXeroClientsClock()
    {
        await using var rig = await SettingsRig.CreateAsync();
        rig.Clock.Advance(TimeSpan.FromDays(400));
        var reader = new XeroSettingsReader(rig.NewApi(), rig.Cache, rig.SecretStore, rig.Audit);

        var reading = (await reader.RefreshAsync()).Value!;

        Assert.Equal(rig.Clock.GetUtcNow(), reading.ReadAtUtc);
        Assert.Empty(rig.Simulator.Violations);
    }

    // Backlog X1-3: If-Modified-Since never returns an account deleted in Xero, so a code reused by a new
    // account left the stale one first in the merge and Check answered its status.
    [Fact]
    public void AnIncrementalMerge_DropsAnEarlierAccountWhoseCodeANewAccountNowCarries()
    {
        var organisation = new XeroWireOrganisation("org", "Org");
        var previous = XeroSettingsReader.Build(
            "t", organisation, [],
            [
                new XeroWireAccount("acc-old", "493", "Travel (old)", "OVERHEADS", "EXPENSE", "ARCHIVED"),
                new XeroWireAccount("acc-200", "200", "Sales", "REVENUE", "REVENUE", "ACTIVE"),
            ],
            null, DateTimeOffset.UnixEpoch)!;

        var merged = XeroSettingsReader.Build(
            "t", organisation, [], [new XeroWireAccount("acc-new", "493", "Travel", "OVERHEADS", "EXPENSE", "ACTIVE")], previous, DateTimeOffset.UnixEpoch.AddDays(1))!;

        Assert.Equal(["acc-200", "acc-new"], merged.Accounts.Select(a => a.AccountId).Order(StringComparer.Ordinal));
        Assert.Equal("493", XeroAccountCodeMap.Check("493", XeroAccountPurpose.Expense, merged).Code);
    }

    [Fact]
    public void Check_PrefersTheActiveAccount_WhenAnArchivedOneSharesItsCode()
    {
        var reading = XeroSettingsReader.Build(
            "t", new XeroWireOrganisation("org", "Org"), [],
            [
                new XeroWireAccount("acc-old", "493", "Travel (old)", "OVERHEADS", "EXPENSE", "ARCHIVED"),
                new XeroWireAccount("acc-new", "493", "Travel", "OVERHEADS", "EXPENSE", "ACTIVE"),
            ],
            null, DateTimeOffset.UnixEpoch)!;

        Assert.Equal("493", XeroAccountCodeMap.Check("493", XeroAccountPurpose.Expense, reading).Code);
    }

    [Fact]
    public void Build_PrefersTheStreetAddressAndTheDefaultPhone_AndFallsBackOnPoBoxAndLegalName()
    {
        var organisation = new XeroWireOrganisation(
            "org", "Trading Name", LegalName: "  ", Addresses:
            [
                new XeroWireAddress("STREET"),
                new XeroWireAddress("POBOX", "PO Box 9", City: "Town", PostalCode: "AB1 2CD"),
            ],
            Phones:
            [
                new XeroWirePhone("FAX", "000"),
                new XeroWirePhone("MOBILE", "07700 900000"),
                new XeroWirePhone("DEFAULT", "567890", PhoneAreaCode: "01234"),
            ]);

        var reading = XeroSettingsReader.Build("t", organisation, [new XeroWireTaxRate(null, "x", "ACTIVE")], [new XeroWireAccount(null, "1", "x", "BANK")], null, DateTimeOffset.UnixEpoch)!;

        Assert.Null(reading.Organisation.LegalName);
        Assert.Equal("POBOX", reading.Organisation.Address!.AddressType);
        Assert.Equal(["PO Box 9"], reading.Organisation.Address.Lines);
        Assert.Equal("01234 567890", reading.Organisation.Phone);
        Assert.False(reading.Organisation.IsDemoCompany);
        Assert.Empty(reading.TaxRates);
        Assert.Empty(reading.Accounts);
        Assert.Equal("Trading Name", XeroCompanyDetails.From(reading).Name);

        Assert.Null(XeroSettingsReader.Build("t", new XeroWireOrganisation(null, "No id"), [], [], null, DateTimeOffset.UnixEpoch));
    }

    [Fact]
    public async Task CompanyDetails_AreWhatAPdfPrints_WithTheReadAtNote()
    {
        await using var rig = await SettingsRig.CreateAsync();
        var reading = (await rig.NewReader().RefreshAsync()).Value!;

        var details = XeroCompanyDetails.From(reading);

        Assert.Equal("Demo Company (UK)", details.Name);
        Assert.Equal("01234567", details.RegistrationNumber);
        Assert.Equal("GB 123 4567 89", details.VatNumber);
        Assert.Equal(["23 Main Street", "Central City", "Marineville", "Wessex MA12 3BC", "United Kingdom"], details.AddressLines);
        Assert.Equal("01234 567890", details.Phone);
        Assert.Equal("https://www.example.co.uk", details.Website);
        Assert.Equal("12-34-56 12345678", Assert.Single(details.BankAccounts).BankAccountNumber);
        Assert.Equal("from Xero, read at 2 Oct 2026 09:00", details.SourceNote(TimeZoneInfo.Utc));
        Assert.Empty(rig.Simulator.Violations);
    }

    // Backlog U1 minor: X1 keeps each bank account's type, so a credit card is never printed as payment details.
    [Fact]
    public async Task ACreditCardAccount_IsReadWithItsType_AndNeverPrintedAsBankDetails()
    {
        await using var rig = await SettingsRig.CreateAsync();
        rig.Simulator.AddBankAccountInXero("091", "Company Credit Card", "4111 1111", "CREDITCARD");

        var reading = (await rig.NewReader().RefreshAsync()).Value!;

        Assert.Contains(new XeroBankAccount("Company Credit Card", "4111 1111", "GBP", "CREDITCARD"), reading.Organisation.BankAccounts);
        var printed = Assert.Single(XeroCompanyDetails.From(reading).BankAccounts);
        Assert.Equal("12-34-56 12345678", printed.BankAccountNumber);

        // A reading cached before the type was kept still prints its accounts.
        var older = reading with { Organisation = reading.Organisation with { BankAccounts = [new XeroBankAccount("Business Bank Account", "12-34-56 12345678", "GBP")] } };
        Assert.Single(XeroCompanyDetails.From(older).BankAccounts);
        Assert.Empty(rig.Simulator.Violations);
    }
}
